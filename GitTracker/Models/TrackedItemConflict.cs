using System.Collections.Generic;
using GitTracker.Interfaces;

namespace GitTracker.Models
{
    public class TrackedItemConflict
    {
        public ITrackedItem Ancestor { get; set; }
        public ITrackedItem Theirs { get; set; }
        public ITrackedItem Ours { get; set; }
        public IList<ValueProviderConflict> ValueProviderConflicts { get; set; }
    }
}