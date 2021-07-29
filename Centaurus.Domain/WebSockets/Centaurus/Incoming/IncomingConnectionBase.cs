using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class IncomingConnectionBase : ConnectionBase
    {
        public IncomingConnectionBase(ExecutionContext context, KeyPair keyPair, WebSocket webSocket, string ip)
            : base(context, keyPair, webSocket, ip)
        {
        }

        protected void SendHandshake()
        {
            Task.Factory.StartNew(async () =>
            {
                await SendMessage(new HandshakeRequest { HandshakeData = handshakeData });
                await Task.Delay(5000); //wait for 5 sec to validate connection
                if (ConnectionState == ConnectionState.Connected)
                    await CloseConnection(WebSocketCloseStatus.ProtocolError, "Handshake response wasn't send.");
            });
        }

        private HandshakeData handshakeData = new HandshakeData().Randomize();

        public bool TryValidate(HandshakeData handshakeData)
        {
            if (handshakeData == null
                || !handshakeData.Equals(this.handshakeData))
                return false;

            //auditor Ready state would be set after success quanta delay inspection
            ConnectionState = IsAuditor ? ConnectionState.Validated : ConnectionState.Ready;
            return true;
        }
    }
}
