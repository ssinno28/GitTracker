using System;
using System.Threading.Tasks;
using GitTracker.Models;

namespace GitTracker.Interfaces
{
    public interface IDeleteOperation
    {
        bool IsMatch(Type contentType);
        Task Delete(ITrackedItem trackedItem);
    }
}