using System;
using System.Collections.Generic;

namespace GitTracker.Models
{
    public abstract class TrackedItem
    {
        protected TrackedItem()
        {
            PreviousPaths = new List<string>();
        }

        public string Id { get; set; }
        public string Name { get; set; }
        public string TypeDefinition => GetType().Name;
        public DateTimeOffset CreatedDate { get; set; }
        public DateTimeOffset ModifiedDate { get; set; }
        public IList<string> PreviousPaths { get; set; }
    }
}