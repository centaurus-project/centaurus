﻿using Centaurus.DAL;
using Centaurus.DAL.Models;
using Centaurus.Domain;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Test
{
    public class MockStorage : BaseStorage
    {

        private List<OrderModel> ordersCollection;
        private List<AccountModel> accountsCollection;
        private List<BalanceModel> balancesCollection;
        private List<QuantumModel> quantaCollection;
        private List<WithdrawalModel> withdrawalsCollection;
        private List<EffectModel> effectsCollection;
        private List<SettingsModel> settingsCollection;
        private List<AssetSettingsModel> assetSettings;
        private StellarData stellarData;

        public override Task OpenConnection(string connectionString)
        {
            return Task.CompletedTask;
        }

        public override Task CloseConnection()
        {
            return Task.CompletedTask;
        }

        public override Task<ulong> GetLastApex()
        {
            var res = settingsCollection.LastOrDefault()?.Apex ?? 0;
            return Task.FromResult(res);
        }

        public override Task<List<QuantumModel>> LoadQuanta(params ulong[] apexes)
        {
            List<QuantumModel> res = quantaCollection;
            if (apexes.Length > 0)
                res = quantaCollection.Where(q => apexes.Contains(q.Apex)).ToList();
            if (res.Count != apexes.Length)
                throw new Exception("Not all quanta were found");
            return Task.FromResult(res);
        }

        public override Task<List<EffectModel>> LoadEffectsAboveApex(ulong apex)
        {
            var effects = effectsCollection.Where(e => e.Apex > apex).ToList();
            return Task.FromResult(effects);
        }

        public override Task<List<EffectModel>> LoadEffectsForApex(ulong apex)
        {
            var effects = effectsCollection.Where(e => e.Apex == apex).ToList();
            return Task.FromResult(effects);
        }

        public override Task<List<AccountModel>> LoadAccounts()
        {
            return Task.FromResult(accountsCollection);
        }

        public override Task<List<BalanceModel>> LoadBalances()
        {
            return Task.FromResult(balancesCollection);
        }

        public override Task<SettingsModel> LoadSettings(ulong apex)
        {
            var settings = settingsCollection
                .OrderByDescending(s => s.Apex)
                .FirstOrDefault(s => s.Apex <= apex);
            return Task.FromResult(settings);
        }

        public override Task<List<AssetSettingsModel>> LoadAssets(ulong apex)
        {
            var assets = assetSettings
                .Where(s => s.Apex <= apex)
                .ToList();

            return Task.FromResult(assets);
        }

        public override Task<List<WithdrawalModel>> LoadWithdrawals()
        {
            return Task.FromResult(withdrawalsCollection);
        }

        public override Task<List<OrderModel>> LoadOrders()
        {
            return Task.FromResult(ordersCollection);
        }

        public override Task<StellarData> LoadStellarData()
        {
            return Task.FromResult(stellarData);
        }

        public override Task Update(UpdateObject update)
        {
            UpdateSettings(update.Settings, update.Assets);

            UpdateStellarData(update.StellarData);

            UpdateAccount(update.Accounts);

            GetBalanceUpdates(update.Balances);

            UpdateOrders(update.Orders);

            UpdateWithdrawals(update.Widthrawals);

            UpdateQuanta(update.Quanta);

            UpdateEffects(update.Effects);

            return Task.CompletedTask;
        }

        private void UpdateEffects(List<EffectModel> effects)
        {
            effectsCollection.AddRange(effects);
        }

        private void UpdateQuanta(List<QuantumModel> quanta)
        {
            quantaCollection.AddRange(quanta);
        }

        private void UpdateSettings(SettingsModel settings, List<AssetSettingsModel> assets)
        {
            if (settings != null)
            {
                settingsCollection.Add(settings);

                var currentAssets = assetSettings.Select(a => a.AssetId);
                var newAssets = assets.Where(a => !currentAssets.Contains(a.AssetId));

                if (newAssets.Count() > 0)
                    assetSettings.AddRange(newAssets);
            }
        }

        private void UpdateStellarData(StellarDataCalcModel _stellarData)
        {
            if (_stellarData != null)
            {
                stellarData.Ledger = _stellarData.Ledger;
                stellarData.VaultSequence = _stellarData.VaultSequence;
            }
        }

        private void UpdateWithdrawals(List<WithdrawalCalcModel> withdrawals)
        {
            for (int i = 0; i < withdrawals.Count; i++)
            {
                var withdrawal = withdrawals[i];
                var apex = withdrawal.Apex;
                var trHash = withdrawal.TransactionHash;
                var currentWidthrawal = withdrawalsCollection.FirstOrDefault(w => w.Apex == apex);
                if (withdrawal.IsInserted)
                    withdrawalsCollection.Add(new WithdrawalModel { Apex = apex, TransactionHash = trHash });
                else if (withdrawal.IsDeleted)
                    withdrawalsCollection.Remove(currentWidthrawal);
                else
                    throw new InvalidOperationException("Withdrawal object cannot be updated");
            }
        }

        private void UpdateAccount(List<AccountCalcModel> accounts)
        {
            var accLength = accounts.Count;
            for (int i = 0; i < accLength; i++)
            {
                var acc = accounts[i];
                var pubKey = acc.PubKey;
                var currentAcc = accountsCollection.FirstOrDefault(a => ByteArrayPrimitives.Equals(a.PubKey, pubKey));
                if (acc.IsInserted)
                    accountsCollection.Add(new AccountModel { PubKey = pubKey, Nonce = acc.Nonce });
                else if (acc.IsDeleted)
                    accountsCollection.Remove(currentAcc);
                else
                    currentAcc.Nonce = acc.Nonce;
            }
        }

        private void GetBalanceUpdates(List<BalanceCalcModel> balances)
        {
            var balancesLength = balances.Count;

            for (int i = 0; i < balancesLength; i++)
            {
                var balance = balances[i];
                var pubKey = balance.PubKey;
                var asset = balance.AssetId;
                var amount = balance.Amount;
                var liabilities = balance.Liabilities;

                var currentBalance = balancesCollection.FirstOrDefault(b => b.AssetId == asset && ByteArrayPrimitives.Equals(b.Account, pubKey));

                if (balance.IsInserted)
                    balancesCollection.Add(new BalanceModel
                    {
                        Account = pubKey,
                        Amount = amount,
                        AssetId = asset,
                        Liabilities = liabilities
                    });
                else if (balance.IsDeleted)
                    balancesCollection.Remove(currentBalance);
                else
                {
                    currentBalance.Amount += amount;
                    currentBalance.Liabilities += liabilities;
                }
            }
        }

        private void UpdateOrders(List<OrderCalcModel> orders)
        {
            var ordersLength = orders.Count;

            for (int i = 0; i < ordersLength; i++)
            {
                var order = orders[i];
                var orderId = order.OrderId;
                var price = order.Price;
                var amount = order.Amount;
                var pubKey = order.Pubkey;

                var currentOrder = ordersCollection.FirstOrDefault(s => s.OrderId == orderId);

                if (order.IsInserted)
                    ordersCollection.Add(new OrderModel
                    {
                        OrderId = orderId,
                        Amount = amount,
                        Price = price,
                        Pubkey = pubKey
                    });
                else if (order.IsDeleted)
                    ordersCollection.Remove(currentOrder);
                else
                {
                    currentOrder.Amount += amount;
                }
            }
        }
    }
}
