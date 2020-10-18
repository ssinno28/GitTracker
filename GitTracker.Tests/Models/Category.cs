using System;
using System.Collections.Generic;
using GitTracker.Helpers;
using GitTracker.Interfaces;
using GitTracker.Models;

namespace GitTracker.Tests.Models
{
    public class Category : ITrackedItem
    {
        public Category()
        {
            ParentIds = new List<string>();
        }
        public string Description { get; set; }
        public string SafeName => Name.MakeUrlFriendly();
        public IList<string> ParentIds { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        public string TypeDefinition { get; set; }
        public DateTimeOffset CreatedDate { get; set; }
        public DateTimeOffset ModifiedDate { get; set; }
        public IList<string> PreviousPaths { get; set; }
    }
}