using Centaurus.Models;
using Centaurus.Xdr;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    //TODO: add Stop method
    public class AlphaQuantumHandler : QuantumHandler<AlphaContext>
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public AlphaQuantumHandler(AlphaContext context)
            : base(context)
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
            var quantumEnvelope = GetQuantumEnvelope(envelope);

            var quantum = (Quantum)quantumEnvelope.Message;

            quantum.Apex = Context.QuantumStorage.CurrentApex + 1;
            quantum.PrevHash = Context.QuantumStorage.LastQuantumHash;
            quantum.Timestamp = timestamp == default ? DateTime.UtcNow.Ticks : timestamp;//it could be assigned, if this quantum was handled already and we handle it during the server rising


            var result = await ProcessQuantum(quantumEnvelope);

            quantum.EffectsHash = result.Effects.Hash;

            var messageHash = quantumEnvelope.ComputeMessageHash(buffer.Buffer);
            //we need to sign the quantum here to prevent multiple signatures that can occur if we sign it when sending
            quantumEnvelope.Signatures.Add(messageHash.Sign(Context.Settings.KeyPair));

            RegisterResult(result);

            Context.QuantumStorage.AddQuantum(quantumEnvelope, messageHash);

            result.EffectProcessorsContainer.Complete(buffer.Buffer);

            logger.Trace($"Message of type {quantum.MessageType} with apex {quantum.Apex} is handled.");

            return result.ResultMessage;
        }

        private void RegisterResult(QuantumProcessingResult result)
        {
            var resultEnvelope = result.ResultMessage.CreateEnvelope();
            var resultMessageHash = result.ResultMessage.CreateEnvelope().ComputeMessageHash(buffer.Buffer);
            resultEnvelope.Signatures.Add(resultMessageHash.Sign(Context.Settings.KeyPair));

            Context.AuditResultManager.Register(resultEnvelope, resultMessageHash, result.EffectProcessorsContainer.GetNotificationMessages());
        }
    }
}
