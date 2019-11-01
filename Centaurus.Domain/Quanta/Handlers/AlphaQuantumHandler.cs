using Centaurus.Models;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Threading.Tasks;
using NLog;
using System.Threading;

namespace Centaurus.Domain
{
    public class AlphaQuantumHandler : BaseQuantumHandler
    {
        private Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="quanta">A quanta array ordered by an apex</param>
        public AlphaQuantumHandler(IEnumerable<MessageEnvelope> quanta)
        {
            quantaQueue = new QuantaQueue(quanta);
        }

        readonly QuantaQueue quantaQueue;

        public override void Handle(MessageEnvelope envelope)
        {
            lock (syncRoot)
                quantaQueue.Enqueue(envelope);
        }

        private void RunQuantumWorker()
        {
            try
            {
                while (true)
                {
                    lock (syncRoot)
                    {

                        MessageEnvelope envelope = null;
                        if (!quantaQueue.TryDequeue(out envelope))
                        {
                            Thread.Sleep(50);
                            continue;
                        }
                        ProcessQuantum(envelope);
                    }
                }
            }
            catch (Exception exc)
            {
                logger.Error(exc);
                throw;
            }
        }

        void ProcessQuantum(MessageEnvelope envelope)
        {
            try
            {
                if (!Global.QuantumProcessor.TryGetValue(envelope.Message.MessageType, out IQuantumRequestProcessor processor))
                {
                    //TODO: do not fail here - return unsupported error message;
                    throw new InvalidOperationException($"Quantum {envelope.Message.MessageType} is not supported.");
                }

                var quantumEnvelope = envelope;
                //we need to wrap client request
                if (!(envelope.Message is Quantum))
                {
                    quantumEnvelope = new RequestQuantum { RequestEnvelope = envelope }.CreateEnvelope();
                }

                var quantum = (Quantum)quantumEnvelope.Message;

                quantum.IsProcessed = false;

                processor.Validate(quantumEnvelope);

                //we must add it before processing, otherwise the quantum that we are processing here will be different from the quantum that will come to the auditor
                Global.QuantumStorage.AddQuantum(quantumEnvelope);

                //we need to sign the quantum here to prevent multiple signatures that can occur if we sign it when sending
                quantumEnvelope.Sign(Global.Settings.KeyPair);

                var resultMessage = processor.Process(quantumEnvelope);

                quantum.IsProcessed = true;

                Notifier.OnMessageProcessResult(resultMessage);

                logger.Trace($"Message of type {quantum.MessageType.ToString()} with apex {quantum.Apex} is handled.");
            }
            catch (Exception exc)
            {
                logger.Error(exc);
                Notifier.OnMessageProcessResult(new ResultMessage
                {
                    Status = ClientExceptionHelper.GetExceptionStatusCode(exc),
                    OriginalMessage = envelope
                });
            }
        }

        protected override void StartInternal()
        {
            Task.Factory.StartNew(RunQuantumWorker);
        }
    }
}
