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
        protected async Task AssertMessageHandling<T>(T connection, MessageEnvelope envelope, Type excpectedException = null)
            where T: BaseWebSocketConnection
        {
            if (excpectedException == null)
            {
                var isHandled = await MessageHandlers<T>.HandleMessage(connection, envelope);
                Assert.IsTrue(isHandled);
            }
            else
                Assert.ThrowsAsync(excpectedException, async () => await MessageHandlers<T>.HandleMessage(connection, envelope));
        }
    }
}
