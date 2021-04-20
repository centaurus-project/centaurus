using Centaurus.Alpha;
using Centaurus.Domain;
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
        private AlphaContext alphaContext;
        private MockWebSocket webSocketServer;

        public MockClientConnectionWrapper(AlphaContext alphaContext, MockWebSocket webSocket, MockWebSocket webSocketServer)
            : base(webSocket)
        {
            this.alphaContext = alphaContext;
            this.webSocketServer = webSocketServer;
        }

        public override Task Connect(Uri uri, CancellationToken cancellationToken)
        {
            if (!AlphaHostBuilder.ValidApplicationStates.Contains(alphaContext.AppState.State))
                throw new InvalidOperationException("Alpha is not ready");

            ((MockWebSocket)WebSocket).Connect(webSocketServer);
            (webSocketServer).Connect((MockWebSocket)WebSocket);
            _ = Task.Factory.StartNew(() => alphaContext.ConnectionManager.OnNewConnection(webSocketServer, null));
            return Task.CompletedTask;
        }
    }
}
