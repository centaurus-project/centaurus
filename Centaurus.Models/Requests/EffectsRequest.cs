using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class EffectsRequest : RequestMessage
    {
        public const string Desc = "DESC";

        public const string Asc = "ASC";

        [XdrField(0)]
        public string Cursor { get; set; }

        [XdrField(1)]
        public string Order { get; set; }

        [XdrField(2)]
        public int Limit { get; set; }

        public bool IsDesc => Desc.Equals(Order, StringComparison.OrdinalIgnoreCase);
    }
}