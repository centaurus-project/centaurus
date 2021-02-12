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
    public static class ConnectionManager
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Gets the connection by the account public key
        /// </summary>
        /// <param name="pubKey">Account public key</param>
        /// <param name="connection">Current account connection</param>
        /// <returns>True if connection is found, otherwise false</returns>
        public static bool TryGetConnection(RawPubKey pubKey, out AlphaWebSocketConnection connection)
        {
            return connections.TryGetValue(pubKey, out connection);
        }


        /// <summary>
        /// Gets all auditor connections
        /// </summary>
        /// <returns>The list of current auditor connections</returns>
        public static List<AlphaWebSocketConnection> GetAuditorConnections()
        {
            var auditorConnections = new List<AlphaWebSocketConnection>();
            var auditors = Global.Constellation.Auditors;
            for (var i = 0; i < Global.Constellation.Auditors.Count; i++)
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
        public static async Task OnNewConnection(WebSocket webSocket, string ip)
        {
            Global.ExtensionsManager.BeforeNewConnection(webSocket, ip);
            if (webSocket == null)
                throw new ArgumentNullException(nameof(webSocket));
            using (var connection = new AlphaWebSocketConnection(webSocket, ip))
            {
                Subscribe(connection);
                await connection.Listen();
            }
        }

        /// <summary>
        /// Closes all connection
        /// </summary>
        public static async Task CloseAllConnections(bool includingAuditors = true)
        {
            foreach (var connection in connections)
                try
                {
                    //skip if auditor
                    if (!includingAuditors && Global.Constellation.Auditors.Contains(connection.Value.ClientPubKey))
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

        static AlphaStateManager AlphaStateManager
        {
            get
            {
                return (AlphaStateManager)Global.AppState;
            }
        }

        static ConcurrentDictionary<RawPubKey, AlphaWebSocketConnection> connections = new ConcurrentDictionary<RawPubKey, AlphaWebSocketConnection>();

        static void Subscribe(AlphaWebSocketConnection connection)
        {
            connection.OnConnectionStateChanged += OnConnectionStateChanged;
        }

        static void Unsubscribe(AlphaWebSocketConnection connection)
        {
            connection.OnConnectionStateChanged -= OnConnectionStateChanged;
        }

        static async Task UnsubscribeAndClose(AlphaWebSocketConnection connection)
        {
            Unsubscribe(connection);
            await connection.CloseConnection();
            logger.Trace($"{connection.ClientPubKey} is disconnected.");
        }

        static void AddConnection(AlphaWebSocketConnection connection)
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

        static void OnConnectionStateChanged(object sender, ConnectionState e)
        {
            switch (e)
            {
                case ConnectionState.Validated:
                    Validated((AlphaWebSocketConnection)sender);
                    break;
                case ConnectionState.Closed:
                    RemoveConnection((AlphaWebSocketConnection)sender);
                    break;
                case ConnectionState.Ready:
                    EnsureAuditorConnected((AlphaWebSocketConnection)sender);
                    break;
                default:
                    break;
            }
        }

        private static void EnsureAuditorConnected(AlphaWebSocketConnection connection)
        {
            if (Global.Constellation.Auditors.Contains(connection.ClientPubKey))
            {
                AlphaStateManager.AuditorConnected(connection.ClientPubKey);
                logger.Trace($"Auditor {connection.ClientPubKey} is connected.");
            }
        }

        static void RemoveConnection(AlphaWebSocketConnection connection)
        {
            lock (connection)
            {
                _ = UnsubscribeAndClose(connection);

                if (connection.ClientPubKey != null)
                {
                    connections.TryRemove(connection.ClientPubKey, out _);
                    if (Global.Constellation.Auditors.Contains(connection.ClientPubKey))
                        AlphaStateManager.AuditorConnectionClosed(connection.ClientPubKey);
                }
            }
        }

        static void Validated(BaseWebSocketConnection baseConnection)
        {
            lock (baseConnection)
            {
                Global.ExtensionsManager.ConnectionValidated(baseConnection);
                var connection = (AlphaWebSocketConnection)baseConnection;
                AddConnection(connection);
            }
        }

        #endregion
    }
}
