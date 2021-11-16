using Centaurus.Domain.Models;
using Centaurus.Models;
using Centaurus.PaymentProvider;

namespace Centaurus.Domain
{
    public static class ProcessorContextExtensions
    {
        public static void AddAccountCreate(this QuantumProcessingItem processingItem, AccountStorage accountStorage, RawPubKey publicKey, RequestRateLimits requestRateLimits)
        {
            processingItem.AddEffectProcessor(new AccountCreateEffectProcessor(
                new AccountCreateEffect
                {
                    Account = publicKey,
                    Apex = processingItem.Apex
                },
                accountStorage,
                requestRateLimits
            ));
        }

        public static void AddBalanceCreate(this QuantumProcessingItem processingItem, Account account, string asset)
        {
            processingItem.AddEffectProcessor(new BalanceCreateEffectProcessor(
                new BalanceCreateEffect
                {
                    Account = account.Pubkey,
                    Asset = asset,
                    Apex = processingItem.Apex
                },
                account
            ));
        }

        public static void AddBalanceUpdate(this QuantumProcessingItem processingItem, Account account, string asset, ulong amount, UpdateSign sign)
        {
            processingItem.AddEffectProcessor(new BalanceUpdateEffectProcesor(
                new BalanceUpdateEffect
                {
                    Account = account.Pubkey,
                    Amount = amount,
                    Asset = asset,
                    Apex = processingItem.Apex,
                    Sign = sign
                },
                account
            ));
        }

        public static void AddOrderPlaced(this QuantumProcessingItem processingItem, Orderbook orderBook, OrderWrapper order, string baseAsset)
        {
            var effect = new OrderPlacedEffect
            {
                Apex = processingItem.Apex,
                Account = order.Account.Pubkey,
                Asset = order.Order.Asset,
                Amount = order.Order.Amount,
                QuoteAmount = order.Order.QuoteAmount,
                Price = order.Order.Price,
                Side = order.Order.Side
            };

            processingItem.AddEffectProcessor(new OrderPlacedEffectProcessor(effect, order.Account, orderBook, order, baseAsset));
        }

        public static void AddTrade(this QuantumProcessingItem processingItem, OrderWrapper order, ulong assetAmount, ulong quoteAmount, string baseAsset, bool isNewOrder)
        {
            var trade = new TradeEffect
            {
                OrderId = order.OrderId,
                Asset = order.Order.Asset,
                Side = order.Order.Side,
                Apex = processingItem.Apex,
                Account = order.Account.Pubkey,
                AssetAmount = assetAmount,
                QuoteAmount = quoteAmount,
                IsNewOrder = isNewOrder
            };

            processingItem.AddEffectProcessor(new TradeEffectProcessor(trade, order.Account, order, baseAsset));
        }


        public static void AddOrderRemoved(this QuantumProcessingItem processingItem, Orderbook orderbook, OrderWrapper order, string baseAsset)
        {
            processingItem.AddEffectProcessor(new OrderRemovedEffectProccessor(
                new OrderRemovedEffect
                {
                    Apex = processingItem.Apex,
                    Account = order.Account.Pubkey,
                    Amount = order.Order.Amount,
                    QuoteAmount = order.Order.QuoteAmount,
                    Price = order.Order.Price,
                    Side = order.Order.Side,
                    Asset = order.Order.Asset,
                    OrderId = order.OrderId
                },
                order.Account,
                orderbook,
                baseAsset
            ));
        }



        public static void AddNonceUpdate(this QuantumProcessingItem processingItem, Account account, ulong newNonce, ulong currentNonce)
        {
            processingItem.AddEffectProcessor(new NonceUpdateEffectProcessor(
                new NonceUpdateEffect
                {
                    Nonce = newNonce,
                    PrevNonce = currentNonce,
                    Account = account.Pubkey,
                    Apex = processingItem.Apex
                },
                account
            ));
        }

        public static void AddCursorUpdate(this QuantumProcessingItem processingItem, DepositNotificationManager notificationManager, string providerId, string newCursor, string prevCursor)
        {
            processingItem.AddEffectProcessor(new TxCursorUpdateEffectProcessor(
                new CursorUpdateEffect
                {
                    Apex = processingItem.Apex,
                    Cursor = newCursor,
                    PrevCursor = prevCursor,
                    Provider = providerId
                },
                notificationManager
            ));
        }

        public static void AddConstellationUpdate(this QuantumProcessingItem processingItem, ConstellationSettings settings, ConstellationSettings prevSettings)
        {
            processingItem.AddEffectProcessor(new ConstellationInitUpdateProcessor(
                new ConstellationUpdateEffect
                {
                    Apex = processingItem.Apex,
                    Settings = settings,
                    PrevSettings = prevSettings
                }
            ));
        }
    }
}
