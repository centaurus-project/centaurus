using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class TinySignature: BinaryData
    {
        public override int ByteLength => 64;
    }
}
