﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using GitTracker.Helpers;
using GitTracker.Interfaces;
using GitTracker.Models;
using GitTracker.Serializer;
using LibGit2Sharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using IValueProvider = GitTracker.Interfaces.IValueProvider;

namespace GitTracker.Services
{
    public class GitTrackingService : IGitTrackingService
    {
        private readonly ContentContractResolver _contentContractResolver;
        private readonly IEnumerable<IValueProvider> _valueProviders;
        private readonly IFileProvider _fileProvider;
        private readonly IPathProvider _pathProvider;
        private readonly IGitRepo _gitRepo;
        private readonly IEnumerable<IUpdateOperation> _updateOperations;
        private readonly IEnumerable<ICreateOperation> _createOperations;
        private readonly IEnumerable<IDeleteOperation> _deleteOperations;
        private readonly GitConfig _gitConfig;

        public GitTrackingService(
            ContentContractResolver contentContractResolver,
            IEnumerable<IValueProvider> valueProviders,
            IFileProvider fileProvider,
            IPathProvider pathProvider,
            IGitRepo gitRepo,
            IEnumerable<IUpdateOperation> updateOperations,
            IEnumerable<ICreateOperation> createOperations,
            IEnumerable<IDeleteOperation> deleteOperations,
            GitConfig gitConfig)
        {
            _contentContractResolver = contentContractResolver;
            _valueProviders = valueProviders;
            _fileProvider = fileProvider;
            _pathProvider = pathProvider;
            _gitRepo = gitRepo;
            _updateOperations = updateOperations;
            _createOperations = createOperations;
            _deleteOperations = deleteOperations;
            _gitConfig = gitConfig;
        }

        public async Task<bool> Publish(string email, IList<Type> contentTypes,
            CheckoutFileConflictStrategy strategy = CheckoutFileConflictStrategy.Normal, string userName = null)
        {
            var result = await Sync(email, contentTypes, strategy, userName);
            if (!result) return false;

            return _gitRepo.Push(email, userName);
        }

        public async Task<bool> Sync(string email, IList<Type> contentTypes, CheckoutFileConflictStrategy strategy = CheckoutFileConflictStrategy.Normal, string userName = null)
        {
            // if this is the first pull then no need to check the diff
            var commits = _gitRepo.GetCommits();
            if (!commits.Any())
            {
                if (!_gitRepo.Pull(email, strategy, userName)) return false;

                var files = _fileProvider.GetFiles(contentTypes);
                foreach (var fileContent in files)
                {
                    var trackedItem = await DeserializeContentItem(fileContent, contentTypes);
                    await PerformCreate(trackedItem);
                }

                return true;
            }

            string currentCommitId = _gitRepo.GetCurrentCommitId();

            if (!_gitRepo.Pull(email, strategy, userName)) return false;

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
                        string fileContents = _gitRepo.GetFileFromCommit(currentCommitId, gitDiff.Path);
                        var deletedItem = await DeserializeContentItem(fileContents, contentTypes);

                        await PerformDelete(deletedItem);
                        break;
                    case ChangeKind.Modified:
                        var modifiedItem = await GetTrackedItem(gitDiff.Path, contentTypes);
                        await PerformUpdate(modifiedItem);
                        break;
                }
            }

            return true;
        }

        public async Task<IList<TrackedItemConflict>> GetTrackedItemConflicts(IList<Type> contentTypes)
        {
            var trackedItemConflicts = new List<TrackedItemConflict>();
            var mergeConflicts = _gitRepo.GetMergeConflicts();

            foreach (var conflictGrouping in mergeConflicts.GroupBy(x => Path.GetDirectoryName(x.Ours.Path)))
            {
                TrackedItemConflict trackedItemConflict = new TrackedItemConflict()
                {
                    ValueProviderConflicts = new List<ValueProviderConflict>(),
                    ChangedProperties = new List<PropertyInfo>()
                };

                foreach (var conflict in conflictGrouping.Where(x => x.Ours.Path.EndsWith(".json")))
                {
                    var fileContents =
                        _gitRepo.GetDiff3Files(conflict.Ours.Path, conflict.Theirs.Path, conflict.Ancestor?.Path);

                    if (!string.IsNullOrEmpty(fileContents.BaseFile))
                    {
                        trackedItemConflict.Ancestor = await DeserializeContentItem(fileContents.BaseFile, contentTypes);
                    }

                    trackedItemConflict.Ours = await DeserializeContentItem(fileContents.OurFile, contentTypes);
                    trackedItemConflict.Theirs = await DeserializeContentItem(fileContents.TheirFile, contentTypes);

                    trackedItemConflict.ChangedProperties =
                        trackedItemConflict.Ours.GetType()
                            .GetProperties()
                            .Where(x =>
                            {
                                var valueProvider =
                                    _valueProviders.FirstOrDefault(vp => vp.IsMatch(x));
                                if (valueProvider != null)
                                {
                                    return false;
                                }

                                var ourValue = x.GetValue(trackedItemConflict.Ours);
                                var theirValue = x.GetValue(trackedItemConflict.Theirs);

                                if (ourValue == null && theirValue != null)
                                {
                                    return true;
                                }

                                if (theirValue == null && ourValue != null)
                                {
                                    return true;
                                }

                                if (ourValue == null && theirValue == null)
                                {
                                    return false;
                                }

                                return !ourValue.Equals(theirValue);
                            })
                            .ToList();
                }

                foreach (var conflict in conflictGrouping.Where(x => !x.Ours.Path.EndsWith(".json")))
                {
                    var fileContents =
                        _gitRepo.GetDiff3Files(conflict.Ours.Path, conflict.Theirs.Path, conflict.Ancestor?.Path);

                    string fileName = Path.GetFileNameWithoutExtension(conflict.Ours.Path);

                    var propertyInfo =
                        trackedItemConflict.Ours.GetType()
                            .GetProperties()
                            .First(x => x.Name.ToSentenceCase().MakeUrlFriendly().Equals(fileName));

                    trackedItemConflict.ChangedProperties.Add(propertyInfo);
                    propertyInfo.SetValue(trackedItemConflict.Ours, fileContents.OurFile);
                    propertyInfo.SetValue(trackedItemConflict.Theirs, fileContents.TheirFile);

                    var valueProviderConflict =
                        new ValueProviderConflict
                        {
                            LocalPath = Path.Combine(_gitConfig.LocalPath, $"{conflict.Ours.Path}.LOCAL"),
                            RemotePath = Path.Combine(_gitConfig.LocalPath, $"{conflict.Theirs.Path}.REMOTE")
                        };

                    if (!string.IsNullOrEmpty(fileContents.BaseFile))
                    {
                        valueProviderConflict.BasePath = Path.Combine(_gitConfig.LocalPath, $"{conflict.Ancestor.Path}.BASE");
                        File.WriteAllText(valueProviderConflict.BasePath, fileContents.BaseFile);
                        propertyInfo.SetValue(trackedItemConflict.Ancestor, fileContents.BaseFile);
                    }

                    File.WriteAllText(valueProviderConflict.LocalPath, fileContents.OurFile);
                    File.WriteAllText(valueProviderConflict.RemotePath, fileContents.TheirFile);

                    trackedItemConflict.ValueProviderConflicts.Add(valueProviderConflict);
                }

                trackedItemConflicts.Add(trackedItemConflict);
            }

            return trackedItemConflicts;
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
            var trackedItem = await DeserializeContentItem(fileContent, contentTypes);

            return trackedItem;
        }

        public async Task<TrackedItem> Create(string entity, IList<Type> contentTypes)
        {
            var contentItem = await DeserializeContentItem(entity, contentTypes);
            return await Create(contentItem);
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

        public async Task<TrackedItem> Create(TrackedItem trackedItem)
        {
            await SetNonJsonValues(trackedItem);

            trackedItem.Id = Guid.NewGuid().ToString();
            trackedItem.CreatedDate = DateTimeOffset.Now;

            await _fileProvider.UpsertFiles(trackedItem);
            await PerformCreate(trackedItem);

            return trackedItem;
        }

        public async Task<T> Create<T>(T trackedItem) where T : TrackedItem
        {
            return (T)await Create((TrackedItem)trackedItem);
        }

        public async Task<T> CreateDraft<T>(string name, Type contentType, T trackedItem = null) where T : TrackedItem
        {
            return (T)await CreateDraft(name, contentType, (TrackedItem)trackedItem);
        }

        public bool Stage(TrackedItem trackedItem)
        {
            var relativeTrackedItemPath =
                _pathProvider.GetTrackedItemPath(trackedItem.GetType(), trackedItem);

            var paths =
                Directory.GetFiles(relativeTrackedItemPath, "*", SearchOption.AllDirectories)
                    .ToArray();

            return _gitRepo.Stage(paths);
        }

        public async Task<T> Update<T>(T trackedItem) where T : TrackedItem
        {
            return (T)await Update((TrackedItem)trackedItem);
        }

        public async Task<TrackedItem> Update(TrackedItem trackedItem)
        {
            await SetNonJsonValues(trackedItem);

            trackedItem.ModifiedDate = DateTimeOffset.Now;
            await _fileProvider.UpsertFiles(trackedItem);
            await PerformUpdate(trackedItem);

            return trackedItem;
        }

        public async Task<TrackedItem> ChangeName(string newName, TrackedItem trackedItem)
        {
            trackedItem.PreviousPaths.Add(_pathProvider.GetRelativeTrackedItemPath(trackedItem.GetType(), trackedItem));
            await _fileProvider.MoveFile(newName, trackedItem);
            await _fileProvider.UpsertFiles(trackedItem);
            await PerformUpdate(trackedItem);

            return trackedItem;
        }

        public async Task<T> ChangeName<T>(string newName, T trackedItem) where T : TrackedItem
        {
            return (T)await ChangeName(newName, (TrackedItem)trackedItem);
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

        private async Task<TrackedItem> DeserializeContentItem(string document, IList<Type> contentTypes)
        {
            var contentType = GetContentType(document, contentTypes);
            var serializerSettings = new JsonSerializerSettings
            {
                ContractResolver = _contentContractResolver
            };

            var trackedItem = (TrackedItem)JsonConvert.DeserializeObject(document, contentType, serializerSettings);
            await SetNonJsonValues(trackedItem);

            return trackedItem;
        }
    }
}