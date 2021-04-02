using Centaurus.DAL;
using Centaurus.DAL.Models;
using Centaurus.DAL.Mongo;
using Centaurus.Models;
using Centaurus.Xdr;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public static class UpdatesAggregator
    {
        public static void Aggregate(this EffectProcessorsContainer processorsContainer, MessageEnvelope quantumEnvelope, Effect quatumEffect, int effectIndex)
        {
            if (processorsContainer == null)
                throw new ArgumentNullException(nameof(processorsContainer));

            var pendingDiffObject = processorsContainer.PendingDiffObject;

            if (pendingDiffObject == null)
                throw new ArgumentNullException(nameof(pendingDiffObject));

            if (quantumEnvelope == null)
                throw new ArgumentNullException(nameof(quantumEnvelope));

            if (quatumEffect == null)
                throw new ArgumentNullException(nameof(quatumEffect));

            switch (quatumEffect)
            {
                case ConstellationInitEffect constellationInit:
                    pendingDiffObject.ConstellationSettings = GetConstellationSettings(constellationInit);
                    pendingDiffObject.StellarInfoData = new DiffObject.ConstellationState { TxCursor = constellationInit.TxCursor, IsInserted = true };
                    break;
                case ConstellationUpdateEffect constellationUpdate:
                    throw new NotImplementedException();
                    pendingDiffObject.ConstellationSettings = GetConstellationSettings(constellationUpdate);
                    break;
                case AccountCreateEffect accountCreateEffect:
                    {
                        var pubKey = accountCreateEffect.Pubkey;
                        var accId = accountCreateEffect.Account;
                        pendingDiffObject.Accounts.Add(accId, new DiffObject.Account { PubKey = pubKey, Id = accId, IsInserted = true });
                    }
                    break;
                case NonceUpdateEffect nonceUpdateEffect:
                    {
                        var accId = nonceUpdateEffect.AccountWrapper.Account.Id;
                        GetAccount(pendingDiffObject.Accounts, accId).Nonce = nonceUpdateEffect.Nonce;
                    }
                    break;
                case BalanceCreateEffect balanceCreateEffect:
                    {
                        var accId = balanceCreateEffect.AccountWrapper.Account.Id;
                        var balanceId = BalanceModelIdConverter.EncodeId(accId, balanceCreateEffect.Asset);
                        GetBalance(pendingDiffObject.Balances, balanceId).IsInserted = true;
                    }
                    break;
                case BalanceUpdateEffect balanceUpdateEffect:
                    {
                        var accId = balanceUpdateEffect.AccountWrapper.Account.Id;
                        var balanceId = BalanceModelIdConverter.EncodeId(accId, balanceUpdateEffect.Asset);
                        GetBalance(pendingDiffObject.Balances, balanceId).AmountDiff += balanceUpdateEffect.Amount;
                    }
                    break;
                case RequestRateLimitUpdateEffect requestRateLimitUpdateEffect:
                    {
                        var accId = requestRateLimitUpdateEffect.AccountWrapper.Account.Id;
                        GetAccount(pendingDiffObject.Accounts, accId).RequestRateLimits = new RequestRateLimitsModel
                        {
                            HourLimit = requestRateLimitUpdateEffect.RequestRateLimits.HourLimit,
                            MinuteLimit = requestRateLimitUpdateEffect.RequestRateLimits.MinuteLimit
                        };
                    }
                    break;
                case OrderPlacedEffect orderPlacedEffect:
                    {
                        var accId = orderPlacedEffect.AccountWrapper.Account.Id;
                        var orderId = orderPlacedEffect.OrderId;
                        pendingDiffObject.Orders[orderId] = new DiffObject.Order
                        {
                            AmountDiff = orderPlacedEffect.Amount,
                            QuoteAmountDiff = orderPlacedEffect.QuoteAmount,
                            IsInserted = true,
                            OrderId = orderId,
                            Price = orderPlacedEffect.Price,
                            Account = accId
                        };
                        UpdateLiabilities(pendingDiffObject.Accounts, pendingDiffObject.Balances, accId, orderId, orderPlacedEffect.QuoteAmount, orderPlacedEffect.Amount);
                    }
                    break;
                case OrderRemovedEffect orderRemovedEffect:
                    {
                        var accId = orderRemovedEffect.AccountWrapper.Account.Id;
                        var orderId = orderRemovedEffect.OrderId;
                        var quoteAmount = -orderRemovedEffect.QuoteAmount;
                        var assetAmount = -orderRemovedEffect.Amount;
                        GetOrder(pendingDiffObject.Orders, orderId).IsDeleted = true;
                        UpdateLiabilities(pendingDiffObject.Accounts, pendingDiffObject.Balances, accId, orderId, quoteAmount, assetAmount);
                    }
                    break;
                case TradeEffect tradeEffect:
                    {
                        var accId = tradeEffect.AccountWrapper.Account.Id;
                        var orderId = tradeEffect.OrderId;
                        var assetAmount = tradeEffect.AssetAmount;
                        var quoteAmount = tradeEffect.QuoteAmount;

                        var decodedId = OrderIdConverter.Decode(orderId);
                        var quoteBalance = GetBalance(pendingDiffObject.Balances, BalanceModelIdConverter.EncodeId(accId, 0));
                        var assetBalance = GetBalance(pendingDiffObject.Balances, BalanceModelIdConverter.EncodeId(accId, decodedId.Asset));
                        if (decodedId.Side == OrderSide.Buy)
                        {
                            if (!tradeEffect.IsNewOrder)
                                quoteBalance.LiabilitiesDiff += -quoteAmount;
                            quoteBalance.AmountDiff += -quoteAmount;
                            assetBalance.AmountDiff += assetAmount;

                        }
                        else
                        {
                            if (!tradeEffect.IsNewOrder)
                                assetBalance.LiabilitiesDiff += -assetAmount;
                            assetBalance.AmountDiff += -assetAmount;
                            quoteBalance.AmountDiff += quoteAmount;
                        }

                        if (tradeEffect.IsNewOrder)
                            return;
                        var order = GetOrder(pendingDiffObject.Orders, orderId);
                        order.AmountDiff += -assetAmount;
                        order.QuoteAmountDiff += -quoteAmount;
                    }
                    break;
                case TxCursorUpdateEffect cursorUpdateEffect:
                    {
                        if (pendingDiffObject.StellarInfoData == null)
                            pendingDiffObject.StellarInfoData = new DiffObject.ConstellationState { TxCursor = cursorUpdateEffect.Cursor };
                        else
                            pendingDiffObject.StellarInfoData.TxCursor = cursorUpdateEffect.Cursor;
                    }
                    break;
                case WithdrawalCreateEffect withdrawalCreateEffect:
                    {
                        var accId = withdrawalCreateEffect.AccountWrapper.Account.Id;
                        GetAccount(pendingDiffObject.Accounts, accId).Withdrawal = withdrawalCreateEffect.Apex;
                        foreach (var withdrawalItem in withdrawalCreateEffect.Items)
                            GetBalance(pendingDiffObject.Balances, BalanceModelIdConverter.EncodeId(accId, withdrawalItem.Asset)).LiabilitiesDiff += withdrawalItem.Amount;
                    }
                    break;
                case WithdrawalRemoveEffect withdrawalRemoveEffect:
                    {
                        var accId = withdrawalRemoveEffect.AccountWrapper.Account.Id;
                        GetAccount(pendingDiffObject.Accounts, accId).Withdrawal = 0;
                        foreach (var withdrawalItem in withdrawalRemoveEffect.Items)
                        {
                            var balance = GetBalance(pendingDiffObject.Balances, BalanceModelIdConverter.EncodeId(accId, withdrawalItem.Asset));
                            if (withdrawalRemoveEffect.IsSuccessful)
                                balance.AmountDiff += -withdrawalItem.Amount;
                            balance.LiabilitiesDiff += -withdrawalItem.Amount;
                        }
                    }
                    break;
                default:
                    break;
            }
        }

        private static void UpdateLiabilities(Dictionary<int, DiffObject.Account> accounts, Dictionary<BsonObjectId, DiffObject.Balance> balances, int accountId, ulong orderId, long quoteAmount, long assetAmount)
        {
            var decodedId = OrderIdConverter.Decode(orderId);
            if (decodedId.Side == OrderSide.Buy)
                GetBalance(balances, BalanceModelIdConverter.EncodeId(accountId, 0)).LiabilitiesDiff += quoteAmount;
            else
                GetBalance(balances, BalanceModelIdConverter.EncodeId(accountId, decodedId.Asset)).LiabilitiesDiff += assetAmount;
        }

        private static DiffObject.Account GetAccount(Dictionary<int, DiffObject.Account> accounts, int accountId)
        {
            if (!accounts.TryGetValue(accountId, out var account))
            {
                account = new DiffObject.Account { Id = accountId };
                accounts.Add(accountId, account);
            }
            return account;
        }

        private static DiffObject.Balance GetBalance(Dictionary<BsonObjectId, DiffObject.Balance> balances, BsonObjectId balanceId)
        {
            if (!balances.TryGetValue(balanceId, out var balance))
            {
                balance = new DiffObject.Balance { Id = balanceId };
                balances.Add(balanceId, balance);
            }
            return balance;
        }

        private static DiffObject.Order GetOrder(Dictionary<ulong, DiffObject.Order> orders, ulong orderId)
        {
            if (!orders.TryGetValue(orderId, out var order))
            {
                order = new DiffObject.Order { OrderId = orderId };
                orders.Add(orderId, order);
            }
            return order;
        }

        private static SettingsModel GetConstellationSettings(ConstellationEffect constellationInit)
        {
            var settingsModel = new SettingsModel
            {
                Auditors = constellationInit.Auditors.Select(a => a.Data).ToArray(),
                MinAccountBalance = constellationInit.MinAccountBalance,
                MinAllowedLotSize = constellationInit.MinAllowedLotSize,
                Vault = constellationInit.Vault.Data,
                Assets = constellationInit.Assets.Select(a => a.ToAssetModel()).ToList()
            };

            if (constellationInit.RequestRateLimits != null)
                settingsModel.RequestRateLimits = new RequestRateLimitsModel
                {
                    HourLimit = constellationInit.RequestRateLimits.HourLimit,
                    MinuteLimit = constellationInit.RequestRateLimits.MinuteLimit
                };

            return settingsModel;
        }
    }
}
