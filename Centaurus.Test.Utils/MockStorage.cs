using Centaurus.DAL;
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

        private List<OrderModel> ordersCollection = new List<OrderModel>();
        private List<AccountModel> accountsCollection = new List<AccountModel>();
        private List<BalanceModel> balancesCollection = new List<BalanceModel>();
        private List<QuantumModel> quantaCollection = new List<QuantumModel>();
        private List<WithdrawalModel> withdrawalsCollection = new List<WithdrawalModel>();
        private List<EffectModel> effectsCollection = new List<EffectModel>();
        private List<SettingsModel> settingsCollection = new List<SettingsModel>();
        private List<AssetModel> assetSettings = new List<AssetModel>();
        private ConstellationState constellationState;

        public override Task OpenConnection(string connectionString)
        {
            return Task.CompletedTask;
        }

        public override Task CloseConnection()
        {
            return Task.CompletedTask;
        }

        public override Task<long> GetLastApex()
        {
            var res = quantaCollection.FirstOrDefault()?.Apex ?? -1;
            return Task.FromResult(res);
        }

        public override Task<QuantumModel> LoadQuantum(long apex)
        {
            return Task.FromResult(quantaCollection.First(q => q.Apex == apex));
        }

        public override Task<List<QuantumModel>> LoadQuanta(params long[] apexes)
        {
            List<QuantumModel> res = quantaCollection;
            if (apexes.Length > 0)
                res = quantaCollection.Where(q => apexes.Contains(q.Apex)).ToList();
            if (res.Count != apexes.Length)
                throw new Exception("Not all quanta were found");
            return Task.FromResult(res);
        }

        public override Task<List<QuantumModel>> LoadQuantaAboveApex(long apex, int count = 0)
        {
            var query = quantaCollection.Where(q => q.Apex > apex);
            if (count > 0)
                query = query.Take(count);
            var res = query.ToList();
            return Task.FromResult(res);
        }

        public override Task<long> GetFirstEffectApex()
        {
            var firstEffect = effectsCollection
                   .OrderBy(e => e.Apex)
                   .FirstOrDefault();

            var firstApex = firstEffect?.Apex ?? -1;
            return Task.FromResult(firstApex);
        }

        public override Task<List<EffectModel>> LoadEffectsAboveApex(long apex)
        {
            var effects = effectsCollection.Where(e => e.Apex > apex).ToList();
            return Task.FromResult(effects);
        }

        public override Task<List<EffectModel>> LoadEffectsForApex(long apex)
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

        public override Task<SettingsModel> LoadSettings(long apex)
        {
            var settings = settingsCollection
                .OrderByDescending(s => s.Apex)
                .FirstOrDefault(s => s.Apex <= apex);
            return Task.FromResult(settings);
        }

        public override Task<List<AssetModel>> LoadAssets(long apex)
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

        public override Task<ConstellationState> LoadConstellationState()
        {
            return Task.FromResult(constellationState);
        }

        public override Task Update(DiffObject update)
        {
            UpdateSettings(update.Settings, update.Assets);

            UpdateStellarData(update.StellarInfoData);

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

        private void UpdateSettings(SettingsModel settings, List<AssetModel> assets)
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

        private void UpdateStellarData(DiffObject.ConstellationState _stellarData)
        {
            if (_stellarData != null)
            {
                if (constellationState == null)
                    constellationState = new ConstellationState();
                if (_stellarData.Ledger > 0)
                    constellationState.Ledger = _stellarData.Ledger;
                if (_stellarData.VaultSequence > 0)
                    constellationState.VaultSequence = _stellarData.VaultSequence;
            }
        }

        private void UpdateWithdrawals(List<DiffObject.Withdrawal> withdrawals)
        {
            for (int i = 0; i < withdrawals.Count; i++)
            {
                var withdrawal = withdrawals[i];
                var apex = (long)withdrawal.Apex;
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

        private void UpdateAccount(List<DiffObject.Account> accounts)
        {
            var accLength = accounts.Count;
            for (int i = 0; i < accLength; i++)
            {
                var acc = accounts[i];
                var pubKey = acc.PubKey;
                var currentAcc = accountsCollection.FirstOrDefault(a => ByteArrayPrimitives.Equals(a.PubKey, pubKey));
                if (acc.IsInserted)
                    accountsCollection.Add(new AccountModel { PubKey = pubKey, Nonce = (long)acc.Nonce });
                else if (acc.IsDeleted)
                    accountsCollection.Remove(currentAcc);
                else
                    currentAcc.Nonce = (long)acc.Nonce;
            }
        }

        private void GetBalanceUpdates(List<DiffObject.Balance> balances)
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

        private void UpdateOrders(List<DiffObject.Order> orders)
        {
            var ordersLength = orders.Count;

            for (int i = 0; i < ordersLength; i++)
            {
                var order = orders[i];
                var orderId = order.OrderId;
                var price = order.Price;
                var amount = order.Amount;
                var pubKey = order.Pubkey;

                var currentOrder = ordersCollection.FirstOrDefault(s => s.OrderId == (long)orderId);

                if (order.IsInserted)
                    ordersCollection.Add(new OrderModel
                    {
                        OrderId = (long)orderId,
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
