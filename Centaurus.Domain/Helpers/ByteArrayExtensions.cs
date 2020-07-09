using Centaurus.Models;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public static class ByteArrayExtensions
    {

        /// <summary>
        /// Signs data and returns signature object
        /// </summary>
        /// <param name="binaryData">Data to sign</param>
        /// <param name="keyPair">KeyPair to sign the data. If null, Global.Settings.KeyPair will be used.</param>
        /// <returns></returns>
        public static Ed25519Signature Sign(this byte[] binaryData, KeyPair keyPair = null)
        {
            if (keyPair == null)
                keyPair = Global.Settings.KeyPair;

            var rawSignature = keyPair.Sign(binaryData);
            return new Ed25519Signature()
            {
                Signature = rawSignature,
                Signer = keyPair.PublicKey
            };
        }

        public static byte[] FromHexString(string hexString)
        {
            if (string.IsNullOrWhiteSpace(hexString))
                return null;
            return Enumerable.Range(0, hexString.Length)
                      .Where(x => x % 2 == 0)
                      .Select(x => Convert.ToByte(hexString.Substring(x, 2), 16))
                      .ToArray();
        }
    }
}
