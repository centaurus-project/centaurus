﻿using Centaurus.Domain;
using Centaurus.Models;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Test
{
    public abstract class BaseMessageHandlerTests
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
            if (context.Constellation.Auditors.Any(a => a.PubKey.Equals(pubKey)))
                connection = new IncomingAuditorConnection(context, pubKey, new FakeWebSocket(), "127.0.0.1");
            else
                connection = new IncomingClientConnection(context, pubKey, new FakeWebSocket(), "127.0.0.1");

            if (state != null)
                connection.ConnectionState = state.Value;
            return connection;
        }

        protected OutgoingConnection GetOutgoingConnection(ExecutionContext context, RawPubKey keyPair, ConnectionState? state = null)
        {
            var connection = new OutgoingConnection(context, keyPair, new MockAuditorConnectionInfo(new FakeWebSocket()));
            if (state != null)
                connection.ConnectionState = state.Value;
            return connection;
        }
    }
}
