using Centaurus.Client;
using Centaurus.Models;
using NLog;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
