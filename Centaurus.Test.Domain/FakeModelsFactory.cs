using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;
using stellar_dotnet_sdk;

namespace Centaurus.Test
{
    public static class FakeModelsFactory
    {
        public static Ed25519Signature RandomSignature()
        {
            var signerKeypair = KeyPair.Random();
            return new Ed25519Signature
            {
                Signer = new RawPubKey() { Data = signerKeypair.PublicKey },
                Signature = 64.RandomBytes()
            };
        }
    }
}
