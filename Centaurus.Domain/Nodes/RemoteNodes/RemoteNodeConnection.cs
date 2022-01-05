using Centaurus.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Centaurus.Domain.RemoteNodes
{
    internal class RemoteNodeConnection : ContextualBase
    {
        public RemoteNodeConnection(RemoteNode node)
            : base(node.Context)
        {
            Node = node ?? throw new ArgumentNullException(nameof(node));

        }

        public RemoteNode Node { get; }

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

        public async Task Shutdown()
        {
            var closeTask = Task.CompletedTask;
            lock (syncRoot)
            {
                isAborted = true;
                if (Connection != null)
                    closeTask = Connection.CloseConnection();
            }
            await closeTask;
        }

        public RawPubKey PubKey => Node.PubKey;

        public Uri Address => Node.Address;

        public event Action OnConnected;

        public event Action OnConnectionClosed;

        private void ConnectToAuditor()
        {
            Task.Factory.StartNew(async () =>
            {
                Uri connectionUri = GetConnectionUri();
                var connectionAttempts = 0;
                while (!isAborted)
                {
                    lock (syncRoot)
                    {
                        if (isAborted)
                            return;
                        Connection = new OutgoingConnection(Context, Node, Context.OutgoingConnectionFactory.GetConnection());
                        Connection.OnAuthenticated += Connection_OnAuthenticated;
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
                        Connection.OnAuthenticated -= Connection_OnAuthenticated;
                        Connection.Dispose();
                        Connection = null;
                        OnConnectionClosed?.Invoke();
                    }
                }
            });
        }

        private void Connection_OnAuthenticated(ConnectionBase obj)
        {
            OnConnected?.Invoke();
        }

        private Uri GetConnectionUri()
        {
            var uriBuilder = new UriBuilder(new Uri(Address, WebSocketConstants.CentaurusWebSocketEndPoint));
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
