using Centaurus.Domain;
using NLog;
using System.Net.WebSockets;

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
