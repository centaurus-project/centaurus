using Centaurus.Client;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Test
{
    public class MockAuditorConnectionInfo : OutgoingConnectionWrapperBase
    {
        public MockAuditorConnectionInfo(FakeWebSocket webSocket) 
            : base(webSocket)
        {
        }

        public override Task Connect(Uri uri, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
