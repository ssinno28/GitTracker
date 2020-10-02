using System.Collections.Generic;
using System.Reflection;

namespace GitTracker.Models
{
    public class TrackedItemDiff
    {
        public TrackedItem Final { get; set; }
        public TrackedItem Initial { get; set; }
        public GitDiff TrackedItemGitDiff { get; set; }
        public IList<PropertyInfo> ChangedProperties { get; set; }
        public IList<GitDiff> ValueProviderDiffs { get; set; }
    }
}