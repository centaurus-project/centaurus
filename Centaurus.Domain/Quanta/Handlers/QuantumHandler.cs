using Centaurus.Domain.Models;
using Centaurus.Models;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class QuantumHandler : ContextualBase, IDisposable
    {
        class HandleItem
        {
            public HandleItem(Quantum quantum, Task<bool> signatureValidation)
            {
                Quantum = quantum ?? throw new ArgumentNullException(nameof(quantum));
                SignatureValidation = signatureValidation ?? throw new ArgumentNullException(nameof(signatureValidation));
                HandlingTaskSource = new TaskCompletionSource<QuantumResultMessageBase>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            public Quantum Quantum { get; }

            public Task<bool> SignatureValidation { get; }

            public TaskCompletionSource<QuantumResultMessageBase> HandlingTaskSource { get; }
        }

        public QuantumHandler(ExecutionContext context)
            : base(context)
        {
        }

        static Logger logger = LogManager.GetCurrentClassLogger();

        public virtual void Start()
        {
            Task.Factory.StartNew(RunQuantumWorker, TaskCreationOptions.LongRunning).Unwrap();
        }

        BlockingCollection<HandleItem> awaitedQuanta = new BlockingCollection<HandleItem>();

        /// <summary>
        /// Handles the quantum and returns Task.
        /// </summary>
        /// <param name="quantum">Quantum to handle</param>s
        public Task HandleAsync(Quantum quantum, Task<bool> signatureValidation)
        {
            if (Context.IsAlpha //if current node is not alpha, than we need to keep process quanta
                && QuantaThrottlingManager.Current.IsThrottlingEnabled
                && QuantaThrottlingManager.Current.MaxItemsPerSecond <= awaitedQuanta.Count)
                throw new TooManyRequestsException("Server is too busy. Try again later.");
            var newHandleItem = new HandleItem(quantum, signatureValidation);
            if (newHandleItem.Quantum.Apex > 0)
                LastAddedQuantumApex = newHandleItem.Quantum.Apex;
            awaitedQuanta.Add(newHandleItem);
            return newHandleItem.HandlingTaskSource.Task;
        }

        public ulong LastAddedQuantumApex { get; private set; }

        public int QuantaQueueLenght => awaitedQuanta.Count;

        private async Task RunQuantumWorker()
        {
            try
            {
                foreach (var handlingItem in awaitedQuanta.GetConsumingEnumerable())
                {
                    var isSignatureValid = await handlingItem.SignatureValidation;
                    if (!isSignatureValid)
                        throw new Exception("Signature is invalid.");
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
                await Context.PendingUpdatesManager.SyncRoot.WaitAsync();
                try
                {
                    Context.PendingUpdatesManager.UpdateBatch();
                    result = await HandleQuantum(handleItem.Quantum);
                }
                finally
                {
                    Context.PendingUpdatesManager.SyncRoot.Release();
                }
                if (result.Status != ResultStatusCode.Success)
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
                Context.OnMessageProcessResult(result, Context.AccountStorage.GetAccount(requestQuantum.RequestMessage.Account).Pubkey);
                logger.Error(exc, "Error on handling.");
            }
        }

        void ValidateQuantum(Quantum quantum, Account account)
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

                if (!quantum.PrevHash.AsSpan().SequenceEqual(Context.QuantumStorage.LastQuantumHash))
                    throw new Exception($"Quantum previous hash doesn't equal to last quantum hash.");
            }
            if (!Context.IsAlpha || Context.StateManager.State == State.Rising)
                ValidateRequestQuantum(quantum, account);
        }

        Account GetAccountWrapper(Quantum quantum)
        {
            if (quantum is RequestQuantumBase requestQuantum)
                return Context.AccountStorage.GetAccount(requestQuantum.RequestMessage.Account);
            else
                return null; //the quantum is not client quantum
        }

        async Task<QuantumResultMessageBase> HandleQuantum(Quantum quantum)
        {
            var account = GetAccountWrapper(quantum);

            ValidateQuantum(quantum, account);

            var processor = GetProcessorItem(quantum);

            var processorContext = processor.GetContext(quantum, account);

            await ProcessQuantum(processor, processorContext);

            AddToQuantumStorage(processorContext.ProcessingResult);

            ProcessResult(processorContext);

            logger.Trace($"Message of type {quantum.GetType().Name} with apex {quantum.Apex} is handled.");

            return processorContext.ProcessingResult.ResultMessage;
        }

        private void AddToQuantumStorage(QuantaProcessingResult processingResult)
        {
            Context.QuantumStorage.AddQuantum(processingResult.Quantum, processingResult.QuantumHash);
            Context.StateManager.UpdateDelay();
        }

        private void ProcessResult(ProcessorContext processorContext)
        {
            //register result
            Context.ResultManager.Add(processorContext.ProcessingResult);
        }

        void ValidateRequestQuantum(Quantum quantum, Account accountWrapper)
        {
            var request = quantum as RequestQuantumBase;
            if (request == null)
                return;
            ValidateAccountRequestRate(request, accountWrapper);
        }

        void ValidateAccountRequestSignature(RequestQuantumBase request, Account accountWrapper)
        {
            if (!request.RequestEnvelope.IsSignatureValid(accountWrapper.Pubkey, false))
                throw new UnauthorizedAccessException("Request quantum has invalid signature.");
        }

        void ValidateAccountRequestRate(RequestQuantumBase request, Account accountWrapper)
        {
            if (!accountWrapper.RequestCounter.IncRequestCount(request.Timestamp, out string error))
                throw new TooManyRequestsException($"Request limit reached for account {accountWrapper.Pubkey}.");
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

        async Task ProcessQuantum(QuantumProcessorBase processor, ProcessorContext processorContext)
        {
            await processor.Validate(processorContext);

            var resultMessage = await processor.Process(processorContext);

            processorContext.Complete(resultMessage);
        }

        string GetMessageType(Quantum quantum)
        {
            if (quantum is RequestQuantumBase requestQuantum)
                return requestQuantum.RequestMessage.GetMessageType();
            else if (quantum is ConstellationQuantum constellationQuantum)
                return constellationQuantum.RequestMessage.GetMessageType();
            return quantum.GetType().Name;
        }

        public void Dispose()
        {
            awaitedQuanta.Dispose();
        }
    }
}
