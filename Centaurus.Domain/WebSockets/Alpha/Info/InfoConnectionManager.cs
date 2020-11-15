using Centaurus.Domain;
using Centaurus.Models;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading.Tasks;
using System.Linq;
using Centaurus.Exchange.Analytics;
using Centaurus.Analytics;
using System.Threading;

namespace Centaurus.Domain
{
    /// <summary>
    /// Manages all client websocket connections
    /// </summary>
    public static class InfoConnectionManager
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Registers new client websocket connection
        /// </summary>
        /// <param name="webSocket">New websocket connection</param>
        public static async Task OnNewConnection(WebSocket webSocket, string connectionId, string ip)
        {
            var connection = new InfoWebSocketConnection(webSocket, connectionId, ip);
            Subscribe(connection);
            if (!connections.TryAdd(connectionId, connection))
                throw new Exception($"Connection with id {connectionId} already exists.");
            await connection.Listen();
        }

        /// <summary>
        /// Closes all connection
        /// </summary>
        public static void CloseAllConnections()
        {
            Parallel.ForEach(connections, async (c) =>
            {
                try
                {
                    await UnsubscribeAndClose(c.Value);
                }
                catch (Exception e)
                {
                    logger.Error(e, "Unable to close connection");
                }
            });
        }

        #region Private members

        static ConcurrentDictionary<string, InfoWebSocketConnection> connections = new ConcurrentDictionary<string, InfoWebSocketConnection>();

        static void Subscribe(InfoWebSocketConnection connection)
        {
            connection.OnClosed += OnClosed;
        }

        static void Unsubscribe(InfoWebSocketConnection connection)
        {
            connection.OnClosed -= OnClosed;
        }

        static void OnClosed(object sender, EventArgs args)
        {
            RemoveConnection((InfoWebSocketConnection)sender);
        }

        static async Task UnsubscribeAndClose(InfoWebSocketConnection connection)
        {
            Unsubscribe(connection);
            await connection.CloseConnection();
            connection.Dispose();
        }

        static void RemoveConnection(InfoWebSocketConnection connection)
        {
            _ = UnsubscribeAndClose(connection);
            connections.TryRemove(connection.ConnectionId, out _);
        }

        #endregion
    }
}
