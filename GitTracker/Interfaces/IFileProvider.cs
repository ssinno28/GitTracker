using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GitTracker.Models;

namespace GitTracker.Interfaces
{
    public interface IFileProvider
    {
        IList<string> GetFiles(IList<Type> contentTypes);
        string GetFile(string path);
        Task<bool> DeleteFiles(params ITrackedItem[] trackedItems);
        Task<bool> UpsertFiles(params ITrackedItem[] trackedItems);
        Task<bool> MoveFile(string newName, ITrackedItem trackedItem);
    }
}