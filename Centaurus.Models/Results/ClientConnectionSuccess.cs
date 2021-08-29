using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    public class ClientConnectionSuccess : ResultMessageBase
    {
        [XdrField(0)]
        public ulong AccountId { get; set; }
    }
}