using Centaurus.Domain.Models;
using Centaurus.Models;
using Centaurus.PaymentProvider;

namespace Centaurus.Domain
{
    public static class ProcessorContextExtensions
    {
        public static void AddAccountCreate(this ProcessorContext effectProcessors, AccountStorage accountStorage, ulong accountId, RawPubKey publicKey)
        {
            effectProcessors.AddEffectProcessor(new AccountCreateEffectProcessor(
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

        public static void AddBalanceCreate(this ProcessorContext effectProcessors, AccountWrapper account, string asset)
        {
            effectProcessors.AddEffectProcessor(new BalanceCreateEffectProcessor(
                new BalanceCreateEffect
                {
                    Account = account.Id,
                    Asset = asset,
                    Apex = effectProcessors.Apex
                },
                account
            ));
        }

        public static void AddBalanceUpdate(this ProcessorContext effectProcessors, AccountWrapper account, string asset, ulong amount, UpdateSign sign)
        {
            effectProcessors.AddEffectProcessor(new BalanceUpdateEffectProcesor(
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

        public static void AddOrderPlaced(this ProcessorContext effectProcessors, OrderbookBase orderBook, OrderWrapper order, string baseAsset)
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

            effectProcessors.AddEffectProcessor(new OrderPlacedEffectProcessor(effect, order.AccountWrapper, orderBook, order, baseAsset));
        }

        public static void AddTrade(this ProcessorContext effectProcessors, OrderWrapper order, ulong assetAmount, ulong quoteAmount, string baseAsset, bool isNewOrder)
        {
            var trade = new TradeEffect
            {
                Apex = effectProcessors.Apex,
                Account = order.AccountWrapper.Id,
                AssetAmount = assetAmount,
                QuoteAmount = quoteAmount,
                IsNewOrder = isNewOrder
            };

            effectProcessors.AddEffectProcessor(new TradeEffectProcessor(trade, order.AccountWrapper, order, baseAsset));
        }


        public static void AddOrderRemoved(this ProcessorContext effectProcessors, OrderbookBase orderbook, OrderWrapper order, string baseAsset)
        {
            effectProcessors.AddEffectProcessor(new OrderRemovedEffectProccessor(
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



        public static void AddNonceUpdate(this ProcessorContext effectProcessors, AccountWrapper account, ulong newNonce, ulong currentNonce)
        {
            effectProcessors.AddEffectProcessor(new NonceUpdateEffectProcessor(
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

        public static void AddCursorUpdate(this ProcessorContext effectProcessors, DepositNotificationManager notificationManager, string providerId, string newCursor, string prevCursor)
        {
            effectProcessors.AddEffectProcessor(new TxCursorUpdateEffectProcessor(
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

        public static void AddConstellationUpdate(this ProcessorContext effectProcessors, ConstellationSettings settings, ConstellationSettings prevSettings)
        {
            effectProcessors.AddEffectProcessor(new ConstellationInitUpdateProcessor(
                new ConstellationUpdateEffect
                {
                    Apex = effectProcessors.Apex,
                    Settings = settings,
                    PrevSettings = prevSettings
                }
            ));
        }
    }
}
