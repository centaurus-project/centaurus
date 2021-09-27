using Centaurus.Models;
using Centaurus.PersistentStorage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public class QuantaProcessingResult
    {
        public Quantum Quantum { get; set; }

        public RawPubKey Initiator { get; set; }

        public ulong Apex => Quantum.Apex;

        public byte[] RawQuantum { get; set; }

        public byte[] QuantumHash { get; set; }

        public byte[] PayloadHash { get; set; }

        public List<RawEffectsDataContainer> Effects { get; set; }

        public bool HasCursorUpdate { get; set; }

        public bool HasSettingsUpdate { get; set; }

        public long Timestamp { get; set; }

        public QuantumResultMessageBase ResultMessage { get; set; }

        public QuantumPersistentModel PersistentModel { get; set; }

        public int CurrentAuditorId { get; set; }

        /// <summary>
        /// Creates message notifications for accounts that were affected by quantum
        /// </summary>
        /// <returns></returns>
        public Dictionary<RawPubKey, EffectsNotification> GetNotificationMessages(RawPubKey initiator, RequestHashInfo requestHashInfo, PayloadProof payloadProof)
        {
            if (payloadProof == null)
                throw new ArgumentNullException(nameof(payloadProof));

            var result = new Dictionary<RawPubKey, EffectsNotification>();
            foreach (var effectsGroup in Effects)
            {
                //initiator will receive result message
                if (effectsGroup.Account == null || (initiator != null && effectsGroup.Account.Equals(initiator)))
                    continue;
                var notification = new EffectsNotification
                {
                    Request = requestHashInfo,
                    PayloadProof = payloadProof,
                    Effects = Effects.GetAccountEffects(effectsGroup.Account),
                    Apex = Apex
                };
                result.Add(effectsGroup.Account, notification);
            }
            return result.ToDictionary(k => k.Key, v => v.Value);
        }
    }

    public class RawEffectsDataContainer
    {
        public RawEffectsDataContainer(EffectsGroup effectsGroup, byte[] rawEffects, byte[] hash)
        {
            Effects = effectsGroup ?? throw new ArgumentNullException(nameof(effectsGroup));
            RawEffects = rawEffects ?? throw new ArgumentNullException(nameof(rawEffects));
            Hash = hash ?? throw new ArgumentNullException(nameof(hash));
        }

        public EffectsGroup Effects { get; }

        public RawPubKey Account => Effects.Account;

        public ulong AccountSequence => Effects.AccountSequence;

        public byte[] RawEffects { get; }

        public byte[] Hash { get; }
    }
}
