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
                Quantum = (Quantum)XdrConverter.Deserialize<Message>(quantumPersistentModel.RawQuantum)
            };
        }

        public static MajoritySignaturesBatchItem ToMajoritySignatures(this QuantumPersistentModel quantumPersistentModel)
        {
            return new MajoritySignaturesBatchItem
            {
                Apex = quantumPersistentModel.Apex,
                Signatures = quantumPersistentModel.Signatures.Select(s => s.ToDomainModel()).ToList()
            };
        }
    }
}
