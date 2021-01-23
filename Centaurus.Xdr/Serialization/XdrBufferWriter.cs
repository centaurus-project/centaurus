using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Centaurus.Xdr
{
    public class XdrBufferWriter : XdrWriter, IDisposable
    {
        public XdrBufferWriter()
        {
            AllocateBuffer();
        }

        private XdrBuffer buffer;

        /// <summary>
        /// Current writer position.
        /// </summary>
        public override int Position => buffer.Position;

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
        public override void WriteInt32(int value)
        {
            EnsureCapacity(4);
            var span = buffer.Data.AsSpan(Position, 4);
            BinaryPrimitives.WriteInt32BigEndian(span, value);
            buffer.Position += 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void WriteUInt32(uint value)
        {
            EnsureCapacity(4);
            var span = buffer.Data.AsSpan(Position, 4);
            BinaryPrimitives.WriteUInt32BigEndian(span, value);
            buffer.Position += 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void WriteInt64(long value)
        {
            EnsureCapacity(8);
            var span = buffer.Data.AsSpan(Position, 8);
            BinaryPrimitives.WriteInt64BigEndian(span, value);
            buffer.Position += 8;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void WriteUInt64(ulong value)
        {
            EnsureCapacity(8);
            var span = buffer.Data.AsSpan(Position, 8);
            BinaryPrimitives.WriteUInt64BigEndian(span, value);
            buffer.Position += 8;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void WriteFloat(float value)
        {
            WriteInt32(BitConverter.SingleToInt32Bits(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void WriteDouble(double value)
        {
            WriteInt64(BitConverter.DoubleToInt64Bits(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void WriteBoolean(bool value)
        {
            WriteInt32(value ? 1 : 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void WriteEnum(Enum value)
        {
            WriteInt32((int)(object)value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void WriteString(string value)
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
            WritePadding(length);
        }

        private void WritePadding(int length)
        {
            var reminder = length % 4;
            if (reminder == 0) return;
            var padding = 4 - reminder;
            buffer.Data.AsSpan(Position, padding).Fill(0);
            buffer.Position += padding;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void WriteVariable(byte[] value, int? count = null)
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
            WritePadding(length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void WriteObject(object value, Type type)
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
                Data = BufferPool.Rent(DefaultBufferSize);
            }

            public XdrBuffer PrevChunk;

            public byte[] Data;

            public int Position;

            public void Dispose()
            {
                BufferPool.Return(Data);
            }
        }
    }
}
