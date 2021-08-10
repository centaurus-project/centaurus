using Centaurus.Models;
using Centaurus.PersistentStorage;
using Centaurus.Xdr;
using System.Collections.Generic;
using System.Linq;

namespace Centaurus.Domain
{
    public static class QuantumPersistentModelExtensions
    {
        public static InProgressQuantum ToInProgressQuantum(this QuantumPersistentModel quantumPersistentModel)
        {
            var quantum = new InProgressQuantum
            {
                QuantumEnvelope = XdrConverter.Deserialize<MessageEnvelope>(quantumPersistentModel.RawQuantum),
                Signatures = new List<AuditorSignature>()
            };

            foreach (var signature in quantumPersistentModel.Signatures)
            {
                quantum.Signatures.Add(new AuditorSignature
                {
                    EffectsSignature = new TinySignature { Data = signature.EffectsSignature },
                    TxSignature = signature.TxSignature,
                    TxSigner = signature.TxSigner
                });
            }

            return quantum;
        }
    }
}
