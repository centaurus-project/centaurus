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

            using var writer = new XdrBufferWriter(buffer);

            XdrConverter.Serialize(quantum, writer);

            var quantumData = writer.ToArray();

            var effectsData = effects.Select(e =>
            {
                XdrConverter.Serialize(e, writer);
                return writer.ToArray();
            }).ToList();

            var quantumMessage = (Quantum)quantum.Message;
            return new QuantumPersistentModel
            {
                Apex = quantumMessage.Apex,
                Effects = effectsData,
                RawQuantum = quantumData,
                Proof = effectsProof.ToPersistentModel(),
                TimeStamp = quantumMessage.Timestamp
            };
        }
    }
}
