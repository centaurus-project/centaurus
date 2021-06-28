using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class PaymentCursor
    {
        [XdrField(0)]
        public string Provider { get; set; }

        [XdrField(1)]
        public string Cursor { get; set; }
    }
}
