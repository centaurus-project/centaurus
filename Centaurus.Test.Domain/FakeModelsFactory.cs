using Centaurus.Models;

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
