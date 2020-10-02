using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GitTracker.Models;
using LibGit2Sharp;

namespace GitTracker.Interfaces
{
    public interface IGitTrackingService
    {
        /// <summary>
        /// Will perform a pull request to get all new changes from the remote. Once the changes are pulled git diff is called to see
        /// what has been added, deleted or modified and will call the appropriate CRUD operation to update your content.
        /// </summary>
        /// <param name="email"></param>
        /// <param name="strategy"></param>
        /// <param name="userName"></param>
        /// <returns></returns>
        Task<bool> Sync(string email, CheckoutFileConflictStrategy strategy = CheckoutFileConflictStrategy.Normal, string userName = null);

        /// <summary>
        /// Will create an entity based on a json string that is passed in. It derives the Type from the TypeDefiniton field in the json.
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        Task<TrackedItem> Create(string entity);

        /// <summary>
        /// Creates a folder for this specific tracked item and places a json file along with any other files (markdown for example)
        /// into it.
        /// </summary>
        /// <param name="trackedItem"></param>
        /// <returns></returns>
        Task<TrackedItem> Create(TrackedItem trackedItem);

        /// <summary>
        /// Performs cast of type before and after creating a tracked item
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="trackedItem"></param>
        /// <returns></returns>
        Task<T> Create<T>(T trackedItem) where T : TrackedItem;

        /// <summary>
        /// Deletes a tracked item folder.
        /// </summary>
        /// <param name="trackedItem"></param>
        /// <returns></returns>
        Task Delete(TrackedItem trackedItem);

        /// <summary>
        /// Updates a specific tracked item
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="trackedItem"></param>
        /// <returns></returns>
        Task<T> Update<T>(T trackedItem) where T : TrackedItem;

        /// <summary>
        /// Updates a specific tracked item
        /// </summary>
        /// <param name="trackedItem"></param>
        /// <returns></returns>
        Task<TrackedItem> Update(TrackedItem trackedItem);

        /// <summary>
        /// Creates a new folder for a tracked item and moves all of the files into it 
        /// </summary>
        /// <param name="newName"></param>
        /// <param name="trackedItem"></param>
        /// <returns></returns>
        Task<TrackedItem> ChangeName(string newName, TrackedItem trackedItem);

        /// <summary>
        /// Creates a new folder for a tracked item and moves all of the files into it
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="newName"></param>
        /// <param name="trackedItem"></param>
        /// <returns></returns>
        Task<T> ChangeName<T>(string newName, T trackedItem) where T : TrackedItem;

        /// <summary>
        /// Creates a draft for a tracked item
        /// </summary>
        /// <param name="name"></param>
        /// <param name="contentType"></param>
        /// <param name="trackedItem"></param>
        /// <returns></returns>
        Task<TrackedItem> CreateDraft(string name, Type contentType, TrackedItem trackedItem = null);

        /// <summary>
        /// Creates a draft for a tracked item
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name"></param>
        /// <param name="contentType"></param>
        /// <param name="trackedItem"></param>
        /// <returns></returns>
        Task<T> CreateDraft<T>(string name, Type contentType, T trackedItem = null) where T : TrackedItem;

        /// <summary>
        /// Stages all of the files associated with a tracked item
        /// </summary>
        /// <param name="trackedItem"></param>
        /// <returns></returns>
        bool Stage(TrackedItem trackedItem);

        /// <summary>
        /// Unstages all of the files associated with a tracked item
        /// </summary>
        /// <param name="trackedItem"></param>
        /// <returns></returns>
        bool Unstage(TrackedItem trackedItem);

        /// <summary>
        /// Gets all of the current merge conflicts in the repo
        /// </summary>
        /// <returns></returns>
        Task<IList<TrackedItemConflict>> GetTrackedItemConflicts();

        /// <summary>
        /// Performs a sync then then push to the remote repo
        /// </summary>
        /// <param name="email"></param>
        /// <param name="strategy"></param>
        /// <param name="userName"></param>
        /// <returns></returns>
        Task<bool> Publish(string email,
            CheckoutFileConflictStrategy strategy = CheckoutFileConflictStrategy.Normal, string userName = null);

        /// <summary>
        /// Gets all of the diffs between two commits
        /// </summary>
        /// <param name="currentCommitId"></param>
        /// <param name="newCommitId"></param>
        /// <returns></returns>
        Task<IList<TrackedItemDiff>> GetTrackedItemDiffs(string currentCommitId = null, string newCommitId = null);

        /// <summary>
        /// Gets all of the diffs between two commits
        /// </summary>
        /// <param name="trackedType"></param>
        /// <param name="currentCommitId"></param>
        /// <param name="newCommitId"></param>
        /// <returns></returns>
        Task<IList<TrackedItemDiff>> GetTrackedItemDiffs(Type trackedType, string currentCommitId = null,
            string newCommitId = null);

        /// <summary>
        /// Gets all of the diffs between two commits
        /// </summary>
        /// <param name="trackedItem"></param>
        /// <param name="currentCommitId"></param>
        /// <param name="newCommitId"></param>
        /// <returns></returns>
        Task<IList<TrackedItemDiff>> GetTrackedItemDiffs(TrackedItem trackedItem,
            string currentCommitId = null, string newCommitId = null);
    }
}