﻿using Centaurus.Models;
using Centaurus.Xdr;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Domain
{

    public class OutgoingMessageStorage
    {
        private readonly Queue<MessageEnvelope> outgoingMessages = new Queue<MessageEnvelope>();

        public void OnTransaction(TxNotification tx)
        {
            EnqueueMessage(tx);
        }

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

    public class OutgoingResultsStorage
    {
        const int MaxMessageBatchSize = 50;

        static Logger logger = LogManager.GetCurrentClassLogger();

        readonly static List<AuditorResultMessage> results = new List<AuditorResultMessage>();
        private readonly AuditorContext context;

        public OutgoingResultsStorage(AuditorContext context)
        {
            this.context = context;
            _ = Task.Factory.StartNew(RunWorker);
        }

        private void RunWorker()
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
                            resultsBatch = results.Take(MaxMessageBatchSize).ToList();
                            var removeCount = Math.Min(MaxMessageBatchSize, results.Count);
                            results.RemoveRange(0, removeCount);
                        }
                    }
                    if (resultsBatch != default)
                    {
                        context.OutgoingMessageStorage.EnqueueMessage(new AuditorResultsBatch { AuditorResultMessages = resultsBatch });
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="result"></param>
        /// <param name="buffer">Buffer to use for serialization</param>
        public void EnqueueResult(ResultMessage result, byte[] buffer)
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
            resultEnvelope.Sign(context.Settings.KeyPair, buffer);
            signature = resultEnvelope.Signatures[0].Signature;

            lock (results)
                results.Add(new AuditorResultMessage
                {
                    Apex = result.MessageId,
                    Signature = signature,
                    TxSignature = txSignature
                });
        }
    }
}
