using Centaurus.Domain;
using Centaurus.Models;
using NLog;
using stellar_dotnet_sdk;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class AuditorWebSocketConnection : ClientWebSocketConnection
    {

        static Logger logger = LogManager.GetCurrentClassLogger();

        CancellationTokenSource cancellationToken = new CancellationTokenSource();

        protected override async Task<bool> HandleMessage(MessageEnvelope envelope)
        {
            return await MessageHandlers<AuditorWebSocketConnection>.HandleMessage(this, envelope);
        }

        public override async Task EstablishConnection()
        {
            await base.EstablishConnection();
            _ = ProcessOutgoingMessageQueue();
        }

        private async Task ProcessOutgoingMessageQueue()
        {
            await Task.Factory.StartNew(async () =>
            {
                while (!cancellationToken.Token.IsCancellationRequested)
                {
                    try
                    {
                        if (ConnectionState == ConnectionState.Ready
                            && !cancellationToken.Token.IsCancellationRequested
                            && OutgoingMessageStorage.TryPeek(out Message message)
                            )
                        {
                            try
                            {
                                await base.SendMessage(message, cancellationToken.Token);
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
            }, cancellationToken.Token);
        }

        public override void Dispose()
        {
            cancellationToken?.Cancel();
            base.Dispose();
        }
    }
}
