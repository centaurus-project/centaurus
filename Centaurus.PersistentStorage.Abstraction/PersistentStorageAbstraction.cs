using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Centaurus.PersistentStorage.Abstraction
{
    public class PersistentStorageAbstraction: IPersistentStorage
    {
        private PersistentStorage storage;
        private PersistentStorage Storage => storage ?? throw new Exception("Storage is not connected.");

        private StorageQuery query;
        public StorageQuery Query => query ?? throw new Exception("Storage is not connected.");

        public void Connect(string path)
        {
            storage = new PersistentStorage(path);
            query = new StorageQuery(storage);
        }

        public ulong GetLastApex()
        {
            return Query.GetLastApex();
        }

        public IEnumerable<AccountPersistentModel> LoadAccounts()
        {
            return Query.LoadAccounts();
        }

        public AccountPersistentModel LoadAccount(byte[] accountPubkey)
        {
            return Query.LoadAccount(accountPubkey);
        }

        public SettingsPersistentModel LoadSettings(ulong fromApex)
        {
            return Query.LoadSettings(fromApex);
        }

        public QuantumPersistentModel LoadQuantum(ulong apex)
        {
            return Query.LoadQuantum(apex);
        }

        public List<QuantumPersistentModel> LoadQuanta(params ulong[] apexes)
        {
            return Query.LoadQuanta(apexes);
        }

        public IEnumerable<QuantumPersistentModel> LoadQuantaAboveApex(ulong apex)
        {
            return Query.LoadQuantaAboveApex(apex);
        }

        public List<AccountQuantumDTO> LoadQuantaForAccount(byte[] accountPubkey, ulong apex, int limit, QueryOrder order = QueryOrder.Asc)
        {
            return Query.LoadQuantaForAccount(accountPubkey, apex, limit, order);
        }

        public CursorsPersistentModel LoadCursors()
        {
            return Query.LoadCursors() ?? new CursorsPersistentModel { Cursors = new Dictionary<string, string>() };
        }

        public IEnumerable<PriceHistoryFramePersistentModel> GetPriceHistory(string market, int period, int from, int to)
        {
            return Query.GetPriceHistory(market, period, from, to);
        }

        public void SaveBatch(List<IPersistentModel> batch)
        {
            Storage.SaveBatch(batch);
        }

        public void Dispose()
        {
            storage?.Dispose();
        }
    }
}