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

        public XdrBufferWriter(byte[] into)
        {
            externalBuffer = into;
            bufferChunk = new XdrBuffer(externalBuffer);
        }

        private byte[] externalBuffer;

        private XdrBuffer bufferChunk;

        public int Length => bufferChunk.TotalLength;

        public void Dispose()
        {
            if (externalBuffer != null) return;
            var chunk = bufferChunk;
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
            if (externalBuffer != null)
                throw new IndexOutOfRangeException("Serialized object exceeded capacity of the external buffer");
            bufferChunk = new XdrBuffer(bufferChunk);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Span<byte> Allocate(int requiredCapacity)
        {
            //allocate new chunk when the buffer size capacity is reached
            if (externalBuffer == null && bufferChunk.Position + requiredCapacity > DefaultBufferSize)
            {
                AllocateBuffer();
            }
            return bufferChunk.Allocate(requiredCapacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void WriteInt32(int value)
        {
            BinaryPrimitives.WriteInt32BigEndian(Allocate(4), value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void WriteUInt32(uint value)
        {
            BinaryPrimitives.WriteUInt32BigEndian(Allocate(4), value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void WriteInt64(long value)
        {
            BinaryPrimitives.WriteInt64BigEndian(Allocate(8), value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void WriteUInt64(ulong value)
        {
            BinaryPrimitives.WriteUInt64BigEndian(Allocate(8), value);
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
            //write chars to span
            StringEncoding.GetBytes(raw, Allocate(length));
            //padd offset to match 4-bytes chunks
            WritePadding(length);
        }

        private void WritePadding(int length)
        {
            var reminder = length % 4;
            if (reminder == 0) return;
            var padding = 4 - reminder;
            Allocate(padding).Fill(0);
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
            //write bytes
            value.CopyTo(Allocate(length));
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
            var chunk = bufferChunk;
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
            chunk = bufferChunk;
            int pointer = totalLength;
            do
            {
                pointer -= chunk.Position;
                Array.Copy(chunk.Data, 0, result, pointer, chunk.Position);
                chunk = chunk.PrevChunk;
            }
            while (chunk != null);
            return result;
        }

        internal class XdrBuffer
        {
            public XdrBuffer(XdrBuffer prevChunk = null)
            {
                buffer = XdrBufferFactory.Rent(DefaultBufferSize);
                Data = buffer.Buffer;
                if (prevChunk != null)
                {
                    PrevChunk = prevChunk;
                    prevChunkTotalLength = prevChunk.TotalLength;
                }
            }

            public XdrBuffer(byte[] externalBuffer)
            {
                Data = externalBuffer;
            }

            private readonly XdrBufferFactory.RentedBuffer buffer;

            private readonly int prevChunkTotalLength;

            public readonly XdrBuffer PrevChunk;

            public int TotalLength => prevChunkTotalLength + Position;

            public byte[] Data { get; private set; }

            public int Position { get; private set; }

            public Span<byte> Allocate(int length)
            {
                var span = Data.AsSpan(Position, length);
                Position += length;
                return span;
            }

            public void Dispose()
            {
                if (buffer != null)
                {
                    buffer.Dispose();
                }
            }
        }
    }
}
