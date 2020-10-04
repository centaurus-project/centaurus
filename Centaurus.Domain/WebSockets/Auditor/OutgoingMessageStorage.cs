using Centaurus.Domain;
using Centaurus.Models;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Centaurus.Domain
{

    public static class OutgoingMessageStorage
    {
        private readonly static ConcurrentQueue<MessageEnvelope> outgoingMessages = new ConcurrentQueue<MessageEnvelope>();

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
            outgoingMessages.Enqueue(message);
        }

        public static bool TryPeek(out MessageEnvelope message)
        {
            return outgoingMessages.TryPeek(out message);
        }

        public static bool TryDequeue(out MessageEnvelope message)
        {
            return outgoingMessages.TryDequeue(out message);
        }
    }
}
