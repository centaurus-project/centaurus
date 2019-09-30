using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class TransactionSignedEffectSerializer : IXdrSerializer<TransactionSignedEffect>
    {
        public void Deserialize(ref TransactionSignedEffect value, XdrReader reader)
        {
            value.TransactionHash = reader.ReadVariable();
            value.Signature = reader.Read<Ed25519Signature>();
        }

        public void Serialize(TransactionSignedEffect value, XdrWriter writer)
        {
            writer.Write(value.TransactionHash);
            writer.Write(value.Signature);
        }
    }
}
