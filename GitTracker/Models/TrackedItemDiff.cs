using System.Collections.Generic;

namespace GitTracker.Models
{
    public class TrackedItemDiff
    {
        public TrackedItem Final { get; set; }
        public TrackedItem Initial { get; set; }
        public GitDiff TrackedItemGitDiff { get; set; }
        public IList<GitDiff> ValueProviderDiffs { get; set; } = new List<GitDiff>();
    }
}