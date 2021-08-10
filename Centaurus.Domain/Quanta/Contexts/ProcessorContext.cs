using Centaurus.Domain.Models;
using Centaurus.Models;
using Centaurus.PersistentStorage;
using Centaurus.Xdr;
using System;
using System.Collections.Generic;
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

        public ProcessorContext(ExecutionContext context, Quantum quantum, AccountWrapper account)
            : base(context)
        {
            Quantum = quantum ?? throw new ArgumentNullException(nameof(quantum));
            SourceAccount = account;
        }

        private readonly List<ContextEffect> effects = new List<ContextEffect>();

        public ExecutionContext CentaurusContext => Context;

        public Quantum Quantum { get; }

        public AccountWrapper SourceAccount { get; }

        public ulong Apex => Quantum.Apex;

        public bool IsCompleted => EffectsHash != null;

        public byte[] EffectsHash { get; private set; }

        /// <summary>
        /// Adds effect processor to container
        /// </summary>
        /// <param name="effectProcessor"></param>
        public void AddEffectProcessor(IEffectProcessor<Effect> effectProcessor)
        {
            if (IsCompleted)
                throw new InvalidOperationException("The quantum already processed.");

            using var writer = new XdrBufferWriter(buffer);
            XdrConverter.Serialize(effectProcessor.Effect, writer);
            var rawEffect = writer.ToArray();
            effects.Add(new ContextEffect(rawEffect.ComputeHash(), effectProcessor.Effect, rawEffect));
            effectProcessor.CommitEffect();
        }

        public void ComputeEffectsHash()
        {
            EffectsHash = effects.SelectMany(h => h.Hash)
                .ToArray()
                .ComputeHash(buffer);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>Updates batch id</returns>
        public uint PersistQuantum()
        {
            if (!IsCompleted)
                throw new InvalidOperationException("The quantum must be processed before persistence.");


            var result = new ProcessingResult
            {
                Apex = Apex,
                QuantumEnvelope = XdrConverter.Serialize(Quantum),
                Signature = EffectsHash.Sign(Context.Settings.KeyPair).Data,
                Timestamp = Quantum.Timestamp
            };


            foreach (var effect in effects)
            {
                if (effect.Effect.Account > 0) //ConstellationUpdateEffect and CursorUpdateEffect cannot have account
                    result.Accounts.Add(effect.Effect.Account);
                else if (effect.Effect is ConstellationUpdateEffect)
                    result.HasSettingsUpdate = true;
                else if (effect.Effect is CursorUpdateEffect)
                    result.HasCursorUpdate = true;
                result.Effects.Add(effect.RawEffect);
            }

            return Context.PendingUpdatesManager.AddQuantum(result);
        }

        public List<Effect> GetClientEffects()
        {
            if (Quantum is RequestQuantum request)
                return effects.Where(e => e.Effect.Account == request.RequestMessage.Account).Select(e => e.Effect).ToList();
            return effects.Select(e => e.Effect).ToList();
        }

        /// <summary>
        /// Creates message notifications for accounts that were affected by quantum
        /// </summary>
        /// <returns></returns>
        public Dictionary<ulong, Message> GetNotificationMessages()
        {
            var requestAccount = 0ul;
            if (Quantum is RequestQuantum request)
                requestAccount = request.RequestMessage.Account;

            var result = new Dictionary<ulong, EffectsNotification>();
            foreach (var effect in effects)
            {
                if (effect.Effect.Account == 0 || effect.Effect.Account == requestAccount)
                    continue;
                if (!result.TryGetValue(effect.Effect.Account, out var effectsNotification))
                {
                    effectsNotification = new EffectsNotification { ClientEffects = new List<Effect>(), Apex = Apex };
                    result.Add(effect.Effect.Account, effectsNotification);
                }
                effectsNotification.ClientEffects.Add(effect.Effect);
            }
            return result.ToDictionary(k => k.Key, v => (Message)v.Value);
        }

        public EffectsProof GetEffectProof()
        {
            return new EffectsProof 
            { 
                Hashes = effects.Select(e => new Hash { Data = e.Hash }).ToList(), 
                Signatures = new List<TinySignature>()
            };
        }

        class ContextEffect
        {
            public ContextEffect(byte[] hash, Effect effect, byte[] rawEffect)
            {
                Hash = hash ?? throw new ArgumentNullException(nameof(hash));
                Effect = effect ?? throw new ArgumentNullException(nameof(effect));
                RawEffect = rawEffect ?? throw new ArgumentNullException(nameof(rawEffect));
            }

            public byte[] Hash { get; }

            public Effect Effect { get; }

            public byte[] RawEffect { get; }
        }

        public class ProcessingResult
        {
            public ulong Apex { get; set; }

            public byte[] QuantumEnvelope { get; set; }

            public List<byte[]> Effects { get; } = new List<byte[]>();

            public HashSet<ulong> Accounts { get; } = new HashSet<ulong>();

            public bool HasCursorUpdate { get; set; }

            public bool HasSettingsUpdate { get; set; }

            public byte[] Signature { get; set; }

            public long Timestamp { get; set; }
        }
    }
}
