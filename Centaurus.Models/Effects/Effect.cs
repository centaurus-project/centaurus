using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    [XdrUnion((int)EffectTypes.OrderPlaced, typeof(OrderPlacedEffect))]
    [XdrUnion((int)EffectTypes.OrderRemoved, typeof(OrderRemovedEffect))]
    [XdrUnion((int)EffectTypes.Trade, typeof(TradeEffect))]
    [XdrUnion((int)EffectTypes.TransactionSigned, typeof(TransactionSignedEffect))]
    public class Effect
    {
        public virtual EffectTypes EffectType => throw new InvalidOperationException();

        //ignore it during the serialization - we need it only to decide which effects to send back to a particular user
        public RawPubKey Pubkey { get; set; }
    }
}
