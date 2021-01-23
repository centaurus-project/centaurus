using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Centaurus.Xdr
{
    public class XdrBufferReader : XdrReader
    {
        public XdrBufferReader(byte[] source, int length)
        {
            this.source = source;
            Length = length;
        }

        public XdrBufferReader(byte[] source) : this(source, source.Length) { }

        private readonly byte[] source;

        public int Length { get; private set; }

        private int position;

        public override int Position => position;

        public bool CanRead => position < Length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override ReadOnlySpan<byte> ReadAndAdvance(int bytesToRead)
        {
            if (bytesToRead + position > Length)
                throw new FormatException($"Unexpected attempt to read {bytesToRead} bytes at position {position}. Source stream is too short.");
            var span = source.AsSpan(position, bytesToRead);
            position += bytesToRead;
            return span;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Advance(int bytes)
        {
            position += bytes;
        }
    }
}
