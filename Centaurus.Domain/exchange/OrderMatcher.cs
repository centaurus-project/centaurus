﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Centaurus.Analytics;
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
                OrderId = OrderIdConverter.FromRequest(orderRequest, effectsContainer.Apex),
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
        public List<Trade> Match()
        {
            var counterOrder = orderbook.Head;

            var trades = new List<Trade>();

            //orders in the orderbook are already sorted by price and age, so we can iterate through them in natural order
            while (counterOrder != null)
            {
                //check that counter order price matches our order
                if (side == OrderSides.Sell && counterOrder.Price < takerOrder.Price) break;
                if (side == OrderSides.Buy && counterOrder.Price > takerOrder.Price) break;

                var match = new OrderMatch(this, counterOrder);
                trades.Add(match.ProcessOrderMatch());

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
            return trades;
        }

        private void PlaceReminderOrder(long amount)
        {
            if (amount <= 0) return;
            var xmlAmount = EstimateTradedXlmAmount(amount, takerOrder.Price);
            if (xmlAmount <= 0) return;
            //lock order reserve
            if (side == OrderSides.Buy)
            {
                //TODO: check this - potential rounding error with multiple trades
                resultEffects.AddUpdateLiabilities(takerOrder.Account, 0, xmlAmount);
            }
            else
            {
                resultEffects.AddUpdateLiabilities(takerOrder.Account, asset, amount);
            }
            //select the market to add new order
            var reminderOrderbook = market.GetOrderbook(side);
            //record maker trade effect
            resultEffects.AddOrderPlaced(reminderOrderbook, takerOrder, amount, asset, side);
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
            public Trade ProcessOrderMatch()
            {
                //trade assets
                if (matcher.side == OrderSides.Buy)
                {
                    //unlock required asset amount on maker's side
                    matcher.resultEffects.AddUpdateLiabilities(makerOrder.Account, matcher.asset, -AssetAmount);

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
                    matcher.resultEffects.AddUpdateLiabilities(makerOrder.Account, 0, -xlmAmount);

                    //transfer asset from taker to maker
                    matcher.resultEffects.AddBalanceUpdate(matcher.takerOrder.Account, matcher.asset, -AssetAmount);
                    matcher.resultEffects.AddBalanceUpdate(makerOrder.Account, matcher.asset, AssetAmount);

                    //transfer XLM from maker to taker
                    matcher.resultEffects.AddBalanceUpdate(makerOrder.Account, 0, -xlmAmount);
                    matcher.resultEffects.AddBalanceUpdate(matcher.takerOrder.Account, 0, xlmAmount);
                }

                //record trade effects
                var trade = RecordTrade();

                if (makerOrder.Amount == 0)
                { //schedule removal for the fully executed counter order
                    //matcher.orderbook.RemoveEmptyHeadOrder();
                    RecordOrderRemoved();
                }

                return trade;
            }

            private Trade RecordTrade()
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

                return new Trade
                {
                    Amount = AssetAmount,
                    Asset = matcher.asset,
                    BaseAmount = xlmAmount,
                    Price = makerOrder.Price,
                    Timestamp = DateTime.UtcNow.Ticks
                };
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