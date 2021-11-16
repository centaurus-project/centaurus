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
    [XdrUnion((int)EffectTypes.ConstellationUpdate, typeof(ConstellationUpdateEffect))]
    [XdrUnion((int)EffectTypes.RequestRateLimitUpdate, typeof(RequestRateLimitUpdateEffect))]
    public abstract class Effect
    {
        public ulong Apex { get; set; }
    }

    public abstract class AccountEffect: Effect
    {
        public RawPubKey Account { get; set; }
    }
}