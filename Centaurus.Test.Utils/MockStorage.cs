﻿using Centaurus.Models;
using Centaurus.DAL;
using Centaurus.DAL.Models;
using Centaurus.DAL.Models.Analytics;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Centaurus.DAL.Mongo;
using MongoDB.Bson;

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
        private List<PriceHistoryFrameModel> frames = new List<PriceHistoryFrameModel>();
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
                res = quantaCollection
                    .OrderBy(q => q.Apex)
                    .Where(q => apexes.Contains(q.Apex))
                    .ToList();
            if (res.Count != apexes.Length)
                throw new Exception("Not all quanta were found");
            return Task.FromResult(res);
        }

        public Task<List<QuantumModel>> LoadQuantaAboveApex(long apex, int count = 0)
        {
            var query = quantaCollection
                    .OrderBy(q => q.Apex)
                    .Where(q => q.Apex > apex);
            if (count > 0)
                query = query.Take(count);
            var res = query.ToList();
            return Task.FromResult(res);
        }

        public Task<long> GetFirstEffectApex()
        {
            var firstEffect = effectsCollection
                   .OrderBy(e => e.Id)
                   .FirstOrDefault();

            if (firstEffect == null)
                return Task.FromResult((long)-1);

            var decodedId = EffectModelIdConverter.DecodeId(firstEffect.Id);
            return Task.FromResult(decodedId.apex);
        }

        public Task<List<EffectModel>> LoadEffectsAboveApex(long apex)
        {
            var cursor = EffectModelIdConverter.EncodeId(apex + 1, 0);
            var effects = effectsCollection
                .OrderBy(e => e.Id)
                .Where(e => e.Id >= cursor)
                .ToList();
            return Task.FromResult(effects);
        }

        public Task<List<EffectModel>> LoadEffectsForApex(long apex)
        {
            var cursorFrom = EffectModelIdConverter.EncodeId(apex, 0);
            var cursorTo = EffectModelIdConverter.EncodeId(apex + 1, 0);
            var effects = effectsCollection
                .OrderBy(e => e.Id)
                .Where(e => e.Id >= cursorFrom && e.Id < cursorTo)
                .ToList();
            return Task.FromResult(effects);
        }

        public Task<List<AccountModel>> LoadAccounts()
        {
            return Task.FromResult(accountsCollection.OrderBy(a => a.Id).ToList());
        }

        public Task<List<BalanceModel>> LoadBalances()
        {
            return Task.FromResult(balancesCollection.OrderBy(b => b.Id).ToList());
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
                .Where(a => a.Apex <= apex)
                .OrderBy(a => a.Id)
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
                    .FirstOrDefault(e => e.Account == acc);
                if (lastEffect?.EffectType == (int)EffectTypes.WithdrawalCreate)
                {
                    var decodedId = EffectModelIdConverter.DecodeId(lastEffect.Id);
                    var quantum = quantaCollection
                        .FirstOrDefault(q => q.Apex == decodedId.apex);
                    if (quantum == null)
                        throw new Exception($"Unable to find quantum with apex {decodedId.apex}");
                    withdrawals.Add(quantum);
                }
            }
            return Task.FromResult(withdrawals.OrderBy(w => w.Apex).ToList());
        }

        public Task<List<OrderModel>> LoadOrders()
        {
            return Task.FromResult(ordersCollection.OrderBy(o => o.Id).ToList());
        }

        public Task<ConstellationState> LoadConstellationState()
        {
            return Task.FromResult(constellationState);
        }

        public Task Update(DiffObject update)
        {
            UpdateSettings(update.ConstellationSettings, update.Assets);

            UpdateStellarData(update.StellarInfoData);

            UpdateAccount(update.Accounts.Values.ToList());

            GetBalanceUpdates(update.Balances.Values.ToList());

            UpdateOrders(update.Orders.Values.ToList());

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

                var currentAssets = assetSettings.Select(a => a.Id);
                var newAssets = assets.Where(a => !currentAssets.Contains(a.Id));

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
                var currentAcc = accountsCollection.FirstOrDefault(a => a.Id == acc.Id);
                if (acc.IsInserted)
                    accountsCollection.Add(new AccountModel { Id = acc.Id, PubKey = acc.PubKey, Nonce = acc.Nonce, RequestRateLimits = acc.RequestRateLimits });
                else if (acc.IsDeleted)
                    accountsCollection.Remove(currentAcc);
                else
                {
                    if (acc.Nonce != 0)
                        currentAcc.Nonce = acc.Nonce;
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

                var currentBalance = balancesCollection.FirstOrDefault(b => b.Id == balance.Id);

                if (balance.IsInserted)
                    balancesCollection.Add(new BalanceModel
                    {
                        Id = balance.Id,
                        Amount = balance.Amount,
                        Liabilities = balance.Liabilities
                    });
                else if (balance.IsDeleted)
                    balancesCollection.Remove(currentBalance);
                else
                {
                    currentBalance.Amount += balance.Amount;
                    currentBalance.Liabilities += balance.Liabilities;
                }
            }
        }

        private void UpdateOrders(List<DiffObject.Order> orders)
        {
            var ordersLength = orders.Count;

            for (int i = 0; i < ordersLength; i++)
            {
                var order = orders[i];

                var currentOrder = ordersCollection.FirstOrDefault(s => s.Id == (long)order.OrderId);

                if (order.IsInserted)
                    ordersCollection.Add(new OrderModel
                    {
                        Id = (long)order.OrderId,
                        Amount = order.Amount,
                        Price = order.Price,
                        Account = order.Account
                    });
                else if (order.IsDeleted)
                    ordersCollection.Remove(currentOrder);
                else
                {
                    currentOrder.Amount += order.Amount;
                }
            }
        }

        public Task<List<EffectModel>> LoadEffects(byte[] cursor, bool isDesc, int limit, int account)
        {
            if (account == default)
                throw new ArgumentNullException(nameof(account));
            IEnumerable<EffectModel> query = effectsCollection
                    .Where(e => e.Account == account);

            if (isDesc)
                query = query.OrderByDescending(e => e.Id);
            else
                query = query.OrderBy(e => e.Id);

            if (cursor != null && cursor.Any(x => x != 0))
            {
                var c = new BsonObjectId(new ObjectId(cursor));
                if (isDesc)
                    query = query
                        .Where(e => e.Id < c);
                else
                    query = query
                        .Where(e => e.Id > c);
            }

            var effects = query
                .Take(limit)
                .ToList();

            return Task.FromResult(effects);
        }

        public Task<List<PriceHistoryFrameModel>> GetPriceHistory(int cursorTimeStamp, int toUnixTimeStamp, int asset, PriceHistoryPeriod period)
        {
            var cursorId = PriceHistoryExtensions.EncodeId(asset, (int)period, cursorTimeStamp);
            var toId = PriceHistoryExtensions.EncodeId(asset, (int)period, toUnixTimeStamp);

            var result = frames
                .Where(f => f.Id >= cursorId && f.Id < toId)
                .OrderByDescending(f => f.Id)
                .ToList();

            return Task.FromResult(result);
        }

        public Task<int> GetFirstPriceHistoryFrameDate(int market, PriceHistoryPeriod period)
        {
            var firstId = PriceHistoryExtensions.EncodeId(market, (int)period, 0);
            var firstFrame = frames
                .OrderByDescending(f => f.Id)
                .FirstOrDefault(f => f.Id >= firstId);

            if (firstFrame == null)
                return Task.FromResult(0);

            return Task.FromResult(PriceHistoryExtensions.DecodeId(firstFrame.Id).timestamp);
        }

        public Task SaveAnalytics(List<PriceHistoryFrameModel> update)
        {
            foreach (var frame in update)
            {
                var frameIndex = frames.FindIndex(f => f.Id == frame.Id);
                if (frameIndex >= 0)
                    frames[frameIndex] = frame;
                else
                    frames.Add(frame);
            }
            return Task.CompletedTask;
        }

        public Task DropDatabase()
        {
            ordersCollection.Clear();
            accountsCollection.Clear();
            balancesCollection.Clear();
            quantaCollection.Clear();
            effectsCollection.Clear();
            settingsCollection.Clear();
            assetSettings.Clear();
            frames.Clear();
            constellationState = null;
            return Task.CompletedTask;
        }
    }
}
