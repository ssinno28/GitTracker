using System.Collections.Generic;
using System.Reflection;

namespace GitTracker.Models
{
    public class TrackedItemConflict
    {
        public TrackedItem Ancestor { get; set; }
        public TrackedItem Theirs { get; set; }
        public TrackedItem Ours { get; set; }
        public IList<PropertyInfo> ChangedProperties { get; set; }
        public IList<ValueProviderConflict> ValueProviderConflicts { get; set; }
    }
}