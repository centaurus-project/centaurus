using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace Centaurus.Xdr
{
    public class XdrStreamWriter : XdrWriter, IDisposable
    {
        public XdrStreamWriter(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanWrite) throw new ArgumentException("Stream is not writable");
            this.stream = stream;
            sharedBuffer = XdrBufferFactory.Rent(DefaultBufferSize);
        }

        private readonly Stream stream;

        public int Length = 0;

        private XdrBufferFactory.RentedBuffer sharedBuffer;

        private Span<byte> Allocate(int length)
        {
            Length += length;
            return sharedBuffer.AsSpan(0, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void WriteInt32(int value)
        {
            var span = Allocate(4);
            BinaryPrimitives.WriteInt32BigEndian(span, value);
            stream.Write(span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void WriteUInt32(uint value)
        {
            var span = Allocate(4);
            BinaryPrimitives.WriteUInt32BigEndian(span, value);
            stream.Write(span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void WriteInt64(long value)
        {
            var span = Allocate(8);
            BinaryPrimitives.WriteInt64BigEndian(span, value);
            stream.Write(span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void WriteUInt64(ulong value)
        {
            var span = Allocate(8);
            BinaryPrimitives.WriteUInt64BigEndian(span, value);
            stream.Write(span);
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
            //length prefix
            WriteInt32(length);
            //write chars to span
            var span = Allocate(length);
            StringEncoding.GetBytes(raw, span);
            stream.Write(span);
            Length += length;
            //padd offset to match 4-bytes chunks
            WritePadding(length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteSpan(ReadOnlySpan<byte> value)
        {
            var length = value.Length;
            //length prefix
            WriteInt32(length);
            stream.Write(value);
            Length += length;
            //padd offset to match 4-bytes chunks
            WritePadding(length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WritePadding(int length)
        {
            var reminder = length % 4;
            if (reminder == 0) return;
            var padding = 4 - reminder;
            var span = Allocate(padding);
            span.Fill(0);
            stream.Write(span);
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
        public override void WriteObject(object value, Type type)
        {
            XdrConverter.Serialize(value, this);
        }

        public void Dispose()
        {
            sharedBuffer.Dispose();
        }
    }
}
