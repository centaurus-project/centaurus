using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Centaurus
{
    internal class XdrContractSerializer
    {
        public XdrContractSerializer(Type serializedType)
        {
            SerializedType = serializedType;
            var contractBuilder = new XdrSerializerContractBuilder(serializedType);
            var serializerType = contractBuilder.CreateDynamicSerializer();
            if (contractBuilder.UnionSwitch.Count > 0)
            {
                IsUnion = true;
                UnionSwitch = contractBuilder.UnionSwitch;
            }
            AncestorUnionsCounts = contractBuilder.AncestorUnionsCounts;
            DynamicSerializer = Activator.CreateInstance(serializerType.AsType()) as IXdrRuntimeContractSerializer;
        }

        public readonly Type SerializedType;

        public readonly bool IsUnion;
        public readonly int AncestorUnionsCounts;
        private readonly Dictionary<int, Type> UnionSwitch;
        
        public readonly IXdrRuntimeContractSerializer DynamicSerializer;

        public Type ReadUnionType(XdrReader reader)
        {
            var typeId = reader.ReadInt32();
            if (UnionSwitch.TryGetValue(typeId, out Type actualType)) return actualType;
            throw new InvalidOperationException($"Failed to find type mapping for union type id {typeId}.");
        }
    }
}
