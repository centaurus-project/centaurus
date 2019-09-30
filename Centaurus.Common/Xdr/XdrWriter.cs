using System;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Centaurus
{
    internal class XdrBuffer
    {
        public XdrBuffer()
        {

            bufferSize = 128;
            data = new byte[bufferSize];
        }

        public int bufferSize;

        public byte[] data;

        public int position;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureCapacity(int requiredCapacity)
        {
            if (position + requiredCapacity > bufferSize)
            {
                var newSize = bufferSize * 2;
                if (newSize < requiredCapacity)
                {
                    newSize = bufferSize + requiredCapacity;
                }

                bufferSize = newSize;
                Array.Resize(ref data, bufferSize);
            }
        }
    }

    public class XdrWriter
    {
        //TODO: try ArrayBufferWriter instead of byte[]
        public XdrWriter()
        {
            AllocateBuffer();
        }

        private XdrBuffer buffer;

        private readonly List<XdrBuffer> bufferChunks = new List<XdrBuffer>(1);

        /// <summary>
        /// Current writer position.
        /// </summary>
        public int Position { get { return buffer.position; } }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AllocateBuffer()
        {
            buffer = new XdrBuffer();
            bufferChunks.Add(buffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureCapacity(int requiredCapacity)
        {
            //split chunks into buffers of up to ~24KB
            if (buffer.data.Length >= 24000)
            {
                AllocateBuffer();
            }
            buffer.EnsureCapacity(requiredCapacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(int value)
        {
            EnsureCapacity(4);
            var span = buffer.data.AsSpan(Position, 4);
            BinaryPrimitives.WriteInt32BigEndian(span, value);
            buffer.position += 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(uint value)
        {
            EnsureCapacity(4);
            var span = buffer.data.AsSpan(Position, 4);
            BinaryPrimitives.WriteUInt32BigEndian(span, value);
            buffer.position += 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(long value)
        {
            EnsureCapacity(8);
            var span = buffer.data.AsSpan(Position, 8);
            BinaryPrimitives.WriteInt64BigEndian(span, value);
            buffer.position += 8;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(ulong value)
        {
            EnsureCapacity(8);
            var span = buffer.data.AsSpan(Position, 8);
            BinaryPrimitives.WriteUInt64BigEndian(span, value);
            buffer.position += 8;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(float value)
        {
            Write(BitConverter.SingleToInt32Bits(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(double value)
        {
            Write(BitConverter.DoubleToInt64Bits(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(bool value)
        {
            Write(value ? 1 : 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(Enum value)
        {
            Write((int)(object)value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(int[] value)
        {
            Write(value.Length);
            foreach (var item in value)
            {
                Write(item);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(long[] value)
        {
            Write(value.Length);
            foreach (var item in value)
            {
                Write(item);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(float[] value)
        {
            Write(value.Length);
            foreach (var item in value)
            {
                Write(item);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(double[] value)
        {
            Write(value.Length);
            foreach (var item in value)
            {
                Write(item);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(string value)
        {
            Write(Encoding.UTF8.GetBytes(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(byte[] value, int? count = null)
        {
            if (count == null)
            {
                Write(value.AsSpan());
                return;
            }
            if (count > value.Length) throw new FormatException($"Actual variable length ({value.Length}) is less than requested variable length ({count.Value}).");
            Write(value.AsSpan(0, count.Value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(ReadOnlySpan<byte> value, int? count = null)
        {
            var length = value.Length;
            if (count.HasValue && count.Value != length)
            {
                if (count > length) throw new FormatException($"Actual variable length ({value.Length}) is less than requested variable length ({count.Value}).");
                length = count.Value;
                //resize span to match requested count
                value = value.Slice(length);
            }
            //reserve additional 4 bytes for the length prefix
            var totalVarLength = length + 4;
            EnsureCapacity(totalVarLength);
            //pin memory for the variable length and write it
            var span = buffer.data.AsSpan(Position, 4);
            BinaryPrimitives.WriteInt32BigEndian(span, length);
            //pin memory for the variable itself
            span = buffer.data.AsSpan(Position + 4, length);
            //write bytes
            value.CopyTo(span);
            buffer.position += totalVarLength;
            //padd offset to match 4-bytes chunks
            var padding = length % 4;
            if (padding > 0)
            {
                buffer.position += 4 - padding;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(object value)
        {
            if (value == null) throw new ArgumentNullException();
            if (value is IXdrSerializableModel)
            {
                XdrConverter.Serialize((IXdrSerializableModel)value, this);
                return;
            }
            if (value is IList)
            {
                var length = (value as IList).Count;
                Write(length);
                XdrConverter.SerializeList(value as IList, this);
                return;
            }
            throw new FormatException($"Failed to serialize item {value} of type {value.GetType().FullName}. No suitable serialization method found.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteOptional(IXdrSerializableModel value)
        {
            if (value == null)
            {
                Write(0);
            }
            else
            {
                Write(1);
                XdrConverter.Serialize(value, this);
            }
        }

        public byte[] ToArray()
        {
            //return the data from the first chunk if we have only one
            if (bufferChunks.Count == 1) return buffer.data;
            //calculate total output size
            var totalLength = 0;
            foreach (var chunk in bufferChunks)
            {
                totalLength += chunk.position;
            }
            //allocate output buffer
            var result = new byte[totalLength];
            //merge all buffers
            int pointer = 0;
            foreach (var chunk in bufferChunks)
            {
                Buffer.BlockCopy(chunk.data, 0, result, pointer, chunk.position);
                pointer += chunk.position;
            }
            return result;
        }
    }
}