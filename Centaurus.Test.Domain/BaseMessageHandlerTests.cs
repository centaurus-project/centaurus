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
                var isHandled = false;
                if (connection.Context.IsAlpha)
                    isHandled = await connection.Context.AlphaMessageHandlers.HandleMessage(connection, message);
                else
                    isHandled = await connection.Context.AuditorMessageHandlers.HandleMessage(connection, message);
                Assert.IsTrue(isHandled);
            }
            else
                Assert.ThrowsAsync(excpectedException, 
                    async () => {
                        if (connection.Context.IsAlpha)
                            await connection.Context.AlphaMessageHandlers.HandleMessage(connection, message);
                        else
                            await connection.Context.AuditorMessageHandlers.HandleMessage(connection, message);
                    });
        }
    }
}
