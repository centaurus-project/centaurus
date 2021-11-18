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
    public class QuantumHandler : ContextualBase
    {
        public QuantumHandler(ExecutionContext context, ulong lastApex, byte[] lastQuantumHash)
            : base(context)
        {
            CurrentApex = LastAddedQuantumApex = lastApex;
            LastQuantumHash = lastQuantumHash ?? throw new ArgumentNullException(nameof(lastQuantumHash));
            Task.Factory.StartNew(RunQuantumWorker).Unwrap();
        }

        static Logger logger = LogManager.GetCurrentClassLogger();

        object awaitedQuantaSyncRoot = new { };
        Queue<QuantumProcessingItem> awaitedQuanta = new Queue<QuantumProcessingItem>();

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
            if (!Context.IsAlpha)
                LastAddedQuantumApex = processingItem.Quantum.Apex;
            lock (awaitedQuantaSyncRoot)
                awaitedQuanta.Enqueue(processingItem);
            return processingItem;
        }

        public ulong LastAddedQuantumApex { get; private set; }

        public ulong CurrentApex { get; private set; }

        public byte[] LastQuantumHash { get; private set; }

        public int QuantaQueueLenght => awaitedQuanta.Count;

        private async Task RunQuantumWorker()
        {
            while (true)
            {
                try
                {
                    var handlingItem = default(QuantumProcessingItem);
                    lock (awaitedQuantaSyncRoot)
                        awaitedQuanta.TryDequeue(out handlingItem);
                    if (handlingItem != null)
                    {
                        await HandleItem(handlingItem);
                        if (Context.IsAlpha && QuantaThrottlingManager.Current.IsThrottlingEnabled)
                            Thread.Sleep(QuantaThrottlingManager.Current.SleepTime);
                    }
                    else
                        Thread.Sleep(20);
                }
                catch (Exception exc)
                {
                    //if exception get here, than we faced with fatal exception

                    lock (awaitedQuantaSyncRoot)
                    {
                        while (awaitedQuanta.TryDequeue(out var item))
                            item.SetException(new Exception("Cancelled."));
                    }
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
                NotifyOnException(processingItem, originalException);
                if (!Context.IsAlpha) //if current node is auditor, than all quanta received from Alpha must pass the validation
                    throw originalException;
            }
            catch (Exception exc)
            {
                processingItem.SetException(exc);
                NotifyOnException(processingItem, exc);
                throw;
            }
        }

        void NotifyOnException(QuantumProcessingItem processingItem, Exception exc)
        {
            if (processingItem.Initiator != null && !EnvironmentHelper.IsTest)
                Context.Notify(processingItem.Initiator.Pubkey, ((RequestQuantumBase)processingItem.Quantum).RequestEnvelope.CreateResult(exc).CreateEnvelope());
        }

        void ValidateQuantum(QuantumProcessingItem processingItem)
        {
            var quantum = processingItem.Quantum;
            if (quantum.Apex == 0)
            {
                quantum.Apex = CurrentApex + 1;
                quantum.PrevHash = LastQuantumHash;
                quantum.Timestamp = DateTime.UtcNow.Ticks;
            }
            else
            {
                if (quantum.Apex != CurrentApex + 1)
                    throw new Exception($"Current quantum apex is {quantum.Apex} but {CurrentApex + 1} was expected.");

                if (!quantum.PrevHash.AsSpan().SequenceEqual(LastQuantumHash))
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

            CurrentApex = quantum.Apex;
            LastQuantumHash = processingItem.QuantumHash;

            if (CurrentApex % 1000 == 0)
                Context.StateManager.UpdateDelay();

            ProcessResult(processingItem);

            if (Context.IsAlpha)
                LastAddedQuantumApex = processingItem.Quantum.Apex;

            processingItem.Processed();

            logger.Trace($"Message of type {quantum.GetType().Name} with apex {quantum.Apex} is handled.");
        }

        void ProcessResult(QuantumProcessingItem processingItem)
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

        class QuantumValidationException : Exception
        {
            public QuantumValidationException(Exception innerException)
                : base("Quantum validation error", innerException)
            {
            }
        }
    }
}
