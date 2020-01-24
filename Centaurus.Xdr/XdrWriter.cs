using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Centaurus.Xdr
{

    public class XdrWriter : IDisposable
    {
        const int DefaultBufferSize = 64 * 1024; //64KB
        static readonly Encoding StringEncoding = Encoding.UTF8;

        //TODO: try ArrayBufferWriter instead of byte[]
        public XdrWriter()
        {
            AllocateBuffer();
        }

        private XdrBuffer buffer;

        //private readonly List<XdrBuffer> bufferChunks = new List<XdrBuffer>(1);

        /// <summary>
        /// Current writer position.
        /// </summary>
        public int Position => buffer.Position;

        public void Dispose()
        {
            var chunk = buffer;
            do
            {
                chunk.Dispose();
                chunk = chunk.PrevChunk;
            }
            while (chunk != null);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AllocateBuffer()
        {
            var newBuffer = new XdrBuffer();
            newBuffer.PrevChunk = buffer;
            buffer = newBuffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureCapacity(int requiredCapacity)
        {
            //allocate new chunk when the buffer size capacity is reached
            if (buffer.Position + requiredCapacity > DefaultBufferSize)
            {
                AllocateBuffer();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt32(int value)
        {
            EnsureCapacity(4);
            var span = buffer.Data.AsSpan(Position, 4);
            BinaryPrimitives.WriteInt32BigEndian(span, value);
            buffer.Position += 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt32(uint value)
        {
            EnsureCapacity(4);
            var span = buffer.Data.AsSpan(Position, 4);
            BinaryPrimitives.WriteUInt32BigEndian(span, value);
            buffer.Position += 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt64(long value)
        {
            EnsureCapacity(8);
            var span = buffer.Data.AsSpan(Position, 8);
            BinaryPrimitives.WriteInt64BigEndian(span, value);
            buffer.Position += 8;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt64(ulong value)
        {
            EnsureCapacity(8);
            var span = buffer.Data.AsSpan(Position, 8);
            BinaryPrimitives.WriteUInt64BigEndian(span, value);
            buffer.Position += 8;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteFloat(float value)
        {
            WriteInt32(BitConverter.SingleToInt32Bits(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteDouble(double value)
        {
            WriteInt64(BitConverter.DoubleToInt64Bits(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBool(bool value)
        {
            WriteInt32(value ? 1 : 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteEnum(Enum value)
        {
            WriteInt32((int)(object)value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt32Array(int[] value)
        {
            WriteInt32(value.Length);
            foreach (var item in value)
            {
                WriteInt32(item);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt32Array(uint[] value)
        {
            WriteInt32(value.Length);
            foreach (var item in value)
            {
                WriteUInt32(item);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt64Array(long[] value)
        {
            WriteInt32(value.Length);
            foreach (var item in value)
            {
                WriteInt64(item);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt64Array(ulong[] value)
        {
            WriteInt32(value.Length);
            foreach (var item in value)
            {
                WriteUInt64(item);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteFloatArray(float[] value)
        {
            WriteInt32(value.Length);
            foreach (var item in value)
            {
                WriteFloat(item);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteDoubleArray(double[] value)
        {
            WriteInt32(value.Length);
            foreach (var item in value)
            {
                WriteDouble(item);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteList(IList value)
        {
            var total = value.Count;
            WriteInt32(total);
            if (total > 0)
            {
                XdrConverter.SerializeList(value, this);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt32List(List<int> value)
        {
            WriteInt32(value.Count);
            foreach (var item in value)
            {
                WriteInt32(item);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt64List(List<long> value)
        {
            WriteInt32(value.Count);
            foreach (var item in value)
            {
                WriteInt64(item);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteFloatList(List<float> value)
        {
            WriteInt32(value.Count);
            foreach (var item in value)
            {
                WriteFloat(item);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteDoubleList(List<double> value)
        {
            WriteInt32(value.Count);
            foreach (var item in value)
            {
                WriteDouble(item);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteString(string value)
        {
            var raw = value.AsSpan();
            int length = StringEncoding.GetByteCount(raw);
            //reserve additional 4 bytes for the length prefix
            WriteInt32(length);
            //check that a variable fits into buffer
            EnsureCapacity(length);
            //write chars to span
            StringEncoding.GetBytes(raw, buffer.Data.AsSpan(Position, length));
            buffer.Position += length;
            //padd offset to match 4-bytes chunks
            var reminder = length % 4;
            if (reminder > 0)
            {
                WritePadding(reminder);
            }
        }

        private void WritePadding(int reminder)
        {
            var padding = 4 - reminder;
            buffer.Data.AsSpan(Position, padding).Fill(0);
            buffer.Position += padding;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteVariable(byte[] value, int? count = null)
        {
            if (count == null)
            {
                WriteSpan(value.AsSpan());
                return;
            }
            if (count > value.Length) throw new FormatException($"Actual variable length ({value.Length}) is less than requested variable length ({count.Value}).");
            WriteSpan(value.AsSpan(0, count.Value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteSpan(ReadOnlySpan<byte> value)
        {
            var length = value.Length;
            //reserve additional 4 bytes for the length prefix
            WriteInt32(length);
            //check that a variable fits into buffer
            EnsureCapacity(length);
            //pin memory for the variable itself
            var span = buffer.Data.AsSpan(Position, length);
            //write bytes
            value.CopyTo(span);
            buffer.Position += length;
            //padd offset to match 4-bytes chunks
            var reminder = length % 4;
            if (reminder > 0)
            {
                WritePadding(reminder);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteObject(object value)
        {
            XdrConverter.Serialize(value, this);
        }

        public byte[] ToArray()
        {
            //calculate total output size
            var chunk = buffer;
            int totalLength = 0;
            do
            {
                totalLength += chunk.Position;
                chunk = chunk.PrevChunk;
            }
            while (chunk != null);

            //allocate output buffer
            var result = new byte[totalLength];
            //merge all chunks
            chunk = buffer;
            int pointer = totalLength;
            do
            {
                pointer -= chunk.Position;
                Buffer.BlockCopy(chunk.Data, 0, result, pointer, chunk.Position);
                chunk = chunk.PrevChunk;
            }
            while (chunk != null);
            return result;
        }

        internal class XdrBuffer
        {
            public XdrBuffer()
            {
                Data = bufferPool.Rent(DefaultBufferSize);
            }

            public XdrBuffer PrevChunk;

            private static readonly ArrayPool<byte> bufferPool = ArrayPool<byte>.Create(DefaultBufferSize, 10000);

            public byte[] Data;

            public int Position;

            public void Dispose()
            {
                bufferPool.Return(Data);
            }
        }
    }
}