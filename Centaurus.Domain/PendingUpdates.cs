using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace Centaurus.Domain
{
    public class PendingUpdates
    {
        Dictionary<MessageEnvelope, Effect[]> updates = new Dictionary<MessageEnvelope, Effect[]>();

        public void Add(MessageEnvelope quantum, Effect[] effects)
        {
            updates.Add(quantum, effects);
        }

        public Dictionary<MessageEnvelope, Effect[]> GetAll()
        {
            return updates;
        }
    }
}
