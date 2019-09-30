using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Centaurus.Models;

namespace Centaurus.Domain
{
    public class OrderMatcher
    {
        public OrderMatcher(OrderRequest orderRequest, ulong apex)
        {
            resultEffects = new List<Effect>();
            takerOrder = new Order()
            {
                OrderId = OrderIdConverter.Encode(apex, orderRequest.Asset, orderRequest.Side),
                Pubkey = orderRequest.Account,
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
            var takerAccount = Global.AccountStorage.GetAccount(takerOrder.Pubkey);
            takerAssetBalance = takerAccount.GetBalance(asset, true);
            takerXlmBalance = takerAccount.GetBalance(0, true);
        }

        private Order takerOrder;

        private TimeInForce timeInForce;

        private int asset;

        private OrderSides side;

        private Balance takerAssetBalance;

        private Balance takerXlmBalance;

        private Orderbook orderbook;

        private Market market;

        private List<Effect> resultEffects;

        /// <summary>
        /// Match using specified time-in-force logic.
        /// </summary>
        /// <param name="timeInForce">Order time in force</param>
        /// <returns>Matching effects</returns>
        public List<Effect> Match()
        {
            //orders in the orderbook are already sorted by price and age, so we can iterate through them in natural order
            while (orderbook.Head != null)
            {
                var counterOrder = orderbook.Head;
                //check that counter order price matches our order
                if (side == OrderSides.Sell && counterOrder.Price < takerOrder.Price) break;
                if (side == OrderSides.Buy && counterOrder.Price > takerOrder.Price) break;

                var match = new OrderMatch(this, counterOrder);
                if (!match.ProcessOrderMatch()) break;

                //stop if incoming order has been executed in full
                if (takerOrder.Amount == 0)
                {
                    break;
                }
            }

            if (timeInForce == TimeInForce.GoodTillExpire)
            {
                PlaceReminderOrder();
            }

            return resultEffects;
        }

        private void PlaceReminderOrder()
        {
            if (takerOrder.Amount <= 0) return;
            var xmlAmount = EstimateTradedXlmAmount(takerOrder.Amount, takerOrder.Price);
            if (xmlAmount <= 0) return;
            //lock order reserve
            if (side == OrderSides.Buy)
            {
                //TODO: check this - potential rounding error with multiple trades
                takerXlmBalance.LockLiabilities(xmlAmount);
            }
            else
            {
                takerAssetBalance.LockLiabilities(takerOrder.Amount);
            }
            //select the market to add new order
            var reminderOrderbook = market.GetOrderbook(side);
            //add order to the orderbook
            reminderOrderbook.InsertOrder(takerOrder);
            //record maker trade effect
            resultEffects.Add(new OrderPlacedEffect
            {
                Pubkey = takerOrder.Pubkey,
                Asset = asset,
                Amount = takerOrder.Amount,
                Price = takerOrder.Price,
                OrderId = takerOrder.OrderId,
                OrderSide = side
            });
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
                assetAmount = Math.Min(matcher.takerOrder.Amount, makerOrder.Amount);
                xlmAmount = EstimateTradedXlmAmount(assetAmount, makerOrder.Price);
            }

            private OrderMatcher matcher;

            private Order makerOrder;

            private long assetAmount;

            private long xlmAmount;

            /// <summary>
            /// Process matching.
            /// </summary>
            public bool ProcessOrderMatch()
            {
                if (assetAmount <= 0 || xlmAmount <= 0)
                {
                    //TODO: remove offer that can't be fulfilled
                    //return false;
                }
                //fetch accounts
                var makerAccount = Global.AccountStorage.GetAccount(makerOrder.Pubkey);

                //fetch balances
                var makerAssetBalance = makerAccount.GetBalance(matcher.asset);
                var makerXlmBalance = makerAccount.GetBalance(0);

                //trade assets
                if (matcher.side == OrderSides.Buy)
                {
                    //unlock required asset amount on maker's side
                    makerAssetBalance.UnlockLiabilities(assetAmount);
                    //transfer asset from maker to taker
                    makerAssetBalance.UpdateBalance(-assetAmount);
                    matcher.takerAssetBalance.UpdateBalance(assetAmount);
                    //transfer XLM from taker to maker
                    matcher.takerXlmBalance.UpdateBalance(-xlmAmount);
                    makerXlmBalance.UpdateBalance(xlmAmount);
                }
                else
                {
                    //unlock required XLM amount on maker's side
                    makerXlmBalance.UnlockLiabilities(xlmAmount);
                    //transfer asset from taker to maker
                    matcher.takerAssetBalance.UpdateBalance(-assetAmount);
                    makerAssetBalance.UpdateBalance(assetAmount);
                    //transfer XLM from maker to taker
                    makerXlmBalance.UpdateBalance(-xlmAmount);
                    matcher.takerXlmBalance.UpdateBalance(xlmAmount);
                }

                //update amounts for both orders
                makerOrder.Amount -= assetAmount;
                matcher.takerOrder.Amount -= assetAmount;

                //record trade effects
                RecordTrade();

                if (makerOrder.Amount == 0)
                { //schedule removal for the fully executed counter order
                    matcher.orderbook.RemoveEmptyHeadOrder();
                    RecordOrderRemoved();
                }

                return true;
            }

            private void RecordTrade()
            {
                //record maker trade effect
                matcher.resultEffects.Add(new TradeEffect
                {
                    Pubkey = makerOrder.Pubkey,
                    Asset = matcher.asset,
                    AssetAmount = assetAmount,
                    XlmAmount = xlmAmount,
                    Price = makerOrder.Price,
                    OrderId = makerOrder.OrderId,
                    OrderSide = matcher.side.Inverse() //opposite the matched order
                });

                //record taker trade effect
                matcher.resultEffects.Add(new TradeEffect
                {
                    Pubkey = matcher.takerOrder.Pubkey,
                    Asset = matcher.asset,
                    AssetAmount = assetAmount,
                    XlmAmount = xlmAmount,
                    Price = makerOrder.Price,
                    OrderId = matcher.takerOrder.OrderId,
                    OrderSide = matcher.side
                });
            }

            private void RecordOrderRemoved()
            {
                //record remove maker's order effect
                matcher.resultEffects.Add(new OrderRemovedEffect
                {
                    Pubkey = makerOrder.Pubkey,
                    OrderId = makerOrder.OrderId
                });
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
