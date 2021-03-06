﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Centaurus.Xdr
{

    public class XdrPropertySerializationDescriptor
    {
        public XdrPropertySerializationDescriptor(PropertyInfo prop, bool inherited)
        {
            Property = prop;
            Inherited = inherited;
            PrimitiveType = prop.PropertyType;
            if (prop.GetMethod == null) throw new InvalidOperationException($"Property {FullPropertyName} does not have getter and cannot be serialized.");
            if (prop.SetMethod == null) throw new InvalidOperationException($"Property {FullPropertyName} does not have setter and cannot be serialized.");
            if (PrimitiveType == typeof(object)) throw new InvalidOperationException($"Generalized object serialization not supported. Check {FullPropertyName}.");

            //determine optional property
            IsOptional = prop.GetCustomAttribute<XdrFieldAttribute>().Optional;

            //if this is a primitive type, we are done
            if (PrimitiveValueTypes.Contains(PrimitiveType)) return;

            //check if it's a serializable contract by itself
            if (IsXdrContractType(PrimitiveType))
            {
                GenericArgument = PrimitiveType;
                PrimitiveType = typeof(object);
                return;
            }

            //handle enums
            if (PrimitiveType.IsEnum)
            {
                //verify enum underlying type
                EnsureValidEnumType(PrimitiveType);
                //and treat it as int32
                PrimitiveType = typeof(int);
                return;
            }

            //handle nullable types
            if (PrimitiveType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                var valueType = PrimitiveType.GetGenericArguments().SingleOrDefault();
                if (IsPrimitiveValueType(valueType))
                {
                    IsOptional = true;
                    IsNullable = true;
                    PrimitiveType = valueType;
                    return;
                }
            }

            //handle lists serialization
            if (PrimitiveType.GetGenericTypeDefinition() == typeof(List<>))
            {
                var listGenericType = PrimitiveType.GetGenericArguments().SingleOrDefault();
                if (IsXdrContractType(listGenericType) || IsPrimitiveValueType(listGenericType))
                {
                    PrimitiveType = typeof(List<>);
                    GenericArgument = listGenericType;
                    return;
                }
            }

            //no suitable primitive serializer found
            throw new InvalidOperationException($"{PrimitiveType.FullName} XDR serialization is not supported. Check {FullPropertyName}.");
        }

        public readonly PropertyInfo Property;

        public readonly bool Inherited;

        public readonly Type PrimitiveType;

        public readonly Type GenericArgument;

        public readonly bool IsOptional;

        public readonly bool IsNullable;

        public string PropertyName => Property.Name;

        public Type PropertyType => Property.PropertyType;

        private string FullPropertyName => $"{Property.DeclaringType.FullName}.{Property.Name}";

        private static bool IsXdrContractType(Type type)
        {
            return type.GetCustomAttribute<XdrContractAttribute>(true) != null;
        }

        private static bool IsPrimitiveValueType(Type type)
        {
            return PrimitiveValueTypes.Contains(type);
        }

        private void EnsureValidEnumType(Type enumType)
        {
            var underylingType = Enum.GetUnderlyingType(enumType);
            if (!AllowedEnumTypes.Contains(underylingType))
                throw new InvalidCastException($"XDR serialization is not supported for enums with the underlying type ${underylingType.FullName}.");
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
                typeof(double[])
            };

        private static readonly HashSet<Type> AllowedEnumTypes = new HashSet<Type>() {
                typeof(byte),
                typeof(short),
                typeof(ushort),
                typeof(int),
                typeof(uint)
            };

        public override string ToString()
        {
            return FullPropertyName;
        }

        public override int GetHashCode()
        {
            return Property.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return Property.Equals((obj as XdrPropertySerializationDescriptor)?.Property);
        }
    }
}
