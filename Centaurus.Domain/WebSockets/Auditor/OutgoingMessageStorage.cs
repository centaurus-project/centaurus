using Centaurus.Models;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Domain
{

    public static class OutgoingMessageStorage
    {
        private readonly static Queue<MessageEnvelope> outgoingMessages = new Queue<MessageEnvelope>();

        public static void OnTransaction(TxNotification tx)
        {
            EnqueueMessage(tx);
        }

        public static void EnqueueMessage(Message message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            EnqueueMessage(message.CreateEnvelope());
        }

        public static void EnqueueMessage(MessageEnvelope message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            lock (outgoingMessages)
                outgoingMessages.Enqueue(message);
        }

        public static bool TryPeek(out MessageEnvelope message)
        {
            lock (outgoingMessages)
                return outgoingMessages.TryPeek(out message);
        }

        public static bool TryDequeue(out MessageEnvelope message)
        {
            lock (outgoingMessages)
                return outgoingMessages.TryDequeue(out message);
        }
    }

    public static class OutgoingResultsStorage
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        private readonly static List<AuditorResultMessage> results = new List<AuditorResultMessage>();

        static OutgoingResultsStorage()
        {
            _ = Task.Factory.StartNew(RunWorker);
        }

        private static void RunWorker()
        {
            while (true)
            {
                try
                {
                    var resultsBatch = default(List<AuditorResultMessage>);
                    lock (results)
                    {
                        if (results.Count != 0)
                        {
                            resultsBatch = results.Take(Global.MaxMessageBatchSize).ToList();
                            var removeCount = Math.Min(Global.MaxMessageBatchSize, results.Count);
                            results.RemoveRange(0, removeCount);
                        }
                    }
                    if (resultsBatch != default)
                    {
                        OutgoingMessageStorage.EnqueueMessage(new AuditorResultsBatch { AuditorResultMessages = resultsBatch });
                        continue;
                    }
                    Thread.Sleep(50);
                }
                catch (Exception exc)
                {
                    logger.Error(exc);
                }
            }
        }

        public static void EnqueueResult(ResultMessage result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            var signature = default(byte[]);
            var txSignature = default(byte[]);
            if (result is ITransactionResultMessage txResult)
            {
                txSignature = txResult.TxSignatures.FirstOrDefault()?.Signature;
                if (txSignature == null)
                    throw new Exception("Tx signature is missing in ITransactionResultMessage.");
                //TODO: create special envelope for storing tx signature
                //ugly solution 
                txResult.TxSignatures.Clear();
            }

            var resultEnvelope = result.CreateEnvelope();
            resultEnvelope.Sign(Global.Settings.KeyPair);
            signature = resultEnvelope.Signatures[0].Signature;

            lock (results)
                results.Add(new AuditorResultMessage { Apex = result.MessageId, Signature = signature, TxSignature = txSignature });
        }
    }
}
