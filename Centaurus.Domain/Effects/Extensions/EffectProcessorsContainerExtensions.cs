using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public static class EffectProcessorsContainerExtensions
    {
        public static void AddWithdrawalCreate(this EffectProcessorsContainer effectProcessors, WithdrawalWrapper withdrawal, WithdrawalStorage withdrawalStorage)
        {
            var effect = new WithdrawalCreateEffect
            {
                Apex = effectProcessors.Apex,
                Account = withdrawal.Source.Account.Id,
                AccountWrapper = withdrawal.Source,
                Items = withdrawal.Withdrawals.Select(w => new WithdrawalEffectItem { Asset = w.Asset, Amount = w.Amount }).OrderBy(a => a.Asset).ToList()
            };
            effectProcessors.Add(new WithdrawalCreateEffectProcessor(effect, withdrawal, withdrawalStorage));
        }

        public static void AddWithdrawalRemove(this EffectProcessorsContainer effectProcessors, WithdrawalWrapper withdrawal, bool isSuccessful, WithdrawalStorage withdrawalStorage)
        {
            var effect = new WithdrawalRemoveEffect
            {
                Apex = effectProcessors.Apex,
                Account = withdrawal.Source.Account.Id,
                AccountWrapper = withdrawal.Source,
                IsSuccessful = isSuccessful,
                Items = withdrawal.Withdrawals.Select(w => new WithdrawalEffectItem { Asset = w.Asset, Amount = w.Amount }).OrderBy(a => a.Asset).ToList()
            };
            effectProcessors.Add(new WithdrawalRemoveEffectProcessor(effect, withdrawal, withdrawalStorage));
        }

        public static void AddAccountCreate(this EffectProcessorsContainer effectProcessors, AccountStorage accountStorage, int accountId, RawPubKey publicKey)
        {
            effectProcessors.Add(new AccountCreateEffectProcessor(
                new AccountCreateEffect
                {
                    Account = accountId,
                    Pubkey = publicKey,
                    Apex = effectProcessors.Apex
                },
                accountStorage
            ));
        }

        public static void AddBalanceCreate(this EffectProcessorsContainer effectProcessors, AccountWrapper account, int asset)
        {
            effectProcessors.Add(new BalanceCreateEffectProcessor(
                new BalanceCreateEffect
                {
                    Account = account.Id,
                    AccountWrapper = account,
                    Asset = asset,
                    Apex = effectProcessors.Apex
                }
            ));
        }

        public static void AddBalanceUpdate(this EffectProcessorsContainer effectProcessors, AccountWrapper account, int asset, long amount)
        {
            effectProcessors.Add(new BalanceUpdateEffectProcesor(
                new BalanceUpdateEffect
                {
                    Account = account.Id,
                    AccountWrapper = account,
                    Amount = amount,
                    Asset = asset,
                    Apex = effectProcessors.Apex
                }
            ));
        }

        public static void AddOrderPlaced(this EffectProcessorsContainer effectProcessors, OrderbookBase orderBook, Order order)
        {
            var decodedOrderId = OrderIdConverter.Decode(order.OrderId);
            var effect = new OrderPlacedEffect
            {
                Apex = effectProcessors.Apex,
                Account = order.AccountWrapper.Id,
                AccountWrapper = order.AccountWrapper,
                Asset = decodedOrderId.Asset,
                Amount = order.Amount,
                QuoteAmount = order.QuoteAmount,
                Price = order.Price,
                OrderId = order.OrderId,
                OrderSide = decodedOrderId.Side
            };

            effectProcessors.Add(new OrderPlacedEffectProcessor(effect, orderBook, order));
        }

        public static void AddTrade(this EffectProcessorsContainer effectProcessors, Order order, long assetAmount, long quoteAmount, bool isNewOrder)
        {
            var trade = new TradeEffect
            {
                Apex = effectProcessors.Apex,
                Account = order.AccountWrapper.Id,
                AccountWrapper = order.AccountWrapper,
                AssetAmount = assetAmount,
                QuoteAmount = quoteAmount,
                OrderId = order.OrderId,
                IsNewOrder = isNewOrder
            };

            effectProcessors.Add(new TradeEffectProcessor(trade, order));
        }


        public static void AddOrderRemoved(this EffectProcessorsContainer effectProcessors, OrderbookBase orderbook, Order order)
        {
            effectProcessors.Add(new OrderRemovedEffectProccessor(
                new OrderRemovedEffect
                {
                    Apex = effectProcessors.Apex,
                    OrderId = order.OrderId,
                    Account = order.AccountWrapper.Id,
                    AccountWrapper = order.AccountWrapper,
                    Amount = order.Amount,
                    QuoteAmount = order.QuoteAmount,
                    Price = order.Price
                },
                orderbook
            ));
        }



        public static void AddNonceUpdate(this EffectProcessorsContainer effectProcessors, AccountWrapper account, long newNonce, long currentNonce)
        {
            effectProcessors.Add(new NonceUpdateEffectProcessor(
                new NonceUpdateEffect
                {
                    Nonce = newNonce,
                    PrevNonce = currentNonce,
                    Account = account.Id,
                    AccountWrapper = account,
                    Apex = effectProcessors.Apex
                }
            ));
        }

        public static void AddCursorUpdate(this EffectProcessorsContainer effectProcessors, TxCursorManager txManager, long newCursor, long prevCursor)
        {
            effectProcessors.Add(new TxCursorUpdateEffectProcessor(
                new TxCursorUpdateEffect
                {
                    Apex = effectProcessors.Apex,
                    Cursor = newCursor,
                    PrevCursor = prevCursor
                },
                txManager
            ));
        }

        public static void AddConstellationInit(this EffectProcessorsContainer effectProcessors, ConstellationInitQuantum initQuantum)
        {
            effectProcessors.Add(new ConstellationInitEffectProcessor(
                new ConstellationInitEffect
                {
                    Apex = initQuantum.Apex,
                    Assets = initQuantum.Assets,
                    Auditors = initQuantum.Auditors,
                    MinAccountBalance = initQuantum.MinAccountBalance,
                    MinAllowedLotSize = initQuantum.MinAllowedLotSize,
                    Vault = initQuantum.Vault,
                    RequestRateLimits = initQuantum.RequestRateLimits,
                    TxCursor = initQuantum.TxCursor
                }
            ));
        }
    }
}
