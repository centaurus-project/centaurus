using Centaurus.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class AlphaEffectsRequestMessageHandler : BaseAlphaMessageHandler
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public override bool IsAuditorOnly => false;

        public override MessageTypes SupportedMessageType => MessageTypes.EffectsRequest;

        public override ConnectionState[] ValidConnectionStates => new ConnectionState[] { ConnectionState.Ready };

        public override Task HandleMessage(AlphaWebSocketConnection connection, MessageEnvelope messageEnvelope)
        {
            //run it in the separate thread to avoid blocking quanta handling
            Task.Factory.StartNew(async () =>
            {
                try
                {
                    ResultMessage result;
                    try
                    {
                        var request = messageEnvelope.Message as EffectsRequest;
                        var effectsResponse = await Global.SnapshotManager.LoadEffects(request.Cursor, request.IsDesc, request.Limit, request.Account.Data);
                        effectsResponse.OriginalMessage = messageEnvelope;
                        effectsResponse.Effects = new List<Effect>();
                        effectsResponse.Status = ResultStatusCodes.Success;
                        result = effectsResponse;
                    }
                    catch (Exception exc)
                    {
                        result = messageEnvelope.CreateResult(exc.GetStatusCode());
                    }
                    await connection.SendMessage(result);
                }
                catch (Exception exc)
                {
                    logger.Error(exc, "Error on sending effects.");
                }
            });
            return Task.CompletedTask;
        }
    }
}
