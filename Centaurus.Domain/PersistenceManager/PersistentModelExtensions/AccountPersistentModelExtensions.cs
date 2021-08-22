using Centaurus.Domain.Models;
using Centaurus.Models;
using Centaurus.PersistentStorage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Centaurus.Domain
{
    public static class AccountPersistentModelExtensions
    {
        public static AccountWrapper ToDomainModel(this AccountPersistentModel accountModel, string asset, RequestRateLimits defaultRequestRateLimits)
        {
            if (accountModel == null)
                throw new ArgumentNullException(nameof(accountModel));

            var acc = new AccountWrapper(defaultRequestRateLimits)
            {
                Id = accountModel.AccountId,
                Nonce = accountModel.Nonce,
                Pubkey = accountModel.AccountPubkey,
                Balances = accountModel.Balances.Select(b => b.ToDomainModel()).ToDictionary(b => b.Asset, b => b),
                Orders = new Dictionary<ulong, Order>() 
            };

            foreach (var orderModel in accountModel.Orders.OrderBy(o => o.Apex))
            {
                var order = orderModel.ToDomainModel();
                acc.Orders.Add(order.OrderId, order);

                if (order.Side == OrderSide.Buy)
                    acc.GetBalance(asset).UpdateLiabilities(order.QuoteAmount, UpdateSign.Plus);
                else
                    acc.GetBalance(order.Asset).UpdateLiabilities(order.Amount, UpdateSign.Plus);
            }

            if (accountModel.RequestRateLimits != null)
            {
                acc.RequestRateLimits = new RequestRateLimits { HourLimit = accountModel.RequestRateLimits.HourLimit, MinuteLimit = accountModel.RequestRateLimits.MinuteLimit };
                acc.RequestCounter.SetLimits(acc.RequestRateLimits);
            }

            return acc;
        }

        public static AccountPersistentModel ToPersistentModel(this AccountWrapper accountModel)
        {
            if (accountModel == null)
                throw new ArgumentNullException(nameof(accountModel));

            var acc = new AccountPersistentModel
            {
                AccountId = accountModel.Id,
                Nonce = accountModel.Nonce,
                AccountPubkey = accountModel.Pubkey,
                Balances = accountModel.Balances.Values.Select(b => b.ToPersistentModel()).ToList(),
                Orders = accountModel.Orders.Values.Select(o => o.ToPersistentModel()).ToList()
            };

            if (accountModel.RequestRateLimits != null)
                acc.RequestRateLimits = new RequestRateLimitPersistentModel { HourLimit = accountModel.RequestRateLimits.HourLimit, MinuteLimit = accountModel.RequestRateLimits.MinuteLimit };

            return acc;
        }
    }
}
