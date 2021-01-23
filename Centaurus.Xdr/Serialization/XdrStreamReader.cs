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
            buffer = bufferPool.Rent(64 * 1024);
        }

        private Stream stream;

        private byte[] buffer;

        private int position = 0;

        private static readonly ArrayPool<byte> bufferPool = ArrayPool<byte>.Create();

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
            bufferPool.Return(buffer);
        }
    }
}
