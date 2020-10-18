using System;
using System.IO;
using GitTracker.Helpers;
using GitTracker.Interfaces;
using GitTracker.Models;

namespace GitTracker.Providers
{
    public class PathProvider : IPathProvider
    {
        private readonly GitConfig _gitConfig;

        public PathProvider(GitConfig gitConfig)
        {
            _gitConfig = gitConfig;
        }

        public string GetTrackedItemPath(Type contentType, ITrackedItem contentItem = null)
        {
            string contentTypeName = contentType.Name;
            var contentItemPath =
                          contentItem != null
                              ? Path.Combine(_gitConfig.LocalPath, contentTypeName, contentItem.Name.MakeUrlFriendly())
                              : Path.Combine(_gitConfig.LocalPath, contentTypeName);

            return contentItemPath;
        }

        public string GetRelativeTrackedItemPath(Type contentType, ITrackedItem contentItem = null)
        {
            string contentTypeName = contentType.Name;
            var contentItemPath =
                contentItem != null
                    ? $"{contentTypeName}/{contentItem.Name.MakeUrlFriendly()}"
                    : $"{contentTypeName}";

            return contentItemPath;
        }
    }
}