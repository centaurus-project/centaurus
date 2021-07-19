using Centaurus.Domain.Models;
using Centaurus.Models;
using Centaurus.Xdr;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class QuantumHandler : ContextualBase, IDisposable
    {
        class HandleItem
        {
            public HandleItem(MessageEnvelope quantum, long timestamp = 0)
            {
                Quantum = quantum;
                HandlingTaskSource = new TaskCompletionSource<ResultMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
                Timestamp = timestamp;
            }
            public MessageEnvelope Quantum { get; }
            public TaskCompletionSource<ResultMessage> HandlingTaskSource { get; }
            public long Timestamp { get; }
        }

        public QuantumHandler(ExecutionContext context)
            : base(context)
        {
            buffer = XdrBufferFactory.Rent(256 * 1024);
        }

        static Logger logger = LogManager.GetCurrentClassLogger();

        public virtual void Start()
        {
            Task.Factory.StartNew(RunQuantumWorker, TaskCreationOptions.LongRunning).Unwrap();
        }

        BlockingCollection<HandleItem> awaitedQuanta = new BlockingCollection<HandleItem>();
        XdrBufferFactory.RentedBuffer buffer;

        MessageEnvelope GetQuantumEnvelope(MessageEnvelope envelope)
        {
            var quantumEnvelope = envelope;
            if (envelope.Message is SequentialRequestMessage)//we need to wrap client request
            {
                if (envelope.Message is WithdrawalRequest)
                    quantumEnvelope = new RequestTransactionQuantum { RequestEnvelope = envelope }.CreateEnvelope();
                else
                    quantumEnvelope = new RequestQuantum { RequestEnvelope = envelope }.CreateEnvelope();
            }
            else if (envelope.Message is ConstellationRequestMessage)
                quantumEnvelope = new ConstellationQuantum { RequestEnvelope = envelope }.CreateEnvelope();
            return quantumEnvelope;
        }

        /// <summary>
        /// Handles the quantum and returns Task.
        /// </summary>
        /// <param name="envelope">Quantum to handle</param>
        /// <param name="long">Quantum timestamp. We need it for quanta recovery, otherwise Alpha will have different hash.</param>
        public virtual Task<ResultMessage> HandleAsync(MessageEnvelope envelope, long timestamp = 0)
        {
            if (QuantaThrottlingManager.Current.IsThrottlingEnabled && QuantaThrottlingManager.Current.MaxItemsPerSecond <= awaitedQuanta.Count)
                throw new TooManyRequestsException("Server is too busy. Try again later.");
            var newHandleItem = new HandleItem(GetQuantumEnvelope(envelope), timestamp);
            awaitedQuanta.Add(newHandleItem);
            if (!Context.IsAlpha)
                LastAddedQuantumApex = ((Quantum)newHandleItem.Quantum.Message).Apex;
            return newHandleItem.HandlingTaskSource.Task;
        }

        public ulong LastAddedQuantumApex { get; private set; }

        public int QuantaQueueLenght => awaitedQuanta?.Count ?? 0;

        private async Task RunQuantumWorker()
        {
            try
            {
                foreach (var handlingItem in awaitedQuanta.GetConsumingEnumerable())
                {
                    await ProcessQuantum(handlingItem);
                    if (Context.IsAlpha && QuantaThrottlingManager.Current.IsThrottlingEnabled)
                        Thread.Sleep(QuantaThrottlingManager.Current.SleepTime);
                }
            }
            catch (Exception exc)
            {
                logger.Error(exc, "Quantum worker failed");
                Context.AppState.State = ApplicationState.Failed;
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
                Context.ExtensionsManager.BeforeQuantumHandle(envelope);

                result = await HandleQuantumInternal(envelope, handleItem.Timestamp);
                if (result.Status != ResultStatusCodes.Success)
                    throw new Exception("Failed to handle quantum.");
                tcs.SetResult(result);
            }
            catch (Exception exc)
            {
                tcs.SetException(exc);
                OnProcessException(handleItem, result, exc);
            }
            Context.ExtensionsManager.AfterQuantumHandle(result);
        }

        void OnProcessException(HandleItem handleItem, ResultMessage result, Exception exc)
        {
            if (!Context.IsAlpha)
                throw exc;
            if (result == null)
                result = handleItem.Quantum.CreateResult(exc);
            Context.OnMessageProcessResult(result);
        }

        async Task<ResultMessage> HandleQuantumInternal(MessageEnvelope quantumEnvelope, long timestamp)
        {
            Context.PendingUpdatesManager.ApplyUpdates();
            return await HandleQuantum(quantumEnvelope, timestamp);
        }

        void ValidateQuantum(MessageEnvelope envelope, AccountWrapper account, long timestamp)
        {
            var quantum = (Quantum)envelope.Message;

            if (Context.IsAlpha)
            {
                quantum.Apex = Context.QuantumStorage.CurrentApex + 1;
                quantum.PrevHash = Context.QuantumStorage.LastQuantumHash;
                quantum.Timestamp = timestamp == default ? DateTime.UtcNow.Ticks : timestamp;//it could be assigned, if this quantum was handled already and we handle it during the server rising
            }
            else
            {
                if (quantum.Apex != Context.QuantumStorage.CurrentApex + 1)
                    throw new Exception($"Current quantum apex is {quantum.Apex} but {Context.QuantumStorage.CurrentApex + 1} was expected.");

                ValidateRequestQuantum(envelope, account);
            }
        }

        AccountWrapper GetAccountWrapper(MessageEnvelope envelope)
        {
            var requestMessage = default(RequestMessage);
            if (envelope.Message is RequestQuantum requestQuantum)
                requestMessage = requestQuantum.RequestMessage;
            else if (envelope.Message is RequestMessage)
                requestMessage = (RequestMessage)envelope.Message;
            else
                return null; //the quantum is not client quantum

            return Context.AccountStorage.GetAccount(requestMessage.Account);
        }

        async Task<ResultMessage> HandleQuantum(MessageEnvelope envelope, long timestamp)
        {
            var quantum = (Quantum)envelope.Message;

            var account = GetAccountWrapper(envelope);

            ValidateQuantum(envelope, account, timestamp);

            var processor = GetProcessorItem(envelope);

            var processorContext = processor.GetContext(envelope, account);

            var result = await ProcessQuantumEnvelope(envelope, account, processor, processorContext);

            var messageHash = envelope.ComputeMessageHash(buffer.Buffer);

            //we need to sign the quantum here to prevent multiple signatures that can occur if we sign it when sending
            envelope.Signatures.Add(messageHash.Sign(Context.Settings.KeyPair));

            RegisterResult(result, processorContext);

            Context.QuantumStorage.AddQuantum(envelope, messageHash);

            processorContext.PersistQuantum();

            EnsureOutgoingResult(result.Quantum.Apex, result);

            logger.Trace($"Message of type {quantum.MessageType} with apex {quantum.Apex} is handled.");

            return result;
        }

        void EnsureOutgoingResult(ulong apex, QuantumResultMessage result)
        {
            Context.OutgoingResultsStorage.EnqueueResult(apex, result);
        }

        private void RegisterResult(QuantumResultMessage result, ProcessorContext processorContext)
        {
            //compute hash before adding any signatures, otherwise hashes would be different
            var resultMessageHash = result.ComputeHash(buffer.Buffer);

            //add transaction signature
            if (processorContext is ITransactionProcessorContext transactionContext)
            {
                if (result is TransactionResultMessage transactionResult)
                    transactionResult.TxSignatures.Add(transactionContext.PaymentProvider.SignTransaction(transactionContext.Transaction).ToDomainModel());
                else
                    throw new Exception($"Unable to add transaction signature. Result is not {nameof(TransactionResultMessage)}.");
            }

            //add effects proof signature
            result.Effects.Signatures.Add(result.Quantum.EffectsHash.Sign(Context.Settings.KeyPair));
            Context.AuditResultManager.Add(result.Quantum.Apex, result, resultMessageHash, processorContext.GetNotificationMessages());
        }

        void ValidateRequestQuantum(MessageEnvelope envelope, AccountWrapper accountWrapper)
        {
            var request = envelope.Message as RequestQuantum;
            if (request == null)
                return;
            ValidateAccountRequestSignature(request, accountWrapper);
            ValidateAccountRequestRate(request, accountWrapper);
        }

        void ValidateAccountRequestSignature(RequestQuantum request, AccountWrapper accountWrapper)
        {
            if (!(request.RequestEnvelope.IsSignedBy(accountWrapper.Account.Pubkey)
                && request.RequestEnvelope.AreSignaturesValid()))
                throw new UnauthorizedAccessException("Request quantum has invalid signature.");
        }

        void ValidateAccountRequestRate(RequestQuantum request, AccountWrapper accountWrapper)
        {
            if (!accountWrapper.RequestCounter.IncRequestCount(request.Timestamp, out string error))
                throw new TooManyRequestsException($"Request limit reached for account {accountWrapper.Account.Pubkey}.");
        }

        /// <summary>
        /// Looks for a processor for the specified message type
        /// </summary>
        QuantumProcessorBase GetProcessorItem(MessageEnvelope envelope)
        {
            var messageType = GetMessageType(envelope);

            if (!Context.QuantumProcessor.TryGetValue(messageType, out var processor))
                //TODO: do not fail here - return unsupported error message;
                throw new InvalidOperationException($"Quantum {messageType} is not supported.");
            return processor;
        }

        async Task<QuantumResultMessage> ProcessQuantumEnvelope(MessageEnvelope envelope, AccountWrapper account, QuantumProcessorBase processor, ProcessorContext processorContext)
        {
            await processor.Validate(processorContext);

            var resultMessage = await processor.Process(processorContext);

            processorContext.ComputeEffectsHash();

            EnsureMessageHash(envelope, processorContext);

            resultMessage.Effects = processorContext.GetEffectProof();

            resultMessage.ClientEffects = processorContext.GetClientEffects();

            return resultMessage;
        }

        void EnsureMessageHash(MessageEnvelope envelope, ProcessorContext context)
        {
            var quantum = (Quantum)envelope.Message;
            if (Context.IsAlpha)
                quantum.EffectsHash = context.EffectsHash;
            else
            {
                if (!ByteArrayComparer.Default.Equals(context.EffectsHash, quantum.EffectsHash) && !EnvironmentHelper.IsTest)
                    throw new Exception($"Effects hash for quantum {quantum.Apex} is not equal to provided by Alpha.");
            }
        }

        MessageTypes GetMessageType(MessageEnvelope envelope)
        {
            if (envelope.Message is RequestQuantum)
                return ((RequestQuantum)envelope.Message).RequestMessage.MessageType;
            else if (envelope.Message is ConstellationQuantum)
                return ((ConstellationQuantum)envelope.Message).RequestMessage.MessageType;
            return envelope.Message.MessageType;
        }

        public void Dispose()
        {
            awaitedQuanta?.Dispose();
            awaitedQuanta = null;
        }
    }
}
