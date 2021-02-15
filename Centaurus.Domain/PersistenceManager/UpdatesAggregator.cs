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

            var account = quatumEffect.Account;
            var apex = processorsContainer.Apex;

            processorsContainer.QuantumModel.AddEffect(account, quatumEffect.FromEffect(effectIndex));

            switch (quatumEffect)
            {
                case ConstellationInitEffect constellationInit:
                    pendingDiffObject.ConstellationSettings = GetConstellationSettings(constellationInit);
                    pendingDiffObject.StellarInfoData = new DiffObject.ConstellationState { TxCursor = constellationInit.TxCursor, IsInserted = true };
                    pendingDiffObject.Assets = GetAssets(constellationInit, null);
                    break;
                case ConstellationUpdateEffect constellationUpdate:
                    throw new NotImplementedException();
                    pendingDiffObject.ConstellationSettings = GetConstellationSettings(constellationUpdate);
                    pendingDiffObject.Assets = GetAssets(constellationUpdate, Global.PermanentStorage.LoadAssets(long.MaxValue).Result);
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
                        var accId = nonceUpdateEffect.Account;
                        GetAccount(pendingDiffObject.Accounts, accId).Nonce = nonceUpdateEffect.Nonce;
                    }
                    break;
                case BalanceCreateEffect balanceCreateEffect:
                    {
                        var accId = balanceCreateEffect.Account;
                        var balanceId = BalanceModelIdConverter.EncodeId(accId, balanceCreateEffect.Asset);
                        GetBalance(pendingDiffObject.Balances, balanceId).IsInserted = true;
                    }
                    break;
                case BalanceUpdateEffect balanceUpdateEffect:
                    {
                        var accId = balanceUpdateEffect.Account;
                        var balanceId = BalanceModelIdConverter.EncodeId(accId, balanceUpdateEffect.Asset);
                        GetBalance(pendingDiffObject.Balances, balanceId).AmountDiff += balanceUpdateEffect.Amount;
                    }
                    break;
                case UpdateLiabilitiesEffect lockLiabilitiesEffect:
                    {
                        var accId = lockLiabilitiesEffect.Account;
                        var balanceId = BalanceModelIdConverter.EncodeId(accId, lockLiabilitiesEffect.Asset);
                        GetBalance(pendingDiffObject.Balances, balanceId).LiabilitiesDiff += lockLiabilitiesEffect.Amount;
                    }
                    break;
                case RequestRateLimitUpdateEffect requestRateLimitUpdateEffect:
                    {
                        var accId = requestRateLimitUpdateEffect.Account;
                        GetAccount(pendingDiffObject.Accounts, accId).RequestRateLimits = new RequestRateLimitsModel
                        {
                            HourLimit = requestRateLimitUpdateEffect.RequestRateLimits.HourLimit,
                            MinuteLimit = requestRateLimitUpdateEffect.RequestRateLimits.MinuteLimit
                        };
                    }
                    break;
                case OrderPlacedEffect orderPlacedEffect:
                    {
                        var orderId = orderPlacedEffect.OrderId;
                        pendingDiffObject.Orders[orderId] = new DiffObject.Order
                        {
                            Amount = orderPlacedEffect.Amount,
                            QuoteAmount = orderPlacedEffect.QuoteAmount,
                            IsInserted = true,
                            OrderId = orderId,
                            Price = orderPlacedEffect.Price,
                            Account = orderPlacedEffect.Account
                        };
                    }
                    break;
                case OrderRemovedEffect orderRemovedEffect:
                    {
                        GetOrder(pendingDiffObject.Orders, orderRemovedEffect.OrderId).IsDeleted = true;
                    }
                    break;
                case TradeEffect tradeEffect:
                    {
                        var order = GetOrder(pendingDiffObject.Orders, tradeEffect.OrderId);
                        order.Amount -= tradeEffect.AssetAmount;
                        order.QuoteAmount -= tradeEffect.QuoteAmount;
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
                        var accId = withdrawalCreateEffect.Account;
                        GetAccount(pendingDiffObject.Accounts, accId).Withdrawal = withdrawalCreateEffect.Apex;
                    }
                    break;
                case WithdrawalRemoveEffect withdrawalRemoveEffect:
                    {
                        var accId = withdrawalRemoveEffect.Account;
                        GetAccount(pendingDiffObject.Accounts, accId).Withdrawal = 0;
                    }
                    break;
                default:
                    break;
            }
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
                Vault = constellationInit.Vault.Data
            };

            if (constellationInit.RequestRateLimits != null)
                settingsModel.RequestRateLimits = new RequestRateLimitsModel
                {
                    HourLimit = constellationInit.RequestRateLimits.HourLimit,
                    MinuteLimit = constellationInit.RequestRateLimits.MinuteLimit
                };

            return settingsModel;
        }

        /// <summary>
        /// Builds asset models for only new assets.
        /// </summary>
        private static List<AssetModel> GetAssets(ConstellationEffect constellationEffect, List<AssetModel> permanentAssets)
        {
            var newAssets = constellationEffect.Assets;
            if (permanentAssets != null && permanentAssets.Count > 0)
            {
                var permanentAssetsIds = permanentAssets.Select(a => a.Id);
                newAssets = constellationEffect.Assets.Where(a => !permanentAssetsIds.Contains(a.Id)).ToList();
            }

            var assetsLength = newAssets.Count;
            var assets = new List<AssetModel>();
            for (var i = 0; i < assetsLength; i++)
            {
                var currentAsset = newAssets[i];
                var assetModel = new AssetModel { Id = currentAsset.Id, Code = currentAsset.Code, Issuer = currentAsset.Issuer.Data };
                assets.Add(assetModel);
            }

            return assets.ToList();
        }
    }
}
