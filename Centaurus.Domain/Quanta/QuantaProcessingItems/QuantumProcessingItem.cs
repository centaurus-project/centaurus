using Centaurus.Domain.Models;
using Centaurus.Models;
using Centaurus.PersistentStorage;
using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuantumProcessingTaskSource = System.Threading.Tasks.TaskCompletionSource<Centaurus.Models.QuantumResultMessageBase>;

namespace Centaurus.Domain
{
    public class QuantumProcessingItem
    {
        static byte[] buffer = new byte[256 * 1024];

        public QuantumProcessingItem(Quantum quantum, Task<bool> signatureValidation)
        {
            Quantum = quantum ?? throw new ArgumentNullException(nameof(quantum));
            SignatureValidationTask = signatureValidation ?? throw new ArgumentNullException(nameof(signatureValidation));
        }

        public Quantum Quantum { get; }

        public Task<bool> SignatureValidationTask { get; }

        public Account Initiator { get; set; }

        public ulong Apex => Quantum.Apex;

        public byte[] RawQuantum { get; private set; }

        public byte[] QuantumHash { get; private set; }

        public byte[] PayloadHash { get; private set; }

        public List<RawEffectsDataContainer> Effects { get; private set; }

        public bool HasCursorUpdate { get; private set; }

        public bool HasSettingsUpdate { get; private set; }

        public QuantumResultMessageBase ResultMessage { get; private set; }

        public QuantumPersistentModel PersistentModel { get; private set; }

        public int CurrentAuditorId { get; private set; }

        public Task<QuantumResultMessageBase> OnProcessed => onProcessedTaskSource.Task;

        QuantumProcessingTaskSource onProcessedTaskSource = new QuantumProcessingTaskSource();

        public void SetException(Exception exc)
        {
            onProcessedTaskSource.TrySetException(exc);
        }

        public void Processed()
        {
            onProcessedTaskSource.TrySetResult(ResultMessage);
        }

        /// <summary>
        /// Creates message notifications for accounts that were affected by quantum
        /// </summary>
        /// <returns></returns>
        public Dictionary<RawPubKey, EffectsNotification> GetNotificationMessages()
        {
            var requestHashInfo = new RequestHashInfo { Data = QuantumHash };

            var result = new Dictionary<RawPubKey, EffectsNotification>();
            foreach (var effectsGroup in Effects)
            {
                //initiator will receive result message
                if (effectsGroup.Account == null || (Initiator != null && effectsGroup.Account.Equals(Initiator.Pubkey)))
                    continue;
                var notification = new EffectsNotification
                {
                    Request = requestHashInfo,
                    PayloadProof = ResultMessage.PayloadProof,
                    Effects = Effects.GetAccountEffects(effectsGroup.Account),
                    Apex = Apex
                };
                result.Add(effectsGroup.Account, notification);
            }
            return result.ToDictionary(k => k.Key, v => v.Value);
        }

        private readonly List<EffectsGroup> effects = new List<EffectsGroup>();

        /// <summary>
        /// Adds effect processor to container
        /// </summary>
        /// <param name="effectProcessor"></param>
        public void AddEffectProcessor(IEffectProcessor<Effect> effectProcessor)
        {
            var accountPubKey = default(RawPubKey);
            //get account id for account effects
            if (effectProcessor.Effect is AccountEffect clientEffect)
                accountPubKey = clientEffect.Account;

            //get or add account effects group
            var effectsGroup = effects.FirstOrDefault(e => accountPubKey == null && e.Account == null || e.Account == accountPubKey);
            if (effectsGroup == null)
            {
                var accountSequence = 0ul;
                //increment and set account sequence
                if (effectProcessor is AccountEffectProcessor accountEffectProcessor)
                {
                    accountSequence = ++accountEffectProcessor.Account.AccountSequence;
                    accountPubKey = accountEffectProcessor.Account.Pubkey;
                }
                effectsGroup = new EffectsGroup
                {
                    Account = accountPubKey,
                    AccountSequence = accountSequence,
                    Effects = new List<Effect>()
                };
                effects.Add(effectsGroup);
            }
            //register new effect
            effectsGroup.Effects.Add(effectProcessor.Effect);
            //commit the effect
            effectProcessor.CommitEffect();

            if (!HasSettingsUpdate && effectProcessor.Effect is ConstellationUpdateEffect)
                HasSettingsUpdate = true;
            else if (!HasCursorUpdate && effectProcessor.Effect is CursorUpdateEffect)
                HasCursorUpdate = true;
        }

        /// <summary>
        /// Calculates and sets hashes after quantum was handled
        /// </summary>
        public void Complete(ExecutionContext context, QuantumResultMessageBase quantumResultMessage)
        {
            var rawEffects = GetRawEffectsDataContainer();

            //set (if Alpha) or compare with presented property
            EnsureEffectsProof(rawEffects);

            BuildProcessingResult(context, quantumResultMessage, rawEffects);

            //add quantum data to updates batch and assign persistent model
            PersistentModel = context.PendingUpdatesManager.AddQuantum(this);
        }

        List<RawEffectsDataContainer> GetRawEffectsDataContainer()
        {
            return effects.Select(e =>
            {
                var rawEffectsGroup = XdrConverter.Serialize(e, buffer);
                return new RawEffectsDataContainer(e, rawEffectsGroup, rawEffectsGroup.ComputeHash(buffer));
            }).ToList();
        }

        void EnsureEffectsProof(List<RawEffectsDataContainer> rawEffects)
        {
            //compound effects hash
            var effectsProof = rawEffects
                .SelectMany(e => e.Hash)
                .ToArray()
                .ComputeHash(buffer); //compute hash of concatenated effects groups hashes

            //if EffectsProof is null set it, otherwise validate equality
            if (Quantum.EffectsProof == null)
                Quantum.EffectsProof = effectsProof;
            else
            {
                if (!effectsProof.AsSpan().SequenceEqual(Quantum.EffectsProof) && !EnvironmentHelper.IsTest)
                {
                    throw new Exception($"Effects hash for quantum {Apex} is not equal to provided by Alpha.");
                }
            }
        }

        void BuildProcessingResult(ExecutionContext context, QuantumResultMessageBase quantumResultMessage, List<RawEffectsDataContainer> rawEffects)
        {
            if (quantumResultMessage == null)
                throw new ArgumentNullException(nameof(quantumResultMessage));

            if (rawEffects == null)
                throw new ArgumentNullException(nameof(rawEffects));

            //serialize quantum
            using var writer = new XdrBufferWriter(buffer);
            XdrConverter.Serialize(Quantum, writer);
            var rawQuantum = writer.ToArray();

            //compute quantum hash
            var quantumHash = rawQuantum.ComputeHash();

            //compute payload hash
            var payloadHash = ByteArrayExtensions.ComputeQuantumPayloadHash(Apex, quantumHash, Quantum.EffectsProof);

            //set computed properties
            RawQuantum = rawQuantum;
            QuantumHash = quantumHash;
            PayloadHash = payloadHash;
            Effects = rawEffects;
            ResultMessage = quantumResultMessage;
            CurrentAuditorId = context.AuditorPubKeys[context.Settings.KeyPair];

            //assign the serialized quantum to the result
            ResultMessage.Request = new RequestInfo { Data = RawQuantum };
            //assign apex to the result
            ResultMessage.Apex = Apex;
            //assign account's effects
            ResultMessage.Effects = Effects.GetAccountEffects(Initiator?.Pubkey);
            //assign payload proof
            ResultMessage.PayloadProof = new PayloadProof { PayloadHash = payloadHash, Signatures = new List<TinySignature>() };
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
