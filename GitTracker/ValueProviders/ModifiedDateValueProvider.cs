using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using GitTracker.Interfaces;
using GitTracker.Models;

namespace GitTracker.ValueProviders
{
    public class ModifiedDateValueProvider : IValueProvider
    {
        private readonly IGitRepo _gitRepo;
        private readonly IPathProvider _pathProvider;

        public ModifiedDateValueProvider(IGitRepo gitRepo, IPathProvider pathProvider)
        {
            _gitRepo = gitRepo;
            _pathProvider = pathProvider;
        }

        public bool IgnoreInJson => true;
        public string Extension => string.Empty;
        public bool IsMatch(PropertyInfo propertyInfo)
        {
            return propertyInfo.Name.Equals(nameof(TrackedItem.ModifiedDate));
        }

        public async Task<object> GetValue(TrackedItem trackedItem, PropertyInfo propertyInfo)
        {
            var relativeTrackedItemPath =
                _pathProvider.GetRelativeTrackedItemPath(trackedItem.GetType(), trackedItem);

            var commits = 
                _gitRepo.GetAllCommitsForPath(relativeTrackedItemPath);

            return !commits.Any() ? default(DateTimeOffset) : commits.Max(x => x.Date);
        }
    }
}