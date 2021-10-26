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
        private readonly List<MessageEnvelopeBase> outgoingMessages = new List<MessageEnvelopeBase>();

        private object syncRoot = new { };

        public void EnqueueMessage(MessageEnvelopeBase message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            lock (syncRoot)
                outgoingMessages.Add(message);
        }

        public bool TryPeek(out MessageEnvelopeBase message)
        {
            lock (syncRoot)
                message = outgoingMessages.FirstOrDefault();
            return message != null;
        }

        public void Dequeue()
        {
            lock (syncRoot)
                outgoingMessages.RemoveAt(0);
        }
    }

    public class OutgoingResultsStorage : IDisposable
    {
        const int MaxMessageBatchSize = 500;

        static Logger logger = LogManager.GetCurrentClassLogger();
        readonly OutgoingMessageStorage outgoingMessageStorage;
        readonly List<AuditorResult> results = new List<AuditorResult>();
        object syncRoot = new { };

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
                        var resultsBatch = default(List<AuditorResult>);
                        lock (syncRoot)
                        {
                            if (results.Count != 0)
                            {
                                resultsBatch = results.Take(MaxMessageBatchSize).ToList();
                                var removeCount = Math.Min(MaxMessageBatchSize, results.Count);
                                results.RemoveRange(0, removeCount);

                                logger.Trace($"Results batch sent. Batch size: {resultsBatch.Count}, apexes: {resultsBatch[0].Apex}-{resultsBatch[resultsBatch.Count - 1].Apex}.");
                            }
                        }
                        if (resultsBatch != default)
                        {
                            outgoingMessageStorage.EnqueueMessage(
                                new AuditorSignaturesBatch
                                {
                                    AuditorResultMessages = resultsBatch
                                }.CreateEnvelope<MessageEnvelopeSignless>());
                        }
                        else
                        {
                            Thread.Sleep(50);
                        }
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
        public void EnqueueResult(AuditorResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            lock (syncRoot)
                results.Add(result);
        }

        public void Dispose()
        {
            cts.Dispose();
        }
    }
}