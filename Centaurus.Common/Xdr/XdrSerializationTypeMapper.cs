using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Centaurus
{
    /// <summary>
    /// Mapper used to build serializer vectors for every serializable type by traversing classes 
    /// hierarchy in runtime.
    /// </summary>
    internal class XdrSerializationTypeMapper
    {
        private Dictionary<Type, IXdrRuntimeGenericSerializer> discoveredSerializers;

        private Dictionary<Type, XdrSerializationVector> mapping;

        public Dictionary<Type, XdrSerializationVector> Map()
        {

            //discover all XDR serializers
            DiscoverXdrSerializers();
            //create the type mapping with serialization vectors
            MapAllTypes();
            //and return the result
            return mapping;
        }

        private void DiscoverXdrSerializers()
        {
            discoveredSerializers = new Dictionary<Type, IXdrRuntimeGenericSerializer>();
            foreach (var type in GetAllTypes())
            {
                //look for the IXdrSerializer interface implementation
                var serializerType = type.FindGenericInterfaceImplementation(typeof(IXdrSerializer<>));
                if (serializerType != null)
                {
                    //retrieve the type of class being serialized
                    var serializedType = serializerType.GenericTypeArguments[0];
                    var genericSerializerType = typeof(XdrRuntimeGenericSerializer<>).MakeGenericType(serializedType);
                    //map serializer to the serialized type
                    discoveredSerializers.Add(serializedType, Activator.CreateInstance(genericSerializerType, new object[] { type }) as IXdrRuntimeGenericSerializer);
                }
            }
        }

        private void MapAllTypes()
        {
            mapping = new Dictionary<Type, XdrSerializationVector>();
            foreach (var type in GetAllTypes())
            {
                XdrSerializationVector typeSerializers = null;
                //check if any type mappings exist
                FindTypeMappings(type, ref typeSerializers);
                if (typeSerializers != null)
                {
                    //lock modifications
                    typeSerializers.Freeze();
                    //add mapping
                    mapping.Add(type, typeSerializers);
                }
            }
        }

        private void FindTypeMappings(Type type, ref XdrSerializationVector typeSerializers)
        {
            if (type == null || type == typeof(object) || type.IsValueType) return;
            //check whether we have a corresponding serializer class
            if (discoveredSerializers.TryGetValue(type, out var serializer))
            {
                if (typeSerializers == null)
                {
                    typeSerializers = new XdrSerializationVector();
                }
                //insert the serializer at the beginning
                typeSerializers.AddSerializer(serializer);
            }
            //check if a base type also has a serializer
            FindTypeMappings(type.BaseType, ref typeSerializers);
        }

        private IEnumerable<Type> GetAllTypes()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                //analyze only own assemblies
                .Where(assembly => assembly.FullName.StartsWith("Centaurus"))
                //project contained types
                .SelectMany(assembly => assembly.GetTypes())
                //we are looking for classes only
                .Where(type => type.IsClass);
        }
    }
}
