using Centaurus.Domain;
using Centaurus.Models;
using System.Collections.Concurrent;

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
