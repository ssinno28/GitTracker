using System.Reflection;
using Newtonsoft.Json;

namespace GitTracker.Converters
{
    public abstract class ContentJsonConverter : JsonConverter
    {
        public abstract bool IsMatch(PropertyInfo propertyInfo);
    }
}