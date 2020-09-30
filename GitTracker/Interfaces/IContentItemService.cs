using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GitTracker.Models;

namespace GitTracker.Interfaces
{
    public interface IContentItemService
    {
        Task Add(string entity, IList<Type> contentTypes);
        Task Add(TrackedItem trackedItem);
        Task Delete(TrackedItem trackedItem);
        Task<bool> Update(TrackedItem trackedItem);
        Task<bool> ChangeName(string newName, TrackedItem trackedItem);
        Task<TrackedItem> CreateDraft(string name, Type contentType, TrackedItem trackedItem = null);
    }
}