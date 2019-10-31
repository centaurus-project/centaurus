using Centaurus.Models;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public abstract class BaseQuantumHandler
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        bool isStarted;

        protected object syncRoot = new { };

        Dictionary<MessageEnvelope, TaskCompletionSource<MessageEnvelope>> awaitedQuanta = new Dictionary<MessageEnvelope, TaskCompletionSource<MessageEnvelope>>();

        /// <summary>
        /// Handles quantum
        /// </summary>
        /// <param name="envelope">Quantum to handle</param>
        public abstract void Handle(MessageEnvelope envelope);

        /// <summary>
        /// Handles the quantum and returns Task.
        /// This method is intended for testing purposes.
        /// </summary>
        /// <param name="envelope">Quantum to handle</param>
        /// <returns></returns>
        public Task<MessageEnvelope> HandleAsync(MessageEnvelope envelope)
        {
            lock (syncRoot)
            {
                var quantumCompletionSource = new TaskCompletionSource<MessageEnvelope>();
                if (!awaitedQuanta.TryAdd(envelope, quantumCompletionSource))
                    throw new Exception("Unable to add envelope to the awaited quanta");
                try
                {
                    Handle(envelope);
                    logger.Trace($"Envelope '{envelope.Message.MessageType.ToString()}:{envelope.Message.MessageId}' is added to the awaited quanta");
                }
                catch (Exception exc)
                {
                    if (!awaitedQuanta.Remove(envelope, out _))
                        logger.Error("Unable to remove the quantum from the awaited quanta");
                    quantumCompletionSource.SetException(exc);
                }
                return quantumCompletionSource.Task;
            }
        }

        protected void QuantumIsHandled(MessageEnvelope envelope)
        {
            lock (syncRoot)
            {
                if (awaitedQuanta.Remove(envelope, out TaskCompletionSource<MessageEnvelope> quantumCompletionSource))
                {
                    quantumCompletionSource.SetResult(envelope);
                    logger.Trace($"Envelope '{envelope.Message.MessageType.ToString()}:{envelope.Message.MessageId}' was removed from the awaited quanta");
                }
            }
        }

        /// <summary>
        /// Starts quantum handling
        /// </summary>
        public void Start()
        {
            lock (syncRoot)
            {
                if (isStarted)
                    throw new InvalidOperationException("Handler is already started");
                StartInternal();
                isStarted = true;
            }
        }

        protected abstract void StartInternal();
    }
}
