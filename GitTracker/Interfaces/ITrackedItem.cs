using System;
using System.Collections.Generic;

namespace GitTracker.Interfaces
{
    public interface ITrackedItem
    {
        string Id { get; set; }
        string Name { get; set; }
        string TypeDefinition { get; set; }
        DateTimeOffset CreatedDate { get; set; }
        DateTimeOffset ModifiedDate { get; set; }
        IList<string> PreviousPaths { get; set; }
    }
}