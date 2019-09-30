using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace Centaurus
{
    public class XdrReader
    {
        public XdrReader(byte[] source, int length)
        {
            this.source = source;
            Length = length;
        }

        public XdrReader(byte[] source) : this(source, source.Length) { }

        private readonly byte[] source;

        public int Length { get; private set; }

        private int position = 0;

        public bool CanRead { get { return position < Length; } }

        public byte[] ToArray()
        {
            var res = new byte[Length];
            Array.Copy(source, 0, res, 0, Length);
            return res;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ReadOnlySpan<byte> ReadAndAdvance(int bytesToRead)
        {
            if (bytesToRead + position > Length)
                throw new FormatException($"Unexpected attempt to read {bytesToRead} bytes at position {position}. Source stream is too short.");
            var span = source.AsSpan(position, bytesToRead);
            position += bytesToRead;
            return span;
        }        

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
        public bool ReadBool()
        {
            return ReadInt32() == 1 ? true : false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T ReadEnum<T>() where T : Enum
        {
            return (T)(object)ReadInt32();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int[] ReadInt32Array()
        {
            var length = ReadInt32();
            var res = new int[length];
            for (var i = 0; i < length; i++)
            {
                res[i] = ReadInt32();
            }
            return res;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long[] ReadInt64Array()
        {
            var length = ReadInt32();
            var res = new long[length];
            for (var i = 0; i < length; i++)
            {
                res[i] = ReadInt64();
            }
            return res;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float[] ReadFloatArray()
        {
            var length = ReadInt32();
            var res = new float[length];
            for (var i = 0; i < length; i++)
            {
                res[i] = ReadFloat();
            }
            return res;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double[] ReadDoubleArray()
        {
            var length = ReadInt32();
            var res = new double[length];
            for (var i = 0; i < length; i++)
            {
                res[i] = ReadDouble();
            }
            return res;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ReadString()
        {
            return Encoding.UTF8.GetString(ReadVariableAsSpan());
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
            var res = ReadAndAdvance(length);
            var tailSize = length % 4;
            if (tailSize != 0)
            {
                var tail = ReadAndAdvance(4 - tailSize);
                foreach (var b in tail)
                {
                    if (b != 0b0) throw new FormatException($"Non-zero variable padding at position {position - tailSize}.");
                }
            }
            return res;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Read<T>() where T : class, IXdrSerializableModel
        {
            return XdrConverter.Deserialize<T>(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T ReadOptional<T>() where T : class, IXdrSerializableModel
        {
            var valueExists = ReadInt32();
            if (valueExists == 0) return null;
            return XdrConverter.Deserialize<T>(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public List<T> ReadList<T>() where T : class, IXdrSerializableModel
        {
            var length = ReadInt32();
            var res = new List<T>(length);
            for (var i = 0; i < length; i++)
            {
                res.Add(Read<T>());
            }
            return res;
        }
    }
}
