using System.Reflection;
using System.Threading.Tasks;
using GitTracker.Models;

namespace GitTracker.Interfaces
{
    public interface IValueProvider
    {
        bool IsMatch(PropertyInfo propertyInfo);
        Task<object> GetValue(TrackedItem trackedItem, PropertyInfo propertyInfo);
    }
}