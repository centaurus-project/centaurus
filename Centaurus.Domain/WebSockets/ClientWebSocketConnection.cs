using Centaurus.Models;
using NLog;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public abstract class ClientWebSocketConnection : BaseWebSocketConnection
    {
        public ClientWebSocketConnection()
            : base(new ClientWebSocket())
        {
            //we don't need to create and sign heartbeat message on every sending
            hearbeatMessage = new Heartbeat().CreateEnvelope();
            hearbeatMessage.Sign(Global.Settings.KeyPair);
#if !DEBUG
            InitTimer();
#endif
        }

        private System.Timers.Timer heartbeatTimer = null;

        //If we didn't receive message during specified interval, we should close connection
        private void InitTimer()
        {
            heartbeatTimer = new System.Timers.Timer();
            heartbeatTimer.Interval = 5000;
            heartbeatTimer.AutoReset = false;
            heartbeatTimer.Elapsed += HeartbeatTimer_Elapsed;
        }

        private MessageEnvelope hearbeatMessage;

        private async void HeartbeatTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            await SendMessage(hearbeatMessage);
        }

        protected ClientWebSocket clientWebSocket => webSocket as ClientWebSocket;

        public virtual async Task EstablishConnection()
        {
            await (webSocket as ClientWebSocket).ConnectAsync(new Uri(((AuditorSettings)Global.Settings).AlphaAddress), CancellationToken.None);
            _ = Listen();
        }

        public override async Task SendMessage(MessageEnvelope envelope, CancellationToken ct = default)
        {
            await base.SendMessage(envelope, ct);
            if (heartbeatTimer != null)
                heartbeatTimer.Reset();
        }
    }
}
