using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Centaurus
{
    public static class ByteArrayExtensions
    {
        static ByteArrayExtensions()
        {
            _HexLookup32 = Enumerable.Range(0, 255).Select(i =>
            {
                var s = i.ToString("X2");
                return ((uint)s[0]) + ((uint)s[1] << 16);
            }).ToArray();
        }

        private static uint[] _HexLookup32;

        public static string ToHex(this byte[] bytes)
        {
            var result = new char[bytes.Length * 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                var val = _HexLookup32[bytes[i]];
                result[2 * i] = (char)val;
                result[2 * i + 1] = (char)(val >> 16);
            }
            return new string(result);
        }

        public static byte[] ComputeHash(this byte[] bytes)
        {
            return SHA256.Create().ComputeHash(bytes);
        }
    }
}
