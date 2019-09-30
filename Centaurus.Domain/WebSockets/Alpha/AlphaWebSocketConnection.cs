using System;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Centaurus.Models;
using Centaurus.Domain;
using NLog;

namespace Centaurus
{
    public class AlphaWebSocketConnection : BaseWebSocketConnection
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public AlphaWebSocketConnection(WebSocket webSocket)
            : base(webSocket, true)
        {
            var hd = new HandshakeData();
            hd.Randomize();
            HandshakeData = hd;
            _ = SendMessage(new HandshakeInit { HandshakeData = hd });
        }

        public HandshakeData HandshakeData { get; }

        public bool IsAuditor { get; set; }

        private QuantumSyncWorker quantumWorker;

        private readonly object apexCursorSyncRoot = new { };

        private void ResetApexCursor(ulong newApexCursor)
        {
            lock (apexCursorSyncRoot)
            {
                //cancel current quantum worker
                quantumWorker?.CancelAndDispose();

                //set new apex cursor, and start quantum worker
                quantumWorker = new QuantumSyncWorker(newApexCursor, this);
            }
        }

        internal void ResetApexCursor(SetApexCursor message)
        {
            ResetApexCursor(message.Apex);
            if (ConnectionState != ConnectionState.Ready)
                ConnectionState = ConnectionState.Ready;
        }

        protected override async Task<bool> HandleMessage(MessageEnvelope envelope)
        {
            return await MessageHandlers<AlphaWebSocketConnection>.HandleMessage(this, envelope);
        }

        public override void Dispose()
        {
            quantumWorker?.CancelAndDispose();
            base.Dispose();
        }
    }
}
