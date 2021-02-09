using Centaurus.Domain;
using Centaurus.Models;
using NLog;
using stellar_dotnet_sdk;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class AuditorWebSocketConnection : BaseWebSocketConnection
    {

        static Logger logger = LogManager.GetCurrentClassLogger();

        public AuditorWebSocketConnection(WebSocket webSocket, string ip)
            : base(webSocket, ip)
        {
            //we don't need to create and sign heartbeat message on every sending
            hearbeatMessage = new Heartbeat().CreateEnvelope();
            hearbeatMessage.Sign(Global.Settings.KeyPair);
            MaxMessageSize = MaxMessageSize * Global.MaxMessageBatchSize;
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
            try
            {
                await SendMessage(hearbeatMessage);
            }
            catch (Exception exc)
            {
                logger.Error(exc, "Unable to send heartbeat message.");
                heartbeatTimer?.Reset();
            }
        }

        protected ClientWebSocket clientWebSocket => webSocket as ClientWebSocket;

        public async Task EstablishConnection()
        {
            await clientWebSocket.ConnectAsync(new Uri(((AuditorSettings)Global.Settings).AlphaAddress), cancellationToken);
            _ = Listen();
            _ = ProcessOutgoingMessageQueue();
        }

        public override async Task SendMessage(MessageEnvelope envelope)
        {
            await base.SendMessage(envelope);
            heartbeatTimer?.Reset();
        }

        protected override async Task<bool> HandleMessage(MessageEnvelope envelope)
        {
            return await MessageHandlers<AuditorWebSocketConnection>.HandleMessage(this, envelope);
        }

        private async Task ProcessOutgoingMessageQueue()
        {
            await Task.Factory.StartNew(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        if (ConnectionState == ConnectionState.Ready
                            && !cancellationToken.IsCancellationRequested
                            && OutgoingMessageStorage.TryPeek(out MessageEnvelope message)
                            )
                        {
                            try
                            {
                                await base.SendMessage(message);
                                if (!OutgoingMessageStorage.TryDequeue(out message))
                                {
                                    logger.Error("Unable to dequeue");
                                }
                            }
                            catch (Exception exc)
                            {
                                logger.Error(exc);
                            }
                        }
                        else
                        {
                            Thread.Sleep(50);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Error(e);
                    }
                }
            }, cancellationToken);
        }

        public override void Dispose()
        {
            heartbeatTimer?.Stop();
            heartbeatTimer?.Dispose();
            heartbeatTimer = null;

            base.Dispose();
            webSocket?.Dispose();
            webSocket = null;
        }
    }
}
