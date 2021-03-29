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
using Centaurus.Xdr;

namespace Centaurus.Domain
{
    public class AuditorWebSocketConnection : BaseWebSocketConnection
    {

        static Logger logger = LogManager.GetCurrentClassLogger();

        public AuditorWebSocketConnection(WebSocket webSocket, string ip)
            : base(webSocket, ip, AlphaWebSocketConnection.AuditorBufferSize, AlphaWebSocketConnection.AuditorBufferSize)
        {
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
        }

        protected override async Task<bool> HandleMessage(MessageEnvelope envelope)
        {
            return await MessageHandlers<AuditorWebSocketConnection>.HandleMessage(this, envelope.ToIncomingMessage(incommingBuffer));
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
            base.Dispose();
            webSocket?.Dispose();
            webSocket = null;
        }
    }
}
