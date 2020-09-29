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

        public string GetTrackedItemPath(Type contentType, TrackedItem contentItem = null)
        {
            string contentTypeName = contentType.Name;
            var contentItemPath =
                          contentItem != null
                              ? $"{_gitConfig.LocalPath}\\{contentTypeName}\\{contentItem.Name.MakeUrlFriendly()}"
                              : $"{_gitConfig.LocalPath}\\{contentTypeName}\\{contentType.Name}-temp";

            if (!Directory.Exists(contentItemPath))
            {
                Directory.CreateDirectory(contentItemPath);
            }

            return contentItemPath;
        }

        public string GetRelativeTrackedItemPath(Type contentType, TrackedItem contentItem = null)
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