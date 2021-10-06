﻿using Centaurus.Domain.Models;
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
        public QuantumHandler(ExecutionContext context)
            : base(context)
        {
        }

        static Logger logger = LogManager.GetCurrentClassLogger();

        public virtual void Start()
        {
            Task.Factory.StartNew(RunQuantumWorker, TaskCreationOptions.LongRunning).Unwrap();
        }

        BlockingCollection<QuantumProcessingItem> awaitedQuanta = new BlockingCollection<QuantumProcessingItem>();

        /// <summary>
        /// Handles the quantum and returns Task.
        /// </summary>
        /// <param name="quantum">Quantum to handle</param>s
        public QuantumProcessingItem HandleAsync(Quantum quantum, Task<bool> signatureValidation)
        {
            if (Context.IsAlpha //if current node is not alpha, than we need to keep process quanta
                && QuantaThrottlingManager.Current.IsThrottlingEnabled
                && QuantaThrottlingManager.Current.MaxItemsPerSecond <= awaitedQuanta.Count)
                throw new TooManyRequestsException("Server is too busy. Try again later.");
            var processingItem = new QuantumProcessingItem(quantum, signatureValidation);
            if (processingItem.Quantum.Apex > 0)
                LastAddedQuantumApex = processingItem.Quantum.Apex;
            awaitedQuanta.Add(processingItem);
            return processingItem;
        }

        public ulong LastAddedQuantumApex { get; private set; }

        public int QuantaQueueLenght => awaitedQuanta.Count;

        private async Task RunQuantumWorker()
        {
            foreach (var handlingItem in awaitedQuanta.GetConsumingEnumerable())
            {
                try
                {
                    await HandleItem(handlingItem);
                    if (Context.IsAlpha && QuantaThrottlingManager.Current.IsThrottlingEnabled)
                        Thread.Sleep(QuantaThrottlingManager.Current.SleepTime);
                }
                catch (Exception exc)
                {
                    //if exception get here, than we faced with fatal exception
                    awaitedQuanta.CompleteAdding();
                    foreach (var item in awaitedQuanta)
                        item.SetException(new Exception("Cancelled."));
                    Context.StateManager.Failed(new Exception("Quantum worker failed.", exc));
                    return;
                }
            }
        }

        async Task HandleItem(QuantumProcessingItem processingItem)
        {
            try
            {
                Context.ExtensionsManager.BeforeQuantumHandle(processingItem.Quantum);
                await Context.PendingUpdatesManager.SyncRoot.WaitAsync();
                try
                {
                    Context.PendingUpdatesManager.UpdateBatch();
                    await ProcessQuantum(processingItem);
                }
                finally
                {
                    Context.PendingUpdatesManager.SyncRoot.Release();
                }
                Context.ExtensionsManager.AfterQuantumHandle(processingItem.ResultMessage);
            }
            catch (QuantumValidationException exc)
            {
                var originalException = exc.InnerException;
                processingItem.SetException(originalException);
                if (!Context.IsAlpha) //if current node is auditor, than all quanta received from Alpha must pass the validation
                    throw originalException;
            }
            catch (Exception exc)
            {
                processingItem.SetException(exc);
                throw;
            }
        }

        void ValidateQuantum(QuantumProcessingItem processingItem)
        {
            var quantum = processingItem.Quantum;
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
                ValidateRequestQuantum(processingItem);
        }

        Account GetAccountWrapper(Quantum quantum)
        {
            if (quantum is RequestQuantumBase requestQuantum)
            {
                var account = Context.AccountStorage.GetAccount(requestQuantum.RequestMessage.Account);
                if (account == null)
                    throw new Exception($"{account.Pubkey.GetAccountId()} is not registered.");
                return account;
            }
            else
                return null; //the quantum is not client quantum
        }

        async Task ProcessQuantum(QuantumProcessingItem processingItem)
        {
            var quantum = processingItem.Quantum;
            processingItem.Initiator = GetAccountWrapper(quantum);

            var processor = default(QuantumProcessorBase);
            try
            {
                if (!await processingItem.SignatureValidationTask)
                    throw new UnauthorizedException("Signature is invalid.");
                ValidateQuantum(processingItem);
                processor = GetProcessor(quantum);
                await processor.Validate(processingItem);
            }
            catch (Exception exc)
            {
                throw new QuantumValidationException(exc);
            }

            var resultMessage = await processor.Process(processingItem);
            processingItem.Complete(Context, resultMessage);

            AddToQuantumStorage(processingItem);

            ProcessResult(processingItem);

            logger.Trace($"Message of type {quantum.GetType().Name} with apex {quantum.Apex} is handled.");
        }

        private void AddToQuantumStorage(QuantumProcessingItem processingItem)
        {
            Context.QuantumStorage.AddQuantum(processingItem.Quantum, processingItem.QuantumHash);
            Context.StateManager.UpdateDelay();
        }

        private void ProcessResult(QuantumProcessingItem processingItem)
        {
            //register result
            Context.ResultManager.Add(processingItem);
        }

        void ValidateRequestQuantum(QuantumProcessingItem processingItem)
        {
            var request = processingItem.Quantum as RequestQuantumBase;
            if (request == null)
                return;
            if (!processingItem.Initiator.RequestCounter.IncRequestCount(request.Timestamp, out string error))
                throw new TooManyRequestsException($"Request limit reached for account {processingItem.Initiator.Pubkey}.");
        }

        /// <summary>
        /// Looks for a processor for the specified message type
        /// </summary>
        QuantumProcessorBase GetProcessor(Quantum quantum)
        {
            var messageType = GetMessageType(quantum);

            if (!Context.QuantumProcessor.TryGetValue(messageType, out var processor))
                throw new InvalidOperationException($"Quantum {messageType} is not supported.");
            return processor;
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

        class QuantumValidationException : Exception
        {
            public QuantumValidationException(Exception innerException)
                : base("Quantum validation error", innerException)
            {
            }
        }
    }
}
