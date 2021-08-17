using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    public class TradeEffect: AccountEffect
    {
        [XdrField(0)]
        public ulong OrderId { get; set; }

        [XdrField(1)]
        public ulong AssetAmount { get; set; }

        [XdrField(2)]
        public ulong QuoteAmount { get; set; }

        [XdrField(3)]
        public bool IsNewOrder { get; set; }

        [XdrField(4)]
        public OrderSide Side { get; set; }

        [XdrField(5)]
        public string Asset { get; set; }
    }
}
