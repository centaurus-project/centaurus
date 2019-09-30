using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.CompilerServices;
using System.Reflection;

namespace Centaurus
{

    internal interface IXdrRuntimeGenericSerializer
    {
        public void Serialize(object value, XdrWriter writer);

        public void Deserialize(ref object value, XdrReader reader);

        public object CreateInstance();

        public bool IsAbstract { get; }
    }

    /// <summary>
    /// Runtime-constructed XDR serializer that wraps generic Serialize/Deserialize method calls 
    /// for all registered serializers (<see cref="IXdrSerializer{T}"/>).
    /// </summary>
    internal class XdrRuntimeGenericSerializer<T> : IXdrRuntimeGenericSerializer where T : class, IXdrSerializableModel
    {
        public XdrRuntimeGenericSerializer(Type serializerType)
        {
            IsAbstract = typeof(T).IsAbstract;
            SerializerInstance = Activator.CreateInstance(serializerType);

            SerializeMethod = Delegate.CreateDelegate(typeof(SerializeDelegate), SerializerInstance, "Serialize") as SerializeDelegate;
            DeserializeMethod = Delegate.CreateDelegate(typeof(DeserializeDelegate), SerializerInstance, "Deserialize") as DeserializeDelegate;
        }

        /// <summary>
        /// <see langword="true"/> if <typeparamref name="T"/> is abstract.
        /// </summary>
        public bool IsAbstract { get; private set; }

        private object SerializerInstance;

        private SerializeDelegate SerializeMethod;

        private DeserializeDelegate DeserializeMethod;

        /// <summary>
        /// Dynamically invokes a generic Serialize method of a given serializer.
        /// </summary>
        /// <param name="value">Value to serialize.</param>
        /// <param name="writer">XDR stream writer.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Serialize(object value, XdrWriter writer)
        {
            SerializeMethod(value as T, writer);
        }

        /// <summary>
        /// Dynamically invokes a generic Deserialize method of a given serializer.
        /// </summary>
        /// <param name="reader">XDR stream reader.</param>
        /// <returns>Deserialized object.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deserialize(ref object value, XdrReader reader)
        {
            var casted = value as T;
            DeserializeMethod(ref casted, reader);
            value = casted;
        }

        /// <summary>
        /// Creates an instance of the <see cref="SerializedType"/>.
        /// </summary>
        /// <returns>Created instance.</returns>
        public object CreateInstance()
        {
            return Activator.CreateInstance<T>();
        }

        public override int GetHashCode()
        {
            return typeof(T).GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is XdrRuntimeGenericSerializer<T>;
        }

        #region SERIALIZER_INVOCATION_CONTRACTS

        internal delegate void DeserializeDelegate(ref T value, XdrReader reader);

        internal delegate void SerializeDelegate(T value, XdrWriter writer);

        #endregion
    }
}
