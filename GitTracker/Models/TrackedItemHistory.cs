using System.Collections.Generic;

namespace GitTracker.Models
{
    public class TrackedItemHistory
    {
        public int Count { get; set; }
        public IList<GitCommit> Commits { get; set; }
    }
}