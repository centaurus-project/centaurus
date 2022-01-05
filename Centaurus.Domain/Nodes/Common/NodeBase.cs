using Centaurus.Domain.Nodes.Common;
using Centaurus.Models;
using System;

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

        public bool IsAlpha => Context.NodesManager.AlphaNode == this;

        public abstract bool IsPrimeNode { get; }

        public void UpdateSettings(NodeSettings nodeSettings)
        {
            Settings = nodeSettings;
        }

        public NodeSettings Settings { get; protected set; }

        public byte Id => (byte)(Settings?.Id ?? 0);

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