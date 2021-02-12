using System;
using Centaurus.Models;

namespace Centaurus.Domain
{
    public class OrderMatcher
    {
        public OrderMatcher(OrderRequest orderRequest, EffectProcessorsContainer effectsContainer)
        {
            resultEffects = effectsContainer;

            takerOrder = new Order
            {
                OrderId = OrderIdConverter.FromRequest(orderRequest, effectsContainer.Apex),
                Account = orderRequest.AccountWrapper.Account,
                Amount = orderRequest.Amount,
                QuoteAmount = orderRequest.QuoteAmount,
                Price = orderRequest.Price
            };
            timeInForce = orderRequest.TimeInForce;

            //parse data from the ID of the newly arrived order 
            var orderData = OrderIdConverter.Decode(takerOrder.OrderId);
            asset = orderData.Asset;
            side = orderData.Side;
            //get asset orderbook
            market = Global.Exchange.GetMarket(asset);
            orderbook = market.GetOrderbook(side.Inverse());
            //fetch balances
            if (!takerOrder.Account.HasBalance(asset))
                resultEffects.AddBalanceCreate(orderRequest.AccountWrapper.Account, asset);
        }

        private readonly Order takerOrder;

        private readonly TimeInForce timeInForce;

        private readonly int asset;

        private readonly OrderSide side;

        private readonly Orderbook orderbook;

        private readonly Market market;

        private readonly EffectProcessorsContainer resultEffects;

        /// <summary>
        /// Match using specified time-in-force logic.
        /// </summary>
        /// <param name="timeInForce">Order time in force</param>
        /// <returns>Matching effects</returns>
        public ExchangeUpdate Match()
        {
            var counterOrder = orderbook.Head;
            var nextOrder = default(Order);

            var updates = new ExchangeUpdate(asset, new DateTime(resultEffects.Quantum.Timestamp, DateTimeKind.Utc));

            //orders in the orderbook are already sorted by price and age, so we can iterate through them in natural order
            while (counterOrder != null)
            {
                //we need get next order here, otherwise Next will be null after the counter order removed
                nextOrder = counterOrder?.Next;
                //check that counter order price matches our order
                if (side == OrderSide.Sell && counterOrder.Price < takerOrder.Price
                    || side == OrderSide.Buy && counterOrder.Price > takerOrder.Price)
                    break;

                var match = new OrderMatch(this, counterOrder);
                var matchUpdates = match.ProcessOrderMatch();
                updates.Trades.Add(matchUpdates.trade);
                updates.OrderUpdates.Add(matchUpdates.counterOrder);

                //stop if incoming order has been executed in full
                if (takerOrder.Amount == 0)
                    break;
                counterOrder = nextOrder;
            }

            if (timeInForce == TimeInForce.GoodTillExpire)
            {
                if (PlaceReminderOrder())
                    updates.OrderUpdates.Add(takerOrder.ToOrderInfo());
            }
            return updates;
        }

        /// <summary>
        /// Estimate quote amount that will be traded based on the asset amount and its price.
        /// </summary>
        /// <param name="assetAmountToTrade">Amount of an asset to trade</param>
        /// <param name="price">Asset price</param>
        /// <returns></returns>
        public static long EstimateQuoteAmount(long amount, double price, OrderSide side)
        {
            var amt = price * amount;
            switch (side)
            { //add 0.1% to compensate possible rounding errors
                case OrderSide.Buy:
                    return (long)Math.Ceiling(amt * 1.001);
                case OrderSide.Sell:
                    return (long)Math.Floor(amt * 0.999);
                default:
                    throw new InvalidOperationException();
            }
        }

        private bool PlaceReminderOrder()
        {
            if (takerOrder.Amount <= 0) return false;
            var remainingQuoteAmount = EstimateQuoteAmount(takerOrder.Amount, takerOrder.Price, side);
            if (remainingQuoteAmount <= 0) return false;
            takerOrder.QuoteAmount = remainingQuoteAmount;
            //lock order reserve
            if (side == OrderSide.Buy)
            {
                //TODO: check this - potential rounding error with multiple trades
                resultEffects.AddUpdateLiabilities(takerOrder.Account, 0, remainingQuoteAmount);
            }
            else
            {
                resultEffects.AddUpdateLiabilities(takerOrder.Account, asset, takerOrder.Amount);
            }
            //select the market to add new order
            var reminderOrderbook = market.GetOrderbook(side);
            //record maker trade effect
            resultEffects.AddOrderPlaced(reminderOrderbook, takerOrder);
            resultEffects.OrderWasPlaced = true;
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
            /// <param name="makerOrder">Crossed order from the orderbook</param>
            public OrderMatch(OrderMatcher matcher, Order makerOrder)
            {
                this.matcher = matcher;
                this.makerOrder = makerOrder;
                //amount of asset we are going to buy/sell
                AssetAmount = Math.Min(matcher.takerOrder.Amount, makerOrder.Amount);
                QuoteAmount = EstimateQuoteAmount(AssetAmount, makerOrder.Price, matcher.side);
            }

            public long AssetAmount { get; }

            public long QuoteAmount;

            private OrderMatcher matcher;

            private Order makerOrder;

            /// <summary>
            /// Process matching.
            /// </summary>
            public (Trade trade, OrderInfo counterOrder) ProcessOrderMatch()
            {
                //trade assets
                if (matcher.side == OrderSide.Buy)
                {
                    //unlock required asset amount on maker's side
                    matcher.resultEffects.AddUpdateLiabilities(makerOrder.Account, matcher.asset, -AssetAmount);

                    //transfer asset from maker to taker
                    matcher.resultEffects.AddBalanceUpdate(makerOrder.Account, matcher.asset, -AssetAmount);
                    matcher.resultEffects.AddBalanceUpdate(matcher.takerOrder.Account, matcher.asset, AssetAmount);

                    //transfer XLM from taker to maker
                    matcher.resultEffects.AddBalanceUpdate(matcher.takerOrder.Account, 0, -QuoteAmount);
                    matcher.resultEffects.AddBalanceUpdate(makerOrder.Account, 0, QuoteAmount);
                }
                else
                {
                    //unlock required XLM amount on maker's side
                    matcher.resultEffects.AddUpdateLiabilities(makerOrder.Account, 0, -QuoteAmount);

                    //transfer asset from taker to maker
                    matcher.resultEffects.AddBalanceUpdate(matcher.takerOrder.Account, matcher.asset, -AssetAmount);
                    matcher.resultEffects.AddBalanceUpdate(makerOrder.Account, matcher.asset, AssetAmount);

                    //transfer XLM from maker to taker
                    matcher.resultEffects.AddBalanceUpdate(makerOrder.Account, 0, -QuoteAmount);
                    matcher.resultEffects.AddBalanceUpdate(matcher.takerOrder.Account, 0, QuoteAmount);
                }

                //record trade effects
                var trade = RecordTrade();
                var counterOrder = makerOrder.ToOrderInfo();
                if (makerOrder.Amount == 0)
                { //schedule removal for the fully executed counter order
                    RecordOrderRemoved();
                    counterOrder.State = OrderState.Deleted;
                }
                else
                {
                    counterOrder.State = OrderState.Updated;
                    //TODO: add diff field for this purpose
                    //it's not amount but difference with existing amount 
                    counterOrder.Amount = trade.Amount;
                }

                return (trade, counterOrder);
            }

            private Trade RecordTrade()
            {
                //record maker trade effect
                matcher.resultEffects.AddTrade(
                         makerOrder,
                         AssetAmount,
                         QuoteAmount
                     );

                //record taker trade effect
                matcher.resultEffects.AddTrade(
                         matcher.takerOrder,
                         AssetAmount,
                         QuoteAmount
                     );

                return new Trade
                {
                    Amount = AssetAmount,
                    Asset = matcher.asset,
                    BaseAmount = QuoteAmount,
                    Price = makerOrder.Price,
                    TradeDate = new DateTime(matcher.resultEffects.Quantum.Timestamp, DateTimeKind.Utc)
                };
            }

            private void RecordOrderRemoved()
            {
                var info = makerOrder.ToOrderInfo();
                if (makerOrder.QuoteAmount > 0 && info.Side == OrderSide.Buy)
                {
                    matcher.resultEffects.AddUpdateLiabilities(makerOrder.Account, 0, -makerOrder.QuoteAmount);
                }
                matcher.resultEffects.AddOrderRemoved(matcher.orderbook, makerOrder);
            }
        }
    }
}