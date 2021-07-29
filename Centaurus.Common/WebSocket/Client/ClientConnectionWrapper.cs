using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Client
{
    public class ClientConnectionWrapper : OutgoingConnectionWrapperBase
    {
        public ClientConnectionWrapper(ClientWebSocket webSocket)
            :base(webSocket)
        {

        }

        public override async Task Connect(Uri uri, CancellationToken cancellationToken)
        {
            await ((ClientWebSocket)WebSocket).ConnectAsync(uri, cancellationToken);
        }
    }
}
