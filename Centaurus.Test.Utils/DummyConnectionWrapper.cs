using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Test
{
    public class DummyConnectionWrapper : OutgoingConnectionWrapperBase
    {
        public DummyConnectionWrapper(WebSocket webSocket)
            : base(webSocket)
        {
        }

        public override Task Connect(Uri uri, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
