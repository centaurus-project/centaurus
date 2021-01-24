using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Xdr
{
    /// <summary>
    /// Creates reusable buffers for XDR serialization/deserialization.
    /// Optimized memory allocation.
    /// </summary>
    public static class XdrBufferFactory
    {
        private static readonly ArrayPool<byte> bufferPool = ArrayPool<byte>.Create();

        public const int MaxBufferSize = 20480;

        /// <summary>
        /// Rent a reusable buffer from a shared buffers pool.
        /// </summary>
        /// <param name="size">Desired buffer size</param>
        /// <returns>Reusable buffer</returns>
        public static RentedBuffer Rent(int size = MaxBufferSize)
        {
            return new RentedBuffer(bufferPool.Rent(size), size);
        }

        /// <summary>
        /// Wraps a reusable rented buffer and ensures that the buffer is returned to the pool once disposed.
        /// </summary>
        public class RentedBuffer : IDisposable
        {
            public RentedBuffer(byte[] messageBuffer, int length = 0)
            {
                Buffer = messageBuffer;
                Length = length;
            }

            public readonly byte[] Buffer;

            public int Length { get; private set; }

            public void Resize(int newLength)
            {
                if (newLength < 0 || newLength > Buffer.Length) throw new IndexOutOfRangeException();
                Length = newLength;
            }

            public Span<byte> AsSpan()
            {
                return Buffer.AsSpan(0, Length);
            }

            public Span<byte> AsSpan(int startFrom, int length)
            {
                return Buffer.AsSpan(startFrom, length);
            }

            public ArraySegment<byte> AsSegment(int startFrom, int length)
            {
                return new ArraySegment<byte>(Buffer, startFrom, length);
            }

            public void Dispose()
            {
                bufferPool.Return(Buffer);
            }
        }
    }
}
