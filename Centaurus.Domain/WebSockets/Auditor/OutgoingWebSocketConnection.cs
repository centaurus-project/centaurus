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
    public class OutgoingWebSocketConnection : BaseWebSocketConnection
    {
        static Logger logger = LogManager.GetCurrentClassLogger();
        private ClientConnectionWrapperBase connection;

        public OutgoingWebSocketConnection(ExecutionContext context, ClientConnectionWrapperBase connection)
            : base(context, connection.WebSocket, null, IncomingWebSocketConnection.AuditorBufferSize, IncomingWebSocketConnection.AuditorBufferSize)
        {
            this.connection = connection;
        }

        public async Task EstablishConnection()
        {
            await connection.Connect(new Uri(Context.Settings.AuditorAddressBook.First()), cancellationToken);
            _ = Task.Factory.StartNew(Listen, TaskCreationOptions.LongRunning);
            ProcessOutgoingMessageQueue();
        }

        public override async Task SendMessage(MessageEnvelope envelope)
        {
            await base.SendMessage(envelope);
        }

        protected override async Task<bool> HandleMessage(MessageEnvelope envelope)
        {
            return await Context.AuditorMessageHandlers.HandleMessage(this, envelope.ToIncomingMessage(incommingBuffer));
        }

        private void ProcessOutgoingMessageQueue()
        {
            Task.Factory.StartNew(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        if (ConnectionState == ConnectionState.Ready
                            && !cancellationToken.IsCancellationRequested
                            && Context.OutgoingMessageStorage.TryPeek(out MessageEnvelope message)
                            )
                        {
                            try
                            {
                                await base.SendMessage(message);
                                if (!Context.OutgoingMessageStorage.TryDequeue(out message))
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
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public override void Dispose()
        {
            base.Dispose();
            webSocket.Dispose();
        }
    }
}
