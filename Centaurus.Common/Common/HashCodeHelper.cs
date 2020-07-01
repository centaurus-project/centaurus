using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Centaurus
{
    public static class HashCodeHelper
    {
        /// <summary>
        /// Return unique Int64 value for the string
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static ulong GetInt64HashCode(this string value)
        {
            if (string.IsNullOrEmpty(value))
                return 0;

            var byteContents = Encoding.Unicode.GetBytes(value);
            var hash = new SHA256CryptoServiceProvider();
            var hashText = hash.ComputeHash(byteContents);
            //32Byte hashText separate
            //hashCodeStart = 0~7  8Byte
            //hashCodeMedium = 8~23  8Byte
            //hashCodeEnd = 24~31  8Byte
            //and Fold
            var hashCodeStart = BitConverter.ToUInt64(hashText, 0);
            var hashCodeMedium = BitConverter.ToUInt64(hashText, 8);
            var hashCodeEnd = BitConverter.ToUInt64(hashText, 24);
            return hashCodeStart ^ hashCodeMedium ^ hashCodeEnd;
        }
    }
}
