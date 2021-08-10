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
        public List<Hash> Hashes { get; set; }

        [XdrField(1)]
        public List<TinySignature> Signatures { get; set; }
    }
}