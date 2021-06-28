using System;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Centaurus.Models;
using Centaurus.Domain;
using Centaurus.Xdr;
using NLog;
using System.Diagnostics;
using stellar_dotnet_sdk;

namespace Centaurus
{
    public class IncomingWebSocketConnection : BaseWebSocketConnection
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public IncomingWebSocketConnection(ExecutionContext context, WebSocket webSocket, string ip)
            : base(context, webSocket, ip, 1024, 64 * 1024)
        {
        }
    }
}
