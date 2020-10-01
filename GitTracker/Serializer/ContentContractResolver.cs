using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GitTracker.Converters;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using IValueProvider = GitTracker.Interfaces.IValueProvider;

namespace GitTracker.Serializer
{
    public class ContentContractResolver : DefaultContractResolver
    {
        private readonly IEnumerable<ContentJsonConverter> _jsonConverters;
        private readonly IEnumerable<IValueProvider> _valueProviders;

        public ContentContractResolver(IEnumerable<ContentJsonConverter> jsonConverters, IEnumerable<IValueProvider> valueProviders)
        {
            _jsonConverters = jsonConverters;
            _valueProviders = valueProviders;
        }

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            JsonProperty property = base.CreateProperty(member, memberSerialization);

            var propertyInfo = member as PropertyInfo;
            var valueProvider = _valueProviders.FirstOrDefault(x => x.IsMatch(propertyInfo));
            property.ShouldSerialize = instance =>
            {
                if (valueProvider != null)
                {
                    return false;
                }

                return true;
            };

            var jsonConverter = _jsonConverters.FirstOrDefault(x => x.IsMatch(propertyInfo));
            if (jsonConverter != null)
            {
                property.Converter = jsonConverter;
            }

            return property;
        }
    }
}