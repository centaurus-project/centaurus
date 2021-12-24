using Centaurus.Domain;
using Centaurus.Models;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Test
{
    internal abstract class BaseMessageHandlerTests
    {
        protected async Task AssertMessageHandling<T>(T connection, IncomingMessage message, Type excpectedException = null)
            where T : ConnectionBase
        {
            if (excpectedException == null)
            {
                var isHandled = await connection.Context.MessageHandlers.HandleMessage(connection, message);
                Assert.IsTrue(isHandled);
            }
            else
                Assert.ThrowsAsync(excpectedException,
                    async () =>
                    {
                        await connection.Context.MessageHandlers.HandleMessage(connection, message);
                    });
        }

        protected IncomingConnectionBase GetIncomingConnection(ExecutionContext context, RawPubKey pubKey, bool isAuthenticated = false)
        {
            var connection = default(IncomingConnectionBase);
            if (context.NodesManager.TryGetNode(pubKey, out var node))
                connection = new IncomingNodeConnection(context, new DummyWebSocket(), "127.0.0.1", node);
            else
                connection = new IncomingClientConnection(context, pubKey, new DummyWebSocket(), "127.0.0.1");

            if (isAuthenticated)
            {
                MarkAsAuthenticated(connection);
            }
            return connection;
        }

        protected OutgoingConnection GetOutgoingConnection(ExecutionContext context, RawPubKey pubKey)
        {
            context.NodesManager.TryGetNode(pubKey, out var node);
            var connection = new OutgoingConnection(context, node, new DummyConnectionWrapper(new DummyWebSocket()));
            MarkAsAuthenticated(connection);
            return connection;
        }

        private void MarkAsAuthenticated(ConnectionBase connection)
        {
            var connectionType = typeof(ConnectionBase);
            var authField = connectionType.GetProperty("IsAuthenticated", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            authField.SetValue(connection, true);
        }
    }
}
