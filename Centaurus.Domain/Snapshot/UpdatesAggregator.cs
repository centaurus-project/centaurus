﻿using Centaurus.DAL;
using Centaurus.DAL.Models;
using Centaurus.Models;
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
        /// <summary>
        /// Builds update with all changes based ob effects
        /// </summary>
        /// <param name="update"></param>
        /// <returns></returns>
        public static async Task<DiffObject> Aggregate(Dictionary<MessageEnvelope, Effect[]> update)
        {
            SettingsModel constellationSettings = null;

            var stellarData = new DiffObject.StellarInfo();

            var accounts = new Dictionary<byte[], DiffObject.Account>(new ByteArrayComparer());
            var balances = new Dictionary<byte[], Dictionary<int, DiffObject.Balance>>(new ByteArrayComparer());

            var orders = new Dictionary<ulong, DiffObject.Order>();
            var withdrawals = new List<DiffObject.Withdrawal>();

            var assets = new List<AssetModel>();


            var updateLength = update.Count;
            var quanta = new List<QuantumModel>(updateLength);
            var effects = new List<EffectModel>();

            var keys = update.Keys.ToArray();
            for (int i = 0; i < updateLength; i++)
            {
                var quantum = keys[i];
                quanta[i] = QuantumModelExtensions.FromQuantum(quantum);

                var quatumEffects = update[quantum];
                var quatumEffectsLength = quatumEffects.Length;
                for (var c = 0; c < quatumEffectsLength; c++)
                {
                    var effect = quatumEffects[c];

                    effects.Add(EffectModelExtensions.FromEffect(effect, c));

                    switch (effect)
                    {
                        case ConstellationInitEffect constellationInit:
                            constellationSettings = GetConstellationSettings(constellationInit);
                            stellarData = GetStellarData(constellationInit.Ledger, constellationInit.VaultSequence);
                            stellarData.IsInserted = true;
                            assets = GetAssets(constellationInit, null);
                            break;
                        case ConstellationUpdateEffect constellationUpdate:
                            constellationSettings = GetConstellationSettings(constellationUpdate);
                            assets = GetAssets(constellationUpdate, (await Global.PermanentStorage.LoadAssets(ulong.MaxValue)));
                            break;
                        case AccountCreateEffect accountCreateEffect:
                            {
                                var pubKey = accountCreateEffect.Pubkey.Data;
                                accounts.Add(pubKey, new DiffObject.Account { PubKey = pubKey, IsInserted = true });
                            }
                            break;
                        case NonceUpdateEffect nonceUpdateEffect:
                            {
                                var pubKey = nonceUpdateEffect.Pubkey.Data;
                                if (!accounts.ContainsKey(pubKey))
                                    accounts.Add(pubKey, new DiffObject.Account { PubKey = pubKey });
                                accounts[pubKey].Nonce = nonceUpdateEffect.Nonce;
                            }
                            break;
                        case BalanceCreateEffect balanceCreateEffect:
                            {
                                var pubKey = balanceCreateEffect.Pubkey.Data;
                                var asset = balanceCreateEffect.Asset;
                                EnsureBalanceRowExists(balances, pubKey);
                                balances[pubKey].Add(asset, new DiffObject.Balance { IsInserted = true, AssetId = asset });
                            }
                            break;
                        case BalanceUpdateEffect balanceUpdateEffect:
                            {
                                var pubKey = balanceUpdateEffect.Pubkey.Data;
                                var asset = balanceUpdateEffect.Asset;
                                EnsureBalanceExists(balances, pubKey, asset);
                                balances[pubKey][asset].Amount += balanceUpdateEffect.Amount;
                            }
                            break;
                        case LockLiabilitiesEffect lockLiabilitiesEffect:
                            {
                                var pubKey = lockLiabilitiesEffect.Pubkey.Data;
                                var asset = lockLiabilitiesEffect.Asset;
                                EnsureBalanceExists(balances, pubKey, asset);
                                balances[pubKey][asset].Liabilities += lockLiabilitiesEffect.Amount;
                            }
                            break;
                        case UnlockLiabilitiesEffect unlockLiabilitiesEffect:
                            {
                                var pubKey = unlockLiabilitiesEffect.Pubkey.Data;
                                var asset = unlockLiabilitiesEffect.Asset;
                                EnsureBalanceExists(balances, pubKey, asset);
                                balances[pubKey][asset].Liabilities -= unlockLiabilitiesEffect.Amount;
                            }
                            break;
                        case OrderPlacedEffect orderPlacedEffect:
                            {
                                var orderId = orderPlacedEffect.OrderId;
                                orders.Add(orderId, new DiffObject.Order
                                {
                                    Amount = orderPlacedEffect.Amount,
                                    IsInserted = true,
                                    OrderId = orderId,
                                    Price = orderPlacedEffect.Price,
                                    Pubkey = orderPlacedEffect.Pubkey.Data
                                });
                            }
                            break;
                        case OrderRemovedEffect orderRemovedEffect:
                            {
                                var orderId = orderRemovedEffect.OrderId;
                                if (!orders.ContainsKey(orderId))
                                    orders.Add(orderId, new DiffObject.Order { OrderId = orderId });
                                orders[orderId].IsDeleted = true;
                            }
                            break;
                        case TradeEffect tradeEffect:
                            {
                                var orderId = tradeEffect.OrderId;
                                if (!orders.ContainsKey(orderId))
                                    orders.Add(orderId, new DiffObject.Order { OrderId = orderId });
                                orders[orderId].Amount += tradeEffect.AssetAmount;
                            }
                            break;
                        case LedgerUpdateEffect ledgerUpdateEffect:
                            stellarData.Ledger = ledgerUpdateEffect.Ledger;
                            break;
                        case VaultSequenceUpdateEffect vaultSequenceUpdateEffect:
                            stellarData.VaultSequence = vaultSequenceUpdateEffect.Sequence;
                            break;
                        case WithdrawalCreateEffect withdrawalEffect:
                            withdrawals.Add(new DiffObject.Withdrawal
                            {
                                Apex = withdrawalEffect.Apex,
                                TransactionHash = withdrawalEffect.Withdrawal.TransactionHash,
                                RawWithdrawal = XdrConverter.Serialize(withdrawalEffect.Withdrawal),
                                IsInserted = true
                            });
                            break;
                        case WithdrawalRemoveEffect withdrawalEffect:
                            withdrawals.Add(new DiffObject.Withdrawal
                            {
                                Apex = withdrawalEffect.Apex,
                                IsDeleted = true
                            });
                            break;
                        default:
                            break;
                    }
                }
            }

            return new DiffObject
            {
                Accounts = accounts.Values.ToList(),
                Balances = balances.Values.SelectMany(b => b.Values).ToList(),
                Effects = effects,
                Quanta = quanta,
                Assets = assets,
                Orders = orders.Values.ToList(),
                Settings = constellationSettings,
                StellarInfoData = stellarData.Ledger > 0 || stellarData.VaultSequence > 0 ? stellarData : null,
                Widthrawals = withdrawals
            };
        }



        private static void EnsureBalanceRowExists(Dictionary<byte[], Dictionary<int, DiffObject.Balance>> balances, byte[] pubKey)
        {
            if (!balances.ContainsKey(pubKey))
                balances.Add(pubKey, new Dictionary<int, DiffObject.Balance>());
        }


        private static void EnsureBalanceExists(Dictionary<byte[], Dictionary<int, DiffObject.Balance>> balances, byte[] pubKey, int asset)
        {
            EnsureBalanceRowExists(balances, pubKey);
            if (!balances[pubKey].ContainsKey(asset))
                balances[pubKey].Add(asset, new DiffObject.Balance { AssetId = asset, PubKey = pubKey });
        }

        private static DiffObject.StellarInfo GetStellarData(long ledger, long vaultSequence)
        {
            return new DiffObject.StellarInfo { Ledger = ledger, VaultSequence = vaultSequence };
        }

        private static SettingsModel GetConstellationSettings(ConstellationEffect constellationInit)
        {
            return new SettingsModel
            {
                Auditors = constellationInit.Auditors.Cast<byte[]>().ToArray(),
                MinAccountBalance = constellationInit.MinAccountBalance,
                MinAllowedLotSize = constellationInit.MinAllowedLotSize,
                Vault = constellationInit.Vault.Data
            };
        }

        private static List<AssetModel> GetAssets(ConstellationEffect constellationEffect, List<AssetModel> permanentAssets)
        {
            var newAssets = constellationEffect.Assets;
            if (permanentAssets != null && permanentAssets.Count > 0)
            {
                var permanentAssetsIds = permanentAssets.Select(a => a.AssetId);
                newAssets = constellationEffect.Assets.Where(a => !permanentAssetsIds.Contains(a.Id)).ToList();
            }

            var assetsLength = newAssets.Count;
            var assets = new List<AssetModel>();
            for (var i = 0; i < assetsLength; i++)
            {
                var currentAsset = newAssets[i];
                var assetModel = new AssetModel { AssetId = currentAsset.Id, Code = currentAsset.Code, Issuer = currentAsset.Issuer.Data };
                assets[i] = assetModel;
            }

            return assets.ToList();
        }
    }
}