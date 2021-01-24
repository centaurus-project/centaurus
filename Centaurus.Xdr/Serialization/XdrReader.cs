using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace Centaurus.Xdr
{
    public abstract class XdrReader
    {
        protected static readonly Encoding StringEncoding = Encoding.UTF8;

        public abstract int Position { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract ReadOnlySpan<byte> ReadAndAdvance(int bytesToRead);

        internal abstract void Advance(int bytes);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadInt32()
        {
            return BinaryPrimitives.ReadInt32BigEndian(ReadAndAdvance(4));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ReadUInt32()
        {
            return BinaryPrimitives.ReadUInt32BigEndian(ReadAndAdvance(4));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long ReadInt64()
        {
            return BinaryPrimitives.ReadInt64BigEndian(ReadAndAdvance(8));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong ReadUInt64()
        {
            return BinaryPrimitives.ReadUInt64BigEndian(ReadAndAdvance(8));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ReadFloat()
        {
            return BitConverter.Int32BitsToSingle(ReadInt32());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double ReadDouble()
        {
            return BitConverter.Int64BitsToDouble(ReadInt64());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadBoolean()
        {
            return ReadInt32() == 1 ? true : false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T ReadEnum<T>() where T : Enum
        {
            return (T)(object)ReadInt32();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ReadString()
        {
            return StringEncoding.GetString(ReadVariableAsSpan());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public List<T> ReadList<T>()
        {
            var length = ReadInt32();
            var res = new List<T>(length);
            var baseType = typeof(T);
            for (var i = 0; i < length; i++)
            {
                res[i] = (T)XdrConverter.Deserialize(this, baseType);
            }
            return res;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T[] ReadArray<T>()
        {
            var length = ReadInt32();
            var res = new T[length];
            var baseType = typeof(T);
            for (var i = 0; i < length; i++)
            {
                res[i] = (T)XdrConverter.Deserialize(this, baseType);
            }
            return res;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] ReadVariable()
        {
            return ReadVariableAsSpan().ToArray();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> ReadVariableAsSpan()
        {
            var length = ReadInt32();
            var padding = (4 - length % 4) % 4;
            var bufferLength = length + padding;
            var res = ReadAndAdvance(bufferLength);
            if (padding == 0) return res;
            //ensure padding bytes are empty
            for (var i = length; i < bufferLength; i++) //potentially can be slightly faster if operations are inlined (vs loop)
            {
                if (res[i] != 0b0) throw new FormatException($"Non-zero variable padding at position {Position + i}.");
            }
            return res.Slice(0, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public object ReadObject(Type targetType)
        {
            return XdrConverter.Deserialize(this, targetType);
        }
    }
}
