using System;
using System.Collections.Generic;
using Centaurus.Xdr;
using Centaurus.Models;
using stellar_dotnet_sdk;
using stellar_dotnet_sdk.xdr;

namespace Centaurus.Domain
{
    public static class Ed25519SignatureExtensions
    {
        public static DecoratedSignature ToDecoratedSignature(this Ed25519Signature signature)
        {
            return new DecoratedSignature()
            {
                Signature = new Signature(signature.Signature),
                Hint = new SignatureHint(signature.Signer.Data.AsSpan(Index.FromEnd(4)).ToArray())
            };
        }

        public static bool IsValid(this Ed25519Signature signature, byte[] data)
        {
            var keypair = KeyPair.FromPublicKey(signature.Signer.ToArray());
            return keypair.Verify(data, signature.Signature);
        }

        public static bool IsValid(this Ed25519Signature signature, Message message)
        {
            return IsValid(signature, XdrConverter.Serialize(message));
        }
    }
}
