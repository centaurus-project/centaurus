using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Test.Contracts
{
    [XdrContract]
    [XdrUnion(1, typeof(Fish))]
    [XdrUnion(2, typeof(Tetrapod))]
    public abstract class Vertebrate
    {
        [XdrField(0)]
        public string Species { get; set; }

        [XdrField(1)]
        public bool ColdBlooded { get; set; }

        [XdrField(2, Optional = true)]
        public List<Feature> Features { get; set; }
    }
}
