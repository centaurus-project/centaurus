using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Centaurus.Xdr
{
    internal class XdrContractSerializer
    {
        public XdrContractSerializer(Type dynamicSerializerType)
        {
            SerializeMethod = dynamicSerializerType.GetMethod("Serialize");
            DeserializeMethod = dynamicSerializerType.GetMethod("Deserialize");
            ResolveActualUnionTypeMethod = dynamicSerializerType.GetMethod("ResolveActualUnionType");
            var ancestorsProp = dynamicSerializerType.GetField("AncestorUnionsCounts");
            AncestorUnionsCounts = ancestorsProp == null ? 0 : (int)ancestorsProp.GetValue(Activator.CreateInstance(dynamicSerializerType));
        }

        public readonly MethodInfo SerializeMethod;

        public readonly MethodInfo DeserializeMethod;

        public readonly MethodInfo ResolveActualUnionTypeMethod;

        public readonly int AncestorUnionsCounts;

        public bool IsUnion { get { return ResolveActualUnionTypeMethod != null; } }

        //public readonly Type SerializedType;
    }
}
