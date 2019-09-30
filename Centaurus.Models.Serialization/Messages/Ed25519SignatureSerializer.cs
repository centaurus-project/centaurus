using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class Ed25519SignatureSerializer : IXdrSerializer<Ed25519Signature>
    {
        public void Deserialize(ref Ed25519Signature value, XdrReader reader)
        {
            value.Signer = reader.Read<RawPubKey>();
            value.Signature = reader.ReadVariable();
        }

        public void Serialize(Ed25519Signature value, XdrWriter writer)
        {
            writer.Write(value.Signer);
            writer.Write(value.Signature);
        }
    }
}
