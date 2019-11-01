using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Centaurus
{
    /// <summary>
    /// Mapper used to build custom dynamic serializers for every serializable type.
    /// </summary>
    internal class XdrSerializationTypeMapper
    {
        private Dictionary<Type, XdrContractSerializer> mapping;

        public Dictionary<Type, XdrContractSerializer> Map()
        {
            //create the type mapping with serializers
            MapAllTypes();
            //and return the result
            return mapping;
        }

        private void MapAllTypes()
        {
            mapping = new Dictionary<Type, XdrContractSerializer>();
            foreach (var type in GetModels())
            {
                //create serializer class instance
                var serializer = new XdrContractSerializer(type);
                //add mapping
                mapping.Add(type, serializer);
            }
        }

        private IEnumerable<Type> GetModels()
        { //TODO: allow binding to custom assemblies
            return AppDomain.CurrentDomain.GetAssemblies()
                //analyze only own assemblies
                .Where(assembly => assembly.FullName.StartsWith("Centaurus"))
                //project contained types
                .SelectMany(assembly => assembly.GetTypes())
                //we are looking for classes only
                .Where(type => type.IsClass && type.GetCustomAttribute<XdrContractAttribute>() != null);
        }
    }
}
