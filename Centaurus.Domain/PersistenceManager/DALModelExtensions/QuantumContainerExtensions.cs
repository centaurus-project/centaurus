using Centaurus.DAL.Models;
using Centaurus.Models;
using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public static class QuantumContainerExtensions
    {
        public static QuantumModel FromQuantumContainer(MessageEnvelope quantum, List<Effect> effects, EffectsProof effectsProof, int[] accounts, byte[] buffer)
        {
            if (quantum == null)
                throw new ArgumentNullException(nameof(quantum));
            if (accounts == null)
                throw new ArgumentNullException(nameof(accounts));
            if (effects == null)
                throw new ArgumentNullException(nameof(effects));

            var quantumMessage = (Quantum)quantum.Message;
            using var writer = new XdrBufferWriter(buffer);
            XdrConverter.Serialize(new QuantumContainer { Quantum = quantum, Effects = effects, EffectsProof = effectsProof }, writer);
            return new QuantumModel
            {
                Apex = quantumMessage.Apex,
                Accounts = accounts,
                Bin = writer.ToArray()
            };
        }

        public static QuantumContainer ToQuantumContainer(this QuantumModel quantum)
        {
            if (quantum == null)
                throw new ArgumentNullException(nameof(quantum));

            return XdrConverter.Deserialize<QuantumContainer>(quantum.Bin);
        }
    }
}
