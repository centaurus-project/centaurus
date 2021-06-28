using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class EffectsProof
    {
        [XdrField(0)]
        public EffectHashes Hashes { get; set; }

        [XdrField(1)]
        public List<Ed25519Signature> Signatures { get; set; }
    }
}