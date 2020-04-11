using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Centaurus.Models;

namespace Centaurus.Domain
{
    public class OrderMatcher
    {
        public OrderMatcher(OrderRequest orderRequest, EffectProcessorsContainer effectsContainer)
        {
            resultEffects = effectsContainer;
            takerOrder = new Order()
            {
                OrderId = OrderIdConverter.Encode(unchecked((ulong)effectsContainer.Apex), orderRequest.Asset, orderRequest.Side),
                Account = orderRequest.AccountWrapper.Account,
                Amount = orderRequest.Amount,
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

        private readonly OrderSides side;

        private readonly Orderbook orderbook;

        private readonly Market market;

        private readonly EffectProcessorsContainer resultEffects;

        /// <summary>
        /// Match using specified time-in-force logic.
        /// </summary>
        /// <param name="timeInForce">Order time in force</param>
        /// <returns>Matching effects</returns>
        public void Match()
        {
            var counterOrder = orderbook.Head;

            //orders in the orderbook are already sorted by price and age, so we can iterate through them in natural order
            while (counterOrder != null)
            {
                //check that counter order price matches our order
                if (side == OrderSides.Sell && counterOrder.Price < takerOrder.Price) break;
                if (side == OrderSides.Buy && counterOrder.Price > takerOrder.Price) break;

                var match = new OrderMatch(this, counterOrder);
                var orderMatchResult = match.ProcessOrderMatch();
                if (!orderMatchResult) break;

                //stop if incoming order has been executed in full
                if (takerOrder.Amount == 0)
                {
                    break;
                }
                counterOrder = counterOrder.Next;
            }

            if (timeInForce == TimeInForce.GoodTillExpire)
            {
                PlaceReminderOrder(takerOrder.Amount);
            }
        }

        private void PlaceReminderOrder(long emulatedAmount)
        {
            if (emulatedAmount <= 0) return;
            var xmlAmount = EstimateTradedXlmAmount(emulatedAmount, takerOrder.Price);
            if (xmlAmount <= 0) return;
            //lock order reserve
            if (side == OrderSides.Buy)
            {
                //TODO: check this - potential rounding error with multiple trades
                resultEffects.AddLockLiabilities(takerOrder.Account, 0, xmlAmount);
            }
            else
            {
                resultEffects.AddLockLiabilities(takerOrder.Account, asset, emulatedAmount);
            }
            //select the market to add new order
            var reminderOrderbook = market.GetOrderbook(side);
            //record maker trade effect
            resultEffects.AddOrderPlaced(reminderOrderbook, takerOrder, emulatedAmount, asset, side);
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
                xlmAmount = EstimateTradedXlmAmount(AssetAmount, makerOrder.Price);
            }

            public long AssetAmount { get; }

            private OrderMatcher matcher;

            private Order makerOrder;

            private long xlmAmount;

            /// <summary>
            /// Process matching.
            /// </summary>
            public bool ProcessOrderMatch()
            {
                //trade assets
                if (matcher.side == OrderSides.Buy)
                {
                    //unlock required asset amount on maker's side
                    matcher.resultEffects.AddUnlockLiabilities(makerOrder.Account, matcher.asset, AssetAmount);

                    //transfer asset from maker to taker
                    matcher.resultEffects.AddBalanceUpdate(makerOrder.Account, matcher.asset, -AssetAmount);
                    matcher.resultEffects.AddBalanceUpdate(matcher.takerOrder.Account, matcher.asset, AssetAmount);

                    //transfer XLM from taker to maker
                    matcher.resultEffects.AddBalanceUpdate(matcher.takerOrder.Account, 0, -xlmAmount);
                    matcher.resultEffects.AddBalanceUpdate(makerOrder.Account, 0, xlmAmount);
                }
                else
                {
                    //unlock required XLM amount on maker's side
                    matcher.resultEffects.AddUnlockLiabilities(makerOrder.Account, 0, xlmAmount);

                    //transfer asset from taker to maker
                    matcher.resultEffects.AddBalanceUpdate(matcher.takerOrder.Account, matcher.asset, -AssetAmount);
                    matcher.resultEffects.AddBalanceUpdate(makerOrder.Account, matcher.asset, AssetAmount);

                    //transfer XLM from maker to taker
                    matcher.resultEffects.AddBalanceUpdate(makerOrder.Account, 0, -xlmAmount);
                    matcher.resultEffects.AddBalanceUpdate(matcher.takerOrder.Account, 0, xlmAmount);
                }

                //record trade effects
                RecordTrade();

                if (makerOrder.Amount == 0)
                { //schedule removal for the fully executed counter order
                    //matcher.orderbook.RemoveEmptyHeadOrder();
                    RecordOrderRemoved();
                }

                return true;
            }

            private void RecordTrade()
            {
                //record maker trade effect
                matcher.resultEffects.AddTrade(
                         makerOrder,
                         matcher.asset,
                         AssetAmount,
                         xlmAmount,
                         makerOrder.Price,
                         matcher.side.Inverse() //opposite the matched order
                     );

                //record taker trade effect

                matcher.resultEffects.AddTrade(
                         matcher.takerOrder,
                         matcher.asset,
                         AssetAmount,
                         xlmAmount,
                         makerOrder.Price,
                         matcher.side
                     );
            }

            private void RecordOrderRemoved()
            {
                //record remove maker's order effect
                matcher.resultEffects.AddOrderRemoved(matcher.orderbook, makerOrder);
            }
        }

        /// <summary>
        /// Estimate XLM amount that will be traded based on the asset amount and its price.
        /// </summary>
        /// <param name="assetAmountToTrade">Amount of an asset to trade</param>
        /// <param name="price">Asset price</param>
        /// <returns></returns>
        public static long EstimateTradedXlmAmount(long assetAmountToTrade, double price)
        {
            return (long)Math.Ceiling(price * assetAmountToTrade);
        }
    }
}
