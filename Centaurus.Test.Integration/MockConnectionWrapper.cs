using Centaurus.Alpha;
using Centaurus.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Centaurus.Test
{
    public class MockConnectionWrapper : OutgoingConnectionWrapperBase
    {
        private readonly Dictionary<string, StartupWrapper> startupWrappers;
        private readonly MockWebSocket webSocketServer = new MockWebSocket();

        public MockConnectionWrapper(Dictionary<string, StartupWrapper> startupWrappers)
            : base(new MockWebSocket())
        {
            this.startupWrappers = startupWrappers ?? throw new ArgumentNullException(nameof(startupWrappers));
        }

        public override Task Connect(Uri uri, CancellationToken cancellationToken)
        {
            var host = uri.Host;
            var query = HttpUtility.ParseQueryString(uri.Query);
            var pubkey = query.Get(WebSocketConstants.PubkeyParamName);

            if (!StrKey.IsValidEd25519PublicKey(pubkey))
                throw new Exception("\"pubkey\" parameter value is not valid Ed25519 public key.");

            if (!startupWrappers.TryGetValue(host, out var startup))
                throw new Exception("Server is unavailable.");

            //if (!AlphaHostBuilder.ValidStates.Contains(startup.Context?.NodesManager.CurrentNode.State ?? Models.State.Undefined))
            //    throw new InvalidOperationException("Alpha is not ready");

            var currentSideSocket = (MockWebSocket)WebSocket;
            currentSideSocket.KeyPair = new KeyPair(pubkey);

            webSocketServer.KeyPair = startup.Context.Settings.KeyPair;

            currentSideSocket.Connect(webSocketServer);
            webSocketServer.Connect((MockWebSocket)WebSocket);
            Task.Factory.StartNew(async () => await startup.Context.IncomingConnectionManager.OnNewConnection(webSocketServer, currentSideSocket.KeyPair, "192.168.0.1"));
            return Task.CompletedTask;
        }
    }
}
