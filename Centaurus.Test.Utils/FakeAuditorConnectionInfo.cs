using Centaurus.Client;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Test
{
    public class FakeAuditorConnectionInfo : ClientConnectionWrapperBase
    {
        public FakeAuditorConnectionInfo(FakeWebSocket webSocket) 
            : base(webSocket)
        {
        }

        public override Task Connect(Uri uri, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
