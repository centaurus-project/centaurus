using System;
using System.Linq;
using Centaurus.Domain.Models;
using Centaurus.Models;

namespace Centaurus.Domain
{
    public class OrderMatcher
    {
        public OrderMatcher(OrderRequest orderRequest, EffectProcessorsContainer effectsContainer)
        {
            this.effectsContainer = effectsContainer;
            baseAsset = effectsContainer.Context.Constellation.GetBaseAsset();

            takerOrder = new OrderWrapper(
                new Order
                {
                    OrderId = effectsContainer.Apex,
                    Amount = orderRequest.Amount,
                    Price = orderRequest.Price,
                    Asset = orderRequest.Asset,
                    Side = orderRequest.Side
                },
                effectsContainer.AccountWrapper
            );
            timeInForce = orderRequest.TimeInForce;

            asset = takerOrder.Order.Asset;
            side = takerOrder.Order.Side;
            //get asset orderbook
            market = effectsContainer.Context.Exchange.GetMarket(asset);
            orderbook = market.GetOrderbook(side.Inverse());
            //fetch balances
            if (!takerOrder.AccountWrapper.Account.HasBalance(asset))
                this.effectsContainer.AddBalanceCreate(effectsContainer.AccountWrapper, asset);
        }

        private readonly OrderWrapper takerOrder;

        private readonly TimeInForce timeInForce;

        private readonly string asset;

        private readonly OrderSide side;

        private readonly OrderbookBase orderbook;

        private readonly ExchangeMarket market;

        private readonly EffectProcessorsContainer effectsContainer;

        private readonly string baseAsset;

        /// <summary>
        /// Match using specified time-in-force logic.
        /// </summary>
        /// <param name="timeInForce">Order time in force</param>
        /// <returns>Matching effects</returns>
        public ExchangeUpdate Match()
        {
            var counterOrder = orderbook.Head;
            var nextOrder = default(OrderWrapper);

            var updates = new ExchangeUpdate(asset, new DateTime(effectsContainer.Quantum.Timestamp, DateTimeKind.Utc));

            var tradeAssetAmount = 0ul;
            var tradeQuoteAmount = 0ul;
            //orders in the orderbook are already sorted by price and age, so we can iterate through them in natural order
            while (counterOrder != null)
            {
                //we need get next order here, otherwise Next will be null after the counter order removed
                nextOrder = counterOrder?.Next;
                //check that counter order price matches our order
                if (side == OrderSide.Sell && counterOrder.Order.Price < takerOrder.Order.Price
                    || side == OrderSide.Buy && counterOrder.Order.Price > takerOrder.Order.Price)
                    break;

                var availableOrderAmount = takerOrder.Order.Amount - tradeAssetAmount;
                var match = new OrderMatch(this, availableOrderAmount, counterOrder);
                var matchUpdates = match.ProcessOrderMatch();
                updates.Trades.Add(matchUpdates.trade);
                updates.OrderUpdates.Add(matchUpdates.counterOrder);

                tradeAssetAmount += matchUpdates.trade.Amount;
                tradeQuoteAmount += matchUpdates.trade.QuoteAmount;

                //stop if incoming order has been executed in full
                if (tradeAssetAmount == takerOrder.Order.Amount)
                    break;
                counterOrder = nextOrder;
            }

            RecordTrade(tradeAssetAmount, tradeQuoteAmount);

            if (timeInForce == TimeInForce.GoodTillExpire && PlaceReminderOrder())
                updates.OrderUpdates.Add(takerOrder.Order.ToOrderInfo());
            return updates;
        }

        private void RecordTrade(ulong tradeAssetAmount, ulong tradeQuoteAmount)
        {
            if (tradeAssetAmount == 0)
                return;

            //record taker trade effect. AddTrade will update order amount
            effectsContainer.AddTrade(
                takerOrder,
                tradeAssetAmount,
                tradeQuoteAmount,
                baseAsset,
                true
            );
        }

        /// <summary>
        /// Estimate quote amount that will be traded based on the asset amount and its price.
        /// </summary>
        /// <param name="assetAmountToTrade">Amount of an asset to trade</param>
        /// <param name="price">Asset price</param>
        /// <returns></returns>
        public static ulong EstimateQuoteAmount(ulong amount, double price, OrderSide side)
        {
            var amt = price * amount;
            switch (side)
            { //add 0.1% to compensate possible rounding errors
                case OrderSide.Buy:
                    return (ulong)Math.Ceiling(amt * 1.001);
                case OrderSide.Sell:
                    return (ulong)Math.Floor(amt * 0.999);
                default:
                    throw new InvalidOperationException();
            }
        }

        private bool PlaceReminderOrder()
        {
            if (takerOrder.Order.Amount <= 0)
                return false;
            var remainingQuoteAmount = EstimateQuoteAmount(takerOrder.Order.Amount, takerOrder.Order.Price, side);
            if (remainingQuoteAmount <= 0)
                return false;
            takerOrder.Order.QuoteAmount = remainingQuoteAmount;

            //select the market to add new order
            var reminderOrderbook = market.GetOrderbook(side);
            //record maker trade effect
            effectsContainer.AddOrderPlaced(reminderOrderbook, takerOrder, baseAsset);
            return true;
        }

        /// <summary>
        /// Single orders match descriptor.
        /// </summary>
        internal class OrderMatch
        {
            /// <summary>
            /// Create new instance of order match.
            /// </summary>
            /// <param name="matcher">Parent OrderMatcher instance</param>
            /// <param name="takerOrderAmount">Taker order current amount</param>
            /// <param name="makerOrder">Crossed order from the orderbook</param>
            public OrderMatch(OrderMatcher matcher, ulong takerOrderAmount, OrderWrapper makerOrder)
            {
                this.matcher = matcher;
                this.makerOrder = makerOrder;
                //amount of asset we are going to buy/sell
                AssetAmount = Math.Min(takerOrderAmount, makerOrder.Order.Amount);
                QuoteAmount = EstimateQuoteAmount(AssetAmount, makerOrder.Order.Price, matcher.side);
            }

            public ulong AssetAmount { get; }

            public ulong QuoteAmount;

            private OrderMatcher matcher;

            private OrderWrapper makerOrder;

            /// <summary>
            /// Process matching.
            /// </summary>
            public (Trade trade, OrderInfo counterOrder) ProcessOrderMatch()
            {
                //record trade effects
                var trade = RecordTrade();
                var counterOrder = makerOrder.Order.ToOrderInfo();
                if (makerOrder.Order.Amount == 0)
                { //schedule removal for the fully executed counter order
                    RecordOrderRemoved();
                    counterOrder.State = OrderState.Deleted;
                }
                else
                {
                    counterOrder.State = OrderState.Updated;
                    counterOrder.AmountDiff = trade.Amount;
                    counterOrder.QuoteAmountDiff = trade.QuoteAmount;
                }

                return (trade, counterOrder);
            }

            private Trade RecordTrade()
            {
                //record maker trade effect
                matcher.effectsContainer.AddTrade(
                         makerOrder,
                         AssetAmount,
                         QuoteAmount,
                         matcher.baseAsset,
                         false
                     );

                return new Trade
                {
                    Amount = AssetAmount,
                    QuoteAmount = QuoteAmount,
                    Asset = matcher.asset,
                    Price = makerOrder.Order.Price,
                    TradeDate = new DateTime(matcher.effectsContainer.Quantum.Timestamp, DateTimeKind.Utc)
                };
            }

            private void RecordOrderRemoved()
            {
                matcher.effectsContainer.AddOrderRemoved(matcher.orderbook, makerOrder, matcher.baseAsset);
            }
        }
    }
}
