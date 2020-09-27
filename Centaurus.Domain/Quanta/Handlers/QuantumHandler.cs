using Centaurus.Models;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    //TODO: add Stop method
    public class QuantumHandler
    {
        protected class HandleItem
        {
            public HandleItem(MessageEnvelope quantum)
            {
                Quantum = quantum;
                HandlingTaskSource = new TaskCompletionSource<ResultMessage>();
            }
            public MessageEnvelope Quantum { get; }
            public TaskCompletionSource<ResultMessage> HandlingTaskSource { get; }
        }

        static Logger logger = LogManager.GetCurrentClassLogger();

        public QuantumHandler(long lastAddedApex)
        {
            LastAddedQuantumApex = lastAddedApex;
            Start();
        }

        protected BlockingCollection<HandleItem> awaitedQuanta = new BlockingCollection<HandleItem>();

        /// <summary>
        /// Handles the quantum and returns Task.
        /// </summary>
        /// <param name="envelope">Quantum to handle</param>
        public Task<ResultMessage> HandleAsync(MessageEnvelope envelope)
        {
            var newHandleItem = new HandleItem(envelope);
            awaitedQuanta.Add(newHandleItem);
            if (!Global.IsAlpha)
                LastAddedQuantumApex = ((Quantum)envelope.Message).Apex;
            return newHandleItem.HandlingTaskSource.Task;
        }

        public long LastAddedQuantumApex { get; private set; }

        private async Task RunQuantumWorker()
        {
            try
            {
                foreach (var handlingItem in awaitedQuanta.GetConsumingEnumerable())
                    await ProcessQuantum(handlingItem);
            }
            catch (Exception exc)
            {
                logger.Error(exc);
                Global.AppState.State = ApplicationState.Failed;
                throw;
            }
        }

        async Task ProcessQuantum(HandleItem handleItem)
        {
            var envelope = handleItem.Quantum;
            var tcs = handleItem.HandlingTaskSource;
            ResultMessage result = null;
            try
            {
                Global.ExtensionsManager.BeforeQuantumHandle(envelope);

                result = await HandleQuantum(envelope);
                if (result.Status != ResultStatusCodes.Success)
                    throw new Exception();
                tcs.SetResult(result);
            }
            catch (Exception exc)
            {
                if (result == null)
                    result = envelope.CreateResult(exc.GetStatusCode());
                Notifier.OnMessageProcessResult(result);
                tcs.SetException(exc);
                if (!Global.IsAlpha) //auditor should fail on quantum processing handling
                    throw;
            }
            Global.ExtensionsManager.AfterQuantumHandle(result);
        }

        MessageEnvelope GetQuantumEnvelope(MessageEnvelope envelope)
        {
            var quantumEnvelope = envelope;
            if (Global.IsAlpha && !(envelope.Message is Quantum))//we need to wrap client request
                quantumEnvelope = new RequestQuantum { RequestEnvelope = envelope }.CreateEnvelope();
            return quantumEnvelope;
        }

        MessageTypes GetMessageType(MessageEnvelope envelope)
        {
            if (envelope.Message is RequestQuantum)
                return ((RequestQuantum)envelope.Message).RequestMessage.MessageType;
            return envelope.Message.MessageType;
        }

        async Task<ResultMessage> HandleQuantum(MessageEnvelope quantumEnvelope)
        {
            return Global.IsAlpha
                ? await AlphaHandleQuantum(quantumEnvelope)
                : await AuditorHandleQuantum(quantumEnvelope);
        }

        async Task<ResultMessage> AlphaHandleQuantum(MessageEnvelope envelope)
        {
            var processor = GetProcessorItem(envelope.Message.MessageType);

            var quantumEnvelope = GetQuantumEnvelope(envelope);

            var quantum = (Quantum)quantumEnvelope.Message;

            quantum.IsProcessed = false;

            var effectsContainer = GetEffectProcessorsContainer(quantumEnvelope);

            var context = processor.GetContext(effectsContainer);

            await processor.Validate(context);

            //we must add it before processing, otherwise the quantum that we are processing here will be different from the quantum that will come to the auditor
            Global.QuantumStorage.AddQuantum(quantumEnvelope);

            //we need to sign the quantum here to prevent multiple signatures that can occur if we sign it when sending
            quantumEnvelope.Sign(Global.Settings.KeyPair);

            var resultMessage = await processor.Process(context);

            quantum.IsProcessed = true;

            SaveEffects(effectsContainer);

            Notifier.OnMessageProcessResult(resultMessage);

            logger.Trace($"Message of type {envelope.Message} with apex {quantum.Apex} is handled.");

            return resultMessage;
        }

        async Task<ResultMessage> AuditorHandleQuantum(MessageEnvelope envelope)
        {
            var quantum = (Quantum)envelope.Message;

            if (quantum.Apex != Global.QuantumStorage.CurrentApex + 1)
                throw new Exception($"Current quantum apex is {quantum.Apex} but {Global.QuantumStorage.CurrentApex + 1} was expected.");

            var messageType = GetMessageType(envelope);

            var processor = GetProcessorItem(messageType);

            ValidateAccountRequestRate(envelope);

            var effectsContainer = GetEffectProcessorsContainer(envelope);

            var context = processor.GetContext(effectsContainer);

            await processor.Validate(context);

            var result = await processor.Process(context);

            Global.QuantumStorage.AddQuantum(envelope);

            ProcessTransaction(context, result);

            SaveEffects(effectsContainer);

            logger.Trace($"Message of type {messageType} with apex {((Quantum)envelope.Message).Apex} is handled.");

            var resultEnvelope = result.CreateEnvelope();

            OutgoingMessageStorage.EnqueueMessage(resultEnvelope);

            return result;
        }

        EffectProcessorsContainer GetEffectProcessorsContainer(MessageEnvelope envelope)
        {
            return new EffectProcessorsContainer(envelope, Global.AddEffects);
        }

        void SaveEffects(EffectProcessorsContainer effectsContainer)
        {
            //we must ignore init quantum because it will be saved immidiatly
            if (effectsContainer.Apex > 1)
                effectsContainer.SaveEffects();
        }

        void ValidateAccountRequestRate(MessageEnvelope envelope)
        {
            var request = envelope.Message as RequestQuantum;
            if (request == null)
                return;
            var account = request.RequestMessage.AccountWrapper;
            if (!account.RequestCounter.IncRequestCount(request.Timestamp, out string error))
                throw new TooManyRequests($"Request limit reached for account {account.Account.Pubkey}.");
        }

        void ProcessTransaction(object context, ResultMessage resultMessage)
        {
            var transactionContext = context as ITransactionProcessorContext;
            if (transactionContext == null)
                return;
            var txResult = resultMessage as ITransactionResultMessage;
            if (txResult == null)
                throw new Exception("Result is not ITransactionResultMessage");
            txResult.TxSignatures.Add(new Ed25519Signature
            {
                Signature = Global.Settings.KeyPair.Sign(transactionContext.TransactionHash),
                Signer = Global.Settings.KeyPair.PublicKey
            });
        }

        /// <summary>
        /// Looks for a processor for the specified message type
        /// </summary>
        IQuantumRequestProcessor GetProcessorItem(MessageTypes messageType)
        {
            if (!Global.QuantumProcessor.TryGetValue(messageType, out var processor))
                //TODO: do not fail here - return unsupported error message;
                throw new InvalidOperationException($"Quantum {messageType} is not supported.");
            return processor;
        }

        /// <summary>
        /// Starts a new worker thread
        /// </summary>
        void Start()
        {
            Task.Factory.StartNew(RunQuantumWorker, TaskCreationOptions.LongRunning);
        }
    }
}
