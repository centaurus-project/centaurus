using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class AuditorStateRequestHandler : BaseAuditorMessageHandler
    {
        public override MessageTypes SupportedMessageType { get; } = MessageTypes.AuditorStateRequest;

        public override ConnectionState[] ValidConnectionStates { get; } = new ConnectionState[] { ConnectionState.Connected };

        public override async Task HandleMessage(AuditorWebSocketConnection connection, MessageEnvelope messageEnvelope)
        {
            var stateRequestMessage = (AuditorStateRequest)messageEnvelope.Message;
            var allPendingQuanta = await Global.PersistenceManager.GetQuantaAboveApex(stateRequestMessage.TargetApex);
            var skip = 0;
            var maxQuantaPerMessage = 10;
            var hasQuanta = true;
            while (hasQuanta)
            {
                var currentBatch = allPendingQuanta.Skip(skip).Take(maxQuantaPerMessage).ToList();
                hasQuanta = (skip + maxQuantaPerMessage) < allPendingQuanta.Count;
                skip += maxQuantaPerMessage;
                var state = new AuditorState
                {
                    State = Global.AppState.State,
                    PendingQuanta = currentBatch,
                    HasMorePendingQuanta = hasQuanta
                };
                await connection.SendMessage(state);
            };
        }
    }
}
