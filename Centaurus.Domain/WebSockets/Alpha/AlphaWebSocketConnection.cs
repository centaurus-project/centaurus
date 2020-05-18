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

        public AlphaWebSocketConnection(WebSocket webSocket, string ip)
            : base(webSocket, ip)
        {
            var hd = new HandshakeData();
            hd.Randomize();
            HandshakeData = hd;
            _ = SendMessage(new HandshakeInit { HandshakeData = hd });


#if !DEBUG
            InitTimer();
#endif
        }

        private System.Timers.Timer invalidationTimer = null;

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
            await CloseConnection(WebSocketCloseStatus.PolicyViolation, "Connection is inactive");
        }

        public HandshakeData HandshakeData { get; }

        public AccountWrapper Account { get; set; } 

        private QuantumSyncWorker quantumWorker;
        private readonly object apexCursorSyncRoot = new { };

        private void ResetApexCursor(long newApexCursor)
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
        }

        protected override async Task<bool> HandleMessage(MessageEnvelope envelope)
        {
            if (Account != null && envelope.Message is RequestMessage) //if Account prop is not null than it's client connection and it's already validated
            {
                ((RequestMessage)envelope.Message).AccountWrapper = Account;
            }

            var isHandled = await MessageHandlers<AlphaWebSocketConnection>.HandleMessage(this, envelope);

            //reset invalidation timer only if message has been handled
            if (isHandled && invalidationTimer != null)
                invalidationTimer.Reset();
            return isHandled;
        }

        public override void Dispose()
        {
            quantumWorker?.CancelAndDispose();
            base.Dispose();
        }
    }
}
