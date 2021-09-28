using Centaurus.Client;
using Centaurus.Models;
using NLog;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static Centaurus.Domain.StateManager;

namespace Centaurus.Domain
{
    public class OutgoingConnection : ConnectionBase, IAuditorConnection
    {
        static Logger logger = LogManager.GetCurrentClassLogger();
        private readonly OutgoingConnectionWrapperBase connection;
        private readonly OutgoingMessageStorage outgoingMessageStorage;

        public OutgoingConnection(ExecutionContext context, KeyPair keyPair, OutgoingMessageStorage outgoingMessageStorage, OutgoingConnectionWrapperBase connection)
            : base(context, keyPair, connection.WebSocket)
        {
            this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
            this.outgoingMessageStorage = outgoingMessageStorage ?? throw new ArgumentNullException(nameof(outgoingMessageStorage));
            AuditorState = Context.StateManager.GetAuditorState(keyPair);
            //we know that we connect to auditor so we can set connection state to validated immediately
            ConnectionState = ConnectionState.Ready;
        }

        const int BufferSize = 50 * 1024 * 1024;

        protected override int inBufferSize => BufferSize;
        protected override int outBufferSize => BufferSize;

        public AuditorState AuditorState { get; }

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
                        if (!cancellationToken.IsCancellationRequested
                            && outgoingMessageStorage.TryPeek(out MessageEnvelopeBase message)
                            && AuditorState.IsRunning
                            )
                        {
                            try
                            {
                                await base.SendMessage(message);
                                if (!outgoingMessageStorage.TryDequeue(out message))
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
