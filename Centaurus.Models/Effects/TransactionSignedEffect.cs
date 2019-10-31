using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class TransactionSignedEffect : Effect
    {
        public override EffectTypes EffectType => EffectTypes.TransactionSigned;

        [XdrField(0)]
        public byte[] TransactionHash { get; set; }

        [XdrField(1)]
        public Ed25519Signature Signature { get; set; }
    }
}
