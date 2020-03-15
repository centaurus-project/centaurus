using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.ContractGenerator
{
    public class PrimitiveTypeDescriptor
    {
        public PrimitiveTypeDescriptor(Type originalType, string targetType, string primitiveTypeName)
        {
            OriginalType = originalType;
            TargetType = targetType;
            PrimitiveTypeName = primitiveTypeName;
        }

        /// <summary>
        /// Original .NET field type.
        /// </summary>
        public readonly Type OriginalType;

        /// <summary>
        /// Field type reflected to the target language.
        /// </summary>
        public readonly string TargetType;

        /// <summary>
        /// Primitive type name used to explicitly identify encoding/decoding methods.
        /// </summary>
        public readonly string PrimitiveTypeName;

        public PrimitiveTypeDescriptor SubType { get; private set; }

        public Type EnumType { get; private set; }

        public bool IsSubTypeContainer => SubType != null;

        public bool IsEnum => EnumType != null;

        public PrimitiveTypeDescriptor CreateSubtypeContainer(PrimitiveTypeDescriptor subtype)
        {
            if (subtype == null) throw new ArgumentNullException(nameof(subtype));
            return new PrimitiveTypeDescriptor(OriginalType, TargetType, PrimitiveTypeName) { SubType = subtype };
        }

        public PrimitiveTypeDescriptor CreateEnum(Type enumType)
        {
            if (enumType == null) throw new ArgumentNullException(nameof(enumType));
            return new PrimitiveTypeDescriptor(OriginalType, TargetType, PrimitiveTypeName) { EnumType = enumType };
        }
    }
}
