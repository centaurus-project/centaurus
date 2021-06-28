using Centaurus.Domain;
using Centaurus.Models;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading.Tasks;
using System.Linq;

namespace Centaurus.Domain
{
    /// <summary>
    /// Manages all client websocket connections
    /// </summary>
    public class ConnectionManager: ContextualBase
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public ConnectionManager(ExecutionContext context)
            :base(context)
        {
        }

        /// <summary>
        /// Gets the connection by the account public key
        /// </summary>
        /// <param name="pubKey">Account public key</param>
        /// <param name="connection">Current account connection</param>
        /// <returns>True if connection is found, otherwise false</returns>
        public bool TryGetConnection(RawPubKey pubKey, out IncomingWebSocketConnection connection)
        {
            return connections.TryGetValue(pubKey, out connection);
        }


        /// <summary>
        /// Gets all auditor connections
        /// </summary>
        /// <returns>The list of current auditor connections</returns>
        public List<IncomingWebSocketConnection> GetAuditorConnections()
        {
            var auditorConnections = new List<IncomingWebSocketConnection>();
            var auditors = Context.Constellation.Auditors;
            for (var i = 0; i < Context.Constellation.Auditors.Count; i++)
            {
                if (connections.TryGetValue(auditors[i], out IncomingWebSocketConnection auditorConnection))
                    auditorConnections.Add(auditorConnection);
            }
            return auditorConnections;
        }

        /// <summary>
        /// Registers new client websocket connection
        /// </summary>
        /// <param name="webSocket">New websocket connection</param>
        public async Task OnNewConnection(WebSocket webSocket, string ip)
        {
            Context.ExtensionsManager.BeforeNewConnection(webSocket, ip);
            if (webSocket == null)
                throw new ArgumentNullException(nameof(webSocket));
            using (var connection = new IncomingWebSocketConnection(Context, webSocket, ip))
            {
                Subscribe(connection);
                await connection.Listen();
            }
        }

        /// <summary>
        /// Closes all connection
        /// </summary>
        public async Task CloseAllConnections(bool includingAuditors = true)
        {
            var pubkeys = connections.Keys.ToArray();
            foreach (var pk in pubkeys)
                try
                {
                    //skip if auditor
                    if (!includingAuditors && Context.Constellation.Auditors.Contains(pk) 
                        || !connections.TryRemove(pk, out var connection))
                        continue;
                    await UnsubscribeAndClose(connection);

                }
                catch (Exception e)
                {
                    logger.Error(e, "Unable to close connection");
                }
        }

        #region Private members

        ConcurrentDictionary<RawPubKey, IncomingWebSocketConnection> connections = new ConcurrentDictionary<RawPubKey, IncomingWebSocketConnection>();

        void Subscribe(IncomingWebSocketConnection connection)
        {
            connection.OnConnectionStateChanged += OnConnectionStateChanged;
        }

        void Unsubscribe(IncomingWebSocketConnection connection)
        {
            connection.OnConnectionStateChanged -= OnConnectionStateChanged;
        }

        async Task UnsubscribeAndClose(IncomingWebSocketConnection connection)
        {
            Unsubscribe(connection);
            await connection.CloseConnection();
            logger.Trace($"{connection.PubKey} is disconnected.");
        }

        void AddConnection(IncomingWebSocketConnection connection)
        {
            lock (connection)
            {
                connections.AddOrUpdate(connection.PubKey, connection, (key, oldConnection) =>
                {
                    RemoveConnection(oldConnection);
                    return connection;
                });
                logger.Trace($"{connection.PubKey} is connected.");
            }
        }

        void OnConnectionStateChanged((BaseWebSocketConnection connection, ConnectionState prev, ConnectionState current) args)
        {
            var connection = (IncomingWebSocketConnection)args.connection;
            switch (args.current)
            {
                case ConnectionState.Validated:
                    if (args.prev != ConnectionState.Ready)
                        Validated(connection);
                    else
                        TrySetAuditorState(connection, args.current);
                    break;
                case ConnectionState.Closed:
                    RemoveConnection(connection);
                    break;
                case ConnectionState.Ready:
                    TrySetAuditorState(connection, args.current);
                    break;
                default:
                    break;
            }
        }

        void TrySetAuditorState(IncomingWebSocketConnection connection, ConnectionState state)
        {
            if (connection.IsAuditor)
            {
                Context.AppState.RegisterAuditorState(connection.PubKey, state);
                logger.Trace($"Auditor {connection.PubKey} is connected.");
            }
        }

        void RemoveConnection(IncomingWebSocketConnection connection)
        {
            lock (connection)
            {
                _ = UnsubscribeAndClose(connection);

                if (connection.PubKey != null)
                {
                    connections.TryRemove(connection.PubKey, out _);
                    if (connection.IsAuditor)
                    {
                        Context.AppState.AuditorConnectionClosed(connection.PubKey);
                        Context.Catchup.RemoveState(connection.PubKey);
                    }
                }
            }
        }

        void Validated(BaseWebSocketConnection baseConnection)
        {
            lock (baseConnection)
            {
                Context.ExtensionsManager.ConnectionValidated(baseConnection);
                var connection = (IncomingWebSocketConnection)baseConnection;
                AddConnection(connection);
            }
        }

        #endregion
    }
}
