using NLog;
using System;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    internal class OutgoingConnection : ConnectionBase, INodeConnection
    {
        static Logger logger = LogManager.GetCurrentClassLogger();
        private readonly OutgoingConnectionWrapperBase connection;

        public OutgoingConnection(ExecutionContext context, RemoteNode node, OutgoingConnectionWrapperBase connection)
            : base(context, node?.PubKey, connection.WebSocket)
        {
            this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
            Node = node ?? throw new ArgumentNullException(nameof(node));
        }

        const int BufferSize = 50 * 1024 * 1024;

        protected override int inBufferSize => BufferSize;
        protected override int outBufferSize => BufferSize;

        public RemoteNode Node { get; }

        public override bool IsAuditor => true;

        public async Task EstablishConnection(Uri uri)
        {
            await connection.Connect(uri, cancellationToken);
        }

        public void HandshakeDataSend()
        {
            Authenticated();
        }

        public override void Dispose()
        {
            base.Dispose();
            webSocket.Dispose();
        }
    }
}
