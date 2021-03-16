using Centaurus.DAL.Models;
using Centaurus.Models;
using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public static class QuantumModelExtensions
    {
        public static QuantumModel FromQuantum(MessageEnvelope quantum, int[] accounts, byte[] effects)
        {
            if (quantum == null)
                throw new ArgumentNullException(nameof(quantum));
            if (accounts == null)
                throw new ArgumentNullException(nameof(accounts));
            if (effects == null)
                throw new ArgumentNullException(nameof(effects));

            var quantumMessage = (Quantum)quantum.Message;

            return new QuantumModel
            {
                Apex = quantumMessage.Apex,
                Accounts = accounts,
                RawQuantum = XdrConverter.Serialize(quantum),
                Type = (int)quantumMessage.MessageType,
                TimeStamp = quantumMessage.Timestamp,
                Effects = effects
            };
        }

        public static (MessageEnvelope envelope, EffectsContainer effects) ToQuantumData(this QuantumModel quantum, AccountStorage accountStorage = null)
        {
            if (quantum == null)
                throw new ArgumentNullException(nameof(quantum));

            var envelope = XdrConverter.Deserialize<MessageEnvelope>(quantum.RawQuantum);

            var effects = XdrConverter.Deserialize<EffectsContainer>(quantum.Effects);
            if (accountStorage != null)
                foreach (var effect in effects.Effects)
                {
                    if (effect.Account == 0)
                        continue;
                    effect.AccountWrapper = accountStorage.GetAccount(effect.Account);
                }

            return (envelope, effects);
        }
    }
}
