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
        public static void AddAccountCreate(this EffectProcessorsContainer effectProcessors, AccountStorage accountStorage, ulong accountId, RawPubKey publicKey)
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

        public static void AddBalanceCreate(this EffectProcessorsContainer effectProcessors, AccountWrapper account, string asset)
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

        public static void AddBalanceUpdate(this EffectProcessorsContainer effectProcessors, AccountWrapper account, string asset, ulong amount, UpdateSign sign)
        {
            effectProcessors.Add(new BalanceUpdateEffectProcesor(
                new BalanceUpdateEffect
                {
                    Account = account.Id,
                    Amount = amount,
                    Asset = asset,
                    Apex = effectProcessors.Apex
                },
                account,
                sign
            ));
        }

        public static void AddOrderPlaced(this EffectProcessorsContainer effectProcessors, OrderbookBase orderBook, OrderWrapper order, string baseAsset)
        {
            var effect = new OrderPlacedEffect
            {
                Apex = effectProcessors.Apex,
                Account = order.AccountWrapper.Id,
                Asset = order.Order.Asset,
                Amount = order.Order.Amount,
                QuoteAmount = order.Order.QuoteAmount,
                Price = order.Order.Price,
                Side = order.Order.Side
            };

            effectProcessors.Add(new OrderPlacedEffectProcessor(effect, order.AccountWrapper, orderBook, order, baseAsset));
        }

        public static void AddTrade(this EffectProcessorsContainer effectProcessors, OrderWrapper order, ulong assetAmount, ulong quoteAmount, string baseAsset, bool isNewOrder)
        {
            var trade = new TradeEffect
            {
                Apex = effectProcessors.Apex,
                Account = order.AccountWrapper.Id,
                AssetAmount = assetAmount,
                QuoteAmount = quoteAmount,
                IsNewOrder = isNewOrder
            };

            effectProcessors.Add(new TradeEffectProcessor(trade, order.AccountWrapper, order, baseAsset));
        }


        public static void AddOrderRemoved(this EffectProcessorsContainer effectProcessors, OrderbookBase orderbook, OrderWrapper order, string baseAsset)
        {
            effectProcessors.Add(new OrderRemovedEffectProccessor(
                new OrderRemovedEffect
                {
                    Apex = effectProcessors.Apex,
                    Account = order.AccountWrapper.Id,
                    Amount = order.Order.Amount,
                    QuoteAmount = order.Order.QuoteAmount,
                    Price = order.Order.Price,
                    Side = order.Order.Side,
                    Asset = order.Order.Asset,
                    OrderId = order.OrderId
                },
                order.AccountWrapper,
                orderbook,
                baseAsset
            ));
        }



        public static void AddNonceUpdate(this EffectProcessorsContainer effectProcessors, AccountWrapper account, ulong newNonce, ulong currentNonce)
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

        public static void AddCursorUpdate(this EffectProcessorsContainer effectProcessors, DepositNotificationManager notificationManager, string providerId, string newCursor, string prevCursor)
        {
            effectProcessors.Add(new TxCursorUpdateEffectProcessor(
                new CursorUpdateEffect
                {
                    Apex = effectProcessors.Apex,
                    Cursor = newCursor,
                    PrevCursor = prevCursor,
                    ProviderId = providerId
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
