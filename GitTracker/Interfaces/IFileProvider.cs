using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GitTracker.Models;

namespace GitTracker.Interfaces
{
    public interface IFileProvider
    {
        IList<string> GetFiles(IList<Type> contentTypes);
        Task<bool> DeleteFiles(params TrackedItem[] trackedItems);
        Task<bool> UpsertFiles(params TrackedItem[] trackedItems);
        Task<bool> MoveFile(string newName, TrackedItem trackedItem);
    }
}