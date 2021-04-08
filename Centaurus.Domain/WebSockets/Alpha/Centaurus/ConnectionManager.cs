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
    public class ConnectionManager
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public ConnectionManager(AlphaContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Gets the connection by the account public key
        /// </summary>
        /// <param name="pubKey">Account public key</param>
        /// <param name="connection">Current account connection</param>
        /// <returns>True if connection is found, otherwise false</returns>
        public bool TryGetConnection(RawPubKey pubKey, out AlphaWebSocketConnection connection)
        {
            return connections.TryGetValue(pubKey, out connection);
        }


        /// <summary>
        /// Gets all auditor connections
        /// </summary>
        /// <returns>The list of current auditor connections</returns>
        public List<AlphaWebSocketConnection> GetAuditorConnections()
        {
            var auditorConnections = new List<AlphaWebSocketConnection>();
            var auditors = context.Constellation.Auditors;
            for (var i = 0; i < context.Constellation.Auditors.Count; i++)
            {
                if (connections.TryGetValue(auditors[i], out AlphaWebSocketConnection auditorConnection))
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
            context.ExtensionsManager.BeforeNewConnection(webSocket, ip);
            if (webSocket == null)
                throw new ArgumentNullException(nameof(webSocket));
            using (var connection = new AlphaWebSocketConnection(context, webSocket, ip))
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
            foreach (var connection in connections)
                try
                {
                    //skip if auditor
                    if (!includingAuditors && context.Constellation.Auditors.Contains(connection.Value.ClientPubKey))
                        continue;
                    await UnsubscribeAndClose(connection.Value);
                }
                catch (Exception e)
                {
                    logger.Error(e, "Unable to close connection");
                }
            connections.Clear();
        }

        #region Private members

        AlphaStateManager AlphaStateManager
        {
            get
            {
                return (AlphaStateManager)context.AppState;
            }
        }

        ConcurrentDictionary<RawPubKey, AlphaWebSocketConnection> connections = new ConcurrentDictionary<RawPubKey, AlphaWebSocketConnection>();
        private readonly AlphaContext context;

        void Subscribe(AlphaWebSocketConnection connection)
        {
            connection.OnConnectionStateChanged += OnConnectionStateChanged;
        }

        void Unsubscribe(AlphaWebSocketConnection connection)
        {
            connection.OnConnectionStateChanged -= OnConnectionStateChanged;
        }

        async Task UnsubscribeAndClose(AlphaWebSocketConnection connection)
        {
            Unsubscribe(connection);
            await connection.CloseConnection();
            logger.Trace($"{connection.ClientPubKey} is disconnected.");
        }

        void AddConnection(AlphaWebSocketConnection connection)
        {
            lock (connection)
            {
                connections.AddOrUpdate(connection.ClientPubKey, connection, (key, oldConnection) =>
                {
                    RemoveConnection(oldConnection);
                    return connection;
                });
                logger.Trace($"{connection.ClientPubKey} is connected.");
            }
        }

        void OnConnectionStateChanged((BaseWebSocketConnection connection, ConnectionState prev, ConnectionState current) args)
        {
            var connection = (AlphaWebSocketConnection)args.connection;
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

        void TrySetAuditorState(AlphaWebSocketConnection connection, ConnectionState state)
        {
            if (context.Constellation.Auditors.Contains(connection.ClientPubKey))
            {
                AlphaStateManager.RegisterAuditorState(connection.ClientPubKey, state);
                logger.Trace($"Auditor {connection.ClientPubKey} is connected.");
            }
        }

        void RemoveConnection(AlphaWebSocketConnection connection)
        {
            lock (connection)
            {
                _ = UnsubscribeAndClose(connection);

                if (connection.ClientPubKey != null)
                {
                    connections.TryRemove(connection.ClientPubKey, out _);
                    if (context.Constellation.Auditors.Contains(connection.ClientPubKey))
                    {
                        AlphaStateManager.AuditorConnectionClosed(connection.ClientPubKey);
                        context.Catchup.RemoveState(connection.ClientPubKey);
                    }
                }
            }
        }

        void Validated(BaseWebSocketConnection baseConnection)
        {
            lock (baseConnection)
            {
                context.ExtensionsManager.ConnectionValidated(baseConnection);
                var connection = (AlphaWebSocketConnection)baseConnection;
                AddConnection(connection);
            }
        }

        #endregion
    }
}
