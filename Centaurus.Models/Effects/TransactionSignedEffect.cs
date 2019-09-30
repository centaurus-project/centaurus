using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class TransactionSignedEffect : Effect
    {
        public override EffectTypes EffectType => EffectTypes.TransactionSigned;

        public byte[] TransactionHash { get; set; }

        public Ed25519Signature Signature { get; set; }
    }
}
