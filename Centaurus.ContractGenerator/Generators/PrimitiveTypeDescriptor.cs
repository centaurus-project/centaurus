using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.ContractGenerator
{
    public class PrimitiveTypeDescriptor
    {
        public PrimitiveTypeDescriptor(Type originalType, string targetType, string primitiveTypeName, PrimitiveTypeDescriptor subtype = null)
        {
            OriginalType = originalType;
            TargetType = targetType;
            PrimitiveTypeName = primitiveTypeName;
            Subtype = subtype;
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


        public readonly PrimitiveTypeDescriptor Subtype;

        public PrimitiveTypeDescriptor CreateSubtypeContainer(PrimitiveTypeDescriptor subtype)
        {
            if (subtype == null) throw new ArgumentNullException(nameof(subtype));
            return new PrimitiveTypeDescriptor(OriginalType, TargetType, PrimitiveTypeName, subtype);
        }
    }
}
