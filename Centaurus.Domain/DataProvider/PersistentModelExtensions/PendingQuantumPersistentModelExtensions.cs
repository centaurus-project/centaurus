using Centaurus.Models;
using Centaurus.PersistentStorage;
using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class PendingQuantumPersistentModelExtensions
    {
        public static PendingQuantum ToDomainModel(this PendingQuantumPersistentModel quantumModel)
        {
            var quantum = new PendingQuantum
            {
                Quantum = (Quantum)XdrConverter.Deserialize<Message>(quantumModel.RawQuantum),
                Signatures = new List<AuditorSignatureInternal>()
            };

            foreach (var signature in quantumModel.Signatures)
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
