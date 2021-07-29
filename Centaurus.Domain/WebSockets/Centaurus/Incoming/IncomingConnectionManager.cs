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
    public class IncomingConnectionManager : ContextualBase
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public IncomingConnectionManager(ExecutionContext context)
            : base(context)
        {
        }

        /// <summary>
        /// Gets the connection by the account public key
        /// </summary>
        /// <param name="pubKey">Account public key</param>
        /// <param name="connection">Current account connection</param>
        /// <returns>True if connection is found, otherwise false</returns>
        public bool TryGetConnection(RawPubKey pubKey, out IncomingConnectionBase connection)
        {
            return connections.TryGetValue(pubKey, out connection);
        }


        /// <summary>
        /// Gets all auditor connections
        /// </summary>
        /// <returns>The list of current auditor connections</returns>
        public List<IncomingAuditorConnection> GetAuditorConnections()
        {
            var auditorConnections = new List<IncomingAuditorConnection>();
            var auditors = Context.Constellation.Auditors;
            for (var i = 0; i < Context.Constellation.Auditors.Count; i++)
            {
                if (TryGetConnection(auditors[i].PubKey, out IncomingConnectionBase auditorConnection))
                    auditorConnections.Add((IncomingAuditorConnection)auditorConnection);
            }
            return auditorConnections;
        }

        /// <summary>
        /// Registers new client websocket connection
        /// </summary>
        /// <param name="webSocket">New websocket connection</param>
        public async Task OnNewConnection(WebSocket webSocket, RawPubKey rawPubKey, string ip)
        {
            Context.ExtensionsManager.BeforeNewConnection(webSocket, ip);
            if (webSocket == null)
                throw new ArgumentNullException(nameof(webSocket));

            var connection = default(IncomingConnectionBase);
            if (Context.Constellation.Auditors.Any(a => a.PubKey.Equals(rawPubKey)))
                connection = new IncomingAuditorConnection(Context, rawPubKey, webSocket, ip);
            else
                connection = new IncomingClientConnection(Context, rawPubKey, webSocket, ip);
            using (connection)
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
                    if (!includingAuditors && Context.Constellation.Auditors.Any(a => a.PubKey.Equals(pk))
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

        ConcurrentDictionary<RawPubKey, IncomingConnectionBase> connections = new ConcurrentDictionary<RawPubKey, IncomingConnectionBase>();

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

        void OnConnectionStateChanged((ConnectionBase connection, ConnectionState prev, ConnectionState current) args)
        {
            var connection = (IncomingConnectionBase)args.connection;
            switch (args.current)
            {
                case ConnectionState.Validated:
                case ConnectionState.Ready:
                    //avoid multiple validation event firing
                    if (args.prev == ConnectionState.Connected)
                        AddConnection(connection);
                    TrySetAuditorState(connection);
                    break;
                case ConnectionState.Closed:
                    RemoveConnection(connection);
                    break;
                default:
                    break;
            }
        }

        void TrySetAuditorState(IncomingConnectionBase connection)
        {
            if (connection.IsAuditor)
            {
                Context.AppState.RegisterConnection((IAuditorConnection)connection);
                logger.Trace($"Auditor {connection.PubKey} is connected.");
            }
        }

        void RemoveConnection(IncomingConnectionBase connection)
        {
            lock (connection)
            {
                _ = UnsubscribeAndClose(connection);

                connections.TryRemove(connection.PubKey, out _);
                if (connection.IsAuditor)
                {
                    Context.AppState.RemoveConnection((IAuditorConnection)connection);
                    Context.Catchup.RemoveState(connection.PubKey);
                }
            }
        }

        #endregion
    }
}
