using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace Centaurus
{
    public static class XdrConverter
    {
        static XdrConverter()
        {
            serializerMapping = new XdrSerializationTypeMapper().Map();
        }

        private static Dictionary<Type, XdrSerializationVector> serializerMapping;

        public static byte[] Serialize(IXdrSerializableModel value)
        {
            var writer = new XdrWriter();
            Serialize(value, writer);
            return writer.ToArray();
        }

        internal static void Serialize(IXdrSerializableModel value, XdrWriter writer)
        {
            if (value == null) throw new NullReferenceException("Failed to serialize null value. All values should be initialized before the serialization.");
            if (value is IDictionary) throw new InvalidOperationException("Dictionary serialization is not supported.");
            var type = value.GetType();
            var serializer = LookupSerializationVector(type);
            serializer.Serialize(value, writer);
        }

        internal static void SerializeList(IList value, XdrWriter writer)
        {
            if (value == null) throw new NullReferenceException("Failed to serialize null value. All values should be initialized before the serialization.");
            //suppose it's a List<T>
            var genericListType = value.GetType().GetGenericArguments()[0];
            if (genericListType != null)
            {
                var baseSerializer = LookupSerializationVector(genericListType);
                foreach (IXdrSerializableModel item in value)
                {
                    if (value == null) throw new NullReferenceException("Failed to serialize null value. All values should be initialized before the serialization.");
                    var itemType = item.GetType();
                    //the same type - we don't need to lookup for serializer
                    if (itemType == genericListType)
                    {
                        baseSerializer.Serialize(item, writer);
                    }
                    else
                    {
                        var serializer = LookupSerializationVector(itemType);
                        serializer.Serialize(item, writer);
                    }
                }
                return;
            }
            else
            {
                //otherwise we need to lookup for serialization vector each time
                foreach (IXdrSerializableModel item in value)
                {
                    if (value == null) throw new NullReferenceException("Failed to serialize null value. All values should be initialized before the serialization.");
                    var serializer = LookupSerializationVector(item.GetType());
                    serializer.Serialize(item, writer);
                }
            }
        }

        public static IXdrSerializableModel Deserialize(XdrReader reader, Type type)
        {
            //find the corresponding serialization vector for a given type
            var serializer = LookupSerializationVector(type);
            return serializer.Deserialize(reader);
        }

        public static IXdrSerializableModel Deserialize(byte[] serialized, Type type)
        {
            return Deserialize(new XdrReader(serialized), type);
        }

        public static T Deserialize<T>(XdrReader reader) where T : class, IXdrSerializableModel
        {
            return Deserialize(reader, typeof(T)) as T;
        }

        public static T Deserialize<T>(byte[] serialized) where T : class, IXdrSerializableModel
        {
            return Deserialize<T>(new XdrReader(serialized));
        }

        private static XdrSerializationVector LookupSerializationVector(Type type)
        {
            //if (type.IsValueType) throw new InvalidOperationException($"XDR serialization for the value type {type.FullName} is not supported. Use corresponding XdrWriter method instead.");
            if (!serializerMapping.TryGetValue(type, out var serializationVector)) throw new InvalidOperationException($"Serializer for type {type.FullName} not found.");
            return serializationVector;
        }
    }
}
