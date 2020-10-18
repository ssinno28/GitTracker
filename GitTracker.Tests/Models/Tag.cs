using System;
using System.Collections.Generic;
using GitTracker.Helpers;
using GitTracker.Interfaces;
using GitTracker.Models;

namespace GitTracker.Tests.Models
{
    public class Tag : ITrackedItem
    {
        public string SafeName => Name.MakeUrlFriendly();
        public string Id { get; set; }
        public string Name { get; set; }
        public string TypeDefinition { get; set; }
        public DateTimeOffset CreatedDate { get; set; }
        public DateTimeOffset ModifiedDate { get; set; }
        public IList<string> PreviousPaths { get; set; }
    }
}