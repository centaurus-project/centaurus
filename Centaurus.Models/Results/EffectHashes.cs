using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class EffectHashes
    {
        [XdrField(0)]
        public List<Hash> Hashes { get; set; }
    }
}