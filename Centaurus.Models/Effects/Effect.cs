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
    [XdrUnion((int)EffectTypes.LockLiabilities, typeof(LockLiabilitiesEffect))]
    [XdrUnion((int)EffectTypes.UnlockLiabilities, typeof(UnlockLiabilitiesEffect))]
    [XdrUnion((int)EffectTypes.OrderPlaced, typeof(OrderPlacedEffect))]
    [XdrUnion((int)EffectTypes.OrderRemoved, typeof(OrderRemovedEffect))]
    [XdrUnion((int)EffectTypes.Trade, typeof(TradeEffect))]
    [XdrUnion((int)EffectTypes.TransactionSigned, typeof(TransactionSignedEffect))]
    [XdrUnion((int)EffectTypes.LedgerUpdate, typeof(LedgerUpdateEffect))]
    [XdrUnion((int)EffectTypes.VaultSequenceUpdate, typeof(VaultSequenceUpdateEffect))]
    [XdrUnion((int)EffectTypes.ConstellationInit, typeof(ConstellationInitEffect))]
    [XdrUnion((int)EffectTypes.ConstellationUpdate, typeof(ConstellationUpdateEffect))]
    [XdrUnion((int)EffectTypes.WithdrawalCreate, typeof(WithdrawalCreateEffect))]
    [XdrUnion((int)EffectTypes.WithdrawalRemove, typeof(WithdrawalRemoveEffect))]
    [XdrUnion((int)EffectTypes.RequestRateLimitUpdate, typeof(RequestRateLimitUpdateEffect))]
    public class Effect
    {
        public virtual EffectTypes EffectType => throw new InvalidOperationException();

        //ignore it during the serialization - we need it only to decide which effects to send back to a particular user
        public RawPubKey Pubkey { get; set; }

        [XdrField(0)]
        public long Apex { get; set; }
    }
}
