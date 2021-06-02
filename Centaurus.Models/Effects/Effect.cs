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
    [XdrUnion((int)EffectTypes.WithdrawalCreate, typeof(WithdrawalCreateEffect))]
    [XdrUnion((int)EffectTypes.WithdrawalRemove, typeof(WithdrawalRemoveEffect))]
    [XdrUnion((int)EffectTypes.RequestRateLimitUpdate, typeof(RequestRateLimitUpdateEffect))]
    public abstract class Effect
    {
        public abstract EffectTypes EffectType { get; }

        //ignore it during the serialization - we need it only to decide which effects to send back to a particular user
        public AccountWrapper AccountWrapper { get; set; }

        public long Apex { get; set; }

        [XdrField(0)]
        public int Account { get; set; }
    }
}
