using System.Collections.Generic;
using System.Linq;
using Centaurus.Models;

namespace Centaurus.NetSDK
{
    public class AccountState
    {
        public long Nonce { get; internal set; }

        internal ConstellationInfo constellationInfo;

        internal Dictionary<int, BalanceModel> balances = new Dictionary<int, BalanceModel>();

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
                        AssetId = assetId
                    });
                    break;
                case BalanceUpdateEffect balanceUpdateEffect:
                    UpdateBalance(balanceUpdateEffect.Asset, balanceUpdateEffect.Amount);
                    break;
                case OrderPlacedEffect orderPlacedEffect:
                    {
                        var orderModel = new OrderModel
                        {
                            Amount = orderPlacedEffect.Amount,
                            Price = orderPlacedEffect.Price,
                            OrderId = orderPlacedEffect.OrderId
                        };
                        orderModel.Asset = constellationInfo.Assets.FirstOrDefault(a => a.Id == orderModel.AssetId)?.DisplayName ?? orderModel.AssetId.ToString();
                        orders.Add(orderPlacedEffect.OrderId, orderModel);
                        var decodedId = OrderIdConverter.Decode(orderPlacedEffect.OrderId);
                        if (decodedId.Side == OrderSide.Buy)
                            UpdateLiabilities(0, orderPlacedEffect.QuoteAmount);
                        else
                            UpdateLiabilities(decodedId.Asset, orderPlacedEffect.Amount);
                    }
                    break;
                case OrderRemovedEffect orderRemoveEffect:
                    {
                        orders.Remove(orderRemoveEffect.OrderId);
                        var decodedId = OrderIdConverter.Decode(orderRemoveEffect.OrderId);
                        if (decodedId.Side == OrderSide.Buy)
                            UpdateLiabilities(0, -orderRemoveEffect.QuoteAmount);
                        else
                            UpdateLiabilities(decodedId.Asset, -orderRemoveEffect.Amount);
                    }
                    break;
                case TradeEffect tradeEffect:
                    {
                        if (orders.TryGetValue(tradeEffect.OrderId, out var order)) //trade could occur without adding the order to orderbook
                            order.Amount -= tradeEffect.AssetAmount;

                        var decodedId = OrderIdConverter.Decode(tradeEffect.OrderId);
                        if (decodedId.Side == OrderSide.Buy)
                        {
                            if (!tradeEffect.IsNewOrder)
                                UpdateLiabilities(0, -tradeEffect.QuoteAmount);
                            UpdateBalance(0, -tradeEffect.QuoteAmount);
                            UpdateBalance(decodedId.Asset, tradeEffect.AssetAmount);
                        }
                        else
                        {
                            if (!tradeEffect.IsNewOrder)
                                UpdateLiabilities(decodedId.Asset, -tradeEffect.AssetAmount);
                            UpdateBalance(decodedId.Asset, -tradeEffect.AssetAmount);
                            UpdateBalance(0, tradeEffect.QuoteAmount);
                        }
                    }
                    break;
                case WithdrawalCreateEffect withdrawalCreateEffect:
                    foreach (var withdrawalItem in withdrawalCreateEffect.Items)
                    {
                        UpdateLiabilities(withdrawalItem.Asset, withdrawalItem.Amount);
                    }
                    break;
                case WithdrawalRemoveEffect withdrawalRemoveEffect:
                    foreach (var withdrawalItem in withdrawalRemoveEffect.Items)
                    {
                        if (withdrawalRemoveEffect.IsSuccessful)
                            UpdateBalance(withdrawalItem.Asset, -withdrawalItem.Amount);
                        UpdateLiabilities(withdrawalItem.Asset, -withdrawalItem.Amount);
                    }
                    break;
                default:
                    break;
            }
        }

        private void UpdateBalance(int asset, long amount)
        {
            balances[asset].Amount += amount;
        }

        private void UpdateLiabilities(int asset, long liabilities)
        {
            balances[asset].Liabilities += liabilities;
        }
    }
}
