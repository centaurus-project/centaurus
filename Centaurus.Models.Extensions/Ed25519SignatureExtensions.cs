using Centaurus.Models;
using System;

namespace Centaurus
{
    public static class Ed25519SignatureExtensions
    {
        public static bool IsValid(this Ed25519Signature signature, byte[] data)
        {
            if (signature == null)
                throw new ArgumentNullException(nameof(signature));

            if (data == null)
                throw new ArgumentNullException(nameof(data));

            var keypair = (KeyPair)signature.Signer;
            return keypair.Verify(data, signature.Signature);
        }

        public static bool IsValid(this TinySignature signature, KeyPair keyPair, byte[] data)
        {
            if (keyPair == null)
                throw new ArgumentNullException(nameof(keyPair));

            if (signature == null)
                throw new ArgumentNullException(nameof(signature));

            if (data == null)
                throw new ArgumentNullException(nameof(data));

            return keyPair.Verify(data, signature.Data);
        }
    }
}
