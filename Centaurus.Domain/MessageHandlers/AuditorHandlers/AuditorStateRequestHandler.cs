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
            var snapshot = await SnapshotManager.GetSnapshot(stateRequestMessage.TargetApex);
            if (snapshot != null)
            {
                var snapshotHash = snapshot.ComputeHash();
                var signature = snapshotHash.Sign(Global.Settings.KeyPair);
                snapshot.Signatures = new List<Ed25519Signature> { signature };
                state.Snapshot = snapshot;

                state.PendingQuantums = await SnapshotManager.GetQuantaAboveApex(snapshot.Apex);
            }
            _ = connection.SendMessage(state);
        }
    }
}
