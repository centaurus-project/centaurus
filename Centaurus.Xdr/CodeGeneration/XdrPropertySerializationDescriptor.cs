using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Centaurus
{
    internal class XdrPropertySerializationDescriptor
    {
        public XdrPropertySerializationDescriptor(PropertyInfo prop)
        {
            var propType = prop.PropertyType;
            if (propType == typeof(object)) throw new InvalidOperationException($"Generalized object serialization not supported. Check {FormatPropertyName(prop)}.");

            Property = prop;
            PrimitiveType = propType;

            //determine optional property
            IsOptional = prop.GetCustomAttribute<XdrFieldAttribute>().Optional;

            //if this is a primitive type, we are done
            if (PrimitiveValueTypes.Contains(propType)) return;

            //check if it's a serializable contract by itself
            if (IsXdrContractType(propType))
            {
                PrimitiveType = typeof(object);
                GenericArgument = propType;
                return;
            }

            //handle enums
            if (propType.IsEnum)
            {
                //verify enum underlying type
                EnsureValidEnumType(propType);
                //and treat it as int32
                PrimitiveType = typeof(int);
                return;
            }

            //handle nullable types
            if (propType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                var valueType = propType.GetGenericArguments().SingleOrDefault();
                if (IsPrimitiveValueType(valueType))
                {
                    IsOptional = true;
                    IsNullable = true;
                    PrimitiveType = valueType;
                    return;
                }
            }

            //handle lists serialization
            if (propType.GetGenericTypeDefinition() == typeof(List<>))
            {
                var listGenericType = propType.GetGenericArguments().SingleOrDefault();
                if (IsXdrContractType(listGenericType))
                {
                    PrimitiveType = typeof(List<>);
                    GenericArgument = listGenericType;
                    return;
                }
            }

            //no suitable primitive serializer found
            throw new InvalidOperationException($"Type {propType.FullName} serialization not supported. Check {prop.DeclaringType.FullName}.{prop.Name}.");
        }

        public PropertyInfo Property { get; set; }

        public Type PrimitiveType { get; set; }

        public Type GenericArgument { get; set; }

        public bool IsOptional { get; set; }

        public bool IsNullable { get; set; }

        private bool IsXdrContractType(Type type)
        {
            return type.GetCustomAttribute<XdrContractAttribute>(true) != null;
        }

        private bool IsPrimitiveValueType(Type type)
        {
            return PrimitiveValueTypes.Contains(type);
        }

        private void EnsureValidEnumType(Type enumType)
        {
            var underylingType = Enum.GetUnderlyingType(enumType);
            if (!AllowedEnumTypes.Contains(underylingType))
                throw new InvalidCastException($"XDR serialization is not supported for enums with the underlying type ${underylingType.FullName}.");
        }

        private string FormatPropertyName(PropertyInfo prop)
        {
            return $"{prop.DeclaringType.FullName}.{prop.Name}";
        }

        private static readonly HashSet<Type> PrimitiveValueTypes = new HashSet<Type>() {
                typeof(bool),
                typeof(int),
                typeof(uint),
                typeof(long),
                typeof(ulong),
                typeof(string),
                typeof(float),
                typeof(double),
                typeof(byte[]),
                typeof(int[]),
                typeof(long[]),
                typeof(float[]),
                typeof(double[]),
                typeof(List<int>),
                typeof(List<long>),
                typeof(List<float>),
                typeof(List<double>),
            };

        private static readonly HashSet<Type> AllowedEnumTypes = new HashSet<Type>() {
                typeof(byte),
                typeof(short),
                typeof(ushort),
                typeof(int),
                typeof(uint)
            };
    }
}
