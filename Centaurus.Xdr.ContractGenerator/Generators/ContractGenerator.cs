using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.ContractGenerator
{
    public abstract class ContractGenerator
    {
        private readonly Dictionary<Type, PrimitiveTypeDescriptor> PrimitiveTypesMap = new Dictionary<Type, PrimitiveTypeDescriptor>();
        protected Dictionary<Type, XdrContractSerializationDescriptor> ContractsMap;

        public abstract GeneratedContractsBundle Generate();

        public void LoadContracts(IEnumerable<XdrContractSerializationDescriptor> contracts)
        {
            ContractsMap = new Dictionary<Type, XdrContractSerializationDescriptor>();
            foreach (var descriptor in contracts)
            {
                ContractsMap.Add(descriptor.XdrContractType, descriptor);
            }
        }

        protected XdrContractSerializationDescriptor ResolveContractDescriptor(Type type)
        {
            if (ContractsMap.TryGetValue(type, out var xdrContractDescriptor)) return xdrContractDescriptor;
            throw new Exception($"Failed to find contract descriptor for type {type.FullName}.");
        }

        protected void MapType(PrimitiveTypeDescriptor typeDescriptor)
        {
            PrimitiveTypesMap.Add(typeDescriptor.OriginalType, typeDescriptor);
        }

        protected PrimitiveTypeDescriptor ResolvePrimitiveTypeTypeDescriptor(Type type)
        {
            PrimitiveTypesMap.TryGetValue(type, out PrimitiveTypeDescriptor descriptor);
            return descriptor;
        }

        protected PrimitiveTypeDescriptor ResolveRegistiredTypeDescriptor(Type type)
        {
            if (ContractsMap.ContainsKey(type))
                return new PrimitiveTypeDescriptor(type, type.Name, "Object");
            return ResolvePrimitiveTypeTypeDescriptor(type);
        }

        protected PrimitiveTypeDescriptor GetTypeDescriptor(XdrPropertySerializationDescriptor prop)
        {
            if (prop.PrimitiveType == typeof(List<>))
            {
                var subtype = ResolveRegistiredTypeDescriptor(prop.PropertyType.GenericTypeArguments[0]);
                return PrimitiveTypesMap[typeof(List<>)].CreateSubtypeContainer(subtype);
            }
            if (prop.PropertyType.IsArray)
            {
                var subtype = ResolveRegistiredTypeDescriptor(prop.PropertyType.GetElementType());
                return PrimitiveTypesMap[typeof(Array)].CreateSubtypeContainer(subtype);
            }
            if (prop.PropertyType.IsEnum)
            {
                return PrimitiveTypesMap[typeof(int)].CreateEnum(prop.PropertyType);
            }
            if (prop.PrimitiveType == typeof(object))
            {
                var subtype = ResolveRegistiredTypeDescriptor(prop.PropertyType);
                return PrimitiveTypesMap[typeof(Object)].CreateSubtypeContainer(subtype);
            }
            var wellKnown = ResolveRegistiredTypeDescriptor(prop.PropertyType);
            if (wellKnown != null) return wellKnown;
            throw new NotSupportedException($"Type {prop.PropertyType.FullName} is not supported by the {GetType().FullName} code generator.");
        }

        protected string GetMethodPrimitiveTypeName(Type propType)
        {
            return ResolveRegistiredTypeDescriptor(propType).PrimitiveTypeName;
        }

        protected virtual string GetSerializerClassName(XdrContractSerializationDescriptor contractDescriptor)
        {
            return GetContractName(contractDescriptor) + "Serializer";
        }

        protected virtual string GetContractName(XdrContractSerializationDescriptor contractDescriptor)
        {
            return contractDescriptor.XdrContractType.Name;
        }
    }
}