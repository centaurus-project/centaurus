using Centaurus.Domain.Models;
using Centaurus.Models;
using Centaurus.Xdr;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class QuantumHandler : ContextualBase, IDisposable
    {
        class HandleItem
        {
            public HandleItem(MessageEnvelope quantumEnvelope)
            {
                Envelope = quantumEnvelope ?? throw new ArgumentNullException(nameof(quantumEnvelope));
                if (!(Envelope.Message is Quantum))
                    throw new ArgumentException("Message must be Quantum type.");
                HandlingTaskSource = new TaskCompletionSource<QuantumResultMessageBase>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            public MessageEnvelope Envelope { get; }

            public Quantum Quantum => (Quantum)Envelope.Message;

            public TaskCompletionSource<QuantumResultMessageBase> HandlingTaskSource { get; }
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

        /// <summary>
        /// Handles the quantum and returns Task.
        /// </summary>
        /// <param name="quantum">Quantum to handle</param>
        public Task<QuantumResultMessageBase> HandleAsync(Quantum quantum)
        {
            return HandleAsync(quantum.CreateEnvelope());
        }


        /// <summary>
        /// Handles the quantum and returns Task.
        /// </summary>
        /// <param name="quantum">Quantum to handle</param>
        public Task<QuantumResultMessageBase> HandleAsync(MessageEnvelope quantum)
        {
            if (QuantaThrottlingManager.Current.IsThrottlingEnabled && QuantaThrottlingManager.Current.MaxItemsPerSecond <= awaitedQuanta.Count)
                throw new TooManyRequestsException("Server is too busy. Try again later.");
            var newHandleItem = new HandleItem(quantum);
            if (newHandleItem.Quantum.Apex > 0)
                LastAddedQuantumApex = newHandleItem.Quantum.Apex;
            awaitedQuanta.Add(newHandleItem);
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
                Context.StateManager.Failed(new Exception("Quantum worker failed.", exc));
                throw;
            }
        }

        async Task ProcessQuantum(HandleItem handleItem)
        {
            var tcs = handleItem.HandlingTaskSource;
            QuantumResultMessageBase result = null;
            try
            {
                Context.ExtensionsManager.BeforeQuantumHandle(handleItem.Quantum);

                Context.PendingUpdatesManager.UpdateBatch();
                result = await HandleQuantum(handleItem.Envelope);
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

        void OnProcessException(HandleItem handleItem, ResultMessageBase result, Exception exc)
        {
            if (!Context.IsAlpha)
                throw exc;

            if (handleItem.Quantum is RequestQuantum requestQuantum)
            {
                if (result == null)
                    result = requestQuantum.RequestEnvelope.CreateResult(exc);
                Context.OnMessageProcessResult(result, Context.AccountStorage.GetAccount(requestQuantum.RequestMessage.Account).Account.Pubkey);
            }
        }

        void ValidateQuantum(Quantum quantum, AccountWrapper account)
        {
            if (quantum.Apex == 0)
            {
                quantum.Apex = Context.QuantumStorage.CurrentApex + 1;
                quantum.PrevHash = Context.QuantumStorage.LastQuantumHash;
                quantum.Timestamp = DateTime.UtcNow.Ticks;
            }
            else
            {
                if (quantum.Apex != Context.QuantumStorage.CurrentApex + 1)
                    throw new Exception($"Current quantum apex is {quantum.Apex} but {Context.QuantumStorage.CurrentApex + 1} was expected.");

                if (!quantum.PrevHash.SequenceEqual(Context.QuantumStorage.LastQuantumHash))
                    throw new Exception($"Quantum previous hash doesn't equal to last quantum hash.");
                ValidateRequestQuantum(quantum, account);
            }
        }

        AccountWrapper GetAccountWrapper(Quantum quantum)
        {
            if (quantum is RequestQuantum requestQuantum)
                return Context.AccountStorage.GetAccount(requestQuantum.RequestMessage.Account);
            else
                return null; //the quantum is not client quantum
        }

        async Task<QuantumResultMessageBase> HandleQuantum(MessageEnvelope quantumEnvelope)
        {
            var quantum = (Quantum)quantumEnvelope.Message;

            var account = GetAccountWrapper(quantum);

            ValidateQuantum(quantum, account);

            var processor = GetProcessorItem(quantum);

            var processorContext = processor.GetContext(quantum, account);

            var result = await ProcessQuantumEnvelope(quantum, processor, processorContext);

            var quantumHash = quantum.ComputeHash(buffer.Buffer);

            var updatesBatchId = processorContext.PersistQuantum();

            ProcessResult(updatesBatchId, result, processorContext);

            if (Context.IsAlpha)
                quantumEnvelope.Sign(Context.Settings.KeyPair);

            Context.QuantumStorage.AddQuantum(new InProgressQuantum { QuantumEnvelope = quantumEnvelope, Signatures = new List<AuditorSignature>() }, quantumHash);

            EnsureOutgoingResult(result.Quantum.Apex, result);

            logger.Trace($"Message of type {quantum.MessageType} with apex {quantum.Apex} is handled.");

            return result;
        }

        void EnsureOutgoingResult(ulong apex, QuantumResultMessageBase result)
        {
        }

        AuditorSignature GetSignature(QuantumResultMessageBase result, ProcessorContext processorContext)
        {
            var auditorSignature = new AuditorSignature { EffectsSignature = result.Quantum.EffectsHash.Sign(Context.Settings.KeyPair) };
            //add transaction signature
            if (processorContext is ITransactionProcessorContext transactionContext)
            {
                var signature = transactionContext.PaymentProvider.SignTransaction(transactionContext.Transaction);
                auditorSignature.TxSignature = signature.Signature;
                auditorSignature.TxSigner = signature.Signer;
            }
            return auditorSignature;
        }

        private void ProcessResult(uint updatesBatchId, QuantumResultMessageBase result, ProcessorContext processorContext)
        {
            //register result
            Context.AuditResultManager.Add(updatesBatchId, result.Quantum.Apex, result, processorContext.GetNotificationMessages());
            //build result message
            var resultSignature = GetSignature(result, processorContext);
            var auditorResultMessage = new AuditorResultMessage { Apex = result.Quantum.Apex, Signature = resultSignature };
            //add result to manager
            Context.AuditResultManager.Add(auditorResultMessage, Context.Settings.KeyPair);
            //send result to auditors
            Context.OutgoingConnectionManager.EnqueueResult(auditorResultMessage);
        }

        void ValidateRequestQuantum(Quantum quantum, AccountWrapper accountWrapper)
        {
            var request = quantum as RequestQuantum;
            if (request == null)
                return;
            ValidateAccountRequestSignature(request, accountWrapper);
            ValidateAccountRequestRate(request, accountWrapper);
        }

        void ValidateAccountRequestSignature(RequestQuantum request, AccountWrapper accountWrapper)
        {
            if (!request.RequestEnvelope.IsSignatureValid(accountWrapper.Account.Pubkey))
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
        QuantumProcessorBase GetProcessorItem(Quantum quantum)
        {
            var messageType = GetMessageType(quantum);

            if (!Context.QuantumProcessor.TryGetValue(messageType, out var processor))
                //TODO: do not fail here - return unsupported error message;
                throw new InvalidOperationException($"Quantum {messageType} is not supported.");
            return processor;
        }

        async Task<QuantumResultMessageBase> ProcessQuantumEnvelope(Quantum quantum, QuantumProcessorBase processor, ProcessorContext processorContext)
        {
            await processor.Validate(processorContext);

            var resultMessage = await processor.Process(processorContext);

            processorContext.ComputeEffectsHash();

            EnsureMessageHash(quantum, processorContext);

            resultMessage.Effects = processorContext.GetEffectProof();

            resultMessage.ClientEffects = processorContext.GetClientEffects();

            return resultMessage;
        }

        void EnsureMessageHash(Quantum quantum, ProcessorContext context)
        {
            if (Context.IsAlpha)
                quantum.EffectsHash = context.EffectsHash;
            else
            {
                if (!ByteArrayComparer.Default.Equals(context.EffectsHash, quantum.EffectsHash) && !EnvironmentHelper.IsTest)
                    throw new Exception($"Effects hash for quantum {quantum.Apex} is not equal to provided by Alpha.");
            }
        }

        MessageTypes GetMessageType(Quantum quantum)
        {
            if (quantum is RequestQuantum requestQuantum)
                return requestQuantum.RequestMessage.MessageType;
            else if (quantum is ConstellationQuantum constellationQuantum)
                return constellationQuantum.RequestMessage.MessageType;
            return quantum.MessageType;
        }

        public void Dispose()
        {
            awaitedQuanta?.Dispose();
            awaitedQuanta = null;
        }
    }
}
