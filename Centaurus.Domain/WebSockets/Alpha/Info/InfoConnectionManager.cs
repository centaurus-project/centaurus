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
    public class InfoConnectionManager
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        private readonly AlphaContext context;

        public InfoConnectionManager(AlphaContext context)
        {
            this.context = context;
        } 

        /// <summary>
        /// Registers new client websocket connection
        /// </summary>
        /// <param name="webSocket">New websocket connection</param>
        public async Task OnNewConnection(WebSocket webSocket, string connectionId, string ip)
        {
            var connection = new InfoWebSocketConnection(context, webSocket, connectionId, ip);
            Subscribe(connection);
            if (!connections.TryAdd(connectionId, connection))
                throw new Exception($"Connection with id {connectionId} already exists.");
            await connection.Listen();
        }

        /// <summary>
        /// Closes all connection
        /// </summary>
        public async Task CloseAllConnections()
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

        public List<BaseSubscription> GetActiveSubscriptions()
        {
            return connections.Values.SelectMany(c => c.GetSubscriptions()).Distinct().ToList();
        }

        public void SendSubscriptionUpdates(Dictionary<BaseSubscription, SubscriptionUpdateBase> subsUpdates)
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

        public void SendSubscriptionUpdate(BaseSubscription subscription, SubscriptionUpdateBase subsUpdates)
        {
            foreach (var connection in connections)
            {
                Task.Factory.StartNew(async () => await SendSubscriptionUpdate(subscription, subsUpdates, connection.Value));
            }
        }

        #region Private members

        ConcurrentDictionary<string, InfoWebSocketConnection> connections = new ConcurrentDictionary<string, InfoWebSocketConnection>();

        void Subscribe(InfoWebSocketConnection connection)
        {
            connection.OnClosed += OnClosed;
        }

        void Unsubscribe(InfoWebSocketConnection connection)
        {
            connection.OnClosed -= OnClosed;
        }

        async Task SendSubscriptionUpdate(BaseSubscription subscription, SubscriptionUpdateBase update, InfoWebSocketConnection connection)
        {
            await connection.SendSubscriptionUpdate(subscription, update);
        }

        void OnClosed(object sender, EventArgs args)
        {
            RemoveConnection((InfoWebSocketConnection)sender);
        }

        async Task UnsubscribeAndClose(InfoWebSocketConnection connection)
        {
            Unsubscribe(connection);
            await connection.CloseConnection();
            connection.Dispose();
        }

        void RemoveConnection(InfoWebSocketConnection connection)
        {
            _ = UnsubscribeAndClose(connection);
            connections.TryRemove(connection.ConnectionId, out _);
        }

        #endregion
    }
}
