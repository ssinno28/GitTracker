using System;
using GitTracker.Models;

namespace GitTracker.Interfaces
{
    public interface IPathProvider
    {
        string GetTrackedItemPath(Type contentType, TrackedItem contentItem = null);
        string GetRelativeTrackedItemPath(Type contentType, TrackedItem contentItem = null);
    }
}