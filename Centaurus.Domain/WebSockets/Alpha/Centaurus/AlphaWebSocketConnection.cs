using System;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Centaurus.Models;
using Centaurus.Domain;
using Centaurus.Xdr;
using NLog;
using System.Diagnostics;
using stellar_dotnet_sdk;

namespace Centaurus
{
    public class AlphaWebSocketConnection : BaseWebSocketConnection
    {
        public const int AuditorBufferSize = 50 * 1024 * 1024;

        static Logger logger = LogManager.GetCurrentClassLogger();

        public AlphaWebSocketConnection(AlphaContext context, WebSocket webSocket, string ip)
            : base(context, webSocket, ip, 1024, 64 * 1024)
        {

            var hd = new HandshakeData();
            hd.Randomize();
            HandshakeData = hd;
            _ = SendMessage(new HandshakeInit { HandshakeData = hd });
        }

        public AlphaContext AlphaContext => (AlphaContext)Context;

        public HandshakeData HandshakeData { get; }

        public AccountWrapper Account { get; set; }

        public QuantumSyncWorker QuantumWorker { get; private set; }
        private readonly object apexCursorSyncRoot = new { };

        internal void SetAuditor()
        {
            incommingBuffer.Dispose();
            incommingBuffer = XdrBufferFactory.Rent(AuditorBufferSize);
            outgoingBuffer.Dispose();
            outgoingBuffer = XdrBufferFactory.Rent(AuditorBufferSize);
            IsResultRequired = false;
            logger.Trace($"Connection {ClientKPAccountId} promoted to Auditor.");
        }

        private void ResetApexCursor(long newApexCursor)
        {
            lock (apexCursorSyncRoot)
            {
                logger.Trace($"Connection {ClientKPAccountId}, apex cursor reset requested. New apex cursor {newApexCursor}");
                //cancel current quantum worker
                QuantumWorker?.Dispose();

                //set new apex cursor, and start quantum worker
                QuantumWorker = new QuantumSyncWorker((AlphaContext)Context, newApexCursor, this);
                logger.Trace($"Connection {ClientKPAccountId}, apex cursor reseted. New apex cursor {newApexCursor}");
            }
        }

        internal void ResetApexCursor(SetApexCursor message)
        {
            ResetApexCursor(message.Apex);
        }

        protected override async Task<bool> HandleMessage(MessageEnvelope envelope)
        {
            var isHandled = await MessageHandlers<AlphaWebSocketConnection>.HandleMessage(this, envelope.ToIncomingMessage(incommingBuffer));

            return isHandled;
        }

        public override void Dispose()
        {
            QuantumWorker?.Dispose();
            base.Dispose();
        }
    }
}
