using System.Collections.Generic;
using GitTracker.Helpers;
using GitTracker.Models;

namespace GitTracker.Tests.Models
{
    public class Category : TrackedItem
    {
        public Category()
        {
            ParentIds = new List<string>();
        }
        public string Description { get; set; }
        public string SafeName => Name.MakeUrlFriendly();
        public IList<string> ParentIds { get; set; }
    }
}