using Centaurus.Client;
using Centaurus.Models;
using NLog;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class OutgoingConnection : ConnectionBase, IAuditorConnection
    {
        static Logger logger = LogManager.GetCurrentClassLogger();
        private OutgoingConnectionWrapperBase connection;

        public OutgoingConnection(ExecutionContext context, KeyPair keyPair, OutgoingConnectionWrapperBase connection)
            : base(context, keyPair, connection.WebSocket, ":0")
        {
            this.connection = connection;
        }

        const int BufferSize = 50 * 1024 * 1024;

        protected override int inBufferSize => BufferSize;
        protected override int outBufferSize => BufferSize;

        public async Task EstablishConnection(Uri uri)
        {
            await connection.Connect(uri, cancellationToken);
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
