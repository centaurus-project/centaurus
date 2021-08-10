using Centaurus.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class OutgoingMessageStorage
    {
        private readonly Queue<MessageEnvelope> outgoingMessages = new Queue<MessageEnvelope>();

        public void EnqueueMessage(Message message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            EnqueueMessage(message.CreateEnvelope());
        }

        public void EnqueueMessage(MessageEnvelope message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            lock (outgoingMessages)
                outgoingMessages.Enqueue(message);
        }

        public bool TryPeek(out MessageEnvelope message)
        {
            lock (outgoingMessages)
                return outgoingMessages.TryPeek(out message);
        }

        public bool TryDequeue(out MessageEnvelope message)
        {
            lock (outgoingMessages)
                return outgoingMessages.TryDequeue(out message);
        }
    }

    public class OutgoingResultsStorage: IDisposable
    {
        const int MaxMessageBatchSize = 50;

        static Logger logger = LogManager.GetCurrentClassLogger();
        readonly OutgoingMessageStorage outgoingMessageStorage;
        readonly List<AuditorResultMessage> results = new List<AuditorResultMessage>();

        public OutgoingResultsStorage(OutgoingMessageStorage outgoingMessageStorage)
        {
            this.outgoingMessageStorage = outgoingMessageStorage ?? throw new ArgumentNullException(nameof(outgoingMessageStorage));
            Run();
        }

        private CancellationTokenSource cts = new CancellationTokenSource();

        private void Run()
        {
            Task.Factory.StartNew(() =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var resultsBatch = default(List<AuditorResultMessage>);
                        lock (results)
                        {
                            if (results.Count != 0)
                            {
                                resultsBatch = results.Take(MaxMessageBatchSize).ToList();
                                var removeCount = Math.Min(MaxMessageBatchSize, results.Count);
                                results.RemoveRange(0, removeCount);
                            }
                        }
                        if (resultsBatch != default)
                        {
                            outgoingMessageStorage.EnqueueMessage(new AuditorResultsBatch { AuditorResultMessages = resultsBatch });
                            continue;
                        }
                        Thread.Sleep(50);
                    }
                    catch (Exception exc)
                    {
                        logger.Error(exc);
                    }
                }
            }, cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="result"></param>
        /// <param name="buffer">Buffer to use for serialization</param>
        public void EnqueueResult(AuditorResultMessage result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            lock (results)
                results.Add(result);
        }

        public void Dispose()
        {
            cts.Dispose();
        }
    }
}