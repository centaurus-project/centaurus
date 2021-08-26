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
                Quantum = (Quantum)XdrConverter.Deserialize<Message>(quantumPersistentModel.RawQuantum),
                Signatures = new List<AuditorSignatureInternal>()
            };

            foreach (var signature in quantumPersistentModel.Signatures)
            {
                quantum.Signatures.Add(new AuditorSignatureInternal
                {
                    AuditorId = signature.AuditorId,
                    PayloadSignature = new TinySignature { Data = signature.PayloadSignature },
                    TxSignature = signature.TxSignature,
                    TxSigner = signature.TxSigner
                });
            }

            return quantum;
        }
    }
}
