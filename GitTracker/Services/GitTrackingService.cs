﻿using System;
using System.Collections;
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

        public async Task<bool> Publish(string email,
            CheckoutFileConflictStrategy strategy = CheckoutFileConflictStrategy.Normal, string userName = null)
        {
            var result = await Sync(email, strategy, userName);
            if (!result) return false;

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
            CheckoutFileConflictStrategy strategy = CheckoutFileConflictStrategy.Normal, string userName = null)
        {
            var diffFromHead = _gitRepo.GetDiffFromHead();
            if (diffFromHead.Any())
            {
                throw new Exception("Can not merge branch when you have pending changes!");
            }

            string currentCommitId = _gitRepo.GetCurrentCommitId();
            if (!_gitRepo.MergeBranch(branchName, email, strategy, userName)) return false;

            string newCommitId = _gitRepo.GetCurrentCommitId();

            var diff = _gitRepo.GetDiffBetweenBranches(currentCommitId, newCommitId);
            await PerformOpsBasedOnDiff(diff, currentCommitId);

            return true;
        }

        public async Task<bool> Sync(string email, CheckoutFileConflictStrategy strategy = CheckoutFileConflictStrategy.Normal, string userName = null)
        {
            var diffFromHead = _gitRepo.GetDiffFromHead();
            if (diffFromHead.Any())
            {
                throw new Exception("Can not sync when you have pending changes!");
            }

            // if this is the first pull then no need to check the diff
            var commits = _gitRepo.GetCommits();
            if (!commits.Any())
            {
                if (!_gitRepo.Pull(email, strategy, userName)) return false;

                var files = _fileProvider.GetFiles(_gitConfig.TrackedTypes);
                foreach (var fileContent in files)
                {
                    var trackedItem = await DeserializeContentItem(fileContent);
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
            await PerformOpsBasedOnDiff(diff, currentCommitId);

            return true;
        }

        private async Task PerformOpsBasedOnDiff(IList<GitDiff> diff, string currentCommitId)
        {
            foreach (var gitDiff in diff)
            {
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

        public async Task<IList<TrackedItemDiff>> GetTrackedItemDiffs(TrackedItem trackedItem,
            string currentCommitId = null, string newCommitId = null)
        {
            string path = _pathProvider.GetRelativeTrackedItemPath(trackedItem.GetType(), trackedItem);
            return await GetTrackedItemDiffs(new List<string> { path }, currentCommitId, newCommitId);
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
                    trackedItemDiff.Initial = await DeserializeContentItem(gitDiff.InitialFileContent);
                    trackedItemDiff.Final = await DeserializeContentItem(gitDiff.FinalFileContent);

                    trackedItemDiff.TrackedItemGitDiff = gitDiff;
                }

                foreach (var gitDiff in diffGrouping.Where(x => !x.Path.EndsWith(".json")))
                {
                    trackedItemDiff.ValueProviderDiffs.Add(gitDiff);

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
                    if (trackedItemDiff.Initial != null)
                    {
                        propertyInfo.SetValue(trackedItemDiff.Initial, gitDiff.InitialFileContent);
                    }

                    if (trackedItemDiff.Final != null)
                    {
                        propertyInfo.SetValue(trackedItemDiff.Final, gitDiff.FinalFileContent);
                    }
                }

                trackedItemDiff.ChangedProperties = GetChangedProperties(trackedItemDiff.Initial, trackedItemDiff.Final);
                trackedItemDiffs.Add(trackedItemDiff);
            }

            return trackedItemDiffs;
        }

        private IList<PropertyInfo> GetChangedProperties(TrackedItem ours, TrackedItem theirs)
        {
            if (ours == null || theirs == null)
            {
                return new List<PropertyInfo>();
            }

            return ours.GetType()
            .GetProperties()
            .Where(x =>
            {
                var ourValue = x.GetValue(ours);
                var theirValue = x.GetValue(theirs);

                if (x.IsPropertyACollection())
                {
                    //TODO: figure out how to compare lists
                    return false;
                }

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

        private PropertyInfo GetValueProviderProperty(Type contentType, string fileName)
        {
            string propertyName = Path.GetFileNameWithoutExtension(fileName);
            return contentType
                .GetProperties()
                .First(x => x.Name.ToSentenceCase().MakeUrlFriendly().Equals(propertyName));
        }

        public async Task<IList<TrackedItemConflict>> GetTrackedItemConflicts()
        {
            var trackedItemConflicts = new List<TrackedItemConflict>();
            var mergeConflicts = _gitRepo.GetMergeConflicts();

            foreach (var conflictGrouping in mergeConflicts.GroupBy(x => Path.GetDirectoryName(x.Ours.Path)))
            {
                TrackedItemConflict trackedItemConflict = new TrackedItemConflict
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
                        trackedItemConflict.Ancestor = await DeserializeContentItem(fileContents.BaseFile);
                    }

                    trackedItemConflict.Ours = await DeserializeContentItem(fileContents.OurFile);
                    trackedItemConflict.Theirs = await DeserializeContentItem(fileContents.TheirFile);

                    trackedItemConflict.ChangedProperties =
                        GetChangedProperties(trackedItemConflict.Ours, trackedItemConflict.Theirs);
                }

                foreach (var conflict in conflictGrouping.Where(x => !x.Ours.Path.EndsWith(".json")))
                {
                    var fileContents =
                        _gitRepo.GetDiff3Files(conflict.Ours.Path, conflict.Theirs.Path, conflict.Ancestor?.Path);

                    var propertyInfo =
                        GetValueProviderProperty(trackedItemConflict.Ours.GetType(), conflict.Ours.Path);

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

                trackedItemConflict.ChangedProperties =
                    GetChangedProperties(trackedItemConflict.Ours, trackedItemConflict.Theirs);
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

        private async Task<TrackedItem> GetTrackedItem(string path)
        {
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
                    propertyInfo.SetValue(trackedItem, await valueProvider.GetValue(trackedItem, propertyInfo));
                }
            }
        }

        public async Task<TrackedItem> Create(TrackedItem trackedItem)
        {
            CheckNameExists(trackedItem.GetType(), trackedItem);

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

        public bool Stage(TrackedItem trackedItem)
        {
            var relativeTrackedItemPath =
                _pathProvider.GetRelativeTrackedItemPath(trackedItem.GetType(), trackedItem);

            var unstagedItems =
                _gitRepo.GetUnstagedItems().Where(x => x.Contains(relativeTrackedItemPath));

            return _gitRepo.Stage(unstagedItems.ToArray());
        }

        public bool Unstage(TrackedItem trackedItem)
        {
            var relativeTrackedItemPath =
                _pathProvider.GetRelativeTrackedItemPath(trackedItem.GetType(), trackedItem);

            var unstagedItems =
                _gitRepo.GetStagedItems().Where(x => x.Contains(relativeTrackedItemPath));

            return _gitRepo.Unstage(unstagedItems.ToArray());
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

        public async Task<T> ChangeName<T>(string newName, T trackedItem) where T : TrackedItem
        {
            return (T)await ChangeName(newName, (TrackedItem)trackedItem);
        }

        public async Task<TrackedItem> ChangeName(string newName, TrackedItem trackedItem)
        {
            trackedItem.Name = newName;
            CheckNameExists(trackedItem.GetType(), trackedItem);

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
            if (Directory.Exists(trackedItemDirectory))
            {
                throw new Exception("A tracked item with this name already exists!");
            }
        }

        public async Task Delete(TrackedItem trackedItem)
        {
            await _fileProvider.DeleteFiles(trackedItem);
            await PerformDelete(trackedItem);
        }

        private Type GetContentType(string entity)
        {
            string typeDefinition = JObject.Parse(entity).GetValue("TypeDefinition").Value<string>();
            return _gitConfig.TrackedTypes.First(x => x.Name.Equals(typeDefinition));
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