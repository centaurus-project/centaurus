using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public class EffectProcessorsContainer
    {
        List<IEffectProcessor<Effect>> container = new List<IEffectProcessor<Effect>>();

        public EffectProcessorsContainer(MessageEnvelope quantum, Action<MessageEnvelope, Effect[]> saveEffectsFn)
        {
            Envelope = quantum ?? throw new ArgumentNullException(nameof(quantum));
            this.saveEffectsFn = saveEffectsFn ?? throw new ArgumentNullException(nameof(saveEffectsFn));
        }

        private readonly Action<MessageEnvelope, Effect[]> saveEffectsFn;
        public MessageEnvelope Envelope { get; }

        public Quantum Quantum => (Quantum)Envelope.Message;

        public long Apex => Quantum.Apex;

        public RequestQuantum RequestQuantum => (RequestQuantum)Envelope.Message;

        /// <summary>
        /// Adds effect processor to container
        /// </summary>
        /// <param name="effect"></param>
        public void Add(IEffectProcessor<Effect> effect, bool immediateCommit = true)
        {
            container.Add(effect);
            if (immediateCommit)
                effect.CommitEffect();
        }

        /// <summary>
        /// Unwraps and returns effects for specified account.
        /// </summary>
        /// <returns></returns>
        public Effect[] GetEffects(RawPubKey pubKey)
        {
            return GetEffectsInternal()
                .Where(e => e.Pubkey.Equals(pubKey))
                .ToArray();
        }

        /// <summary>
        /// Unwraps and returns effects
        /// </summary>
        /// <returns></returns>
        public Effect[] GetEffects()
        {
            return GetEffectsInternal().ToArray();
        }

        private IEnumerable<Effect> GetEffectsInternal()
        {
            return container.Select(ep => ep.Effect);
        }

        /// <summary>
        /// Iterates over the effect processors and commits each.
        /// </summary>
        public void CommitAll()
        {
            var effectsLength = container.Count;
            for (var i = 0; i < effectsLength; i++)
            {
                var currentEffect = container[i];
                currentEffect.CommitEffect();
            }
        }

        /// <summary>
        /// Sends envelope and all effects to specified callback
        /// </summary>
        public void SaveEffects()
        {
            saveEffectsFn.Invoke(Envelope, GetEffects());
        }

        /// <summary>
        /// Iterates over the effect processors and reverts each
        /// </summary>
        public void Revert()
        {
            for (var i = container.Count - 1; i >= 0; i--)
            {
                var currentEffect = container[i];
                currentEffect.RevertEffect();
            }
        }

        public void AddWithdrawalCreate(Withdrawal withdrawal, WithdrawalStorage withdrawalStorage)
        {
            var effect = new WithdrawalCreateEffect
            {
                Apex = Apex,
                Pubkey = withdrawal.Source,
                Withdrawal = withdrawal
            };
            Add(new WithdrawalCreateEffectProcessor(effect, withdrawalStorage));
        }

        public void AddWithdrawalRemove(Withdrawal withdrawal, WithdrawalStorage withdrawalStorage)
        {
            var effect = new WithdrawalRemoveEffect
            {
                Apex = Apex,
                Withdrawal = withdrawal,
                Pubkey = withdrawal.Source
            };
            Add(new WithdrawalRemoveEffectProcessor(effect, withdrawalStorage));
        }

        public void AddAccountCreate(AccountStorage accountStorage, RawPubKey publicKey)
        {
            Add(new AccountCreateEffectProcessor(
                new AccountCreateEffect { Pubkey = publicKey, Apex = Apex },
                accountStorage
            ));
        }

        public void AddBalanceCreate(Account account, int asset)
        {
            Add(new BalanceCreateEffectProcessor(
                new BalanceCreateEffect { Pubkey = account.Pubkey, Asset = asset, Apex = Apex },
                account
            ));
        }

        public void AddBalanceUpdate(Account account, int asset, long amount)
        {
            Add(new BalanceUpdateEffectProcesor(
                new BalanceUpdateEffect { Pubkey = account.Pubkey, Amount = amount, Asset = asset, Apex = Apex },
                account
            ));
        }

        public void AddUnlockLiabilities(Account account, int asset, long amount)
        {
            Add(new UnlockLiabilitiesEffectProcessor(
                new UnlockLiabilitiesEffect { Amount = amount, Asset = asset, Pubkey = account.Pubkey, Apex = Apex },
                account
            ));
        }

        public void AddLockLiabilities(Account account, int asset, long amount)
        {
            Add(new LockLiabilitiesEffectProcessor(
                new LockLiabilitiesEffect { Amount = amount, Asset = asset, Pubkey = account.Pubkey, Apex = Apex },
                account
            ));
        }

        public void AddOrderPlaced(Orderbook orderBook, Order order, long orderAmount, int asset, OrderSides side)
        {
            var effect = new OrderPlacedEffect
            {
                Apex = Apex,
                Pubkey = order.Account.Pubkey,
                Asset = asset,
                Amount = orderAmount,
                Price = order.Price,
                OrderId = order.OrderId,
                OrderSide = side
            };

            Add(new OrderPlacedEffectProcessor(effect, orderBook, order));
        }


        public void AddTrade(Order order, int asset, long assetAmount, long xlmAmount, double price, OrderSides side)
        {
            var trade = new TradeEffect
            {
                Apex = Apex,
                Pubkey = order.Account.Pubkey,
                Asset = asset,
                AssetAmount = assetAmount,
                XlmAmount = xlmAmount,
                Price = price,
                OrderId = order.OrderId,
                OrderSide = side
            };

            Add(new TradeEffectProcessor(trade, order));
        }


        public void AddOrderRemoved(Orderbook orderbook, Order order)
        {
            Add(new OrderRemovedEffectProccessor(
                new OrderRemovedEffect { Apex = Apex, OrderId = order.OrderId, Price = order.Price, Pubkey = order.Account.Pubkey },
                orderbook,
                order.Account
                ));
        }



        public void AddNonceUpdate(Account account, ulong newNonce, ulong currentNonce)
        {
            Add(new NonceUpdateEffectProcessor(
                new NonceUpdateEffect { Nonce = newNonce, PrevNonce = currentNonce, Pubkey = account.Pubkey, Apex = Apex },
                account
            ));
        }

        public void AddLedgerUpdate(LedgerManager ledgerManager, long newLedger, long prevLedger)
        {
            Add(new LedgerUpdateEffectProcessor(
                new LedgerUpdateEffect {  Apex = Apex, Ledger = newLedger, PrevLedger = prevLedger },
                ledgerManager
            ));
        }
    }
}
