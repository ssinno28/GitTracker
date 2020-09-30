using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GitTracker.Interfaces;
using GitTracker.Models;
using GitTracker.Serializer;
using LibGit2Sharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using IValueProvider = GitTracker.Interfaces.IValueProvider;

namespace GitTracker.Services
{
    public class ContentItemService : IContentItemService
    {
        private readonly ContentContractResolver _contentContractResolver;
        private readonly IEnumerable<IValueProvider> _valueProviders;
        private readonly IFileProvider _fileProvider;
        private readonly IPathProvider _pathProvider;
        private readonly IGitRepo _gitRepo;
        private readonly IEnumerable<IUpdateOperation> _updateOperations;
        private readonly IEnumerable<ICreateOperation> _createOperations;
        private readonly IEnumerable<IDeleteOperation> _deleteOperations;

        public ContentItemService(
            ContentContractResolver contentContractResolver,
            IEnumerable<IValueProvider> valueProviders,
            IFileProvider fileProvider,
            IPathProvider pathProvider, 
            IGitRepo gitRepo, 
            IEnumerable<IUpdateOperation> updateOperations, 
            IEnumerable<ICreateOperation> createOperations,
            IEnumerable<IDeleteOperation> deleteOperations)
        {
            _contentContractResolver = contentContractResolver;
            _valueProviders = valueProviders;
            _fileProvider = fileProvider;
            _pathProvider = pathProvider;
            _gitRepo = gitRepo;
            _updateOperations = updateOperations;
            _createOperations = createOperations;
            _deleteOperations = deleteOperations;
        }

        public async Task Sync(string email, IList<Type> contentTypes, string userName = null)
        {
            string currentCommitId = _gitRepo.GetCurrentCommitId();
            
            if (!_gitRepo.Pull(email, userName)) return;

            // get new commit id
            string newCommitId = _gitRepo.GetCurrentCommitId();

            // get diff from both
            var diff = _gitRepo.GetDiff(new List<string>(), currentCommitId, newCommitId);

            foreach (var gitDiff in diff)
            {
                switch (gitDiff.ChangeKind)
                {
                    case ChangeKind.Added:
                        var addedItem = await GetTrackedItem(gitDiff.Path, contentTypes);
                        await PerformCreate(addedItem);
                        break;
                    case ChangeKind.Deleted:
                        _gitRepo.CheckoutPaths(currentCommitId, gitDiff.Path);
                        var deletedItem = await GetTrackedItem(gitDiff.Path, contentTypes);
                        _gitRepo.Reset(ResetMode.Hard);

                        await PerformDelete(deletedItem);
                        break;
                    case ChangeKind.Modified:
                        var modifiedItem = await GetTrackedItem(gitDiff.Path, contentTypes);
                        await PerformUpdate(modifiedItem);
                        break;
                }
            }
        }

        private async Task PerformCreate(TrackedItem trackedItem)
        {
            var createOperation =
                _createOperations.FirstOrDefault(x => x.IsMatch(trackedItem.GetType()));

            if (createOperation == null) return;

            await createOperation.Create(trackedItem);
        }        
        
        private async Task PerformDelete(TrackedItem trackedItem)
        {
            var deleteOperation =
                _deleteOperations.FirstOrDefault(x => x.IsMatch(trackedItem.GetType()));

            if (deleteOperation == null) return;

            await deleteOperation.Delete(trackedItem);
        }        
        
        private async Task PerformUpdate(TrackedItem trackedItem)
        {
            var updateOperation =
                _updateOperations.FirstOrDefault(x => x.IsMatch(trackedItem.GetType()));

            if (updateOperation == null) return;

            await updateOperation.Update(trackedItem);
        }

        private async Task<TrackedItem> GetTrackedItem(string path, IList<Type> contentTypes)
        {
            string fileContent = _fileProvider.GetFile(path);
            var trackedItem = DeserializeContentItem(fileContent, contentTypes);
            await SetNonJsonValues(trackedItem);

            return trackedItem;
        }

        public async Task Add(string entity, IList<Type> contentTypes)
        {
            var contentItem = DeserializeContentItem(entity, contentTypes);
            await Add(contentItem);
        }

        private async Task SetNonJsonValues(TrackedItem trackedItem)
        {
            foreach (var propertyInfo in trackedItem.GetType().GetProperties())
            {
                var valueProvider =
                    _valueProviders.FirstOrDefault(x => x.IsMatch(propertyInfo));

                if (valueProvider != null)
                {
                    propertyInfo.SetValue(trackedItem, await valueProvider.GetValue(trackedItem, propertyInfo));
                }
            }
        }

        public async Task Add(TrackedItem trackedItem)
        {
            await SetNonJsonValues(trackedItem);

            trackedItem.Id = Guid.NewGuid().ToString();
            trackedItem.CreatedDate = DateTimeOffset.Now;

            await _fileProvider.UpsertFiles(trackedItem);
            await PerformCreate(trackedItem);
        }

        public async Task<bool> Update(TrackedItem trackedItem)
        {
            await SetNonJsonValues(trackedItem);

            trackedItem.ModifiedDate = DateTimeOffset.Now;
            await _fileProvider.UpsertFiles(trackedItem);
            await PerformUpdate(trackedItem);

            return true;
        }

        public async Task<bool> ChangeName(string newName, TrackedItem trackedItem)
        {
            trackedItem.PreviousPaths.Add(_pathProvider.GetRelativeTrackedItemPath(trackedItem.GetType(), trackedItem));
            await _fileProvider.MoveFile(newName, trackedItem);
            await _fileProvider.UpsertFiles(trackedItem);
            await PerformUpdate(trackedItem);

            return true;
        }

        public async Task<TrackedItem> CreateDraft(string name, Type contentType, TrackedItem trackedItem = null)
        {
            if (trackedItem == null)
            {
                trackedItem = (TrackedItem)Activator.CreateInstance(contentType);
            }

            trackedItem.Name = name;
            trackedItem.Id = Guid.NewGuid().ToString();
            trackedItem.CreatedDate = DateTimeOffset.Now;

            await _fileProvider.UpsertFiles(trackedItem);
            await PerformCreate(trackedItem);

            return trackedItem;
        }

        public async Task Delete(TrackedItem trackedItem)
        {
            await _fileProvider.DeleteFiles(trackedItem);
            await PerformDelete(trackedItem);
        }

        private Type GetContentType(string entity, IList<Type> contentTypes)
        {
            string typeDefinition = JObject.Parse(entity).GetValue("TypeDefinition").Value<string>();
            return contentTypes.First(x => x.Name.Equals(typeDefinition));
        }

        private TrackedItem DeserializeContentItem(string document, IList<Type> contentTypes)
        {
            var contentType = GetContentType(document, contentTypes);
            var serializerSettings = new JsonSerializerSettings
            {
                ContractResolver = _contentContractResolver
            };

            var contentItem = (TrackedItem)JsonConvert.DeserializeObject(document, contentType, serializerSettings);

            return contentItem;
        }
    }
}