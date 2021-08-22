using System;
using System.Collections.Generic;
using System.Linq;
using Centaurus.Models;

namespace Centaurus.NetSDK
{
    public class AccountState
    {
        public ulong Nonce { get; internal set; }
        public ulong AccountId { get; internal set; }

        internal ConstellationInfo ConstellationInfo;

        internal Dictionary<string, BalanceModel> balances = new Dictionary<string, BalanceModel>();

        internal Dictionary<ulong, OrderModel> orders = new Dictionary<ulong, OrderModel>();

        public List<BalanceModel> GetBalances()
        {
            return balances.Values.ToList();
        }

        public List<OrderModel> GetOrders()
        {
            return orders.Values.ToList();
        }

        internal void ApplyAccountStateChanges(Effect effect)
        {
            switch (effect)
            {
                case NonceUpdateEffect nonceUpdateEffect:
                    Nonce = nonceUpdateEffect.Nonce;
                    break;
                case BalanceCreateEffect balanceCreateEffect:
                    var assetId = balanceCreateEffect.Asset;
                    balances.Add(assetId, new BalanceModel
                    {
                        Asset = assetId
                    });
                    break;
                case BalanceUpdateEffect balanceUpdateEffect:
                    UpdateBalance(balanceUpdateEffect.Asset, balanceUpdateEffect.Amount, balanceUpdateEffect.Sign);
                    break;
                case OrderPlacedEffect orderPlacedEffect:
                    {
                        var orderModel = new OrderModel
                        {
                            OrderId = orderPlacedEffect.OrderId,
                            Asset = orderPlacedEffect.Asset,
                            Amount = orderPlacedEffect.Amount,
                            Price = orderPlacedEffect.Price,
                            Side = orderPlacedEffect.Side
                        };
                        orders.Add(orderPlacedEffect.OrderId, orderModel);
                        if (orderModel.Side == OrderSide.Buy)
                            UpdateLiabilities(ConstellationInfo.QuoteAsset.Code, orderPlacedEffect.QuoteAmount, UpdateSign.Plus);
                        else
                            UpdateLiabilities(orderModel.Asset, orderPlacedEffect.Amount, UpdateSign.Plus);
                    }
                    break;
                case OrderRemovedEffect orderRemoveEffect:
                    {
                        orders.Remove(orderRemoveEffect.OrderId);
                        if (orderRemoveEffect.Side == OrderSide.Buy)
                            UpdateLiabilities(ConstellationInfo.QuoteAsset.Code, orderRemoveEffect.QuoteAmount, UpdateSign.Minus);
                        else
                            UpdateLiabilities(orderRemoveEffect.Asset, orderRemoveEffect.Amount, UpdateSign.Minus);
                    }
                    break;
                case TradeEffect tradeEffect:
                    {
                        if (orders.TryGetValue(tradeEffect.OrderId, out var order)) //trade could occur without adding the order to orderbook
                            order.Amount -= tradeEffect.AssetAmount;
                        var quoteAsset = ConstellationInfo.QuoteAsset.Code;
                        if (tradeEffect.Side == OrderSide.Buy)
                        {
                            if (!tradeEffect.IsNewOrder)
                                UpdateLiabilities(quoteAsset, tradeEffect.QuoteAmount, UpdateSign.Minus);
                            UpdateBalance(quoteAsset, tradeEffect.QuoteAmount, UpdateSign.Minus);
                            UpdateBalance(tradeEffect.Asset, tradeEffect.AssetAmount, UpdateSign.Plus);
                        }
                        else
                        {
                            if (!tradeEffect.IsNewOrder)
                                UpdateLiabilities(tradeEffect.Asset, tradeEffect.AssetAmount, UpdateSign.Minus);
                            UpdateBalance(tradeEffect.Asset, tradeEffect.AssetAmount, UpdateSign.Minus);
                            UpdateBalance(quoteAsset, tradeEffect.QuoteAmount, UpdateSign.Plus);
                        }
                    }
                    break;
                default:
                    break;
            }
        }

        private void UpdateBalance(string asset, ulong amount, UpdateSign sign)
        {
            if (sign == UpdateSign.Plus)
                balances[asset].Amount += amount;
            else
                balances[asset].Amount -= amount;
        }

        private void UpdateLiabilities(string asset, ulong liabilities, UpdateSign sign)
        {
            if (sign == UpdateSign.Plus)
                balances[asset].Liabilities += liabilities;
            else
                balances[asset].Liabilities -= liabilities;
        }
    }
}
