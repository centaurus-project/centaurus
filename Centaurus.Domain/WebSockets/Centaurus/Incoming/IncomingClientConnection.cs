using Centaurus.Domain;
using Centaurus.Domain.Models;
using Centaurus.Models;
using NLog;
using System.Net.WebSockets;

namespace Centaurus
{
    public class IncomingClientConnection : IncomingConnectionBase
    {
        public IncomingClientConnection(ExecutionContext context, KeyPair keyPair, WebSocket webSocket, string ip)
            : base(context, keyPair, webSocket, ip)
        {
            Account = Context.AccountStorage.GetAccount(PubKey);

            SendHandshake();
        }

        public AccountWrapper Account { get; }
    }
}