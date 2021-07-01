using Centaurus.Models;

namespace Centaurus
{
    public static class Ed25519SignatureExtensions
    {
        public static bool IsValid(this Ed25519Signature signature, byte[] data)
        {
            var keypair = (KeyPair)signature.Signer;
            return keypair.Verify(data, signature.Signature);
        }
    }
}
