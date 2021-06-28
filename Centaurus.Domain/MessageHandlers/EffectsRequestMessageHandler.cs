using Centaurus.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class EffectsRequestMessageHandler : MessageHandlerBase
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public EffectsRequestMessageHandler(ExecutionContext context) 
            : base(context)
        {
        }

        public override MessageTypes SupportedMessageType => MessageTypes.EffectsRequest;

        public override ConnectionState[] ValidConnectionStates => new ConnectionState[] { ConnectionState.Ready };

        public override Task HandleMessage(BaseWebSocketConnection connection, IncomingMessage message)
        {
            //run it in a separate thread to avoid blocking quanta handling
            Task.Factory.StartNew(async () =>
            {
                try
                {
                    ResultMessage result;
                    try
                    {
                        var request = message.Envelope.Message as EffectsRequest;
                        var effectsResponse = await connection.Context.PersistenceManager.LoadEffects(request.Cursor, request.IsDesc, request.Limit, request.Account);
                        effectsResponse.OriginalMessage = message.Envelope;
                        effectsResponse.Status = ResultStatusCodes.Success;
                        result = effectsResponse;
                    }
                    catch (Exception exc)
                    {
                        result = message.Envelope.CreateResult(exc.GetStatusCode());
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
