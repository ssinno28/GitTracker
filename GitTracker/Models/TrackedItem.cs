using System;

namespace GitTracker.Models
{
    public class TrackedItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string TypeDefinition => GetType().Name;
        public DateTimeOffset CreatedDate { get; set; }
        public DateTimeOffset ModifiedDate { get; set; }
    }
}