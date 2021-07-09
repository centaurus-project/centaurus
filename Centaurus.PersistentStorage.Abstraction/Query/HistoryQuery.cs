using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Centaurus.PersistentStorage
{
    public partial class StorageQuery
    {
        public QuantumPersistentModel LoadQuantum(ulong apex)
        {
            return storage.Get<QuantumPersistentModel>( ApexConverter.EncodeApex(apex));
        }

        public List<QuantumPersistentModel> LoadQuanta(params ulong[] apexes)
        {
            return storage.MutliGet(apexes.Select(a => new QuantumPersistentModel { Apex = a }));
        }

        public StorageIterator<QuantumPersistentModel> LoadQuantaAboveApex(ulong apex)
        {
            return storage.Find<QuantumPersistentModel>(ApexConverter.EncodeApex(apex));
        }

        public List<QuantumPersistentModel> LoadQuantaForAccount(byte[] accountPubkey, ulong apex, int limit, QueryResultsOrder order = QueryResultsOrder.Asc)
        {
            var account = LoadAccount(accountPubkey);
            if (account == null) return null;
            return LoadQuantaForAccount(account.AccountId, apex, limit, order);
        }

        public List<QuantumPersistentModel> LoadQuantaForAccount(ulong accountId, ulong fromApex, int limit, QueryResultsOrder order = QueryResultsOrder.Asc)
        {
            var startFrom = new QuantumRefPersistentModel
            { AccountId = accountId, Apex = fromApex + (ulong)(order == QueryResultsOrder.Asc ? 1 : -1) }.Key;
            var refs = storage.Find<QuantumRefPersistentModel>().Take(limit);
            if (refs.Count == 0) return new List<QuantumPersistentModel>(); //nothing found
            var keys = refs.Select(r => ApexConverter.EncodeApex(r.Apex)).ToArray();
            return storage.MutliGet<QuantumPersistentModel>(keys);
        }
    }
}
