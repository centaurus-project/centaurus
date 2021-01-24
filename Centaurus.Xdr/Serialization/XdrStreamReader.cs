using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace Centaurus.Xdr
{
    public class XdrStreamReader : XdrReader, IDisposable
    {
        public XdrStreamReader(Stream stream)
        {
            this.stream = stream;
            buffer = XdrBufferFactory.Rent();
        }

        private Stream stream;

        private XdrBufferFactory.RentedBuffer buffer;

        private int position = 0;

        public override int Position => position;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void Advance(int bytes)
        {
            ReadAndAdvance(bytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override ReadOnlySpan<byte> ReadAndAdvance(int bytesToRead)
        {
            var span = buffer.AsSpan(0, bytesToRead);
            stream.Read(span);
            position += bytesToRead;
            return span;
        }

        public void Dispose()
        {
            buffer.Dispose();
        }
    }
}
