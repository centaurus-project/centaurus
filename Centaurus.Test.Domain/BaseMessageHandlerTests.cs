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

        protected IncomingConnectionBase GetIncomingConnection(ExecutionContext context, RawPubKey pubKey, ConnectionState? state = null)
        {
            var connection = default(IncomingConnectionBase);
            if (context.NodesManager.TryGetNode(pubKey, out var node))
                connection = new IncomingNodeConnection(context, new DummyWebSocket(), "127.0.0.1", node);
            else
                connection = new IncomingClientConnection(context, pubKey, new DummyWebSocket(), "127.0.0.1");

            if (state != null)
                connection.ConnectionState = state.Value;
            return connection;
        }

        protected OutgoingConnection GetOutgoingConnection(ExecutionContext context, RawPubKey pubKey, ConnectionState? state = null)
        {
            context.NodesManager.TryGetNode(pubKey, out var node);
            var connection = new OutgoingConnection(context, node, new OutgoingMessageStorage(), new DummyConnectionWrapper(new DummyWebSocket()));
            if (state != null)
                connection.ConnectionState = state.Value;
            return connection;
        }
    }
}
