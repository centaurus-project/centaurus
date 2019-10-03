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
    public class AlphaQuantumHandler: BaseQuantumHandler
    {
        private Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// The last processed quantum that was added on Alpha rising
        /// </summary>
        private LinkedListNode<MessageEnvelope> lastAddedQuantum;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="quanta">A quanta array ordered by an apex</param>
        public AlphaQuantumHandler(IEnumerable<MessageEnvelope> quanta)
        {
            if (quanta != null)
                foreach (var quantum in quanta)
                {
                    lastAddedQuantum = messageQueue.AddLast(quantum);
                }

            Task.Factory.StartNew(RunQuantumWorker);
        }

        private readonly object syncRoot = new { };

        private readonly LinkedList<MessageEnvelope> messageQueue = new LinkedList<MessageEnvelope>();

        public override void Handle(MessageEnvelope envelope)
        {
            lock (syncRoot)
            {
                if (envelope.Message is SnapshotQuantum)
                {
                    if (lastAddedQuantum == null)
                        messageQueue.AddFirst(envelope);
                    else
                        messageQueue.AddAfter(lastAddedQuantum, envelope);
                }
                else
                    messageQueue.AddLast(envelope);
            }
        }

        private void RunQuantumWorker()
        {
            try
            {
                while (true)
                {
                    MessageEnvelope envelope = null;
                    lock (syncRoot)
                    {
                        LinkedListNode<MessageEnvelope> quantumItem = messageQueue.First;
                        if (quantumItem != null)
                        {
                            envelope = quantumItem.Value;
                            messageQueue.Remove(quantumItem);

                            //check if all restored quanta are processed
                            if (quantumItem == lastAddedQuantum)
                                lastAddedQuantum = null;
                        }
                        if (envelope == null)
                        {
                            Thread.Sleep(50);
                            continue;
                        }
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

                            var resultMessage = processor.Process(quantumEnvelope);

                            quantum.IsProcessed = true;

                            Notifier.OnMessageProcessResult(resultMessage);
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
                }
            }
            catch (Exception exc)
            {
                logger.Error(exc);
                throw;
            }
        }

        public override void Start()
        {
        }
    }
}
