using Centaurus.PersistentStorage;
using Centaurus.PersistentStorage.Abstraction;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Centaurus.Test
{
    public class MockStorage : IPersistentStorage
    {
        private List<AccountPersistentModel> accountsCollection = new List<AccountPersistentModel>();
        private List<QuantumPersistentModel> quantaCollection = new List<QuantumPersistentModel>();
        private List<QuantumRefPersistentModel> quantaRefCollection = new List<QuantumRefPersistentModel>();
        private List<SettingsPersistentModel> settingsCollection = new List<SettingsPersistentModel>();
        private List<PriceHistoryFramePersistentModel> frames = new List<PriceHistoryFramePersistentModel>();
        private CursorsPersistentModel paymentCursors = null;
        private PendingQuantaPersistentModel pendingQuanta = null;

        public void Connect(string path)
        {
        }

        public ulong GetLastApex()
        {
            return quantaCollection.LastOrDefault()?.Apex ?? 0;
        }

        public ulong GetFirstApex()
        {
            return quantaCollection.FirstOrDefault()?.Apex ?? 0;
        }

        public IEnumerable<AccountPersistentModel> LoadAccounts()
        {
            return accountsCollection.ToList();
        }

        public AccountPersistentModel LoadAccount(byte[] accountPubkey)
        {
            return accountsCollection.FirstOrDefault(a => a.AccountPubkey.AsSpan().SequenceEqual(accountPubkey));
        }

        public CursorsPersistentModel LoadCursors()
        {
            return paymentCursors;
        }

        public SettingsPersistentModel LoadSettings(ulong fromApex)
        {
            return settingsCollection.OrderByDescending(s => s.Apex).FirstOrDefault(s => s.Apex <= fromApex);
        }

        public QuantumPersistentModel LoadQuantum(ulong apex)
        {
            return quantaCollection.FirstOrDefault(q => q.Apex == apex);
        }

        public List<QuantumPersistentModel> LoadQuanta(params ulong[] apexes)
        {
            var quanta = quantaCollection.AsEnumerable();
            if (apexes.Length > 0)
                quanta = quanta.Where(q => apexes.Contains(q.Apex));
            return quanta.OrderBy(q => q.Apex).ToList();
        }

        public IEnumerable<QuantumPersistentModel> LoadQuantaAboveApex(ulong apex, int count)
        {
            return quantaCollection.OrderBy(q => q.Apex).SkipWhile(q => q.Apex <= apex).Take(count).ToList();
        }

        public List<AccountQuantumDTO> LoadQuantaForAccount(byte[] account, ulong fromApex, int limit, QueryOrder order = QueryOrder.Asc)
        {
            var quantaRefs = quantaRefCollection.Where(q => q.Account.AsSpan().SequenceEqual(account));
            if (order == QueryOrder.Asc)
            {
                quantaRefs = quantaRefs.OrderBy(q => q.Apex);
                if (fromApex > 0)
                    quantaRefs = quantaRefs.Where(q => q.Apex > fromApex);
            }
            else
            {
                quantaRefs = quantaRefs.OrderByDescending(q => q.Apex);
                if (fromApex > 0)
                    quantaRefs = quantaRefs.Where(q => q.Apex < fromApex);
            }

            var accountQuanta = quantaRefs.Take(limit).Select(a => a.Apex).ToArray();
            if (accountQuanta.Length < 1)
                return new List<AccountQuantumDTO>();
            return LoadQuanta(accountQuanta)
                .Select(q => new AccountQuantumDTO
                {
                    Quantum = q,
                    IsInitiator = quantaRefs.First(qr => qr.Apex == q.Apex).IsQuantumInitiator
                })
                .ToList();
        }

        public IEnumerable<PriceHistoryFramePersistentModel> GetPriceHistory(string asset, int period, int cursorTimeStamp, int toUnixTimeStamp)
        {
            return frames.Where(f =>
                    f.Period == period
                    && f.Market == asset
                    && f.Timestamp >= cursorTimeStamp
                    && f.Timestamp <= toUnixTimeStamp)
                .OrderBy(f => f.Timestamp)
                .ToList();
        }

        public void SaveBatch(List<IPersistentModel> batch)
        {
            foreach (var model in batch)
            {
                switch (model)
                {
                    case AccountPersistentModel account:
                        {
                            var pubkey = account.AccountPubkey;
                            var current = accountsCollection.FirstOrDefault(a => a.AccountPubkey.SequenceEqual(account.AccountPubkey));
                            if (current != null)
                                accountsCollection.Insert(accountsCollection.IndexOf(current), account);
                            else
                                accountsCollection.Add(account);
                        }
                        break;
                    case QuantumPersistentModel quantum:
                        quantaCollection.Add(quantum);
                        break;
                    case QuantumRefPersistentModel quantumRef:
                        quantaRefCollection.Add(quantumRef);
                        break;
                    case SettingsPersistentModel settings:
                        settingsCollection.Add(settings);
                        break;
                    case PriceHistoryFramePersistentModel frame:
                        frames.Add(frame);
                        break;
                    case CursorsPersistentModel _paymentCursors:
                        paymentCursors = _paymentCursors;
                        break;
                    case PendingQuantaPersistentModel _pendingQuanta:
                        pendingQuanta = _pendingQuanta;
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown persistent model type.");
                }
            }
        }

        public void Dispose()
        {
        }

        public PendingQuantaPersistentModel LoadPendingQuanta()
        {
            return pendingQuanta;
        }

        public void DeletePendingQuanta()
        {
            pendingQuanta = null;
        }
    }
}