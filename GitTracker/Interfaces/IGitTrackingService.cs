using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GitTracker.Models;

namespace GitTracker.Interfaces
{
    public interface IGitTrackingService
    {
        Task Sync(string email, IList<Type> contentTypes, string userName = null);
        Task<TrackedItem> Add(string entity, IList<Type> contentTypes);
        Task<TrackedItem> Add(TrackedItem trackedItem);
        Task<T> Add<T>(T trackedItem) where T : TrackedItem;
        Task Delete(TrackedItem trackedItem);
        Task<T> Update<T>(T trackedItem) where T : TrackedItem;
        Task<TrackedItem> Update(TrackedItem trackedItem);
        Task<TrackedItem> ChangeName(string newName, TrackedItem trackedItem);
        Task<T> ChangeName<T>(string newName, T trackedItem) where T : TrackedItem;
        Task<TrackedItem> CreateDraft(string name, Type contentType, TrackedItem trackedItem = null);
        Task<T> CreateDraft<T>(string name, Type contentType, T trackedItem = null) where T : TrackedItem;
        bool Stage(TrackedItem trackedItem);
    }
}