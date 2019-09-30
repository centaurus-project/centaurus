using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus
{
    /// <summary>
    /// IEqualityComparer for hashes - optimized GetHashCode() for hashed values.
    /// </summary>
    public class HashComparer : IEqualityComparer<byte[]>
    {
        public bool Equals(byte[] left, byte[] right)
        {
            return ByteArrayPrimitives.Equals(left, right);
        }

        public int GetHashCode(byte[] key)
        {
            return ByteArrayPrimitives.ReadFirstWord(key);
        }
    }
}
