using System;
using System.Threading.Tasks;
using GitTracker.Models;

namespace GitTracker.Interfaces
{
    public interface ICreateOperation
    {
        bool IsMatch(Type contentType);
        Task Create(ITrackedItem trackedItem);
    }
}