using Centaurus.Domain.WebSockets;
using Centaurus.Models;
using NLog;
using System;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    /// <summary>
    /// Manages all client websocket connections
    /// </summary>
    public class IncomingConnectionManager : ContextualBase
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        internal IncomingConnectionManager(ExecutionContext context)
            : base(context)
        {
        }

        /// <summary>
        /// Registers new client websocket connection
        /// </summary>
        /// <param name="webSocket">New websocket connection</param>
        public async Task OnNewConnection(WebSocket webSocket, RawPubKey rawPubKey, string ip)
        {
            if (webSocket == null)
                throw new ArgumentNullException(nameof(webSocket));

            if (!ValidStates.Contains(Context.NodesManager.CurrentNode.State))
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.ProtocolError, "Server is not ready.", CancellationToken.None);
                return;
            }

            Context.ExtensionsManager.BeforeNewConnection(webSocket, ip);

            var connection = default(IncomingConnectionBase);
            if (Context.NodesManager.TryGetNode(rawPubKey, out var node))
                connection = new IncomingNodeConnection(Context, webSocket, ip, node);
            else
                connection = new IncomingClientConnection(Context, rawPubKey, webSocket, ip);
            using (connection)
            {
                Subscribe(connection);
                await connection.Listen();
            }
        }

        internal bool TryGetConnection(RawPubKey account, out IncomingConnectionBase connection)
        {
            return connections.TryGetConnection(account, out connection);
        }

        /// <summary>
        /// Closes all connection
        /// </summary>
        internal async Task CloseAllConnections(bool includingAuditors = true)
        {
            var pubkeys = connections.GetAllPubKeys();
            foreach (var pk in pubkeys)
                try
                {
                    //skip if auditor
                    if (!includingAuditors && Context.NodesManager.IsNode(pk)
                        || !connections.TryRemove(pk, out var connection))
                        continue;
                    await RemoveConnection(connection);
                }
                catch (Exception e)
                {
                    logger.Error(e, "Unable to close connection");
                }
        }

        #region Private members

        IncomingConnectionsCollection connections = new IncomingConnectionsCollection();

        private static State[] ValidStates = new State[] { State.Rising, State.Running, State.Ready };

        private void Subscribe(IncomingConnectionBase connection)
        {
            connection.OnAuthenticated += Connection_OnAuthenticated;
            connection.OnClosed += Connection_OnClosed;
        }

        private void Unsubscribe(IncomingConnectionBase connection)
        {
            connection.OnAuthenticated -= Connection_OnAuthenticated;
            connection.OnClosed -= Connection_OnClosed;
        }
        private void Connection_OnAuthenticated(ConnectionBase connection)
        {
            AddConnection((IncomingConnectionBase)connection);
        }

        private void Connection_OnClosed(ConnectionBase connection)
        {
            _ = RemoveConnection((IncomingConnectionBase)connection);
        }

        private async Task UnsubscribeAndClose(IncomingConnectionBase connection)
        {
            Unsubscribe(connection);
            await connection.CloseConnection();
            logger.Trace($"{connection.PubKey} is disconnected.");
        }

        void AddConnection(IncomingConnectionBase connection)
        {
            connections.Add(connection);
            logger.Trace($"{connection.PubKey} is connected.");
        }

        async Task RemoveConnection(IncomingConnectionBase connection)
        {
            await UnsubscribeAndClose(connection);

            if (connection.IsAuthenticated)
            {
                connections.TryRemove(connection);
                if (connection.IsAuditor)
                {
                    var nodeConnection = (IncomingNodeConnection)connection;
                    nodeConnection.Node.RemoveIncomingConnection();
                }
            }
        }

        #endregion
    }
}
