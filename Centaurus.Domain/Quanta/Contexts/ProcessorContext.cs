using Centaurus.Domain.Models;
using Centaurus.Models;
using Centaurus.Xdr;
using Microsoft.Extensions.Logging;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    /// <summary>
    /// In some cases we have a lot of duplicate actions on validation and processing envelopes. 
    /// For optimization, we need a context to store already resulted data.
    /// </summary>
    public class ProcessorContext : ContextualBase
    {
        static byte[] buffer = new byte[256 * 1024];

        public ProcessorContext(ExecutionContext context, Quantum quantum, Account account)
            : base(context)
        {
            Quantum = quantum ?? throw new ArgumentNullException(nameof(quantum));
            InitiatorAccount = account;
        }

        private readonly List<EffectsGroup> effects = new List<EffectsGroup>();

        public ExecutionContext CentaurusContext => Context;

        public Quantum Quantum { get; }

        public Account InitiatorAccount { get; }

        public ulong Apex => Quantum.Apex;

        public QuantaProcessingResult ProcessingResult { get; private set; }

        /// <summary>
        /// Adds effect processor to container
        /// </summary>
        /// <param name="effectProcessor"></param>
        public void AddEffectProcessor(IEffectProcessor<Effect> effectProcessor)
        {
            if (ProcessingResult != null)
                throw new InvalidOperationException("The quantum already processed.");

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
        }

        /// <summary>
        /// Calculates and sets hashes after quantum was handled
        /// </summary>
        public void Complete(QuantumResultMessageBase quantumResultMessage)
        {
            var rawEffects = GetRawEffectsDataContainer();

            //set (if Alpha) or compare with presented property
            EnsureEffectsProof(rawEffects);

            BuildProcessingResult(quantumResultMessage, rawEffects);

            //assign the serialized quantum to the result
            ProcessingResult.ResultMessage.Request = new RequestInfo { Data = ProcessingResult.RawQuantum };
            //assign apex to the result
            ProcessingResult.ResultMessage.Apex = Apex;

            //add quantum data to updates batch and assign persistent model
            ProcessingResult.PersistentModel = Context.PendingUpdatesManager.AddQuantum(ProcessingResult);
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

        void BuildProcessingResult(QuantumResultMessageBase quantumResultMessage, List<RawEffectsDataContainer> rawEffects)
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

            var result = new QuantaProcessingResult
            {
                Quantum = Quantum,
                RawQuantum = rawQuantum,
                QuantumHash = quantumHash,
                PayloadHash = payloadHash,
                Timestamp = Quantum.Timestamp,
                Initiator = InitiatorAccount?.Pubkey,
                Effects = rawEffects,
                ResultMessage = quantumResultMessage,
                CurrentAuditorId = Context.AuditorPubKeys[Context.Settings.KeyPair]
            };
            //get constellation effects
            var constellationEffects = effects.FirstOrDefault(e => e.Account == null);
            if (constellationEffects != null)
                foreach (var effect in constellationEffects.Effects)
                {
                    if (effect is ConstellationUpdateEffect)
                        result.HasSettingsUpdate = true;
                    else if (effect is CursorUpdateEffect)
                        result.HasCursorUpdate = true;
                    else
                        continue;
                    if (result.HasSettingsUpdate && result.HasCursorUpdate)
                        break;
                }

            ProcessingResult = result;
        }

    }
}
