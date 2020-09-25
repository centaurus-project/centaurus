using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    public class TransactionSignedEffect : SideEffect
    {
        public override SideEffectTypes EffectType => SideEffectTypes.TransactionSigned;

        [XdrField(0)]
        public byte[] TransactionHash { get; set; }

        [XdrField(1)]
        public Ed25519Signature Signature { get; set; }
    }
}
