using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class AuditorStateRequestHandler: BaseAuditorMessageHandler
    {
        public override MessageTypes SupportedMessageType { get; } = MessageTypes.AuditorStateRequest;

        public override ConnectionState[] ValidConnectionStates { get; } = new ConnectionState[] { ConnectionState.Connected };

        public override async Task HandleMessage(AuditorWebSocketConnection connection, MessageEnvelope messageEnvelope)
        {
            var stateRequestMessage = (AuditorStateRequest)messageEnvelope.Message;

            var state = new AuditorState
            {
                State = Global.AppState.State,
                PendingQuantums = new List<MessageEnvelope>()
            };

            //get snapshot for specified Apex
            var hasDataForApex = stateRequestMessage.TargetApex >= await SnapshotManager.GetMinRevertApex();
            if (hasDataForApex)
                state.PendingQuantums = await SnapshotManager.GetQuantaAboveApex(stateRequestMessage.TargetApex);
            else
                throw new Exception("Alpha has newer data than the auditor");
            _ = connection.SendMessage(state);
        }
    }
}
