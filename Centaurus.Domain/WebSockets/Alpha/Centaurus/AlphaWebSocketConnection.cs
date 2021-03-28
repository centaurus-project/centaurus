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

        public AlphaWebSocketConnection(WebSocket webSocket, string ip)
            : base(webSocket, ip, 1024, 64 * 1024)
        {

            var hd = new HandshakeData();
            hd.Randomize();
            HandshakeData = hd;
            _ = SendMessage(new HandshakeInit { HandshakeData = hd });

            InitTimer();
        }

        private System.Timers.Timer invalidationTimer;

        //If we didn't receive message during specified interval, we should close connection
        private void InitTimer()
        {
            invalidationTimer = new System.Timers.Timer();
            invalidationTimer.Interval = 10000;
            invalidationTimer.AutoReset = false;
            invalidationTimer.Elapsed += InvalidationTimer_Elapsed;
        }

        private async void InvalidationTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (Debugger.IsAttached)
            {
                invalidationTimer.Reset();
                return;
            }
            await CloseConnection(WebSocketCloseStatus.PolicyViolation, "Connection is inactive");
        }

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
                QuantumWorker = new QuantumSyncWorker(newApexCursor, this);
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

            //reset invalidation timer only if message has been handled
            if (isHandled)
                invalidationTimer?.Reset();
            return isHandled;
        }

        public override void Dispose()
        {
            invalidationTimer?.Stop();
            invalidationTimer?.Dispose();
            invalidationTimer = null;

            QuantumWorker?.Dispose();
            base.Dispose();
        }
    }
}
