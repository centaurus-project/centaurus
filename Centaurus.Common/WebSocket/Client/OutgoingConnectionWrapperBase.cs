using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Client
{
    public abstract class OutgoingConnectionWrapperBase
    {
        public OutgoingConnectionWrapperBase(WebSocket webSocket)
        {
            WebSocket = webSocket;
        }

        public WebSocket WebSocket { get; }

        public abstract Task Connect(Uri uri, CancellationToken cancellationToken);
    }
}
