using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;
using NLog;

namespace Centaurus.Domain.Handlers.AlphaHandlers
{
    public class AlphaResultMessageHandler : BaseAlphaMessageHandler
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public override MessageTypes SupportedMessageType { get; } = MessageTypes.ResultMessage;

        public override ConnectionState[] ValidConnectionStates { get; } = new ConnectionState[] { ConnectionState.Ready };

        public override bool IsAuditorOnly { get; } = true;

        //TODO: run result aggregation in separate thread
        public override async Task HandleMessage(AlphaWebSocketConnection connection, MessageEnvelope envelope)
        {
            var resultMessage = (ResultMessage)envelope.Message;
            if (resultMessage.OriginalMessage.Message is Quantum) //we need majority only for quanta
                await Global.AuditResultManager.Add(envelope);
            else if (resultMessage.Status != ResultStatusCodes.Success)
                logger.Error("Auditor message handling failed. " + StringifyResult(connection, resultMessage));
            else
                logger.Trace(StringifyResult(connection, resultMessage));
        }

        private string StringifyResult(AlphaWebSocketConnection connection, ResultMessage resultMessage)
        {
            return $"Result message from auditor {connection.ClientPubKey.ToString()}: {{ " +
                $"Status: {resultMessage.Status}, " +
                $"SourceMessageType: {resultMessage.OriginalMessage.Message.MessageType}" +
                $"}}";
        }
    }
}