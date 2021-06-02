using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;
using NLog;

namespace Centaurus.Domain.Handlers.AlphaHandlers
{
    public class AuditorResultMessageHandler : BaseAuditorMessageHandler
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public AuditorResultMessageHandler(ExecutionContext context) 
            : base(context)
        {
        }

        public override MessageTypes SupportedMessageType { get; } = MessageTypes.ResultMessage;

        public override ConnectionState[] ValidConnectionStates { get; } = null;

        public override Task HandleMessage(OutgoingWebSocketConnection connection, IncomingMessage message)
        {
            var resultMessage = (ResultMessage)message.Envelope.Message;
            logger.Error($"Result message received. Status {resultMessage.Status}, original message type is {resultMessage.OriginalMessage.Message.MessageType}");
            return Task.CompletedTask;
        }
    }
}