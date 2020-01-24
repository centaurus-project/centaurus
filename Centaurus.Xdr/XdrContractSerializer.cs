using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Centaurus.Xdr
{
    internal class XdrContractSerializer
    {
        public XdrContractSerializer(XdrContractDescriptor xdrContractDescriptor)
        {
            SerializedType = xdrContractDescriptor.XdrContractType;
            var contractBuilder = new XdrSerializerContractBuilder(xdrContractDescriptor);
            var serializerType = contractBuilder.CreateDynamicSerializer();
            if (contractBuilder.ContractDescriptor.UnionSwitch.Count > 0)
            {
                IsUnion = true;
                UnionSwitch = contractBuilder.ContractDescriptor.UnionSwitch;
            }
            AncestorUnionsCounts = contractBuilder.ContractDescriptor.AncestorUnionsCounts;
            DynamicSerializer = Activator.CreateInstance(serializerType.AsType()) as IXdrRuntimeContractSerializer;
        }

        public readonly Type SerializedType;

        public readonly bool IsUnion;

        public readonly int AncestorUnionsCounts;

        public readonly Dictionary<int, Type> UnionSwitch;
        
        public readonly IXdrRuntimeContractSerializer DynamicSerializer;
    }
}
