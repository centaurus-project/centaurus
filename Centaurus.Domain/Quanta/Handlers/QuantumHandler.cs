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
                quantumEnvelope = new RequestQuantum { RequestEnvelope = envelope }.CreateEnvelope();
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

        public long LastAddedQuantumApex { get; private set; }

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
            await Context.PendingUpdatesManager.UpdatesSyncRoot.WaitAsync();
            try
            {
                return await HandleQuantum(quantumEnvelope, timestamp);
            }
            finally
            {
                Context.PendingUpdatesManager.UpdatesSyncRoot.Release();
            }
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

            var result = await ProcessQuantumEnvelope(envelope, account);

            EnsureMessageHash(envelope, result);

            var messageHash = envelope.ComputeMessageHash(buffer.Buffer);
            //we need to sign the quantum here to prevent multiple signatures that can occur if we sign it when sending
            envelope.Signatures.Add(messageHash.Sign(Context.Settings.KeyPair));

            ProcessTransaction(result.ResultMessage, result.TxSignature);

            RegisterResult(result);

            Context.QuantumStorage.AddQuantum(envelope, messageHash);

            result.EffectProcessorsContainer.Complete(result.ResultMessage.Effects, buffer.Buffer);

            EnsureOutgoingResult(result.EffectProcessorsContainer.Apex, result.ResultMessage);

            logger.Trace($"Message of type {quantum.MessageType} with apex {quantum.Apex} is handled.");

            return result.ResultMessage;
        }

        void EnsureOutgoingResult(long apex, QuantumResultMessage result)
        {
            Context.OutgoingResultsStorage.EnqueueResult(apex, result);
        }

        private void RegisterResult(QuantumProcessingResult result)
        {
            var effectsHash = result.Effects.EffectsProof.Hashes.ComputeHash(buffer.Buffer);
            result.Effects.EffectsProof.Signatures.Add(effectsHash.Sign(Context.Settings.KeyPair));

            Context.AuditResultManager.Add(result.EffectProcessorsContainer.Apex, result.ResultMessage, effectsHash, result.EffectProcessorsContainer.GetNotificationMessages());
        }

        void ProcessTransaction(ResultMessage resultMessage, TxSignature signature)
        {
            if (signature == null)
                return;

            var txResult = resultMessage as ITransactionResultMessage;
            if (txResult == null)
                throw new Exception("Result is not ITransactionResultMessage");
            txResult.TxSignatures.Add(signature);
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

        EffectProcessorsContainer GetEffectProcessorsContainer(MessageEnvelope envelope, AccountWrapper account)
        {
            return new EffectProcessorsContainer(Context, envelope, Context.PendingUpdatesManager.Current, account);
        }

        /// <summary>
        /// Looks for a processor for the specified message type
        /// </summary>
        IQuantumProcessor GetProcessorItem(MessageEnvelope envelope)
        {
            var messageType = GetMessageType(envelope);

            if (!Context.QuantumProcessor.TryGetValue(messageType, out var processor))
                //TODO: do not fail here - return unsupported error message;
                throw new InvalidOperationException($"Quantum {messageType} is not supported.");
            return processor;
        }

        async Task<QuantumProcessingResult> ProcessQuantumEnvelope(MessageEnvelope envelope, AccountWrapper account)
        {
            var processor = GetProcessorItem(envelope);

            var effectsContainer = GetEffectProcessorsContainer(envelope, account);

            var processorContext = processor.GetContext(effectsContainer);

            await processor.Validate(processorContext);

            var resultMessage = await processor.Process(processorContext);

            var effects = GetEffectsData(effectsContainer);

            if (envelope.Message is RequestQuantum request)
                resultMessage.ClientEffects = effectsContainer.Effects.Where(e => e.Account == request.RequestMessage.Account).ToList();
            else
                resultMessage.ClientEffects = effectsContainer.Effects;

            resultMessage.Effects = effects.EffectsProof;

            var result = new QuantumProcessingResult
            {
                ResultMessage = resultMessage,
                Effects = effects,
                EffectProcessorsContainer = effectsContainer
            };

            if (processorContext is ITransactionProcessorContext transactionContext)
                result.TxSignature = transactionContext.PaymentProvider.SignTransaction(transactionContext.Transaction);

            return result;
        }

        void EnsureMessageHash(MessageEnvelope envelope, QuantumProcessingResult result)
        {
            var quantum = (Quantum)envelope.Message;
            if (Context.IsAlpha)
                quantum.EffectsHash = result.Effects.Hash;
            else
            {
                if (!ByteArrayComparer.Default.Equals(result.Effects.Hash, quantum.EffectsHash) && !EnvironmentHelper.IsTest)
                    throw new Exception($"Effects hash for quantum {quantum.Apex} is not equal to provided by Alpha.");
            }
        }

        EffectsData GetEffectsData(EffectProcessorsContainer effectsContainer)
        {
            var resultEffectsContainer = new EffectsContainer { Effects = effectsContainer.Effects };
            var effectsBin = resultEffectsContainer.ToByteArray(buffer.Buffer);

            var effectsProof = new EffectsProof
            {
                Hashes = new EffectHashes
                {
                    Hashes = effectsContainer.Effects.Select(e => new Hash { Data = e.ComputeHash() }).ToList()
                },
                Signatures = new List<Ed25519Signature>()
            };

            return new EffectsData
            {
                Data = resultEffectsContainer.ToByteArray(buffer.Buffer),
                Hash = effectsBin.ComputeHash(),
                EffectsProof = effectsProof
            };
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

        protected class QuantumProcessingResult
        {
            public EffectProcessorsContainer EffectProcessorsContainer { get; set; }

            public QuantumResultMessage ResultMessage { get; set; }

            public EffectsData Effects { get; set; }

            public TxSignature TxSignature { get; set; }
        }

        protected class EffectsData
        {
            public byte[] Data { get; set; }

            public byte[] Hash { get; set; }
            public EffectsProof EffectsProof { get; internal set; }
        }
    }
}
