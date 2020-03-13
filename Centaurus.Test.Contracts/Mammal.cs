using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Test.Contracts
{
    public class Mammal: Tetrapod
    {
        [XdrField(0, Optional = true)]
        public Fur Fur { get; set; }
    }
}
