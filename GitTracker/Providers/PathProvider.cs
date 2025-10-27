using System;
using System.IO;
using GitTracker.Helpers;
using GitTracker.Interfaces;
using GitTracker.Models;

namespace GitTracker.Providers
{
    public class PathProvider : IPathProvider
    {
        private readonly ILocalPathFactory _localPathFactory;
        private readonly GitConfig _gitConfig;

        public PathProvider(ILocalPathFactory localPathFactory, GitConfig gitConfig)
        {
            _localPathFactory = localPathFactory;
            _gitConfig = gitConfig;
        }

        public string GetTrackedItemPath(Type contentType, TrackedItem contentItem = null)
        {
            _gitConfig.ContentPath ??= string.Empty;

            string contentTypeName = contentType.Name;
            var contentItemPath =
                          contentItem != null
                              ? Path.Combine(_localPathFactory.GetLocalPath(), _gitConfig.ContentPath, contentTypeName, contentItem.Name.MakeUrlFriendly())
                              : Path.Combine(_localPathFactory.GetLocalPath(), _gitConfig.ContentPath, contentTypeName);

            return contentItemPath;
        }

        public string GetRelativeTrackedItemPath(Type contentType, TrackedItem contentItem = null)
        {
            _gitConfig.ContentPath ??= string.Empty;

            string contentTypeName = contentType.Name;
            var contentItemPath =
                contentItem != null
                    ? $"{contentTypeName}/{contentItem.Name.MakeUrlFriendly()}"
                    : $"{contentTypeName}";

            if (!string.IsNullOrEmpty(_gitConfig.ContentPath))
            {
                contentItemPath = $"{_gitConfig.ContentPath}/{contentItemPath}";
            }

            return contentItemPath;
        }
    }
}