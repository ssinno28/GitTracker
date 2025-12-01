using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using GitTracker.Helpers;
using GitTracker.Interfaces;
using GitTracker.Models;
using GitTracker.Serializer;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<GitTrackingService> _logger;
        private readonly ILocalPathFactory _localPathFactory;
        private readonly IFileSystem _fileSystem;

        public GitTrackingService(
            ContentContractResolver contentContractResolver,
            IEnumerable<IValueProvider> valueProviders,
            IFileProvider fileProvider,
            IPathProvider pathProvider,
            IGitRepo gitRepo,
            IEnumerable<IUpdateOperation> updateOperations,
            IEnumerable<ICreateOperation> createOperations,
            IEnumerable<IDeleteOperation> deleteOperations,
            GitConfig gitConfig,
            ILoggerFactory loggerFactory,
            ILocalPathFactory localPathFactory,
            IFileSystem fileSystem)
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
            _localPathFactory = localPathFactory;
            _fileSystem = fileSystem;
            _logger = loggerFactory.CreateLogger<GitTrackingService>();
        }

        public async Task<bool> Publish(string email,
            CheckoutFileConflictStrategy strategy = CheckoutFileConflictStrategy.Normal, string userName = null)
        {
            try
            {
                var result = await Sync(email, strategy, userName);
                if (!result) return false;
            }
            catch (MergeFetchHeadNotFoundException ex)
            {
                // swallowing for now
            }

            return _gitRepo.Push(email, userName);
        }

        public async Task<bool> SwitchBranch(string branchName)
        {
            var diffFromHead = _gitRepo.GetDiffFromHead();
            if (diffFromHead.Any())
            {
                throw new Exception("Can not switch branch when you have pending changes!");
            }

            string currentCommitId = _gitRepo.GetCurrentCommitId();
            await _gitRepo.ChangeBranch(branchName);

            string newCommitId = _gitRepo.GetCurrentCommitId();

            var diff = _gitRepo.GetDiffBetweenBranches(currentCommitId, newCommitId);

            await PerformOpsBasedOnDiff(diff, currentCommitId);
            return true;
        }

        public async Task<bool> CreateBranch(string branchName)
        {
            var diffFromHead = _gitRepo.GetDiffFromHead();
            if (diffFromHead.Any())
            {
                throw new Exception("Can not create branch when you have pending changes!");
            }

            _gitRepo.CreateBranch(branchName);

            return true;
        }

        //public async Task<bool> Stash(string message, string email, string userName, params TrackedItem[] trackedItems)
        //{
        //    foreach (var trackedItem in trackedItems)
        //    {
        //        Stage(trackedItem);
        //    }

        //    string commitId = _gitRepo.Stash(message, email, userName);
        //    if (string.IsNullOrEmpty(commitId)) return false;

        //    var diff = _gitRepo.GetDiffForStash(commitId);
        //    await PerformOpsBasedOnDiff(diff, string.Empty);

        //    return true;
        //}

        public async Task<bool> MergeBranch(string branchName, string email,
            CheckoutFileConflictStrategy strategy = CheckoutFileConflictStrategy.Normal, string name = null)
        {
            var diffFromHead = _gitRepo.GetDiffFromHead();
            if (diffFromHead.Any())
            {
                throw new Exception("Can not merge branch when you have pending changes!");
            }

            string currentCommitId = _gitRepo.GetCurrentCommitId();
            if (!_gitRepo.MergeBranch(branchName, email, strategy, name)) return false;

            string newCommitId = _gitRepo.GetCurrentCommitId();

            var diff = _gitRepo.GetDiffBetweenBranches(currentCommitId, newCommitId);
            await PerformOpsBasedOnDiff(diff, currentCommitId);

            return true;
        }

        public async Task<bool> Sync(string email, CheckoutFileConflictStrategy strategy = CheckoutFileConflictStrategy.Normal, string name = null)
        {
            string localPath = _localPathFactory.GetLocalPath();
            if (!Directory.Exists(localPath) || !Repository.IsValid(localPath))
            {
                Repository.Init(localPath);
            }

            var diffFromHead = _gitRepo.GetDiffFromHead();
            if (diffFromHead.Any())
            {
                throw new Exception("Can not sync when you have pending changes!");
            }

            var remoteHasCommits = _gitRepo.CheckRemoteHasCommits(email);
            if (!remoteHasCommits)
            {
                _logger.LogInformation("Remote repository has no commits. Skipping pull operation.");
                return true;
            }

            // if this is the first pull then no need to check the diff
            var commits = _gitRepo.GetCommits();
            if (!commits.Any())
            {
                if (!_gitRepo.Pull(email, strategy, name)) return false;

                var files = _fileProvider.GetFiles(_gitConfig.TrackedTypes);
                foreach (var fileContent in files)
                {
                    var trackedItem = await DeserializeContentItem(fileContent);
                    await PerformCreate(trackedItem);
                }

                return true;
            }

            string currentCommitId = _gitRepo.GetCurrentCommitId();

            if (!_gitRepo.Pull(email, strategy, name)) return false;

            // get new commit id
            string newCommitId = _gitRepo.GetCurrentCommitId();

            // get diff from both
            var diff = _gitRepo.GetDiff(new List<string>(), currentCommitId, newCommitId);

            await PerformOpsBasedOnDiff(diff, currentCommitId);

            return true;
        }

        public async Task ResetFileChanges(TrackedItemDiff diff)
        {
            var contentItem = diff.Initial ?? diff.Final;
            var relativePath = _pathProvider.GetRelativeTrackedItemPath(contentItem.GetType(), contentItem);
            if (diff.TrackedItemGitDiff.ChangeKind == ChangeKind.Added)
            {
                await _fileProvider.DeleteFiles(contentItem);
            }
            else
            {
                _gitRepo.ResetFileChanges(relativePath);
            }

            foreach (var valueProviderDiff in diff.ValueProviderDiffs)
            {
                if (valueProviderDiff.ChangeKind == ChangeKind.Added)
                {
                    var contentItemPath =
                        _pathProvider.GetTrackedItemPath(contentItem.GetType(), contentItem);

                    await _fileProvider.DeleteFile(Path.Combine(contentItemPath, Path.GetFileName(valueProviderDiff.Path)));
                }
                else
                {
                    _gitRepo.ResetFileChanges(valueProviderDiff.Path);
                }
            }

            if (diff.TrackedItemGitDiff.ChangeKind == ChangeKind.Added)
            {
                await PerformDelete(contentItem);
            }
            else if (diff.TrackedItemGitDiff.ChangeKind == ChangeKind.Deleted)
            {
                await PerformCreate(contentItem);
            }
            else
            {
                await PerformUpdate(contentItem);
            }
        }

        private async Task PerformOpsBasedOnDiff(IList<GitDiff> diff, string currentCommitId)
        {
            foreach (var gitDiff in diff)
            {
                // always perform update when a non guid file is created/updated/deleted
                if (!gitDiff.Path.IsTrackedItemJson())
                {
                    var modifiedItem = await GetTrackedItem(gitDiff.Path);
                    await PerformUpdate(modifiedItem);
                    continue;
                }

                switch (gitDiff.ChangeKind)
                {
                    case ChangeKind.Added:
                        var addedItem = await GetTrackedItem(gitDiff.Path);
                        await PerformCreate(addedItem);
                        break;
                    case ChangeKind.Deleted:
                        string fileContents = _gitRepo.GetFileFromCommit(currentCommitId, gitDiff.Path);
                        var deletedItem = await DeserializeContentItem(fileContents);

                        await PerformDelete(deletedItem);
                        break;
                    case ChangeKind.Modified:
                        var modifiedItem = await GetTrackedItem(gitDiff.Path);
                        await PerformUpdate(modifiedItem);
                        break;
                }
            }
        }

        public async Task<IList<TrackedItemDiff>> GetTrackedItemDiffs(string currentCommitId = null, string newCommitId = null)
        {
            return await GetTrackedItemDiffs(new List<string>(), currentCommitId, newCommitId);
        }

        public async Task<IList<TrackedItemDiff>> GetTrackedItemDiffs(Type trackedType, string currentCommitId = null, string newCommitId = null)
        {
            string path = _pathProvider.GetRelativeTrackedItemPath(trackedType);
            return await GetTrackedItemDiffs(new List<string> { path }, currentCommitId, newCommitId);
        }

        public async Task<IList<TrackedItemDiff>> GetTrackedItemDiffs(IList<Type> trackedTypes, string currentCommitId = null, string newCommitId = null)
        {
            var paths = new List<string>();
            foreach (var trackedType in trackedTypes)
            {
                string path = _pathProvider.GetRelativeTrackedItemPath(trackedType);
                paths.Add(path);
            }

            return await GetTrackedItemDiffs(paths, currentCommitId, newCommitId);
        }

        public async Task<IList<TrackedItemDiff>> GetTrackedItemDiffs(TrackedItem trackedItem,
            string currentCommitId = null, string newCommitId = null)
        {
            string path = _pathProvider.GetRelativeTrackedItemPath(trackedItem.GetType(), trackedItem);
            var paths = new List<string> { path };
            paths.AddRange(trackedItem.PreviousPaths);

            return await GetTrackedItemDiffs(paths, currentCommitId, newCommitId);
        }

        private async Task<IList<TrackedItemDiff>> GetTrackedItemDiffs(IList<string> paths, string currentCommitId = null, string newCommitId = null)
        {
            IList<TrackedItemDiff> trackedItemDiffs = new List<TrackedItemDiff>();

            IList<GitDiff> diffs;
            if (!string.IsNullOrEmpty(currentCommitId) && !string.IsNullOrEmpty(newCommitId))
            {
                diffs = _gitRepo.GetDiff(paths, currentCommitId, newCommitId);
            }
            else if (!string.IsNullOrEmpty(currentCommitId))
            {
                diffs = _gitRepo.GetDiff(paths, currentCommitId);
            }
            else
            {
                diffs = _gitRepo.GetDiffFromHead();
            }

            foreach (var diffGrouping in diffs.GroupBy(x => Path.GetDirectoryName(x.Path)))
            {
                var trackedItemDiff = new TrackedItemDiff
                {
                    ValueProviderDiffs = new List<GitDiff>()
                };

                foreach (var gitDiff in diffGrouping.Where(x => x.Path.EndsWith(".json")))
                {
                    if (!Guid.TryParse(Path.GetFileNameWithoutExtension(gitDiff.Path), out _))
                    {
                        continue;
                    }

                    trackedItemDiff.Initial = await DeserializeContentItem(gitDiff.InitialFileContent);
                    trackedItemDiff.Final = await DeserializeContentItem(gitDiff.FinalFileContent);

                    trackedItemDiff.TrackedItemGitDiff = gitDiff;
                }

                var valueProviderDiffs =
                    diffGrouping.Where(x =>
                        _valueProviders.Any(vp =>
                            !string.IsNullOrEmpty(vp.Extension)
                            && vp.Extension.Equals(Path.GetExtension(x.Path))
                            ));

                foreach (var gitDiff in valueProviderDiffs)
                {
                    trackedItemDiff.ValueProviderDiffs.Add(gitDiff);
                    if (trackedItemDiff.Initial == null && trackedItemDiff.Final == null)
                    {
                        continue;
                    }

                    Type trackedItemType;
                    switch (gitDiff.ChangeKind)
                    {
                        case ChangeKind.Deleted:
                            trackedItemType = trackedItemDiff.Initial.GetType();
                            break;
                        default:
                            trackedItemType = trackedItemDiff.Final.GetType();
                            break;
                    }

                    var propertyInfo = GetValueProviderProperty(trackedItemType, gitDiff.Path);
                    if (propertyInfo != null)
                    {
                        if (trackedItemDiff.Initial != null)
                        {
                            propertyInfo.SetValue(trackedItemDiff.Initial, gitDiff.InitialFileContent);
                        }

                        if (trackedItemDiff.Final != null)
                        {
                            propertyInfo.SetValue(trackedItemDiff.Final, gitDiff.FinalFileContent);
                        }
                    }
                }

                trackedItemDiffs.Add(trackedItemDiff);
            }

            return trackedItemDiffs;
        }

        public TrackedItemHistory GetHistory(TrackedItem trackedItem, int page = 1, int pageSize = 10)
        {
            var history = new TrackedItemHistory();

            var relativeTrackedItemPath =
                _pathProvider.GetRelativeTrackedItemPath(trackedItem.GetType(), trackedItem);

            var paths = new List<string> { relativeTrackedItemPath };
            paths.AddRange(trackedItem.PreviousPaths);

            history.Commits = _gitRepo.GetCommits(page, pageSize, paths);
            history.Count = _gitRepo.Count(paths);

            return history;
        }

        public TrackedItemHistory GetHistory(Type trackedItemType, int page = 1, int pageSize = 10)
        {
            var history = new TrackedItemHistory();

            var relativeTrackedItemPath =
                _pathProvider.GetRelativeTrackedItemPath(trackedItemType);
            var paths = new List<string> { relativeTrackedItemPath };

            history.Commits = _gitRepo.GetCommits(page, pageSize, paths);
            history.Count = _gitRepo.Count(paths);

            return history;
        }

        public TrackedItemHistory GetHistory(int page = 1, int pageSize = 10)
        {
            var history = new TrackedItemHistory
            {
                Commits = _gitRepo.GetCommits(page, pageSize),
                Count = _gitRepo.Count()
            };

            return history;
        }

        private PropertyInfo? GetValueProviderProperty(Type contentType, string fileName)
        {
            string propertyName = Path.GetFileNameWithoutExtension(fileName);
            return contentType
                .GetProperties()
                .FirstOrDefault(x => x.Name.ToSentenceCase().MakeUrlFriendly().Equals(propertyName));
        }

        public async Task<IList<TrackedItemConflict>> GetTrackedItemConflicts()
        {
            var trackedItemConflicts = new List<TrackedItemConflict>();
            var mergeConflicts = _gitRepo.GetMergeConflicts();
            string localPath = _localPathFactory.GetLocalPath();

            foreach (var conflictGrouping in mergeConflicts.GroupBy(x => Path.GetDirectoryName(x.Ours.Path)))
            {
                TrackedItemConflict trackedItemConflict = new TrackedItemConflict
                {
                    ValueProviderConflicts = new List<ValueProviderConflict>()
                };

                foreach (var conflict in conflictGrouping.Where(x => x.Ours.Path.EndsWith(".json")))
                {
                    if (!conflict.Ours.Path.IsTrackedItemJson())
                    {
                        continue;
                    }

                    var fileContents =
                        _gitRepo.GetDiff3Files(conflict.Ours.Path, conflict.Theirs.Path, conflict.Ancestor?.Path);

                    if (!string.IsNullOrEmpty(fileContents.BaseFile))
                    {
                        trackedItemConflict.Ancestor = await DeserializeContentItem(fileContents.BaseFile);
                    }

                    trackedItemConflict.Ours = await DeserializeContentItem(fileContents.OurFile);
                    trackedItemConflict.Theirs = await DeserializeContentItem(fileContents.TheirFile);
                }

                var valueProviderDiffs =
                    conflictGrouping.Where(x =>
                        _valueProviders.Any(vp => vp.Extension.Equals(Path.GetExtension(x.Ours.Path))));

                foreach (var conflict in valueProviderDiffs)
                {
                    if (trackedItemConflict.Ours == null)
                    {
                        string trackedItemJsonPath = _fileProvider.GetTrackedItemJsonForPath(conflict.Ours.Path);
                        trackedItemConflict.Ours = await GetTrackedItem(trackedItemJsonPath);
                        trackedItemConflict.Theirs = await GetTrackedItem(trackedItemJsonPath);
                        trackedItemConflict.Ancestor = await GetTrackedItem(trackedItemJsonPath);
                    }

                    var fileContents =
                        _gitRepo.GetDiff3Files(conflict.Ours.Path, conflict.Theirs.Path, conflict.Ancestor?.Path);

                    var propertyInfo =
                        GetValueProviderProperty(trackedItemConflict.Ours.GetType(), conflict.Ours.Path);

                    if (propertyInfo != null)
                    {
                        propertyInfo.SetValue(trackedItemConflict.Ours, fileContents.OurFile);
                        propertyInfo.SetValue(trackedItemConflict.Theirs, fileContents.TheirFile);
                    }

                    var valueProviderConflict =
                        new ValueProviderConflict
                        {
                            LocalPath = Path.Combine(localPath, $"{conflict.Ours.Path}.LOCAL"),
                            RemotePath = Path.Combine(localPath, $"{conflict.Theirs.Path}.REMOTE")
                        };

                    if (!string.IsNullOrEmpty(fileContents.BaseFile))
                    {
                        valueProviderConflict.BasePath = Path.Combine(localPath, $"{conflict.Ancestor.Path}.BASE");
                        File.WriteAllText(valueProviderConflict.BasePath, fileContents.BaseFile);

                        if (propertyInfo != null)
                        {
                            propertyInfo.SetValue(trackedItemConflict.Ancestor, fileContents.BaseFile);
                        }
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
            foreach (var createOperation in _createOperations.Where(x => x.IsMatch(trackedItem.GetType())))
            {
                await createOperation.Create(trackedItem);
            }
        }

        private async Task PerformDelete(TrackedItem trackedItem)
        {
            foreach (var deleteOperation in _deleteOperations.Where(x => x.IsMatch(trackedItem.GetType())))
            {
                await deleteOperation.Delete(trackedItem);
            }
        }

        private async Task PerformUpdate(TrackedItem trackedItem)
        {
            foreach (var updateOperation in _updateOperations.Where(x => x.IsMatch(trackedItem.GetType())))
            {
                await updateOperation.Update(trackedItem);
            }
        }

        private async Task<TrackedItem> GetTrackedItem(string path)
        {
            if (!path.IsTrackedItemJson())
            {
                path = _fileProvider.GetTrackedItemJsonForPath(path);
            }

            string fileContent = _fileProvider.GetFile(path);
            var trackedItem = await DeserializeContentItem(fileContent);

            return trackedItem;
        }

        public async Task<TrackedItem> Create(string entity)
        {
            var contentItem = await DeserializeContentItem(entity);
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
                    var value = await valueProvider.GetValue(trackedItem, propertyInfo);
                    propertyInfo.SetValue(trackedItem, value);
                }
            }
        }

        public async Task<TrackedItem> Create(TrackedItem trackedItem)
        {
            CheckNameExists(trackedItem.GetType(), trackedItem);
            trackedItem.Id = Guid.NewGuid().ToString();

            await SetNonJsonValues(trackedItem);

            await _fileProvider.UpsertFiles(trackedItem);
            await PerformCreate(trackedItem);

            return trackedItem;
        }

        public async Task<T> Create<T>(T trackedItem) where T : TrackedItem
        {
            return (T)await Create((TrackedItem)trackedItem);
        }

        public bool Stage(TrackedItem trackedItem)
        {
            var relativeTrackedItemPath =
                _pathProvider.GetRelativeTrackedItemPath(trackedItem.GetType(), trackedItem);

            var unstagedItems =
                _gitRepo.GetUnstagedItems().Where(x =>
                    x.Contains(relativeTrackedItemPath) ||
                    trackedItem.PreviousPaths.Any(x.Contains));

            return _gitRepo.Stage(unstagedItems.ToArray());
        }

        public bool Unstage(TrackedItem trackedItem)
        {
            var relativeTrackedItemPath =
                _pathProvider.GetRelativeTrackedItemPath(trackedItem.GetType(), trackedItem);

            var unstagedItems =
                _gitRepo.GetStagedItems().Where(x =>
                    x.Contains(relativeTrackedItemPath) ||
                    trackedItem.PreviousPaths.Any(x.Contains));

            return _gitRepo.Unstage(unstagedItems.ToArray());
        }

        public async Task<T> Update<T>(T trackedItem) where T : TrackedItem
        {
            return (T)await Update((TrackedItem)trackedItem);
        }

        public async Task<TrackedItem> Update(TrackedItem trackedItem)
        {
            await SetNonJsonValues(trackedItem);
            await _fileProvider.UpsertFiles(trackedItem);

            var trackedItemPath = _pathProvider.GetRelativeTrackedItemPath(trackedItem.GetType(), trackedItem);
            var diff = _gitRepo.GetDiffFromHead(new List<string> { trackedItemPath });
            if (diff.Any())
            {
                await PerformUpdate(trackedItem);
            }

            return trackedItem;
        }

        public async Task<IList<TrackedItem>> GetTrackedItemsFromSource(IList<Type> trackedItemTypes)
        {
            IList<TrackedItem> trackedItems = new List<TrackedItem>();
            var documents = _fileProvider.GetFiles(trackedItemTypes);
            foreach (var document in documents)
            {
                var trackedItem = await DeserializeContentItem(document);
                await SetNonJsonValues(trackedItem);

                trackedItems.Add(trackedItem);
            }

            return trackedItems;
        }

        public async Task<IList<TrackedItem>> Commit(string message, string email, string name = null)
        {
            var stagedFiles = _gitRepo.GetStagedItems();
            string commitId = _gitRepo.Commit(message, email, name);
            if (string.IsNullOrEmpty(commitId))
            {
                return null;
            }

            IList<TrackedItem> trackedItems = new List<TrackedItem>();
            foreach (var stagedFile in stagedFiles)
            {
                var trackedItem = await GetTrackedItem(stagedFile);
                if (trackedItems.Any(x => x.Id.Equals(trackedItem.Id)))
                {
                    continue;
                }

                trackedItems.Add(trackedItem);
            }
            return trackedItems;
        }

        public async Task<T> ChangeName<T>(string newName, T trackedItem) where T : TrackedItem
        {
            return (T)await ChangeName(newName, (TrackedItem)trackedItem);
        }

        public async Task<TrackedItem> ChangeName(string newName, TrackedItem trackedItem)
        {
            // make sure we add the previous path before changing the name!
            trackedItem.PreviousPaths.Add(_pathProvider.GetRelativeTrackedItemPath(trackedItem.GetType(), trackedItem));

            await _fileProvider.MoveFile(newName, trackedItem);

            await _fileProvider.UpsertFiles(trackedItem);
            await PerformUpdate(trackedItem);

            return trackedItem;
        }

        public async Task<T> CreateDraft<T>(string name, Type contentType, T trackedItem = null) where T : TrackedItem
        {
            return (T)await CreateDraft(name, contentType, (TrackedItem)trackedItem);
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

            CheckNameExists(contentType, trackedItem);

            await _fileProvider.UpsertFiles(trackedItem);
            await PerformCreate(trackedItem);

            return trackedItem;
        }

        private void CheckNameExists(Type trackedItemType, TrackedItem trackedItem)
        {
            string trackedItemDirectory = _pathProvider.GetTrackedItemPath(trackedItemType, trackedItem);
            if (_fileSystem.Directory.Exists(trackedItemDirectory))
            {
                throw new Exception("A tracked item with this name already exists!");
            }
        }

        public async Task<bool> Delete(TrackedItem trackedItem)
        {
            bool result = await _fileProvider.DeleteFiles(trackedItem);
            if (result)
            {
                try
                {
                    await PerformDelete(trackedItem);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Could not delete tracked item {trackedItem.Id}");
                    return false;
                }
            }

            return true;
        }

        private Type GetContentType(string entity)
        {
            string typeDefinition = JObject.Parse(entity).GetValue("TypeDefinition").Value<string>();
            var trackedType = _gitConfig.TrackedTypes.FirstOrDefault(x => x.Name.Equals(typeDefinition));
            return trackedType ?? typeof(TrackedItem);
        }

        private async Task<TrackedItem> DeserializeContentItem(string document)
        {
            if (string.IsNullOrEmpty(document))
            {
                return null;
            }

            var contentType = GetContentType(document);
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