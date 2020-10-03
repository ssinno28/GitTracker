using System;
using System.Collections.Generic;
using System.Reflection;

namespace GitTracker.Helpers
{
    public static class ReflectionHelpers
    {
        public static bool IsPropertyACollection(this PropertyInfo property)
        {
            return property.PropertyType.GetInterface(typeof(IEnumerable<>).FullName) != null &&
                   property.PropertyType != typeof(String) &&
                   property.PropertyType != typeof(string);
        }

        public static bool IsACollection(this Type @type)
        {
            return @type.GetInterface(typeof(IEnumerable<>).FullName) != null &&
                   @type != typeof(String) &&
                   @type != typeof(string);
        }

        public static bool IsPrimitiveType(this PropertyInfo property)
        {
            var @type = property.PropertyType;
            return @type.IsPrimitive || @type.IsValueType || @type == typeof(string);
        }

        public static bool IsPrimitiveType(this Type @type)
        {
            return @type.IsPrimitive || @type.IsValueType || @type == typeof(string);
        }

        public static Type GetPropertyType(this PropertyInfo propertyInfo)
        {
            bool nullable = Nullable.GetUnderlyingType(propertyInfo.PropertyType) != null;
            var propertyType = nullable
                ? Nullable.GetUnderlyingType(propertyInfo.PropertyType)
                : propertyInfo.PropertyType;

            return propertyType;
        }
    }
}