using Centaurus.Models;
using Centaurus.PersistentStorage;
using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Centaurus.Domain
{
    public static class QuantumPersistentModelExtensions
    {
        public static QuantumPersistentModel ToPersistentModel(this MessageEnvelope quantum, List<Effect> effects, EffectsProof effectsProof, byte[] buffer)
        {
            if (quantum == null)
                throw new ArgumentNullException(nameof(quantum));
            if (effects == null)
                throw new ArgumentNullException(nameof(effects));


            var quantumMessage = (Quantum)quantum.Message;
            return new QuantumPersistentModel
            {
                Apex = quantumMessage.Apex,
                Effects = SerializeEffects(effects, buffer),
                RawQuantum = SerializeQuantum(quantum, buffer),
                Proof = effectsProof.ToPersistentModel(),
                TimeStamp = quantumMessage.Timestamp
            };
        }

        private static byte[] SerializeQuantum(MessageEnvelope quantum, byte[] buffer)
        {
            using var writer = new XdrBufferWriter(buffer);

            XdrConverter.Serialize(quantum, writer);

            return writer.ToArray();
        }

        private static List<byte[]> SerializeEffects(List<Effect> effects, byte[] buffer)
        {
            return effects.Select(e =>
            {
                using var writer = new XdrBufferWriter(buffer);
                XdrConverter.Serialize(e, writer);
                return writer.ToArray();
            }).ToList();
        }
    }
}
