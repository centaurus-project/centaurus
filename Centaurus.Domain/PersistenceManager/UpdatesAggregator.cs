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
        public static void Aggregate(this DiffObject pendingDiffObject, MessageEnvelope quantumEnvelope, Effect quatumEffect, int effectIndex)
        {
            if (pendingDiffObject == null)
                throw new ArgumentNullException(nameof(pendingDiffObject));

            if (quantumEnvelope == null)
                throw new ArgumentNullException(nameof(quantumEnvelope));

            if (quatumEffect == null)
                throw new ArgumentNullException(nameof(quatumEffect));

            var quantum = (Quantum)quantumEnvelope.Message;

            pendingDiffObject.Effects.Add(quatumEffect.FromEffect(effectIndex, quantum.Timestamp));

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
                        EnsureAccountRecordExists(pendingDiffObject.Accounts, accId);
                        pendingDiffObject.Accounts[accId].Nonce = nonceUpdateEffect.Nonce;
                    }
                    break;
                case BalanceCreateEffect balanceCreateEffect:
                    {
                        var accId = balanceCreateEffect.Account;
                        var balanceId = BalanceModelIdConverter.EncodeId(accId, balanceCreateEffect.Asset);
                        EnsureBalanceRecordExists(pendingDiffObject.Balances, balanceId);
                        pendingDiffObject.Balances[balanceId].IsInserted = true;
                    }
                    break;
                case BalanceUpdateEffect balanceUpdateEffect:
                    {
                        var accId = balanceUpdateEffect.Account;
                        var balanceId = BalanceModelIdConverter.EncodeId(accId, balanceUpdateEffect.Asset);
                        EnsureBalanceRecordExists(pendingDiffObject.Balances, balanceId);
                        pendingDiffObject.Balances[balanceId].Amount += balanceUpdateEffect.Amount;
                    }
                    break;
                case UpdateLiabilitiesEffect lockLiabilitiesEffect:
                    {
                        var accId = lockLiabilitiesEffect.Account;
                        var balanceId = BalanceModelIdConverter.EncodeId(accId, lockLiabilitiesEffect.Asset);
                        EnsureBalanceRecordExists(pendingDiffObject.Balances, balanceId);
                        pendingDiffObject.Balances[balanceId].Liabilities += lockLiabilitiesEffect.Amount;
                    }
                    break;
                case RequestRateLimitUpdateEffect requestRateLimitUpdateEffect:
                    {
                        var accId = requestRateLimitUpdateEffect.Account;
                        EnsureAccountRecordExists(pendingDiffObject.Accounts, accId);
                        pendingDiffObject.Accounts[accId].RequestRateLimits = new RequestRateLimitsModel
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
                            IsInserted = true,
                            OrderId = orderId,
                            Price = orderPlacedEffect.Price,
                            Account = orderPlacedEffect.Account
                        };
                    }
                    break;
                case OrderRemovedEffect orderRemovedEffect:
                    {
                        var orderId = orderRemovedEffect.OrderId;
                        if (!pendingDiffObject.Orders.ContainsKey(orderId))
                            pendingDiffObject.Orders.Add(orderId, new DiffObject.Order
                            {
                                OrderId = orderId
                            });
                        pendingDiffObject.Orders[orderId].IsDeleted = true;
                    }
                    break;
                case TradeEffect tradeEffect:
                    {
                        var orderId = tradeEffect.OrderId;
                        if (!pendingDiffObject.Orders.ContainsKey(orderId))
                            pendingDiffObject.Orders.Add(orderId, new DiffObject.Order { OrderId = orderId });
                        pendingDiffObject.Orders[orderId].Amount += -(tradeEffect.AssetAmount);
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
                        EnsureAccountRecordExists(pendingDiffObject.Accounts, accId);
                        pendingDiffObject.Accounts[accId].Withdrawal = withdrawalCreateEffect.Apex;
                    }
                    break;
                case WithdrawalRemoveEffect withdrawalRemoveEffect:
                    {
                        var accId = withdrawalRemoveEffect.Account;
                        EnsureAccountRecordExists(pendingDiffObject.Accounts, accId);
                        pendingDiffObject.Accounts[accId].Withdrawal = 0;
                    }
                    break;
                default:
                    break;
            }
        }

        private static void EnsureAccountRecordExists(Dictionary<int, DiffObject.Account> accounts, int accountId)
        {
            if (!accounts.ContainsKey(accountId))
                accounts.Add(accountId, new DiffObject.Account { Id = accountId });
        }

        private static void EnsureBalanceRecordExists(Dictionary<BsonObjectId, DiffObject.Balance> balances, BsonObjectId balanceId)
        {
            if (!balances.ContainsKey(balanceId))
                balances.Add(balanceId, new DiffObject.Balance { Id = balanceId });
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