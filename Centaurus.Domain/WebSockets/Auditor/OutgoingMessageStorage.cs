using Centaurus.Domain;
using Centaurus.Models;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Centaurus.Domain
{

    public static class OutgoingMessageStorage
    {
        private readonly static ConcurrentQueue<Message> outgoingMessages = new ConcurrentQueue<Message>();

        public static void OnLedger(LedgerUpdateNotification ledger)
        {
            EnqueueMessage(ledger);
        }

        public static void EnqueueMessage(Message message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            outgoingMessages.Enqueue(message);
        }

        public static bool TryPeek(out Message message)
        {
            return outgoingMessages.TryPeek(out message);
        }

        public static bool TryDequeue(out Message message)
        {
            return outgoingMessages.TryDequeue(out message);
        }
    }
}
