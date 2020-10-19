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

        public PathProvider(ILocalPathFactory localPathFactory)
        {
            _localPathFactory = localPathFactory;
        }

        public string GetTrackedItemPath(Type contentType, TrackedItem contentItem = null)
        {
            string contentTypeName = contentType.Name;
            var contentItemPath =
                          contentItem != null
                              ? Path.Combine(_localPathFactory.GetLocalPath(), contentTypeName, contentItem.Name.MakeUrlFriendly())
                              : Path.Combine(_localPathFactory.GetLocalPath(), contentTypeName);

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