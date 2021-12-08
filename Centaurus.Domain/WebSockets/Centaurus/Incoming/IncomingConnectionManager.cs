using Centaurus.Domain;
using Centaurus.Models;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading.Tasks;
using System.Linq;
using Centaurus.Domain.WebSockets;
using Centaurus.Domain.StateManagers;
using System.Threading;

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
                    if (!includingAuditors && Context.Constellation.Auditors.Any(a => a.PubKey.Equals(pk))
                        || !connections.TryRemove(pk, out var connection))
                        continue;
                    await RemoveConnection(connection);

                }
                catch (Exception e)
                {
                    logger.Error(e, "Unable to close connection");
                }
        }

        internal void CleanupAuditorConnections()
        {
            var irrelevantAuditors = Context.NodesManager.GetRemoteNodes()
                .Where(ca => !Context.Constellation.Auditors.Any(a => a.PubKey.Equals(ca)))
                .Select(ca => ca.PubKey)
                .ToList();

            foreach (var pk in irrelevantAuditors)
                try
                {
                    if (!connections.TryRemove(pk, out var connection))
                        continue;
                    UnsubscribeAndClose(connection)
                        .ContinueWith(t =>
                        {
                            //we need to drop it, to prevent sending private constellation data
                            if (t.IsFaulted)
                                Context.NodesManager.CurrentNode.Failed(new Exception("Unable to drop irrelevant auditor connection."));
                        });
                }
                catch (Exception e)
                {
                    logger.Error(e, "Unable to close connection");
                }
        }

        #region Private members

        IncomingConnectionsCollection connections = new IncomingConnectionsCollection();

        private static State[] ValidStates = new State[] { State.Rising, State.Running, State.Ready };

        void Subscribe(IncomingConnectionBase connection)
        {
            connection.OnConnectionStateChanged += OnConnectionStateChanged;
        }

        void Unsubscribe(IncomingConnectionBase connection)
        {
            connection.OnConnectionStateChanged -= OnConnectionStateChanged;
        }

        async Task UnsubscribeAndClose(IncomingConnectionBase connection)
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

        void OnConnectionStateChanged((ConnectionBase connection, ConnectionState prev, ConnectionState current) args)
        {
            var connection = (IncomingConnectionBase)args.connection;
            switch (args.current)
            {
                case ConnectionState.Ready:
                    //avoid multiple validation event firing
                    if (args.prev == ConnectionState.Connected)
                        AddConnection(connection);
                    break;
                case ConnectionState.Closed:
                    //if prev connection wasn't validated than the validated connection could be dropped
                    if (args.prev == ConnectionState.Ready)
                        _ = RemoveConnection(connection);
                    break;
                default:
                    break;
            }
        }

        async Task RemoveConnection(IncomingConnectionBase connection)
        {
            await UnsubscribeAndClose(connection);

            connections.TryRemove(connection);
            if (connection.IsAuditor)
            {
                var nodeConnection = (IncomingNodeConnection)connection;
                nodeConnection.Node.RemoveIncomingConnection();
            }
        }

        #endregion
    }
}
