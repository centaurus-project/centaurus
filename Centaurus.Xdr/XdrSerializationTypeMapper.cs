using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Centaurus.Xdr
{
    /// <summary>
    /// Mapper used to discover and map custom dynamic serializers for each XDR contract.
    /// </summary>
    public class XdrSerializationTypeMapper
    {
        internal static Dictionary<Type, XdrContractSerializer> Map(string assemblyPrefix = "Centaurus")
        {
            //create the type mapping for serializers
            var mapping = new Dictionary<Type, XdrContractSerializer>();
            foreach (var descriptor in DiscoverXdrContracts(assemblyPrefix))
            {
                //create serializer class instance
                var serializer = new XdrContractSerializer(descriptor);
                //add mapping
                mapping.Add(descriptor.XdrContractType, serializer);
            }
            return mapping;
        }

        public static IEnumerable<XdrContractDescriptor> DiscoverXdrContracts(string assemblyPrefix = "Centaurus")
        { //TODO: allow binding to custom assemblies
            return AppDomain.CurrentDomain.GetAssemblies()
                //analyze only own assemblies
                .Where(assembly => assembly.FullName.StartsWith(assemblyPrefix))
                //discover types for each assembly that matches our criteria
                .SelectMany(assembly => DiscoverXdrContracts(assembly));
        }

        public static IEnumerable<XdrContractDescriptor> DiscoverXdrContracts(Assembly assembly)
        {
            //discover all types in the assembly
            return assembly.GetTypes()
                //we are looking for classes only
                .Where(type => type.IsClass && type.GetCustomAttribute<XdrContractAttribute>() != null)
                //wrap with XdrContractDescriptor
                .Select(type => new XdrContractDescriptor(type));
        }
    }
}
