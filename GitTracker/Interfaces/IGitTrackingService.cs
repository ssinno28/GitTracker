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
        Task Delete(TrackedItem trackedItem);
        Task<TrackedItem> Update(TrackedItem trackedItem);
        Task<TrackedItem> ChangeName(string newName, TrackedItem trackedItem);
        Task<TrackedItem> CreateDraft(string name, Type contentType, TrackedItem trackedItem = null);
        bool Stage(TrackedItem trackedItem);
    }
}