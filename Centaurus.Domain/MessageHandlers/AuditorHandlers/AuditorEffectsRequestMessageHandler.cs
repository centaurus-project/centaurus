using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class AuditorEffectsRequestMessageHandler : AuditorClientsRequestMessageHandler
    {

        public override MessageTypes SupportedMessageType => MessageTypes.EffectsRequest;

        public override ConnectionState[] ValidConnectionStates => new ConnectionState[] { ConnectionState.Ready };

        public override Task HandleMessage(AuditorWebSocketConnection connection, MessageEnvelope messageEnvelope)
        {
            //run it in separate thread to avoid blocking quanta 
            Task.Factory.StartNew(async () =>
            {
                ResultMessage result;
                try
                {
                    var request = messageEnvelope.Message as EffectsRequest;
                    var effectsResponse = await Global.SnapshotManager.LoadEffects(request.PagingToken, request.Account.Data);
                    effectsResponse.OriginalMessage = messageEnvelope;
                    effectsResponse.Effects = new List<Effect>();
                    effectsResponse.Status = ResultStatusCodes.Success;
                    result = effectsResponse;
                }
                catch (Exception exc)
                {
                    result = messageEnvelope.CreateResult(exc.GetStatusCode());
                }
                OutgoingMessageStorage.EnqueueMessage(result);
            });
            return Task.CompletedTask;
        }
    }
}
