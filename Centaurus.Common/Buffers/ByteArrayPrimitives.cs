using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Centaurus
{
    /// <summary>
    /// Primitive functions for standard ByteArray and Buffer operations.
    /// </summary>
    public static class ByteArrayPrimitives
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetHashCode(byte[] value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            int hash = 17;
            var v = value.AsSpan();
            if (v.Length % 4 == 0)
            { //TODO: try using v.Length & 3 == 0
                var words = v.Length / 4;
                for (int i = 0; i < words; i++)
                {
                    hash = hash * 31 + BinaryPrimitives.ReadInt32LittleEndian(v.Slice(i * 4, 4));
                }
            }
            else
            {
                unchecked
                {
                    for (int i = 0; i < v.Length; i++)
                    {
                        hash = hash * 31 + v[i];
                    }
                }
            }

            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadFirstWord(byte[] value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (value.Length < 4) return 0;
            return BinaryPrimitives.ReadInt32LittleEndian(value.AsSpan(0, 4));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Equals(byte[] left, byte[] right)
        {
            //check for nulls
            if (left == null || right == null) return left == right;
            var len = left.Length;
            //compare lengths
            if (len != right.Length) return false;
            //convert to span<byte>
            var ls = left.AsSpan();
            var rs = right.AsSpan();
            //compare first bytes of each sequence to optimize the best-guess performance
            if (ls[0] != rs[0]) return false;
            //optimize memory operations performance by casting to the arrays of longs if possible
            if (len % 8 == 0)
            {
                var leftn = MemoryMarshal.Cast<byte, long>(ls);
                var rightn = MemoryMarshal.Cast<byte, long>(rs);
                len /= 8;
                for (int i = 0; i < len; i++)
                    if (leftn[i] != rightn[i])
                        return false;
            }
            else
            {
                //compare rest of bytes one-by-one
                for (int i = 1; i < len; i++)
                    if (ls[i] != rs[i])
                        return false;
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CheckBufferLength(byte[] value, int expectedLength)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (value.Length != expectedLength) throw new ArgumentException("Invalid signature length", nameof(value));
        }
    }
}
