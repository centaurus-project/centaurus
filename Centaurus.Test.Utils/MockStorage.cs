using Centaurus.Analytics;
using Centaurus.DAL;
using Centaurus.DAL.Models;
using Centaurus.DAL.Models.Analytics;
using Centaurus.Domain;
using Centaurus.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Test
{
    public class MockStorage : IStorage
    {

        private List<OrderModel> ordersCollection = new List<OrderModel>();
        private List<AccountModel> accountsCollection = new List<AccountModel>();
        private List<BalanceModel> balancesCollection = new List<BalanceModel>();
        private List<QuantumModel> quantaCollection = new List<QuantumModel>();
        private List<EffectModel> effectsCollection = new List<EffectModel>();
        private List<SettingsModel> settingsCollection = new List<SettingsModel>();
        private List<AssetModel> assetSettings = new List<AssetModel>();
        private List<OHLCFrameModel> frames = new List<OHLCFrameModel>();
        private ConstellationState constellationState;

        public Task OpenConnection(string connectionString)
        {
            return Task.CompletedTask;
        }

        public Task CloseConnection()
        {
            return Task.CompletedTask;
        }

        public Task<long> GetLastApex()
        {
            var res = quantaCollection.LastOrDefault()?.Apex ?? -1;
            return Task.FromResult(res);
        }

        public Task<QuantumModel> LoadQuantum(long apex)
        {
            return Task.FromResult(quantaCollection.First(q => q.Apex == apex));
        }

        public Task<List<QuantumModel>> LoadQuanta(params long[] apexes)
        {
            List<QuantumModel> res = quantaCollection;
            if (apexes.Length > 0)
                res = quantaCollection.Where(q => apexes.Contains(q.Apex)).ToList();
            if (res.Count != apexes.Length)
                throw new Exception("Not all quanta were found");
            return Task.FromResult(res);
        }

        public Task<List<QuantumModel>> LoadQuantaAboveApex(long apex, int count = 0)
        {
            var query = quantaCollection.Where(q => q.Apex > apex);
            if (count > 0)
                query = query.Take(count);
            var res = query.ToList();
            return Task.FromResult(res);
        }

        public Task<long> GetFirstEffectApex()
        {
            var firstEffect = effectsCollection
                   .OrderBy(e => e.Apex)
                   .FirstOrDefault();

            var firstApex = firstEffect?.Apex ?? -1;
            return Task.FromResult(firstApex);
        }

        public Task<List<EffectModel>> LoadEffectsAboveApex(long apex)
        {
            var effects = effectsCollection.Where(e => e.Apex > apex).ToList();
            return Task.FromResult(effects);
        }

        public Task<List<EffectModel>> LoadEffectsForApex(long apex)
        {
            var effects = effectsCollection.Where(e => e.Apex == apex).ToList();
            return Task.FromResult(effects);
        }

        public Task<List<AccountModel>> LoadAccounts()
        {
            return Task.FromResult(accountsCollection);
        }

        public Task<List<BalanceModel>> LoadBalances()
        {
            return Task.FromResult(balancesCollection);
        }

        public Task<SettingsModel> LoadSettings(long apex)
        {
            var settings = settingsCollection
                .OrderByDescending(s => s.Apex)
                .FirstOrDefault(s => s.Apex <= apex);
            return Task.FromResult(settings);
        }

        public Task<List<AssetModel>> LoadAssets(long apex)
        {
            var assets = assetSettings
                .Where(s => s.Apex <= apex)
                .ToList();

            return Task.FromResult(assets);
        }

        public Task<List<QuantumModel>> LoadWithdrawals()
        {
            var allAccounts = effectsCollection.Select(e => e.Account).Distinct().ToList();

            var withdrawals = new List<QuantumModel>();
            var effectTypes = new int[] { (int)EffectTypes.WithdrawalCreate, (int)EffectTypes.WithdrawalRemove };
            foreach (var acc in allAccounts)
            {
                var lastEffect = effectsCollection
                    .OrderByDescending(e => e.Id)
                    .FirstOrDefault(e => ByteArrayPrimitives.Equals(e.Account, acc));
                if (lastEffect?.EffectType == (int)EffectTypes.WithdrawalCreate)
                {
                    var quantum = quantaCollection.FirstOrDefault(q => q.Apex == lastEffect.Apex);
                    if (quantum == null)
                        throw new Exception($"Unable to find quantum with apex {lastEffect.Apex}");
                    withdrawals.Add(quantum);
                }
            }
            return Task.FromResult(withdrawals);
        }

        public Task<List<OrderModel>> LoadOrders()
        {
            return Task.FromResult(ordersCollection);
        }

        public Task<ConstellationState> LoadConstellationState()
        {
            return Task.FromResult(constellationState);
        }

        public Task Update(DiffObject update)
        {
            UpdateSettings(update.Settings, update.Assets);

            UpdateStellarData(update.StellarInfoData);

            UpdateAccount(update.Accounts);

            GetBalanceUpdates(update.Balances);

            UpdateOrders(update.Orders);

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
                if (_stellarData.TxCursor > 0)
                    constellationState.TxCursor = _stellarData.TxCursor;
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
                    accountsCollection.Add(new AccountModel { PubKey = pubKey, Nonce = (long)acc.Nonce, RequestRateLimits = acc.RequestRateLimits });
                else if (acc.IsDeleted)
                    accountsCollection.Remove(currentAcc);
                else
                {
                    if (acc.Nonce != 0)
                        currentAcc.Nonce = (long)acc.Nonce;
                    if (acc.RequestRateLimits != null)
                        currentAcc.RequestRateLimits = acc.RequestRateLimits;
                }
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

        public Task<List<EffectModel>> LoadEffects(byte[] cursor, bool isDesc, int limit, byte[] account)
        {
            if (account == null)
                throw new ArgumentNullException(nameof(account));
            IEnumerable<EffectModel> query = effectsCollection
                    .Where(e => ByteArrayPrimitives.Equals(e.Account, account));

            if (isDesc)
                query = query.Reverse();

            if (cursor != null && cursor.Any(x => x != 0))
            {
                if (isDesc)
                    query = query
                        .Where(e => ((IStructuralComparable)e.Id).CompareTo(cursor, Comparer<byte>.Default) < 0);
                else
                    query = query
                        .Where(e => ((IStructuralComparable)e.Id).CompareTo(cursor, Comparer<byte>.Default) > 0);
            }

            var effects = query
                .Take(limit)
                .ToList();

            return Task.FromResult(effects);
        }

        public Task<List<OHLCFrameModel>> GetFrames(int cursorTimeStamp, int toUnixTimeStamp, int asset, OHLCFramePeriod period)
        {
            var result = frames
                .Where(f => f.Market == asset && f.Period == (int)period)
                .OrderByDescending(f => f.TimeStamp)
                .SkipWhile(f => f.TimeStamp >= cursorTimeStamp)
                .TakeWhile(f => f.TimeStamp >= toUnixTimeStamp)
                .ToList();

            return Task.FromResult(result);
        }

        public Task<int> GetFirstFrameDate(OHLCFramePeriod period)
        {
            return Task.FromResult(frames.FirstOrDefault()?.TimeStamp ?? 0);
        }

        public Task SaveAnalytics(List<OHLCFrameModel> update)
        {
            foreach (var frame in update)
            {
                var frameIndex = frames.FindIndex(f => f.TimeStamp == frame.TimeStamp && f.Period == frame.Period && f.Market == frame.Market);
                if (frameIndex >= 0)
                    frames[frameIndex] = frame;
                else
                    frames.Add(frame);
            }
            return Task.CompletedTask;
        }
    }
}
