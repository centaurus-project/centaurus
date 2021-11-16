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
            return storage.Get<QuantumPersistentModel>(UlongConverter.Encode(apex));
        }

        public List<QuantumPersistentModel> LoadQuanta(params ulong[] apexes)
        {
            return storage.MutliGet(apexes.Select(a => new QuantumPersistentModel { Apex = a }));
        }

        public StorageIterator<QuantumPersistentModel> LoadQuantaAboveApex(ulong apex, int limit)
        {
            var iterator = storage.Find<QuantumPersistentModel>()
                .From(UlongConverter.Encode(apex));
            if (limit > 0)
                iterator = iterator.To(UlongConverter.Encode(apex + (ulong)limit));
            return iterator;
        }

        public List<AccountQuantumDTO> LoadQuantaForAccount(byte[] account, ulong fromApex, int limit, QueryOrder order = QueryOrder.Asc)
        {
            var startFrom = new QuantumRefPersistentModel
            { Account = account, Apex = fromApex }.Key;
            var refs = storage.Find<QuantumRefPersistentModel>(account, order)
                .From(startFrom)
                .Take(limit)
                .ToDictionary(qr => qr.Apex, qr => qr);
            if (refs.Count == 0)
                return new List<AccountQuantumDTO>(); //nothing found
            var keys = refs.Select(r => UlongConverter.Encode(r.Key)).ToArray();
            return storage.MutliGet<QuantumPersistentModel>(keys)
                .Select(q => new AccountQuantumDTO { Quantum = q, IsInitiator = refs[q.Apex].IsQuantumInitiator })
                .ToList();
        }
    }
}
