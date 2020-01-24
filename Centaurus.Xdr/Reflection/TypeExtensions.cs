using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Xdr
{
    public static class TypeExtensions
    {
        public static Type FindGenericInterfaceImplementation(this Type type, Type genericBaseType)
        {
            if (type == null || type == typeof(object) || type.IsValueType) return null;
            foreach (var i in type.GetInterfaces())
            {
                if (i.IsGenericType && i.IsGenericImplementation(genericBaseType)) return i;
            }
            return FindGenericInterfaceImplementation(type.BaseType, genericBaseType);
        }

        public static bool IsGenericImplementation(this Type type, Type genericBaseType)
        {
            return type != null && type.IsGenericType && type.GetGenericTypeDefinition() == genericBaseType;
        }
    }
}
