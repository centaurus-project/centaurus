using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class QuantaBatchRequest : Message
    {
        [XdrField(0)]
        public ulong QuantaCursor { get; set; }

        [XdrField(1)]
        public ulong ResultCursor { get; set; }
    }
}