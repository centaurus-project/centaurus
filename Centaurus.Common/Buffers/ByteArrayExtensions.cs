using System;
using System.Collections.Generic;
using System.Data.HashFunction.FarmHash;
using System.Linq;
using System.Security.Cryptography;
using Centaurus.Xdr;

namespace Centaurus
{
    public static class ByteArrayExtensions
    {
        static ByteArrayExtensions()
        {
            _HexLookup32 = Enumerable.Range(0, 256).Select(i =>
            {
                var s = i.ToString("X2");
                return ((uint)s[0]) + ((uint)s[1] << 16);
            }).ToArray();
        }

        private static uint[] _HexLookup32;

        public static string ToHex(this byte[] bytes)
        {
            if (bytes == null)
                return null;
            var result = new char[bytes.Length * 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                var val = _HexLookup32[bytes[i]];
                result[2 * i] = (char)val;
                result[2 * i + 1] = (char)(val >> 16);
            }
            return new string(result);
        }

        public static byte[] ComputeHash(this byte[] buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            return SHA256.Create().ComputeHash(buffer);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="objToSerialize">Object to serialize</param>
        /// <param name="buffer">Buffer to use for serialization</param>
        /// <returns></returns>
        public static byte[] ComputeHash(this object objToSerialize, byte[] buffer = null)
        {
            var bytes = objToSerialize.ToByteArray(buffer);
            return ComputeHash(bytes);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="objToSerialize">Object to serialize</param>
        /// <param name="buffer">Buffer to use for serialization</param>
        /// <returns></returns>
        public static byte[] ToByteArray(this object objToSerialize, byte[] buffer = null)
        {
            var bytes = objToSerialize as byte[];
            if (bytes != null)
                return bytes;

            using var writer = buffer == null ? new XdrBufferWriter() : new XdrBufferWriter(buffer);
            return objToSerialize.ToByteArray(writer);
        }

        public static byte[] ToByteArray(this object objToSerialize, XdrBufferWriter writer)
        {
            var bytes = objToSerialize as byte[];
            if (bytes != null)
                return bytes;

            XdrConverter.Serialize(objToSerialize, writer);
            return writer.ToArray();
        }

        public static byte[] FromHexString(string hexString)
        {
            if (string.IsNullOrWhiteSpace(hexString))
                return null;
            int NumberChars = hexString.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hexString.Substring(i, 2), 16);
            return bytes;
        }

        static IFarmHashFingerprint64 farmHash = FarmHashFingerprint64Factory.Instance.Create();

        public static long GetInt64Fingerprint(this byte[] data)
        {
            if (data == null)
                return default;
            return BitConverter.ToInt64(farmHash.ComputeHash(data).Hash);
        }
    }
}
