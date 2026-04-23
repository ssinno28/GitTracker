using System.Reflection;
using System.Text.Json.Serialization;

namespace GitTracker.Converters
{
    public abstract class ContentJsonConverter : JsonConverterFactory
    {
        public abstract bool IsMatch(PropertyInfo propertyInfo);
    }
}