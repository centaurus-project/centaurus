using Centaurus.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.SDK.Models
{
    public class AccountDataModel
    {
        public long Nonce { get; set; }

        public Dictionary<int, BalanceModel> Balances { get; set; } = new Dictionary<int, BalanceModel>();

        public Dictionary<ulong, OrderModel> Orders { get; set; } = new Dictionary<ulong, OrderModel>();
    }

    public static class AccountDataModelExtensions
    {
        public static void UpdateNonce(this AccountDataModel account, long nonce)
        {
            lock (account)
                account.Nonce = nonce;
        }

        public static List<BalanceModel> GetBalances(this AccountDataModel account)
        {
            lock (account.Balances)
                return account.Balances.Values.ToList();
        }

        public static void AddBalance(this AccountDataModel account, int asset, ConstellationInfo constellationInfo)
        {
            lock (account.Balances)
                account.Balances.Add(asset, new BalanceModel
                {
                    AssetId = asset,
                    Asset = (constellationInfo.Assets.FirstOrDefault(a => a.Id == asset)?.DisplayName ?? asset.ToString())
                });
        }

        public static void UpdateBalance(this AccountDataModel account, int asset, long amount)
        {
            lock (account.Balances)
                account.Balances[asset].Amount += amount;
        }

        public static void UpdateLiabilities(this AccountDataModel account, int asset, long liabilities)
        {
            lock (account.Balances)
                account.Balances[asset].Liabilities += liabilities;
        }

        public static List<OrderModel> GetOrders(this AccountDataModel account)
        {
            lock (account.Orders)
                return account.Orders.Values.ToList();
        }

        public static void AddOrder(this AccountDataModel account, ulong orderId, long amount, double price, ConstellationInfo constellation)
        {
            var orderModel = new OrderModel
            {
                Amount = amount,
                Price = price,
                OrderId = orderId
            };
            orderModel.Asset = constellation.Assets.FirstOrDefault(a => a.Id == orderModel.AssetId)?.DisplayName ?? orderModel.AssetId.ToString();
            lock (account.Orders)
                account.Orders.Add(orderId, orderModel);
        }

        public static void RemoveOrder(this AccountDataModel account, ulong orderId)
        {
            lock (account.Orders)
                account.Orders.Remove(orderId);
        }

        public static void UpdateOrder(this AccountDataModel account, ulong orderId, long amount)
        {
            lock (account.Orders)
                if (account.Orders.TryGetValue(orderId, out var order)) //trade could occur without adding the order to orderbook
                    order.Amount -= amount;
        }
    }
}
