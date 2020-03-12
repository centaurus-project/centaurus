using Centaurus.DAL;
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
        public static async Task<DiffObject> Aggregate(List<PendingUpdates.PendingUpdatesItem> update)
        {
            if (update == null)
                throw new ArgumentNullException(nameof(update));

            SettingsModel constellationSettings = null;

            DiffObject.ConstellationState stellarData = new DiffObject.ConstellationState();

            var accounts = new Dictionary<byte[], DiffObject.Account>(new ByteArrayComparer());
            var balances = new Dictionary<byte[], Dictionary<int, DiffObject.Balance>>(new ByteArrayComparer());

            var orders = new Dictionary<ulong, DiffObject.Order>();
            var withdrawals = new List<DiffObject.Withdrawal>();

            var assets = new List<AssetModel>();


            var updateLength = update.Count;
            var quanta = new List<QuantumModel>();
            var effects = new List<EffectModel>();

            for (int i = 0; i < updateLength; i++)
            {
                var currentUpdateItem = update[i];

                quanta.Add(QuantumModelExtensions.FromQuantum(currentUpdateItem.Quantum));
                var quantumMessage = (Quantum)currentUpdateItem.Quantum.Message;
                //update current apex
                stellarData.CurrentApex = quantumMessage.Apex;

                var quatumEffects = update[i].Effects;
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
                            assets = GetAssets(constellationUpdate, (await Global.PermanentStorage.LoadAssets(long.MaxValue)));
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
                                EnsureAccountRowExists(accounts, pubKey);
                                accounts[pubKey].Nonce = nonceUpdateEffect.Nonce;
                            }
                            break;
                        case BalanceCreateEffect balanceCreateEffect:
                            {
                                var pubKey = balanceCreateEffect.Pubkey.Data;
                                var asset = balanceCreateEffect.Asset;
                                EnsureBalanceRowExists(balances, pubKey);
                                balances[pubKey].Add(asset, new DiffObject.Balance { IsInserted = true, AssetId = asset, PubKey = pubKey });
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
                        case RequestRateLimitUpdateEffect requestRateLimitUpdateEffect:
                            {
                                var pubKey = requestRateLimitUpdateEffect.Pubkey.Data;
                                EnsureAccountRowExists(accounts, pubKey);
                                accounts[pubKey].RequestRateLimits = new RequestRateLimitsModel
                                {
                                    HourLimit = requestRateLimitUpdateEffect.RequestRateLimits.HourLimit,
                                    MinuteLimit = requestRateLimitUpdateEffect.RequestRateLimits.MinuteLimit
                                };
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
                StellarInfoData = stellarData,
                Widthrawals = withdrawals
            };
        }

        /// <summary>
        /// Builds an update based on the snapshot. All objects will be marked as new.
        /// </summary>
        /// <param name="snapshot"></param>
        public static DiffObject Aggregate(Snapshot snapshot)
        {
            var apex = snapshot.Apex;

            var diffObject = new DiffObject();

            //get account diff objects
            diffObject.Accounts = snapshot.Accounts
                .Select(a => new DiffObject.Account
                {
                    IsInserted = true,
                    Nonce = a.Nonce,
                    PubKey = a.Pubkey.Data
                }).ToList();

            //get account diff objects
            diffObject.Balances = snapshot.Accounts.SelectMany(bs =>
            {
                return bs.Balances.Select(b => new DiffObject.Balance
                {
                    IsInserted = true,
                    Amount = b.Amount,
                    AssetId = b.Asset,
                    Liabilities = b.Liabilities,
                    PubKey = bs.Pubkey.Data
                });
            }).ToList();

            diffObject.Settings = new SettingsModel
            {
                Apex = apex,
                Auditors = snapshot.Settings.Auditors.Select(s => s.Data).ToArray(),
                MinAccountBalance = snapshot.Settings.MinAccountBalance,
                MinAllowedLotSize = snapshot.Settings.MinAllowedLotSize,
                Vault = snapshot.Settings.Vault.Data
            };
            if (snapshot.Settings.RequestRateLimits != null)
                diffObject.Settings.RequestRateLimits = new RequestRateLimitsModel
                {
                    HourLimit = snapshot.Settings.RequestRateLimits.HourLimit,
                    MinuteLimit = snapshot.Settings.RequestRateLimits.MinuteLimit
                };


            diffObject.Assets = snapshot.Settings.Assets.Select(a => new AssetModel
            {
                Apex = apex,
                AssetId = a.Id,
                Code = a.Code,
                Issuer = a.Issuer?.Data
            }).ToList();

            diffObject.Orders = snapshot.Orders.Select(o => new DiffObject.Order
            {
                IsInserted = true,
                OrderId = o.OrderId,
                Amount = o.Amount,
                Price = o.Price,
                Pubkey = o.Account.Pubkey.Data
            }).ToList();

            diffObject.Widthrawals = snapshot.Withdrawals.Select(w => new DiffObject.Withdrawal
            {
                IsInserted = true,
                Apex = apex,
                RawWithdrawal = XdrConverter.Serialize(w),
                TransactionHash = w.TransactionHash
            }).ToList();

            diffObject.StellarInfoData = new DiffObject.ConstellationState
            {
                IsInserted = true,
                CurrentApex = apex,
                Ledger = snapshot.Ledger,
                VaultSequence = snapshot.VaultSequence
            };

            diffObject.Quanta = new List<QuantumModel>();

            diffObject.Effects = new List<EffectModel>();

            return diffObject;
        }

        private static void EnsureAccountRowExists(Dictionary<byte[], DiffObject.Account> accounts, byte[] pubKey)
        {
            if (!accounts.ContainsKey(pubKey))
                accounts.Add(pubKey, new DiffObject.Account { PubKey = pubKey });
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

        private static DiffObject.ConstellationState GetStellarData(long ledger, long vaultSequence)
        {
            return new DiffObject.ConstellationState { Ledger = ledger, VaultSequence = vaultSequence };
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
                var permanentAssetsIds = permanentAssets.Select(a => a.AssetId);
                newAssets = constellationEffect.Assets.Where(a => !permanentAssetsIds.Contains(a.Id)).ToList();
            }

            var assetsLength = newAssets.Count;
            var assets = new List<AssetModel>();
            for (var i = 0; i < assetsLength; i++)
            {
                var currentAsset = newAssets[i];
                var assetModel = new AssetModel { AssetId = currentAsset.Id, Code = currentAsset.Code, Issuer = currentAsset.Issuer.Data };
                assets.Add(assetModel);
            }

            return assets.ToList();
        }
    }
}