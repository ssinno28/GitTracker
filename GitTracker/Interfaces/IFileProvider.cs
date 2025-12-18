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
        Task<bool> DeleteFiles(params TrackedItem[] trackedItems);
        Task<bool> DeleteFile(string path);
        Task<bool> DeleteFileFromRelativePath(string path);
        Task<bool> UpsertFiles(params TrackedItem[] trackedItems);
        Task<bool> MoveFile(string newName, TrackedItem trackedItem);
        string GetTrackedItemJsonForPath(string path);
        IList<string> GetFilesForTrackedItems(Type trackedType, TrackedItem? trackedItem = null);
    }
}