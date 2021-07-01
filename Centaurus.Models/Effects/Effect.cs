using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    [XdrContract]
    [XdrUnion((int)EffectTypes.AccountCreate, typeof(AccountCreateEffect))]
    [XdrUnion((int)EffectTypes.NonceUpdate, typeof(NonceUpdateEffect))]
    [XdrUnion((int)EffectTypes.BalanceCreate, typeof(BalanceCreateEffect))]
    [XdrUnion((int)EffectTypes.BalanceUpdate, typeof(BalanceUpdateEffect))]
    [XdrUnion((int)EffectTypes.OrderPlaced, typeof(OrderPlacedEffect))]
    [XdrUnion((int)EffectTypes.OrderRemoved, typeof(OrderRemovedEffect))]
    [XdrUnion((int)EffectTypes.Trade, typeof(TradeEffect))]
    [XdrUnion((int)EffectTypes.TxCursorUpdate, typeof(CursorUpdateEffect))]
    [XdrUnion((int)EffectTypes.ConstellationInit, typeof(ConstellationInitEffect))]
    [XdrUnion((int)EffectTypes.ConstellationUpdate, typeof(ConstellationUpdateEffect))]
    [XdrUnion((int)EffectTypes.RequestRateLimitUpdate, typeof(RequestRateLimitUpdateEffect))]
    public abstract class Effect
    {
        public abstract EffectTypes EffectType { get; }

        public long Apex { get; set; }

        [XdrField(0)]
        public int Account { get; set; }
    }
}
