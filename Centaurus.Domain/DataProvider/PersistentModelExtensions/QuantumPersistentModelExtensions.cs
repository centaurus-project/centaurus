using Centaurus.Models;
using Centaurus.PersistentStorage;
using Centaurus.Xdr;
using System.Collections.Generic;
using System.Linq;

namespace Centaurus.Domain
{
    public static class QuantumPersistentModelExtensions
    {
        public static SyncQuantaBatchItem ToBatchItemQuantum(this QuantumPersistentModel quantumPersistentModel)
        {
            return new SyncQuantaBatchItem
            {
                Quantum = (Quantum)XdrConverter.Deserialize<Message>(quantumPersistentModel.RawQuantum),
                AlphaSignature = quantumPersistentModel.Signatures.First().ToDomainModel()
            };
        }

        public static QuantumSignatures ToQuantumSignatures(this QuantumPersistentModel quantumPersistentModel)
        {
            return new QuantumSignatures
            {
                Apex = quantumPersistentModel.Apex,
                Signatures = quantumPersistentModel.Signatures.Skip(1).Select(s => s.ToDomainModel()).ToList()
            };
        }
    }
}
