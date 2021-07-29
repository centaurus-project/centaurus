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
            //compare lengths
            if (left.Length != right.Length) return false;
            return left.AsSpan().SequenceEqual(right);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CheckBufferLength(byte[] value, int expectedLength)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (value.Length != expectedLength) throw new ArgumentException("Invalid signature length", nameof(value));
        }
    }
}
