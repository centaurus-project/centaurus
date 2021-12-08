using Centaurus.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain.WebSockets
{
    internal class IncomingConnectionsCollection
    {
        ConcurrentDictionary<RawPubKey, IncomingConnectionBase> connections = new ConcurrentDictionary<RawPubKey, IncomingConnectionBase>();

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

        public void Add(IncomingConnectionBase connection)
        {
            connections.AddOrUpdate(connection.PubKey, connection, (key, oldConnection) =>
            {
                TryRemove(oldConnection);
                return connection;
            });
        }

        public bool TryRemove(IncomingConnectionBase connection)
        {
            return TryRemove(connection.PubKey, out _);
        }

        public bool TryRemove(RawPubKey pubKey, out IncomingConnectionBase connection)
        {
            return connections.TryRemove(pubKey, out connection);
        }

        public List<IncomingConnectionBase> GetAll()
        {
            return connections.Values.ToList();
        }

        internal List<RawPubKey> GetAllPubKeys()
        {
            return connections.Keys.ToList();
        }
    }
}
