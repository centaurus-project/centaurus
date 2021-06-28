using Centaurus.Domain.Models;
using Centaurus.Models;
using Centaurus.PaymentProvider;
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
                Account = withdrawal.AccountWrapper.Account.Id,
                Items = withdrawal.Items.Select(w => new WithdrawalEffectItem { Asset = w.Asset, Amount = w.Amount }).OrderBy(a => a.Asset).ToList()
            };
            effectProcessors.Add(new WithdrawalCreateEffectProcessor(effect, withdrawal.AccountWrapper, withdrawal, withdrawalStorage));
        }

        public static void AddWithdrawalRemove(this EffectProcessorsContainer effectProcessors, WithdrawalWrapper withdrawal, bool isSuccessful, WithdrawalStorage withdrawalStorage)
        {
            var effect = new WithdrawalRemoveEffect
            {
                Apex = effectProcessors.Apex,
                Account = withdrawal.AccountWrapper.Account.Id,
                IsSuccessful = isSuccessful,
                Items = withdrawal.Items.Select(w => new WithdrawalEffectItem { Asset = w.Asset, Amount = w.Amount }).OrderBy(a => a.Asset).ToList()
            };
            effectProcessors.Add(new WithdrawalRemoveEffectProcessor(effect, withdrawal.AccountWrapper, withdrawal, withdrawalStorage));
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
                accountStorage,
                effectProcessors.Context.Constellation.RequestRateLimits
            ));
        }

        public static void AddBalanceCreate(this EffectProcessorsContainer effectProcessors, AccountWrapper account, int asset)
        {
            effectProcessors.Add(new BalanceCreateEffectProcessor(
                new BalanceCreateEffect
                {
                    Account = account.Id,
                    Asset = asset,
                    Apex = effectProcessors.Apex
                },
                account
            ));
        }

        public static void AddBalanceUpdate(this EffectProcessorsContainer effectProcessors, AccountWrapper account, int asset, long amount)
        {
            effectProcessors.Add(new BalanceUpdateEffectProcesor(
                new BalanceUpdateEffect
                {
                    Account = account.Id,
                    Amount = amount,
                    Asset = asset,
                    Apex = effectProcessors.Apex
                },
                account
            ));
        }

        public static void AddOrderPlaced(this EffectProcessorsContainer effectProcessors, OrderbookBase orderBook, OrderWrapper order)
        {
            var decodedOrderId = OrderIdConverter.Decode(order.OrderId);
            var effect = new OrderPlacedEffect
            {
                Apex = effectProcessors.Apex,
                Account = order.AccountWrapper.Id,
                Asset = decodedOrderId.Asset,
                Amount = order.Order.Amount,
                QuoteAmount = order.Order.QuoteAmount,
                Price = order.Order.Price,
                OrderId = order.OrderId,
                OrderSide = decodedOrderId.Side
            };

            effectProcessors.Add(new OrderPlacedEffectProcessor(effect, order.AccountWrapper, orderBook, order));
        }

        public static void AddTrade(this EffectProcessorsContainer effectProcessors, OrderWrapper order, long assetAmount, long quoteAmount, bool isNewOrder)
        {
            var trade = new TradeEffect
            {
                Apex = effectProcessors.Apex,
                Account = order.AccountWrapper.Id,
                AssetAmount = assetAmount,
                QuoteAmount = quoteAmount,
                OrderId = order.OrderId,
                IsNewOrder = isNewOrder
            };

            effectProcessors.Add(new TradeEffectProcessor(trade, order.AccountWrapper, order));
        }


        public static void AddOrderRemoved(this EffectProcessorsContainer effectProcessors, OrderbookBase orderbook, OrderWrapper order)
        {
            effectProcessors.Add(new OrderRemovedEffectProccessor(
                new OrderRemovedEffect
                {
                    Apex = effectProcessors.Apex,
                    OrderId = order.OrderId,
                    Account = order.AccountWrapper.Id,
                    Amount = order.Order.Amount,
                    QuoteAmount = order.Order.QuoteAmount,
                    Price = order.Order.Price
                },
                order.AccountWrapper,
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
                    Apex = effectProcessors.Apex
                },
                account
            ));
        }

        public static void AddCursorUpdate(this EffectProcessorsContainer effectProcessors, PaymentNotificationManager notificationManager, string newCursor, string prevCursor)
        {
            effectProcessors.Add(new TxCursorUpdateEffectProcessor(
                new CursorUpdateEffect
                {
                    Apex = effectProcessors.Apex,
                    Cursor = newCursor,
                    PrevCursor = prevCursor
                },
                notificationManager
            ));
        }

        public static void AddConstellationInit(this EffectProcessorsContainer effectProcessors, ConstellationInitRequest initQuantum)
        {
            effectProcessors.Add(new ConstellationInitEffectProcessor(
                new ConstellationInitEffect
                {
                    Apex = effectProcessors.Apex,
                    Assets = initQuantum.Assets,
                    Auditors = initQuantum.Auditors,
                    MinAccountBalance = initQuantum.MinAccountBalance,
                    MinAllowedLotSize = initQuantum.MinAllowedLotSize,
                    RequestRateLimits = initQuantum.RequestRateLimits,
                    Providers = initQuantum.Providers
                }
            ));
        }
    }
}
