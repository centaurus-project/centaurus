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
                HandlingTaskSource = new TaskCompletionSource<MessageEnvelope>();
            }
            public MessageEnvelope Quantum { get; }
            public TaskCompletionSource<MessageEnvelope> HandlingTaskSource { get; }
        }

        static Logger logger = LogManager.GetCurrentClassLogger();

        public QuantumHandler()
        {
            Start();
        }

        protected BlockingCollection<HandleItem> awaitedQuanta = new BlockingCollection<HandleItem>();

        /// <summary>
        /// Handles the quantum and returns Task.
        /// </summary>
        /// <param name="envelope">Quantum to handle</param>
        public Task<MessageEnvelope> HandleAsync(MessageEnvelope envelope)
        {
            var newHandleItem = new HandleItem(envelope);
            awaitedQuanta.Add(newHandleItem);
            return newHandleItem.HandlingTaskSource.Task;
        }
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
            try
            {
                await HandleQuantum(envelope);

                tcs.SetResult(envelope);

            }
            catch (Exception exc)
            {
                logger.Error(exc);
                Notifier.OnMessageProcessResult(new ResultMessage
                {
                    Status = ClientExceptionHelper.GetExceptionStatusCode(exc),
                    OriginalMessage = envelope
                });

                tcs.SetException(exc);
            }
        }

        MessageEnvelope GetQuantumEnvelope(MessageEnvelope envelope)
        {
            var quantumEnvelope = envelope;
            if (Global.IsAlpha && !(envelope.Message is Quantum))//we need to wrap client request
            {
                quantumEnvelope = new RequestQuantum { RequestEnvelope = envelope }.CreateEnvelope(); 
                quantumEnvelope.Sign(Global.Settings.KeyPair); //alpha should sign every quantum
            }
            return quantumEnvelope;
        }

        MessageTypes GetMessageType(MessageEnvelope envelope)
        {
            var quantumEnvelope = envelope;
            if (envelope.Message is RequestQuantum)
                return ((RequestQuantum)envelope.Message).RequestMessage.MessageType;
            return envelope.Message.MessageType;
        }

        async Task HandleQuantum(MessageEnvelope quantumEnvelope)
        {
            if (Global.IsAlpha)
                await AlphaHandleQuantum(quantumEnvelope);
            else
                await AuditorHandleQuantum(quantumEnvelope);
        }

        async Task AlphaHandleQuantum(MessageEnvelope envelope)
        {
            var processor = GetProcessor(envelope.Message.MessageType);

            var quantumEnvelope = GetQuantumEnvelope(envelope);

            var quantum = (Quantum)quantumEnvelope.Message;

            quantum.IsProcessed = false;

            await processor.Validate(quantumEnvelope);

            //we must add it before processing, otherwise the quantum that we are processing here will be different from the quantum that will come to the auditor
            Global.QuantumStorage.AddQuantum(quantumEnvelope);

            var resultMessage = await processor.Process(quantumEnvelope);

            quantum.IsProcessed = true;

            Notifier.OnMessageProcessResult(resultMessage);

            logger.Trace($"Message of type {envelope.Message.ToString()} with apex {quantum.Apex} is handled.");

        }

        async Task AuditorHandleQuantum(MessageEnvelope envelope)
        {
            var result = envelope.CreateResult();
            try
            {
                var messageType = GetMessageType(envelope);

                var processor = GetProcessor(messageType);

                await processor.Validate(envelope);

                await processor.Process(envelope);

                Global.QuantumStorage.AddQuantum(envelope);

                result.Status = ResultStatusCodes.Success;
                ProcessTransaction(envelope, result);

                logger.Trace($"Message of type {messageType.ToString()} with apex {((Quantum)envelope.Message).Apex} is handled.");
            }
            catch (Exception exc)
            {
                logger.Error(exc);
                result.Status = ResultStatusCodes.InternalError;
                throw;
            }

            OutgoingMessageStorage.EnqueueMessage(result);
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
