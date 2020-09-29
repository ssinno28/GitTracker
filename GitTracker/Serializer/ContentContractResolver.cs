using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GitTracker.Attributes;
using GitTracker.Converters;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace GitTracker.Serializer
{
    public class ContentContractResolver : DefaultContractResolver
    {
        private readonly IEnumerable<ContentJsonConverter> _jsonConverters;

        public ContentContractResolver(IEnumerable<ContentJsonConverter> jsonConverters)
        {
            _jsonConverters = jsonConverters;
        }

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            JsonProperty property = base.CreateProperty(member, memberSerialization);

            var propertyInfo = member as PropertyInfo;
            bool isMarkdownProperty = propertyInfo?.GetCustomAttribute<MarkdownAttribute>() != null;
            property.ShouldSerialize = instance =>
            {
                if (isMarkdownProperty)
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