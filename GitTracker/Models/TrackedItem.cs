using System;
using System.Collections.Generic;

namespace GitTracker.Models
{
    public class TrackedItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string TypeDefinition => GetType().Name;
        public IList<CommitReference> CommitReferences { get; set; } = new List<CommitReference>();
    }

    public class CommitReference
    {
        public string Sha { get; set; }
        public DateTimeOffset CommitDate { get; set; }
    }
}