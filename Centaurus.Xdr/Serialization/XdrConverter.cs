﻿using System;
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
            //serializerMapping = XdrSerializationTypeMapper.Map();
        }

        private static readonly Dictionary<Type, XdrContractSerializer> serializerMapping = new Dictionary<Type, XdrContractSerializer>();

        public static void RegisterSerializer(Type serializerType)
        {
            var dynamicInterface = serializerType.GetInterface(typeof(IXdrRuntimeContractSerializer<>).Name);
            if (dynamicInterface == null) return;
            var contractType = dynamicInterface.GenericTypeArguments[0];
            serializerMapping[contractType] = new XdrContractSerializer(serializerType);
        }

        public static byte[] Serialize(object value)
        {
            using var writer = new XdrBufferWriter();
            Serialize(value, writer);
            return writer.ToArray();
        }

        public static void Serialize(object value, XdrWriter writer)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value), "Failed to serialize null value. All values should be initialized before the serialization.");
            var serializer = LookupSerializer(value.GetType());
            serializer.SerializeMethod.Invoke(null, new object[] { value, writer });
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
                type = serializer.ResolveActualUnionTypeMethod.Invoke(null, new object[] { reader }) as Type;
                serializer = LookupSerializer(type);
            }

            //create new instance of the target model
            var instance = Activator.CreateInstance(type);
            //deserialize using resolved serializer contract
            serializer.DeserializeMethod.Invoke(null, new object[] { instance, reader });
            return instance;
        }

        public static object Deserialize(byte[] serialized, Type type)
        {
            return Deserialize(new XdrBufferReader(serialized), type);
        }

        public static T Deserialize<T>(XdrReader reader) where T : class
        {
            return Deserialize(reader, typeof(T)) as T;
        }

        public static T Deserialize<T>(byte[] serialized) where T : class
        {
            return Deserialize(new XdrBufferReader(serialized), typeof(T)) as T;
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