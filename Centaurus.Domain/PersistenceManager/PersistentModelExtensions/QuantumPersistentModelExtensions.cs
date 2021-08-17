using Centaurus.Models;
using Centaurus.PersistentStorage;
using Centaurus.Xdr;
using System.Collections.Generic;
using System.Linq;

namespace Centaurus.Domain
{
    public static class QuantumPersistentModelExtensions
    {
        public static PendingQuantum ToInProgressQuantum(this QuantumPersistentModel quantumPersistentModel)
        {
            var quantum = new PendingQuantum
            {
                Quantum = XdrConverter.Deserialize<Quantum>(quantumPersistentModel.RawQuantum),
                Signatures = new List<AuditorSignature>()
            };

            foreach (var signature in quantumPersistentModel.Signatures)
            {
                quantum.Signatures.Add(new AuditorSignature
                {
                    PayloadSignature = new TinySignature { Data = signature.PayloadSignature },
                    TxSignature = signature.TxSignature,
                    TxSigner = signature.TxSigner
                });
            }

            return quantum;
        }
    }
}
