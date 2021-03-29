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
    public class QuantumHandler : IDisposable
    {
        class HandleItem
        {
            public HandleItem(MessageEnvelope quantum, long timestamp = 0)
            {
                Quantum = quantum;
                HandlingTaskSource = new TaskCompletionSource<ResultMessage>();
                Timestamp = timestamp;
            }
            public MessageEnvelope Quantum { get; }
            public TaskCompletionSource<ResultMessage> HandlingTaskSource { get; }
            public long Timestamp { get; }
        }

        static Logger logger = LogManager.GetCurrentClassLogger();

        public QuantumHandler(long lastAddedApex)
        {
            LastAddedQuantumApex = lastAddedApex;
            Start();

            options = new JsonSerializerOptions { IgnoreNullValues = true };
            options.Converters.Add(new XdrObjectConverter());

            buffer = XdrBufferFactory.Rent(256 * 1024);
        }

        BlockingCollection<HandleItem> awaitedQuanta = new BlockingCollection<HandleItem>();
        private JsonSerializerOptions options;
        private XdrBufferFactory.RentedBuffer buffer;

        /// <summary>
        /// Handles the quantum and returns Task.
        /// </summary>
        /// <param name="envelope">Quantum to handle</param>
        /// <param name="long">Quantum timestamp. We need it for quanta recovery, otherwise Alpha will have different hash.</param>
        public Task<ResultMessage> HandleAsync(MessageEnvelope envelope, long timestamp = 0)
        {
            if (QuantaThrottlingManager.Current.IsThrottlingEnabled && QuantaThrottlingManager.Current.MaxItemsPerSecond <= awaitedQuanta.Count)
                throw new TooManyRequestsException("Server is too busy. Try again later.");
            var newHandleItem = new HandleItem(envelope, timestamp);
            awaitedQuanta.Add(newHandleItem);
            if (!Global.IsAlpha)
                LastAddedQuantumApex = ((Quantum)envelope.Message).Apex;
            return newHandleItem.HandlingTaskSource.Task;
        }

        public long LastAddedQuantumApex { get; private set; }

        public int QuantaQueueLenght => awaitedQuanta.Count;

        private async Task RunQuantumWorker()
        {
            try
            {
                foreach (var handlingItem in awaitedQuanta.GetConsumingEnumerable())
                {
                    await ProcessQuantum(handlingItem);
                    if (Global.IsAlpha && QuantaThrottlingManager.Current.IsThrottlingEnabled)
                        Thread.Sleep(QuantaThrottlingManager.Current.SleepTime);
                }
            }
            catch (Exception exc)
            {
                logger.Error(exc, "Quantum worker failed");
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

                result = await HandleQuantum(envelope, handleItem.Timestamp);
                if (result.Status != ResultStatusCodes.Success)
                    throw new Exception();
                tcs.SetResult(result);
            }
            catch (Exception exc)
            {
                if (result == null)
                    result = envelope.CreateResult(exc);
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

        async Task<ResultMessage> HandleQuantum(MessageEnvelope quantumEnvelope, long timestamp)
        {
            quantumEnvelope.TryAssignAccountWrapper();
            await Global.PendingUpdatesManager.UpdatesSyncRoot.WaitAsync();
            try
            {
                return Global.IsAlpha
                    ? await AlphaHandleQuantum(quantumEnvelope, timestamp)
                    : await AuditorHandleQuantum(quantumEnvelope);
            }
            finally
            {
                Global.PendingUpdatesManager.UpdatesSyncRoot.Release();
            }
        }

        async Task<ResultMessage> AlphaHandleQuantum(MessageEnvelope envelope, long timestamp)
        {
            var processor = GetProcessorItem(envelope.Message.MessageType);

            var quantumEnvelope = GetQuantumEnvelope(envelope);

            var quantum = (Quantum)quantumEnvelope.Message;

            quantum.Apex = Global.QuantumStorage.CurrentApex + 1;
            quantum.PrevHash = Global.QuantumStorage.LastQuantumHash;
            quantum.Timestamp = timestamp == default ? DateTime.UtcNow.Ticks : timestamp;//it could be assigned, if this quantum was handled already and we handle it during the server rising

            var effectsContainer = GetEffectProcessorsContainer(quantumEnvelope);

            var context = processor.GetContext(effectsContainer);

            await processor.Validate(context);

            var resultMessageEnvelope = (await processor.Process(context)).CreateEnvelope();

            var resultEffectsContainer = new EffectsContainer { Effects = effectsContainer.Effects };

            var effects = resultEffectsContainer.ToByteArray(buffer.Buffer);

            quantum.EffectsHash = effects.ComputeHash();

            var messageHash = quantumEnvelope.ComputeMessageHash(buffer.Buffer);
            //we need to sign the quantum here to prevent multiple signatures that can occur if we sign it when sending
            quantumEnvelope.Signatures.Add(messageHash.Sign(Global.Settings.KeyPair));

            var resultMessageHash = resultMessageEnvelope.ComputeMessageHash(buffer.Buffer);
            resultMessageEnvelope.Signatures.Add(resultMessageHash.Sign(Global.Settings.KeyPair));

            Global.AuditResultManager.Register(resultMessageEnvelope, resultMessageHash, processor.GetNotificationMessages(context));

            Global.QuantumStorage.AddQuantum(quantumEnvelope, messageHash);

            effectsContainer.Complete(effects);

            logger.Trace($"Message of type {envelope.Message} with apex {quantum.Apex} is handled.");

            return (ResultMessage)resultMessageEnvelope.Message;
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

            var resultEffectsContainer = new EffectsContainer { Effects = effectsContainer.Effects };

            var effects = resultEffectsContainer.ToByteArray(buffer.Buffer);

            var effectsHash = effects.ComputeHash();

            if (!ByteArrayComparer.Default.Equals(effectsHash, quantum.EffectsHash) && !EnvironmentHelper.IsTest)
            {
                throw new Exception("Effects hash is not equal to provided by Alpha.");
            }

            var messageHash = envelope.ComputeMessageHash(buffer.Buffer);

            Global.QuantumStorage.AddQuantum(envelope, messageHash);

            ProcessTransaction(context, result);

            effectsContainer.Complete(effects);

            logger.Trace($"Message of type {messageType} with apex {((Quantum)envelope.Message).Apex} is handled.");

            OutgoingResultsStorage.EnqueueResult(result, buffer.Buffer);

            return result;
        }

        EffectProcessorsContainer GetEffectProcessorsContainer(MessageEnvelope envelope)
        {
            return new EffectProcessorsContainer(envelope, Global.PendingUpdatesManager.Current);
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

        public void Dispose()
        {
            awaitedQuanta?.Dispose();
            awaitedQuanta = null;
        }
    }
}
