using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public class QuantaProcessingResult
    {
        public ulong Initiator { get; set; }

        public ulong Apex { get; set; }

        public byte[] RawQuantum { get; set; }

        public byte[] QuantumHash { get; set; }

        public byte[] PayloadHash { get; set; }

        public List<RawEffectsDataContainer> Effects { get; set; }

        public bool HasCursorUpdate { get; set; }

        public bool HasSettingsUpdate { get; set; }

        public long Timestamp { get; set; }

        public AuditorSignatureInternal CurrentNodeSignature { get; set; }

        public QuantumResultMessageBase ResultMessage { get; set; }

        public uint UpdatesBatchId { get; set; }

        public Dictionary<ulong, RawPubKey> AffectedAccounts { get; set; }

        /// <summary>
        /// Creates message notifications for accounts that were affected by quantum
        /// </summary>
        /// <returns></returns>
        public Dictionary<ulong, Message> GetNotificationMessages(PayloadProof payloadProof)
        {
            if (payloadProof == null)
                throw new ArgumentNullException(nameof(payloadProof));

            var requestAccount = 0ul;
            if (ResultMessage.Quantum is RequestQuantumBase request)
                requestAccount = request.RequestMessage.Account;

            var result = new Dictionary<ulong, EffectsNotification>();
            foreach (var effectsGroup in Effects)
            {
                //initiator will receive result message
                if (effectsGroup.Account == requestAccount || effectsGroup.Account == 0)
                    continue;
                var notification = new EffectsNotification
                {
                    PayloadProof = payloadProof,
                    Effects = Effects.GetAccountEffects(effectsGroup.Account),
                    Apex = Apex
                };
                result.Add(effectsGroup.Account, notification);
            }
            return result.ToDictionary(k => k.Key, v => (Message)v.Value);
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

        public ulong Account => Effects.Account;

        public ulong AccountSequence => Effects.AccountSequence;

        public byte[] RawEffects { get; }

        public byte[] Hash { get; }
    }
}
