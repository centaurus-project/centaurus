using Centaurus.Domain.Quanta.Sync;
using Centaurus.Domain.StateManagers;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Centaurus.Domain
{
    public abstract class NodeBase: ContextualBase
    {
        public NodeBase(ExecutionContext context, RawPubKey rawPubKey)
            :base(context)
        {
            PubKey = rawPubKey ?? throw new ArgumentNullException(nameof(rawPubKey));
            AccountId = rawPubKey.GetAccountId();
        }

        public RawPubKey PubKey { get; }

        public string AccountId { get; }

        public State State { get; protected set; } = State.Undefined;

        public ulong LastApex => apexes.LastValue;

        public int QuantaPerSecond => apexes.GetAvg();

        public int QuantaQueueLength => quantaQueueLengths.GetAvg();

        public ulong LastPersistedApex { get; protected set; }

        public DateTime UpdateDate { get; protected set; }

        protected virtual void SetApex(DateTime updateDate, ulong apex)
        {
            apexes.AddValue(updateDate, apex);
        }

        protected virtual void SetQuantaQueueLength(DateTime updateDate, int queueLength)
        {
            quantaQueueLengths.AddValue(updateDate, queueLength);
        }

        private NodeQuantaQueueLengths quantaQueueLengths = new NodeQuantaQueueLengths();
        private NodeApexes apexes = new NodeApexes();
    }
}