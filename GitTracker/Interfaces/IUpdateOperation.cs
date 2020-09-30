using System;
using System.Threading.Tasks;
using GitTracker.Models;

namespace GitTracker.Interfaces
{
    public interface IUpdateOperation
    {
        bool IsMatch(Type contentType);
        Task Update(TrackedItem trackedItem);
    }
}