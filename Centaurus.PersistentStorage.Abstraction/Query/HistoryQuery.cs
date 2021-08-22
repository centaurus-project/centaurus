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
            return storage.Get<QuantumPersistentModel>(ApexConverter.EncodeApex(apex));
        }

        public List<QuantumPersistentModel> LoadQuanta(params ulong[] apexes)
        {
            return storage.MutliGet(apexes.Select(a => new QuantumPersistentModel { Apex = a }));
        }

        public StorageIterator<QuantumPersistentModel> LoadQuantaAboveApex(ulong apex)
        {
            return storage.Find<QuantumPersistentModel>(ApexConverter.EncodeApex(apex));
        }

        public List<AccountQuantumDTO> LoadQuantaForAccount(byte[] accountPubkey, ulong apex, int limit, QueryOrder order = QueryOrder.Asc)
        {
            var account = LoadAccount(accountPubkey);
            if (account == null) return null;
            return LoadQuantaForAccount(account.AccountId, apex, limit, order);
        }

        public List<AccountQuantumDTO> LoadQuantaForAccount(ulong accountId, ulong fromApex, int limit, QueryOrder order = QueryOrder.Asc)
        {
            var startFrom = new QuantumRefPersistentModel
            { AccountId = accountId, Apex = fromApex + (ulong)(order == QueryOrder.Asc ? 1 : -1) }.Key;
            var cursor = storage.Find<QuantumRefPersistentModel>(startFrom);
            if (order == QueryOrder.Desc)
            {
                cursor.Reverse();
            }
            var refs = cursor.Take(limit).ToDictionary(qr => qr.Apex, qr => qr);
            if (refs.Count == 0)
                return new List<AccountQuantumDTO>(); //nothing found
            var keys = refs.Select(r => ApexConverter.EncodeApex(r.Key)).ToArray();
            return storage.MutliGet<QuantumPersistentModel>(keys)
                .Select(q => new AccountQuantumDTO { Quantum = q, IsInitiator = refs[q.Apex].IsQuantumInitiator })
                .ToList();
        }
    }
}
