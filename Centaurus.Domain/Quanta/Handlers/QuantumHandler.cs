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
    public abstract class QuantumHandler : ContextualBase, IDisposable
    {
        protected class HandleItem
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
        protected XdrBufferFactory.RentedBuffer buffer;

        /// <summary>
        /// Handles the quantum and returns Task.
        /// </summary>
        /// <param name="envelope">Quantum to handle</param>
        /// <param name="long">Quantum timestamp. We need it for quanta recovery, otherwise Alpha will have different hash.</param>
        public virtual Task<ResultMessage> HandleAsync(MessageEnvelope envelope, long timestamp = 0)
        {
            if (QuantaThrottlingManager.Current.IsThrottlingEnabled && QuantaThrottlingManager.Current.MaxItemsPerSecond <= awaitedQuanta.Count)
                throw new TooManyRequestsException("Server is too busy. Try again later.");
            var newHandleItem = new HandleItem(envelope, timestamp);
            awaitedQuanta.Add(newHandleItem);
            return newHandleItem.HandlingTaskSource.Task;
        }

        public int QuantaQueueLenght => awaitedQuanta.Count;

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

        protected abstract void OnProcessException(HandleItem handleItem, ResultMessage result, Exception exc);

        async Task<ResultMessage> HandleQuantumInternal(MessageEnvelope quantumEnvelope, long timestamp)
        {
            quantumEnvelope.TryAssignAccountWrapper(Context.AccountStorage);
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

        protected abstract Task<ResultMessage> HandleQuantum(MessageEnvelope envelope, long timestamp);

        protected EffectProcessorsContainer GetEffectProcessorsContainer(MessageEnvelope envelope)
        {
            return new EffectProcessorsContainer(Context, envelope, Context.PendingUpdatesManager.Current);
        }

        /// <summary>
        /// Looks for a processor for the specified message type
        /// </summary>
        private IQuantumRequestProcessor GetProcessorItem(MessageEnvelope envelope)
        {
            var messageType = GetMessageType(envelope);

            if (!Context.QuantumProcessor.TryGetValue(messageType, out var processor))
                //TODO: do not fail here - return unsupported error message;
                throw new InvalidOperationException($"Quantum {messageType} is not supported.");
            return processor;
        }

        protected async Task<QuantumProcessingResult> ProcessQuantumEnvelope(MessageEnvelope envelope)
        {
            var processor = GetProcessorItem(envelope);

            var effectsContainer = GetEffectProcessorsContainer(envelope);

            var processorContext = processor.GetContext(effectsContainer);

            await processor.Validate(processorContext);

            var resultMessage = await processor.Process(processorContext);

            var effectsData = GetEffectsData(effectsContainer);

            var result = new QuantumProcessingResult
            {
                ResultMessage = resultMessage,
                Effects = new QuantumProcessingEffects
                {
                    Data = effectsData,
                    Hash = effectsData.ComputeHash()
                },
                TxHash = GetTxHash(processorContext),
                EffectProcessorsContainer = effectsContainer
            };

            return result;
        }

        byte[] GetTxHash(ProcessorContext processorContext)
        {
            if (processorContext is ITransactionProcessorContext transaction)
                return transaction.TransactionHash;
            return null;
        }

        byte[] GetEffectsData(EffectProcessorsContainer effectsContainer)
        {
            var resultEffectsContainer = new EffectsContainer { Effects = effectsContainer.Effects };

            return resultEffectsContainer.ToByteArray(buffer.Buffer);
        }

        MessageTypes GetMessageType(MessageEnvelope envelope)
        {
            if (envelope.Message is RequestQuantum)
                return ((RequestQuantum)envelope.Message).RequestMessage.MessageType;
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

            public ResultMessage ResultMessage { get; set; }

            public QuantumProcessingEffects Effects { get; set; }

            public byte[] TxHash { get; set; }
        }

        protected class QuantumProcessingEffects
        {
            public byte[] Data { get; set; }

            public byte[] Hash { get; set; }
        }
    }

    public abstract class QuantumHandler<TContext> : QuantumHandler, IContextual<TContext>
        where TContext : ExecutionContext
    {
        public QuantumHandler(TContext context)
            : base(context)
        {
        }

        public new TContext Context => (TContext)base.Context;
    }
}
