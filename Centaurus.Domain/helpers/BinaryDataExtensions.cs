using Centaurus.Models;
using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Centaurus.Domain
{
    public static class BinaryDataExtensions
    {
        private static RNGCryptoServiceProvider _RandomGenerator = new RNGCryptoServiceProvider();
        public static void Randomize(this BinaryData binaryData)
        {

            binaryData.Data = new byte[binaryData.ByteLength];
            _RandomGenerator.GetBytes(binaryData.Data.AsSpan());
        }

        public static void Zero(this BinaryData binaryData)
        {
            binaryData.Data = new byte[binaryData.ByteLength];
        }

        public static bool IsZero(this BinaryData binaryData)
        {
            //convert to span<byte>
            var ds = binaryData.Data.AsSpan();
            //compare first bytes of the array to optimize the best-guess performance
            if (ds[0] != 0) return false;
            //the first byte is 0 - have to compare all of them
            var len = ds.Length;
            //optimize memory operations performance by casting to the array of longs if possible
            if (len % 8 == 0)
            { 
                var asLongs = MemoryMarshal.Cast<byte, long>(ds);
                len /= 8;
                for (int i = 0; i < len; i++)
                    if (asLongs[i] != 0L)
                        return false;
            }
            else
            {
                for (int i = 1; i < len; i++)
                    if (ds[i] != 0)
                        return false;
            }
            return true;
        }
    }
}
