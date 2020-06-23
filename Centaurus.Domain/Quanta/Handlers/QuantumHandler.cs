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
                if (!Global.IsAlpha) //auditor should fail on quantum processing handling
                    Global.AppState.State = ApplicationState.Failed;
                throw;
            }
        }

        async Task ProcessQuantum(HandleItem handleItem)
        {
            var envelope = handleItem.Quantum;
            var tcs = handleItem.HandlingTaskSource;
            ResultMessage result;
            try
            {
                Global.ExtensionsManager.BeforeQuantumHandle(envelope);

                result = await HandleQuantum(envelope);
                tcs.SetResult(result);
            }
            catch (Exception exc)
            {
                logger.Error(exc);
                result = new ResultMessage
                {
                    Status = exc.GetStatusCode(),
                    OriginalMessage = envelope
                };
                Notifier.OnMessageProcessResult(result);

                tcs.SetException(exc);
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
            var processor = GetProcessor(envelope.Message.MessageType);

            var quantumEnvelope = GetQuantumEnvelope(envelope);

            var quantum = (Quantum)quantumEnvelope.Message;

            quantum.IsProcessed = false;

            await processor.Validate(quantumEnvelope);

            //we must add it before processing, otherwise the quantum that we are processing here will be different from the quantum that will come to the auditor
            Global.QuantumStorage.AddQuantum(quantumEnvelope);

            //we need to sign the quantum here to prevent multiple signatures that can occur if we sign it when sending
            quantumEnvelope.Sign(Global.Settings.KeyPair);

            var effectsContainer = GetEffectProcessorsContainer(quantumEnvelope);

            var resultMessage = await processor.Process(quantumEnvelope, effectsContainer);

            quantum.IsProcessed = true;

            SaveEffects(effectsContainer);

            Notifier.OnMessageProcessResult(resultMessage);

            logger.Trace($"Message of type {envelope.Message} with apex {quantum.Apex} is handled.");

            return resultMessage;
        }

        async Task<ResultMessage> AuditorHandleQuantum(MessageEnvelope envelope)
        {
            ResultMessage result = null;
            try
            {
                var quantum = (Quantum)envelope.Message;

                if (quantum.Apex != Global.QuantumStorage.CurrentApex + 1)
                    throw new Exception($"Current quantum apex is {quantum.Apex} but {Global.QuantumStorage.CurrentApex + 1} was expected.");

                var messageType = GetMessageType(envelope);

                var processor = GetProcessor(messageType);

                ValidateAccountRequestRate(envelope);

                await processor.Validate(envelope);

                var effectsContainer = GetEffectProcessorsContainer(envelope);

                result = await processor.Process(envelope, effectsContainer);

                Global.QuantumStorage.AddQuantum(envelope);

                ProcessTransaction(envelope, result);

                SaveEffects(effectsContainer);

                logger.Trace($"Message of type {messageType} with apex {((Quantum)envelope.Message).Apex} is handled.");
            }
            catch (Exception exc)
            {
                logger.Error(exc);
                result = envelope.CreateResult(ResultStatusCodes.InternalError);
                throw;
            }
            finally
            {
                OutgoingMessageStorage.EnqueueMessage(result);
            }
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
                throw new TooManyRequests($"Request limit reached for account {account.Account.Pubkey.ToString()}.");
        }

        void ProcessTransaction(MessageEnvelope envelope, ResultMessage result)
        {
            var quantum = envelope.Message;
            if (quantum is RequestQuantum)//unwrap if needed
                quantum = ((RequestQuantum)quantum).RequestMessage;
            if (quantum is ITransactionContainer)
            {
                var transaction = ((ITransactionContainer)quantum).GetTransaction();
                var txHash = transaction.Hash();
                result.Effects.Add(new TransactionSignedEffect()
                {
                    TransactionHash = txHash,
                    Signature = new Ed25519Signature()
                    {
                        Signature = Global.Settings.KeyPair.Sign(txHash),
                        Signer = new RawPubKey { Data = Global.Settings.KeyPair.PublicKey }
                    }
                });
            }
        }

        /// <summary>
        /// Looks for a processor for the specified message type
        /// </summary>
        IQuantumRequestProcessor GetProcessor(MessageTypes messageType)
        {
            if (!Global.QuantumProcessor.TryGetValue(messageType, out IQuantumRequestProcessor processor))
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
