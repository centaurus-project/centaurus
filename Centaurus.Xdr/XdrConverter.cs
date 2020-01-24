using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Xdr
{
    public static class XdrConverter
    {
        static XdrConverter()
        {
            serializerMapping = XdrSerializationTypeMapper.Map();
        }

        private static readonly Dictionary<Type, XdrContractSerializer> serializerMapping;

        public static byte[] Serialize(object value)
        {
            using (var writer = new XdrWriter())
            {
                Serialize(value, writer);
                return writer.ToArray();
            }
        }

        internal static void Serialize(object value, XdrWriter writer)
        {
            if (value == null)
                throw new NullReferenceException("Failed to serialize null value. All values should be initialized before the serialization.");
            var serializer = LookupSerializer(value.GetType());
            serializer.DynamicSerializer.Serialize(value, writer);
        }

        internal static void SerializeList(IList value, XdrWriter writer)
        {
            //if (value == null) throw new NullReferenceException("Failed to serialize null value. All values should be initialized before the serialization.");
            //suppose it's a List<T>
            var genericListType = value.GetType().GetGenericArguments()[0];
            if (genericListType != null)
            {
                var baseSerializer = LookupSerializer(genericListType);
                foreach (var item in value)
                {
                    //if (item == null) throw new NullReferenceException("Failed to serialize null value. All values should be initialized before the serialization.");
                    var itemType = item.GetType();
                    //the same type - we don't need to lookup for serializer
                    if (itemType == genericListType)
                    {
                        baseSerializer.DynamicSerializer.Serialize(item, writer);
                    }
                    else
                    {
                        var serializer = LookupSerializer(itemType);
                        serializer.DynamicSerializer.Serialize(item, writer);
                    }
                }
            }
            else
            {
                //otherwise we need to lookup for serialization vector each time
                foreach (XdrContractSerializer item in value)
                {
                    if (value == null) 
                        throw new NullReferenceException("Failed to serialize null value. All values should be initialized before the serialization.");
                    var serializer = LookupSerializer(item.GetType());
                    serializer.DynamicSerializer.Serialize(item, writer);
                }
            }
        }

        public static object Deserialize(XdrReader reader, Type type)
        {
            //find the corresponding serialization vector for a given type
            var serializer = LookupSerializer(type);
            //skip discriminators for ancestors
            if (serializer.AncestorUnionsCounts > 0)
            {
                reader.Advance(serializer.AncestorUnionsCounts * 4);
            }
            //resolve unions
            while (serializer.IsUnion)
            {
                var typeId = reader.ReadInt32();
                if (!serializer.UnionSwitch.TryGetValue(typeId, out type))
                    throw new InvalidOperationException($"Failed to find type mapping for union type id {typeId}.");
                serializer = LookupSerializer(type);
            }
            //create new instance of the target model
            var instance = Activator.CreateInstance(type);
            //deserialize using resolved serializer contract
            serializer.DynamicSerializer.Deserialize(instance, reader);
            return instance;
        }

        public static object Deserialize(byte[] serialized, Type type)
        {
            return Deserialize(new XdrReader(serialized), type);
        }

        public static T Deserialize<T>(XdrReader reader) where T : class
        {
            return Deserialize(reader, typeof(T)) as T;
        }

        public static T Deserialize<T>(byte[] serialized) where T : class
        {
            return Deserialize(new XdrReader(serialized), typeof(T)) as T;
        }

        private static XdrContractSerializer LookupSerializer(Type type)
        {
            //if (type.IsValueType) throw new InvalidOperationException($"XDR serialization for the value type {type.FullName} is not supported. Use corresponding XdrWriter method instead.");
            if (!serializerMapping.TryGetValue(type, out var serializer))
                throw new InvalidOperationException($"Serializer for type {type.FullName} not found.");
            return serializer;
        }
    }
}