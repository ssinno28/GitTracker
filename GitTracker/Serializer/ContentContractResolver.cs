using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using GitTracker.Converters;
using GitTracker.Interfaces;
using GitTracker.Models;

namespace GitTracker.Serializer
{
    public class ContentContractResolver
    {
        private readonly IEnumerable<ContentJsonConverter> _jsonConverters;
        private readonly IEnumerable<IValueProvider> _valueProviders;

        public JsonSerializerOptions Options { get; }

        public ContentContractResolver(
            IEnumerable<ContentJsonConverter> jsonConverters,
            IEnumerable<IValueProvider> valueProviders)
        {
            _jsonConverters = jsonConverters;
            _valueProviders = valueProviders;

            Options = new JsonSerializerOptions
            {
                TypeInfoResolver = new DefaultJsonTypeInfoResolver
                {
                    Modifiers = { ModifyTypeInfo }
                }
            };
        }

        private void ModifyTypeInfo(JsonTypeInfo typeInfo)
        {
            if (!typeof(TrackedItem).IsAssignableFrom(typeInfo.Type))
                return;

            foreach (var property in typeInfo.Properties)
            {
                if (property.AttributeProvider is not PropertyInfo memberInfo)
                    continue;

                var valueProvider = _valueProviders.FirstOrDefault(x => x.IsMatch(memberInfo));
                if (valueProvider?.IgnoreInJson == true)
                {
                    property.ShouldSerialize = (_, _) => false;
                }

                var jsonConverter = _jsonConverters.FirstOrDefault(x => x.IsMatch(memberInfo));
                if (jsonConverter != null)
                {
                    property.CustomConverter = jsonConverter;
                }
            }
        }
    }
}