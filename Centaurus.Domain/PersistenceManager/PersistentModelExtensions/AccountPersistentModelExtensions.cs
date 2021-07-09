using Centaurus.Models;
using Centaurus.PersistentStorage;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Centaurus.Domain
{
    public static class AccountPersistentModelExtensions
    {
        public static Account ToDomainModel(this AccountPersistentModel accountModel, string asset)
        {
            if (accountModel == null)
                throw new ArgumentNullException(nameof(accountModel));

            var acc = new Account
            {
                Id = accountModel.AccountId,
                Nonce = accountModel.Nonce,
                Pubkey = accountModel.AccountPubkey,
                Balances = accountModel.Balances.Select(b => b.ToDomainModel()).ToList(),
                Orders = new List<Order>() 
            };

            foreach (var orderModel in accountModel.Orders)
            {
                var order = orderModel.ToDomainModel();
                acc.Orders.Add(order);

                if (order.Side == OrderSide.Buy)
                    acc.GetBalance(asset).UpdateLiabilities(order.QuoteAmount, UpdateSign.Plus);
                else
                    acc.GetBalance(order.Asset).UpdateLiabilities(order.Amount, UpdateSign.Plus);
            }

            if (accountModel.RequestRateLimits != null)
                acc.RequestRateLimits = new RequestRateLimits { HourLimit = accountModel.RequestRateLimits.HourLimit, MinuteLimit = accountModel.RequestRateLimits.MinuteLimit };

            return acc;
        }

        public static AccountPersistentModel ToPersistentModel(this Account accountModel)
        {
            if (accountModel == null)
                throw new ArgumentNullException(nameof(accountModel));

            var acc = new AccountPersistentModel
            {
                AccountId = accountModel.Id,
                Nonce = accountModel.Nonce,
                AccountPubkey = accountModel.Pubkey,
                Balances = accountModel.Balances.Select(b => b.ToPersistentModel()).ToList(),
                Orders = accountModel.Orders.Select(o => o.ToPersistentModel()).ToList()
            };

            if (accountModel.RequestRateLimits != null)
                acc.RequestRateLimits = new RequestRateLimitPersistentModel { HourLimit = accountModel.RequestRateLimits.HourLimit, MinuteLimit = accountModel.RequestRateLimits.MinuteLimit };

            return acc;
        }
    }
}
