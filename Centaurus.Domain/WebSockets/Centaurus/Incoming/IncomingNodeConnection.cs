using Centaurus.Domain;
using Centaurus.Domain.StateManagers;
using System;
using System.Net.WebSockets;

namespace Centaurus
{
    internal class IncomingNodeConnection : IncomingConnectionBase, INodeConnection
    {
        public IncomingNodeConnection(ExecutionContext context,  WebSocket webSocket, string ip, RemoteNode node)
            : base(context, node?.PubKey, webSocket, ip)
        {
            Node = node ?? throw new ArgumentNullException(nameof(node));
            SendHandshake();
        }

        const int AuditorBufferSize = 50 * 1024 * 1024;

        protected override int inBufferSize => AuditorBufferSize;

        protected override int outBufferSize => AuditorBufferSize;

        public RemoteNode Node { get; }

        public override bool IsAuditor => true;
    }
}