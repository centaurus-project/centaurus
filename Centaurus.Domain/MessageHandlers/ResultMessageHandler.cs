using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;
using NLog;

namespace Centaurus.Domain.Handlers.AlphaHandlers
{
    public class ResultMessageHandler : MessageHandlerBase
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public ResultMessageHandler(ExecutionContext context) 
            : base(context)
        {
        }

        public override MessageTypes SupportedMessageType { get; } = MessageTypes.ResultMessage;

        public override bool IsAuditorOnly => true;

        public override Task HandleMessage(BaseWebSocketConnection connection, IncomingMessage message)
        {
            var resultMessage = (ResultMessage)message.Envelope.Message;
            logger.Error($"Result message received. Status {resultMessage.Status}, original message type is {resultMessage.OriginalMessage.Message.MessageType}");
            return Task.CompletedTask;
        }
    }
}