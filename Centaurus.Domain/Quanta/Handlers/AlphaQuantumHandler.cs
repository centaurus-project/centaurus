using Centaurus.Models;
using Centaurus.Xdr;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    //TODO: add Stop method
    public class AlphaQuantumHandler : QuantumHandler<AlphaContext>
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public AlphaQuantumHandler(AlphaContext context)
            :base(context)
        {
        }

        protected override void OnProcessException(HandleItem handleItem, ResultMessage result, Exception exc)
        {
            if (result == null)
                result = handleItem.Quantum.CreateResult(exc);
            Context.OnMessageProcessResult(result);
        }

        MessageEnvelope GetQuantumEnvelope(MessageEnvelope envelope)
        {
            var quantumEnvelope = envelope;
            if (Context.IsAlpha && !(envelope.Message is Quantum))//we need to wrap client request
                quantumEnvelope = new RequestQuantum { RequestEnvelope = envelope }.CreateEnvelope();
            return quantumEnvelope;
        }

        protected override async Task<ResultMessage> HandleQuantum(MessageEnvelope envelope, long timestamp)
        {
            var processor = GetProcessorItem(envelope.Message.MessageType);

            var quantumEnvelope = GetQuantumEnvelope(envelope);

            var quantum = (Quantum)quantumEnvelope.Message;

            quantum.Apex = Context.QuantumStorage.CurrentApex + 1;
            quantum.PrevHash = Context.QuantumStorage.LastQuantumHash;
            quantum.Timestamp = timestamp == default ? DateTime.UtcNow.Ticks : timestamp;//it could be assigned, if this quantum was handled already and we handle it during the server rising

            var effectsContainer = GetEffectProcessorsContainer(quantumEnvelope);

            var processorContext = processor.GetContext(effectsContainer);

            await processor.Validate(processorContext);

            var resultMessageEnvelope = (await processor.Process(processorContext)).CreateEnvelope();

            var resultEffectsContainer = new EffectsContainer { Effects = effectsContainer.Effects };

            var effects = resultEffectsContainer.ToByteArray(buffer.Buffer);

            quantum.EffectsHash = effects.ComputeHash();

            var messageHash = quantumEnvelope.ComputeMessageHash(buffer.Buffer);
            //we need to sign the quantum here to prevent multiple signatures that can occur if we sign it when sending
            quantumEnvelope.Signatures.Add(messageHash.Sign(Context.Settings.KeyPair));

            var resultMessageHash = resultMessageEnvelope.ComputeMessageHash(buffer.Buffer);
            resultMessageEnvelope.Signatures.Add(resultMessageHash.Sign(Context.Settings.KeyPair));

            ((AlphaContext)Context).AuditResultManager.Register(resultMessageEnvelope, resultMessageHash, processor.GetNotificationMessages(processorContext));

            Context.QuantumStorage.AddQuantum(quantumEnvelope, messageHash);

            effectsContainer.Complete(buffer.Buffer);

            logger.Trace($"Message of type {envelope.Message} with apex {quantum.Apex} is handled.");

            return (ResultMessage)resultMessageEnvelope.Message;
        }
    }
}
