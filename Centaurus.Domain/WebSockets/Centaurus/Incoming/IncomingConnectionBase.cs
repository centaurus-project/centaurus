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
            : base(context, keyPair, webSocket)
        {
            Ip = ip ?? throw new ArgumentNullException(nameof(ip));
        }

        public string Ip { get; }

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

        /// <summary>
        /// When closing the connection we need to know if it was validated 
        /// </summary>
        public bool IsValidated { get; private set; }

        public bool TryValidate(HandshakeData handshakeData)
        {
            if (handshakeData == null
                || !handshakeData.Equals(this.handshakeData))
                return false;

            IsValidated = true;

            //auditor Ready state would be set after success quanta delay inspection
            ConnectionState = ConnectionState.Ready;
            return true;
        }
    }
}
