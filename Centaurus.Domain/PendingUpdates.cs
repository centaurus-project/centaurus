using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace Centaurus.Domain
{
    public class PendingUpdates
    {
        public class PendingUpdatesItem
        {
            public PendingUpdatesItem(MessageEnvelope quantum, Effect[] effects)
            {
                Quantum = quantum;
                Effects = effects;
            }

            public MessageEnvelope Quantum { get; }

            public Effect[] Effects { get; }
        }

        List<PendingUpdatesItem> updates = new List<PendingUpdatesItem>();

        public void Add(MessageEnvelope quantum, Effect[] effects)
        {
            updates.Add(new PendingUpdatesItem(quantum, effects));
        }

        public List<PendingUpdatesItem> GetAll()
        {
            return updates;
        }
    }
}
