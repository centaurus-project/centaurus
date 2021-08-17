using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    public class ClientConnectionSuccess : ResultMessage
    {
        [XdrField(0)]
        public ulong AccountId { get; set; }
    }
}