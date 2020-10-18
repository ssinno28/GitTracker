using System;
using GitTracker.Models;

namespace GitTracker.Interfaces
{
    public interface IPathProvider
    {
        string GetTrackedItemPath(Type contentType, ITrackedItem contentItem = null);
        string GetRelativeTrackedItemPath(Type contentType, ITrackedItem contentItem = null);
    }
}