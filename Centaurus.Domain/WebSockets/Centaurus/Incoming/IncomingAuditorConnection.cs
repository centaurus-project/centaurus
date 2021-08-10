using Centaurus.Domain;
using Centaurus.Models;
using NLog;
using System.Net.WebSockets;

namespace Centaurus
{
    public class IncomingAuditorConnection : IncomingConnectionBase, IAuditorConnection
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public IncomingAuditorConnection(ExecutionContext context, KeyPair keyPair, WebSocket webSocket, string ip)
            : base(context, keyPair, webSocket, ip)
        {
            SendHandshake();
        }

        const int AuditorBufferSize = 50 * 1024 * 1024;

        protected override int inBufferSize => AuditorBufferSize;

        protected override int outBufferSize => AuditorBufferSize;

        public QuantumSyncWorker QuantumWorker { get; private set; }
        private readonly object apexCursorSyncRoot = new { };

        public void SetApexCursor(ulong newApexCursor)
        {
            lock (apexCursorSyncRoot)
            {
                logger.Trace($"Connection {PubKeyAddress}, apex cursor reset requested. New apex cursor {newApexCursor}");
                //cancel current quantum worker
                QuantumWorker?.Dispose();

                //set new apex cursor, and start quantum worker
                QuantumWorker = new QuantumSyncWorker(Context, newApexCursor, this);
                logger.Trace($"Connection {PubKeyAddress}, apex cursor reseted. New apex cursor {newApexCursor}");
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            QuantumWorker?.Dispose();
        }
    }
}