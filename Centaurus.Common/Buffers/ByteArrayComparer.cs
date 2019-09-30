using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus
{
    /// <summary>
    /// Default byte array comparer for binary buffers.
    /// </summary>
    public class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        public bool Equals(byte[] left, byte[] right)
        {
            return ByteArrayPrimitives.Equals(left, right);
        }

        public int GetHashCode(byte[] key)
        {
            return ByteArrayPrimitives.GetHashCode(key);
        }
    }
}
