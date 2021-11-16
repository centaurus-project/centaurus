using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public abstract class AlphaUpdate: ConstellationRequestMessage
    {
        [XdrField(0)]
        public RawPubKey Alpha { get; set; }
    }
}
