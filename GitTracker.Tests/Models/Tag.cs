using GitTracker.Helpers;
using GitTracker.Models;

namespace GitTracker.Tests.Models
{
    public class Tag : TrackedItem
    {
        public string SafeName => Name.MakeUrlFriendly();
    }
}