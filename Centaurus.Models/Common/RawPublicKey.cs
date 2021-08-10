namespace Centaurus.Models
{
    public class RawPubKey : BinaryData
    {
        //public static readonly RawPubKey Zero = new RawPubKey(new byte[32]);
        public RawPubKey()
        {
        }

        public RawPubKey(string address)
        {
            var keypair = KeyPair.FromAccountId(address);
            Data = keypair.PublicKey;
        }

        public RawPubKey(byte[] publicKey)
        {
            Data = publicKey;
        }

        public override int ByteLength { get { return 32; } }

        public override string ToString()
        {
            return StrKey.EncodeStellarAccountId(Data);
        }

        public static implicit operator byte[](RawPubKey pubKey)
        {
            return pubKey.Data;
        }

        public static implicit operator RawPubKey(byte[] data)
        {
            return new RawPubKey() { Data = data };
        }

        public static implicit operator RawPubKey(KeyPair keyPair)
        {
            return new RawPubKey { Data = keyPair.PublicKey };
        }

        public static implicit operator KeyPair(RawPubKey rawPubKey)
        {
            return KeyPair.FromPublicKey(rawPubKey.Data);
        }
    }
}
