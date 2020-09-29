using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GitTracker.Models;

namespace GitTracker.Interfaces
{
    public interface IContentItemService
    {
        Task Add(string entity, IList<Type> contentTypes);
        Task Add(TrackedItem contentItem);
        Task Delete(TrackedItem contentItem);
        Task<bool> Update(TrackedItem contentItem);
        Task<bool> ChangeName(string newName, TrackedItem contentItem);
        Task<TrackedItem> CreateDraft(string name, Type contentType, TrackedItem contentItem = null);
    }
}