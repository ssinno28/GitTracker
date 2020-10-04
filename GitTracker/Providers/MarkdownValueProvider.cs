using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using GitTracker.Attributes;
using GitTracker.Helpers;
using GitTracker.Interfaces;
using GitTracker.Models;

namespace GitTracker.Providers
{
    public class MarkdownValueProvider : IValueProvider
    {
        private readonly IPathProvider _pathProvider;

        public MarkdownValueProvider(IPathProvider pathProvider)
        {
            _pathProvider = pathProvider;
        }

        public string Extension => ".md";

        public bool IsMatch(PropertyInfo propertyInfo)
        {
            return propertyInfo.GetCustomAttribute<MarkdownAttribute>() != null;
        }

        public async Task<object> GetValue(TrackedItem trackedItem, PropertyInfo propertyInfo)
        {
            var contentItemPath = _pathProvider.GetTrackedItemPath(trackedItem.GetType(), trackedItem);
            string filePath = Path.Combine(contentItemPath, $"{propertyInfo.Name.ToSentenceCase().MakeUrlFriendly()}.md");

            if (!File.Exists(filePath)) return string.Empty;

            return File.ReadAllText(filePath);
        }
    }
}