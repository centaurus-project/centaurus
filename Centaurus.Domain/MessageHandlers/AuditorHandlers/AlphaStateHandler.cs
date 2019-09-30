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

            //if Alpha is rising, we need to send the current auditor state, and wait for a new AlphaState message
            if (alphaInfo.State == ApplicationState.Rising)
            {
                var lastSnapshot = await SnapshotProviderManager.GetLastSnapshot();
                var pendingQuantums = Global.QuantumStorage?.GetAllQuantums(lastSnapshot?.Apex ?? 0) ?? new MessageEnvelope[] { };
                _ = connection.SendMessage(new AuditorState
                {
                    State = Global.AppState.State,
                    LastSnapshot = lastSnapshot,
                    PendingQuantums = new List<MessageEnvelope>(pendingQuantums)
                });
            }
            else if (alphaInfo.State == ApplicationState.Ready || alphaInfo.State == ApplicationState.Running)
            {
                var statusCode = await AuditorCatchup.Catchup(alphaInfo.LastSnapshot);
                if (statusCode != ResultStatusCodes.Success)
                {
                    throw new ConnectionCloseException(WebSocketCloseStatus.ProtocolError, "Auditor rise failed");
                }
                else
                {
                    connection.ConnectionState = ConnectionState.Ready;
                    //set apex cursor to start receive quanta
                    _ = connection.SendMessage(new SetApexCursor() { Apex = Global.QuantumStorage?.CurrentApex ?? 0 });
                }
            }
        }
    }
}
