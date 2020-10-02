using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GitTracker.Models;
using LibGit2Sharp;

namespace GitTracker.Interfaces
{
    public interface IGitTrackingService
    {
        Task<bool> Sync(string email, CheckoutFileConflictStrategy strategy = CheckoutFileConflictStrategy.Normal, string userName = null);
        Task<TrackedItem> Create(string entity);
        Task<TrackedItem> Create(TrackedItem trackedItem);
        Task<T> Create<T>(T trackedItem) where T : TrackedItem;
        Task Delete(TrackedItem trackedItem);
        Task<T> Update<T>(T trackedItem) where T : TrackedItem;
        Task<TrackedItem> Update(TrackedItem trackedItem);
        Task<TrackedItem> ChangeName(string newName, TrackedItem trackedItem);
        Task<T> ChangeName<T>(string newName, T trackedItem) where T : TrackedItem;
        Task<TrackedItem> CreateDraft(string name, Type contentType, TrackedItem trackedItem = null);
        Task<T> CreateDraft<T>(string name, Type contentType, T trackedItem = null) where T : TrackedItem;
        bool Stage(TrackedItem trackedItem);
        bool Unstage(TrackedItem trackedItem);
        Task<IList<TrackedItemConflict>> GetTrackedItemConflicts();
        Task<bool> Publish(string email,
            CheckoutFileConflictStrategy strategy = CheckoutFileConflictStrategy.Normal, string userName = null);
        Task<IList<TrackedItemDiff>> GetTrackedItemDiffs(string currentCommitId = null, string newCommitId = null);
        Task<IList<TrackedItemDiff>> GetTrackedItemDiffs(Type trackedType, string currentCommitId = null,
            string newCommitId = null);
        Task<IList<TrackedItemDiff>> GetTrackedItemDiffs(TrackedItem trackedItem,
            string currentCommitId = null, string newCommitId = null);
    }
}