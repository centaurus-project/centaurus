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
        public static QuantumModel FromQuantumContainer(MessageEnvelope quantum, List<Effect> effects, int[] accounts, byte[] buffer)
        {
            if (quantum == null)
                throw new ArgumentNullException(nameof(quantum));
            if (accounts == null)
                throw new ArgumentNullException(nameof(accounts));
            if (effects == null)
                throw new ArgumentNullException(nameof(effects));

            var quantumMessage = (Quantum)quantum.Message;
            using var writer = new XdrBufferWriter(buffer);
            XdrConverter.Serialize(new QuantumContainer { Quantum = quantum, Effects = effects }, writer);
            return new QuantumModel
            {
                Apex = quantumMessage.Apex,
                Accounts = accounts,
                Bin = writer.ToArray()
            };
        }

        public static QuantumContainer ToQuantumContainer(this QuantumModel quantum, AccountStorage accountStorage = null)
        {
            if (quantum == null)
                throw new ArgumentNullException(nameof(quantum));

            var quantumContainer = XdrConverter.Deserialize<QuantumContainer>(quantum.Bin);
            if (accountStorage != null)
                foreach (var effect in quantumContainer.Effects)
                {
                    if (effect.Account == 0)
                        continue;
                    effect.AccountWrapper = accountStorage.GetAccount(effect.Account);
                }

            return quantumContainer;
        }
    }
}
