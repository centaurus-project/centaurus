using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.ContractGenerator
{
    public abstract class ContractGenerator
    {
        private readonly Dictionary<Type, PrimitiveTypeDescriptor> PrimitiveTypesMap = new Dictionary<Type, PrimitiveTypeDescriptor>();
        protected Dictionary<Type, XdrContractDescriptor> ContractsMap;

        public abstract GeneratedContractsBundle Generate();

        public void LoadContracts(IEnumerable<XdrContractDescriptor> contracts)
        {
            ContractsMap = new Dictionary<Type, XdrContractDescriptor>();
            foreach (var descriptor in contracts)
            {
                ContractsMap.Add(descriptor.XdrContractType, descriptor);
            }
        }

        protected XdrContractDescriptor ResolveContractDescriptor(Type type)
        {
            if (ContractsMap.TryGetValue(type, out var xdrContractDescriptor)) return xdrContractDescriptor;
            throw new Exception($"Failed to find contract descriptor for type {type.FullName}.");
        }

        protected void MapType(PrimitiveTypeDescriptor typeDescriptor)
        {
            PrimitiveTypesMap.Add(typeDescriptor.OriginalType, typeDescriptor);
        }


        private PrimitiveTypeDescriptor ResolveWellKnownPrimitiveTypeDescriptor(Type type)
        {
            if (PrimitiveTypesMap.TryGetValue(type, out PrimitiveTypeDescriptor descriptor))
                return descriptor;
            if (ContractsMap.ContainsKey(type))
                return new PrimitiveTypeDescriptor(type, type.Name, type.Name);
            return null;
        }

        protected PrimitiveTypeDescriptor GetTypeDescriptor(IXdrPropertySerializationDescriptor prop)
        {
            if (prop.PrimitiveType == typeof(List<>))
            {
                var subtype = ResolveWellKnownPrimitiveTypeDescriptor(prop.PropertyType.GenericTypeArguments[0]);
                return PrimitiveTypesMap[typeof(List<>)].CreateSubtypeContainer(subtype);
            }
            if (prop.PropertyType.IsArray)
            {
                var subtype = ResolveWellKnownPrimitiveTypeDescriptor(prop.PropertyType.GetElementType());
                return PrimitiveTypesMap[typeof(List<>)].CreateSubtypeContainer(subtype);
            }
            if (prop.PropertyType.IsEnum)
            {
                //TODO: recreate enums in contracts
                return ResolveWellKnownPrimitiveTypeDescriptor(typeof(int));
            }
            var wellKnown = ResolveWellKnownPrimitiveTypeDescriptor(prop.PropertyType);
            if (wellKnown != null) return wellKnown;
            throw new NotSupportedException($"Type {prop.PropertyType.FullName} is not supported by the {GetType().FullName} code generator.");
        }

        protected string GetMethodPrimitiveTypeName(Type propType)
        {
            return ResolveWellKnownPrimitiveTypeDescriptor(propType).PrimitiveTypeName;
        }
    }
}