using Centaurus.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Centaurus.Domain.StateManagers
{
    internal class RemoteNodeConnection : ContextualBase
    {
        public RemoteNodeConnection(RemoteNode node)
            : base(node.Context)
        {
            Node = node ?? throw new ArgumentNullException(nameof(node));

            OutgoingMessageStorage = new OutgoingMessageStorage();
            OutgoingResultsStorage = new OutgoingResultsStorage(OutgoingMessageStorage);
        }

        public RemoteNode Node { get; }

        public OutgoingMessageStorage OutgoingMessageStorage { get; }

        public OutgoingResultsStorage OutgoingResultsStorage { get; }

        public void Run()
        {
            try
            {
                ConnectToAuditor();
            }
            catch (Exception exc)
            {
                logger.Error(exc);
                throw;
            }
        }

        public void Shutdown()
        {
            lock (syncRoot)
            {
                isAborted = true;
                Connection?.CloseConnection();
                OutgoingResultsStorage.Dispose();
            }
        }

        public RawPubKey PubKey => Node.PubKey;

        public Uri Address => Node.Address;

        private void ConnectToAuditor()
        {
            Task.Factory.StartNew(async () =>
            {
                Uri connectionUri = GetConnectionUri();
                var connectionAttempts = 0;
                var listenTask = default(Task);
                while (!isAborted)
                {
                    //TODO: remove this condition after refactoring result message broadcasting
                    //wait while all pending quanta will be handled
                    if (Context.NodesManager.CurrentNode.State == State.Rising)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }
                    lock (syncRoot)
                    {
                        if (!isAborted)
                            Connection = new OutgoingConnection(Context, Node, OutgoingMessageStorage, Context.OutgoingConnectionFactory.GetConnection());
                    }
                    try
                    {
                        await Connection.EstablishConnection(connectionUri);
                        await Connection.Listen();
                    }
                    catch (Exception exc)
                    {

                        if (!(exc is OperationCanceledException) && connectionAttempts % 100 == 0)
                            logger.Error(exc, $"Unable establish connection with {connectionUri} after {connectionAttempts} attempts. Retry in 1000ms");
                        Thread.Sleep(1000);
                        connectionAttempts++;
                    }
                    finally
                    {
                        Connection.Dispose();
                        Connection = null;
                    }
                }
            });
        }

        private Uri GetConnectionUri()
        {
            var uriBuilder = new UriBuilder(Address);
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query[WebSocketConstants.PubkeyParamName] = Context.Settings.KeyPair.AccountId;
            uriBuilder.Query = query.ToString();

            var connectionUri = uriBuilder.Uri;
            return connectionUri;
        }

        private bool isAborted = false;

        public OutgoingConnection Connection { get; private set; }

        private static Logger logger = LogManager.GetCurrentClassLogger();
        private object syncRoot = new { };
    }
}
