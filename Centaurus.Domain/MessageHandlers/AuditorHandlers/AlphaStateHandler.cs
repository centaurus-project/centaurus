using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain
{
    public class AlphaStateHandler : BaseAuditorMessageHandler
    {
        public override MessageTypes SupportedMessageType { get; } = MessageTypes.AlphaState;

        public override ConnectionState[] ValidConnectionStates { get; } = new ConnectionState[] { ConnectionState.Connected };

        public override async Task HandleMessage(AuditorWebSocketConnection connection, MessageEnvelope messageEnvelope)
        {
            var alphaInfo = (AlphaState)messageEnvelope.Message;

            //if the app is not in WaitingForInit state, than it has local snapshot and it was already setup
            if (Global.AppState.State == ApplicationState.WaitingForInit)
            {
                var statusCode = await AuditorCatchup.Catchup(alphaInfo.LastSnapshot);
                if (statusCode != ResultStatusCodes.Success)
                    throw new ConnectionCloseException(WebSocketCloseStatus.ProtocolError, "Auditor rise failed");
            }
            //set apex cursor to start receive quanta
            _ = connection.SendMessage(new SetApexCursor() { Apex = Global.QuantumStorage.CurrentApex });
            connection.ConnectionState = ConnectionState.Ready;
        }
    }
}
