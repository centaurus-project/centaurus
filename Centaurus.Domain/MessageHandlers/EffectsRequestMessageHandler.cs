using Centaurus.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    internal class EffectsRequestMessageHandler : MessageHandlerBase<IncomingClientConnection>
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public EffectsRequestMessageHandler(ExecutionContext context)
            : base(context)
        {
        }

        public override string SupportedMessageType { get; } = typeof(QuantumInfoRequest).Name;

        public override bool IsAuthenticatedOnly => true;

        public override async Task HandleMessage(IncomingClientConnection connection, IncomingMessage message)
        {
            try
            {
                ResultMessageBase result;
                try
                {
                    var request = message.Envelope.Message as QuantumInfoRequest;
                    var effectsResponse = connection.Context.DataProvider.LoadQuantaInfo(request.Cursor, request.IsDesc, request.Limit, connection.Account.Pubkey);
                    effectsResponse.OriginalMessageId = message.Envelope.Message.MessageId;
                    effectsResponse.Status = ResultStatusCode.Success;
                    result = effectsResponse;
                }
                catch (Exception exc)
                {
                    result = message.Envelope.CreateResult(exc.GetStatusCode());
                }
                await connection.SendMessage(result.CreateEnvelope<MessageEnvelopeSignless>());
            }
            catch (Exception exc)
            {
                logger.Error(exc, "Error on sending effects.");
            }
        }
    }
}
