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
    public class AuditorQuantumHandler : QuantumHandler
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public AuditorQuantumHandler(AuditorContext context)
            : base(context)
        {
        }

        public long LastAddedQuantumApex { get; private set; }

        public override void Start()
        {
            LastAddedQuantumApex = Context.QuantumStorage.CurrentApex;
            base.Start();
        }

        public override Task<ResultMessage> HandleAsync(MessageEnvelope envelope, long timestamp = 0)
        {
            var task = base.HandleAsync(envelope, timestamp);
            LastAddedQuantumApex = ((Quantum)envelope.Message).Apex;
            return task;
        }

        AuditorContext auditorContext => (AuditorContext)Context;

        protected override void OnProcessException(HandleItem handleItem, ResultMessage result, Exception exc)
        {
            throw exc;
        }

        protected override async Task<ResultMessage> HandleQuantum(MessageEnvelope envelope, long timestamp)
        {
            var quantum = (Quantum)envelope.Message;

            if (quantum.Apex != Context.QuantumStorage.CurrentApex + 1)
                throw new Exception($"Current quantum apex is {quantum.Apex} but {Context.QuantumStorage.CurrentApex + 1} was expected.");

            ValidateAccountRequestRate(envelope);

            var result = await ProcessQuantumEnvelope(envelope);

            if (!ByteArrayComparer.Default.Equals(result.Effects.Hash, quantum.EffectsHash) && !EnvironmentHelper.IsTest)
            {
                throw new Exception("Effects hash is not equal to provided by Alpha.");
            }

            var messageHash = envelope.ComputeMessageHash(buffer.Buffer);

            Context.QuantumStorage.AddQuantum(envelope, messageHash);

            ProcessTransaction(result.ResultMessage, result.TxHash);

            result.EffectProcessorsContainer.Complete(buffer.Buffer);

            auditorContext.OutgoingResultsStorage.EnqueueResult(result.ResultMessage, buffer.Buffer);

            logger.Trace($"Message of type {quantum.MessageType} with apex {quantum.Apex} is handled.");

            return result.ResultMessage;
        }

        void ValidateAccountRequestRate(MessageEnvelope envelope)
        {
            var request = envelope.Message as RequestQuantum;
            if (request == null)
                return;
            var account = request.RequestMessage.AccountWrapper;
            if (!account.RequestCounter.IncRequestCount(request.Timestamp, out string error))
                throw new TooManyRequestsException($"Request limit reached for account {account.Account.Pubkey}.");
        }


        void ProcessTransaction(ResultMessage resultMessage, byte[] txHash)
        {
            if (txHash == null)
                return;

            var txResult = resultMessage as ITransactionResultMessage;
            if (txResult == null)
                throw new Exception("Result is not ITransactionResultMessage");
            txResult.TxSignatures.Add(new Ed25519Signature
            {
                Signature = Context.Settings.KeyPair.Sign(txHash),
                Signer = Context.Settings.KeyPair.PublicKey
            });
        }
    }
}
