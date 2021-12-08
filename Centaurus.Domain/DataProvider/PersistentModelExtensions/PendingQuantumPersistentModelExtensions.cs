using Centaurus.Models;
using Centaurus.PersistentStorage;
using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    internal static class PendingQuantumPersistentModelExtensions
    {
        public static CatchupQuantaBatchItem ToCatchupQuantaBatchItem(this PendingQuantumPersistentModel quantumModel)
        {
            var quantum = new CatchupQuantaBatchItem
            {
                Quantum = XdrConverter.Deserialize<Message>(quantumModel.RawQuantum),
                Signatures = new List<NodeSignatureInternal>()
            };

            foreach (var signature in quantumModel.Signatures)
            {
                var nodeSignature = signature.ToNodeSignature();
                quantum.Signatures.Add(nodeSignature);
            }

            return quantum;
        }

        public static NodeSignatureInternal ToNodeSignature(this SignatureModel signature)
        {
            return new NodeSignatureInternal
            {
                AuditorId = signature.AuditorId,
                PayloadSignature = new TinySignature { Data = signature.PayloadSignature },
                TxSignature = signature.TxSignature,
                TxSigner = signature.TxSigner
            };
        }
    }
}
