using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class QuantumInfoResponse : ResultMessage
    {
        [XdrField(0)]
        public List<QuantumInfo> Items { get; set; }

        [XdrField(1)]
        public string CurrentPagingToken { get; set; }

        [XdrField(2)]
        public string NextPageToken { get; set; }

        [XdrField(3)]
        public string PrevPageToken { get; set; }

        [XdrField(4)]
        public string Order { get; set; }

        [XdrField(5)]
        public int Limit { get; set; }
    }
}