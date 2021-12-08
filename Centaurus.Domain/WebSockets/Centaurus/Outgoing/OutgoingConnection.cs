using Centaurus.Client;
using Centaurus.Domain.StateManagers;
using Centaurus.Models;
using NLog;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static Centaurus.Domain.StateNotifierWorker;

namespace Centaurus.Domain
{
    internal class OutgoingConnection : ConnectionBase, INodeConnection
    {
        static Logger logger = LogManager.GetCurrentClassLogger();
        private readonly OutgoingConnectionWrapperBase connection;
        private readonly OutgoingMessageStorage outgoingMessageStorage;

        public OutgoingConnection(ExecutionContext context, RemoteNode node, OutgoingMessageStorage outgoingMessageStorage, OutgoingConnectionWrapperBase connection)
            : base(context, node?.PubKey, connection.WebSocket)
        {
            this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
            this.outgoingMessageStorage = outgoingMessageStorage ?? throw new ArgumentNullException(nameof(outgoingMessageStorage));
            Node = node;
            //we know that we connect to auditor so we can set connection state to validated immediately
            ConnectionState = ConnectionState.Ready;
        }

        const int BufferSize = 50 * 1024 * 1024;

        protected override int inBufferSize => BufferSize;
        protected override int outBufferSize => BufferSize;

        public RemoteNode Node { get; }

        public override bool IsAuditor => throw new NotImplementedException();

        public async Task EstablishConnection(Uri uri)
        {
            await connection.Connect(uri, cancellationToken);
            ProcessOutgoingMessageQueue();
        }


        Task sendingMessageTask;

        private void ProcessOutgoingMessageQueue()
        {
            sendingMessageTask = Task.Factory.StartNew(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        if (!cancellationToken.IsCancellationRequested
                            && Node.IsRunning()
                            && outgoingMessageStorage.TryPeek(out MessageEnvelopeBase message)
                            )
                        {
                            try
                            {
                                await base.SendMessage(message);
                                if (!outgoingMessageStorage.TryDequeue(out message))
                                    logger.Error("Unable to dequeue");
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
            }, cancellationToken).Unwrap();
        }

        public override void Dispose()
        {
            base.Dispose();
            webSocket.Dispose();
        }
    }
}
