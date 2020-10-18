using System.Collections.Generic;
using GitTracker.Interfaces;

namespace GitTracker.Models
{
    public class TrackedItemDiff
    {
        public ITrackedItem Final { get; set; }
        public ITrackedItem Initial { get; set; }
        public GitDiff TrackedItemGitDiff { get; set; }
        public IList<GitDiff> ValueProviderDiffs { get; set; }
    }
}