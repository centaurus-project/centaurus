using Centaurus.Domain;
using Centaurus.Models;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Test
{
    public abstract class BaseMessageHandlerTests
    {
        protected async Task AssertMessageHandling<T>(T connection, IncomingMessage message, Type excpectedException = null)
            where T : BaseWebSocketConnection
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

        protected IncomingWebSocketConnection GetIncomingConnection(ExecutionContext context, byte[] pubKey, ConnectionState? state = null)
        {
            var connection = new IncomingWebSocketConnection(context, new FakeWebSocket(), "127.0.0.1")
            {
                Account = context.AccountStorage.GetAccount(pubKey)
            };

            connection.SetPubKey(pubKey);
            if (state != null)
                connection.ConnectionState = state.Value;
            return connection;
        }

        protected OutgoingWebSocketConnection GetOutgoingConnection(ExecutionContext context, ConnectionState? state = null)
        {
            var connection = GetOutgoingConnection(context);
            if (state != null)
                connection.ConnectionState = state.Value;
            return connection;
        }
    }
}
