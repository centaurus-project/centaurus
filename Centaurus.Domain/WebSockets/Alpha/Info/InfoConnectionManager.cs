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
        public static async Task CloseAllConnections()
        {
            foreach (var connection in connections)
            {
                try
                {
                    await UnsubscribeAndClose(connection.Value);
                }
                catch (Exception e)
                {
                    logger.Error(e, "Unable to close connection");
                }
            }
            connections.Clear();
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

        public static List<BaseSubscription> GetActiveSubscriptions()
        {
            return connections.Values.SelectMany(c => c.GetSubscriptions()).Distinct().ToList();
        }

        public static void SendSubscriptionUpdates(Dictionary<BaseSubscription, SubscriptionUpdateBase> subsUpdates)
        {
            foreach (var update in subsUpdates)
            {
                var subs = update.Key;
                var subsUpdate = update.Value;
                foreach (var connection in connections)
                {
                    Task.Factory.StartNew(async () => await SendSubscriptionUpdate(subs, subsUpdate, connection.Value));
                }
            }
        }

        static async Task SendSubscriptionUpdate(BaseSubscription subscription, SubscriptionUpdateBase update, InfoWebSocketConnection connection)
        {
            await connection.SendSubscriptionUpdate(subscription, update);
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
