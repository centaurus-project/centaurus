using Centaurus.Models;
using NLog;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public abstract class ClientWebSocketConnection : BaseWebSocketConnection
    {
        public ClientWebSocketConnection() 
            : base(new ClientWebSocket())
        {
        }

        protected ClientWebSocket clientWebSocket => webSocket as ClientWebSocket;

        public virtual async Task EstablishConnection()
        {
            await (webSocket as ClientWebSocket).ConnectAsync(new Uri(Global.Settings.AlphaAddress), CancellationToken.None);
            _ = Listen();
        }
    }
}
