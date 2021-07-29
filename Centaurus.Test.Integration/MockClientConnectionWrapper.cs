using Centaurus.Alpha;
using Centaurus.Domain;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Test
{
    public class MockClientConnectionWrapper : ClientConnectionWrapperBase
    {
        private Domain.ExecutionContext ExecutionContext;
        private MockWebSocket webSocketServer;

        public MockClientConnectionWrapper(Domain.ExecutionContext ExecutionContext, MockWebSocket webSocket, MockWebSocket webSocketServer)
            : base(webSocket)
        {
            this.ExecutionContext = ExecutionContext;
            this.webSocketServer = webSocketServer;
        }

        public override Task Connect(Uri uri, CancellationToken cancellationToken)
        {
            if (ExecutionContext == null || !AlphaHostBuilder.ValidStates.Contains(ExecutionContext.AppState.State))
                throw new InvalidOperationException("Alpha is not ready");


            ((MockWebSocket)WebSocket).Connect(webSocketServer);
            (webSocketServer).Connect((MockWebSocket)WebSocket);
            Task.Factory.StartNew(() => ExecutionContext.IncomingConnectionManager.OnNewConnection(webSocketServer, null), TaskCreationOptions.LongRunning);
            return Task.CompletedTask;
        }
    }
}
