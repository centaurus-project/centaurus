﻿using Centaurus.Domain.Models;
using Centaurus.Models;
using Centaurus.PaymentProvider;

namespace Centaurus.Domain
{
    public static class ProcessorContextExtensions
    {
        public static void AddAccountCreate(this ProcessorContext processorContext, AccountStorage accountStorage, ulong accountId, RawPubKey publicKey)
        {
            processorContext.AddEffectProcessor(new AccountCreateEffectProcessor(
                new AccountCreateEffect
                {
                    Account = accountId,
                    Pubkey = publicKey,
                    Apex = processorContext.Apex
                },
                accountStorage,
                processorContext.Context.Constellation.RequestRateLimits
            ));
        }

        public static void AddBalanceCreate(this ProcessorContext processorContext, AccountWrapper account, string asset)
        {
            processorContext.AddEffectProcessor(new BalanceCreateEffectProcessor(
                new BalanceCreateEffect
                {
                    Account = account.Id,
                    Asset = asset,
                    Apex = processorContext.Apex
                },
                account
            ));
        }

        public static void AddBalanceUpdate(this ProcessorContext processorContext, AccountWrapper account, string asset, ulong amount, UpdateSign sign)
        {
            processorContext.AddEffectProcessor(new BalanceUpdateEffectProcesor(
                new BalanceUpdateEffect
                {
                    Account = account.Id,
                    Amount = amount,
                    Asset = asset,
                    Apex = processorContext.Apex
                },
                account,
                sign
            ));
        }

        public static void AddOrderPlaced(this ProcessorContext processorContext, OrderbookBase orderBook, OrderWrapper order, string baseAsset)
        {
            var effect = new OrderPlacedEffect
            {
                Apex = processorContext.Apex,
                Account = order.AccountWrapper.Id,
                Asset = order.Order.Asset,
                Amount = order.Order.Amount,
                QuoteAmount = order.Order.QuoteAmount,
                Price = order.Order.Price,
                Side = order.Order.Side
            };

            processorContext.AddEffectProcessor(new OrderPlacedEffectProcessor(effect, order.AccountWrapper, orderBook, order, baseAsset));
        }

        public static void AddTrade(this ProcessorContext processorContext, OrderWrapper order, ulong assetAmount, ulong quoteAmount, string baseAsset, bool isNewOrder)
        {
            var trade = new TradeEffect
            {
                OrderId = order.OrderId,
                Asset = order.Order.Asset,
                Side = order.Order.Side,
                Apex = processorContext.Apex,
                Account = order.AccountWrapper.Id,
                AssetAmount = assetAmount,
                QuoteAmount = quoteAmount,
                IsNewOrder = isNewOrder
            };

            processorContext.AddEffectProcessor(new TradeEffectProcessor(trade, order.AccountWrapper, order, baseAsset));
        }


        public static void AddOrderRemoved(this ProcessorContext processorContext, OrderbookBase orderbook, OrderWrapper order, string baseAsset)
        {
            processorContext.AddEffectProcessor(new OrderRemovedEffectProccessor(
                new OrderRemovedEffect
                {
                    Apex = processorContext.Apex,
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



        public static void AddNonceUpdate(this ProcessorContext processorContext, AccountWrapper account, ulong newNonce, ulong currentNonce)
        {
            processorContext.AddEffectProcessor(new NonceUpdateEffectProcessor(
                new NonceUpdateEffect
                {
                    Nonce = newNonce,
                    PrevNonce = currentNonce,
                    Account = account.Id,
                    Apex = processorContext.Apex
                },
                account
            ));
        }

        public static void AddCursorUpdate(this ProcessorContext processorContext, DepositNotificationManager notificationManager, string providerId, string newCursor, string prevCursor)
        {
            processorContext.AddEffectProcessor(new TxCursorUpdateEffectProcessor(
                new CursorUpdateEffect
                {
                    Apex = processorContext.Apex,
                    Cursor = newCursor,
                    PrevCursor = prevCursor,
                    ProviderId = providerId
                },
                notificationManager
            ));
        }

        public static void AddConstellationUpdate(this ProcessorContext processorContext, ConstellationSettings settings, ConstellationSettings prevSettings)
        {
            processorContext.AddEffectProcessor(new ConstellationInitUpdateProcessor(
                new ConstellationUpdateEffect
                {
                    Apex = processorContext.Apex,
                    Settings = settings,
                    PrevSettings = prevSettings
                }
            ));
        }
    }
}
