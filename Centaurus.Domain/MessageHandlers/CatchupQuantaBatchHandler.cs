using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    internal class CatchupQuantaBatchHandler: MessageHandlerBase<IncomingNodeConnection>
    {
        public CatchupQuantaBatchHandler(ExecutionContext context)
            :base(context)
        {
        }

        public override string SupportedMessageType => typeof(CatchupQuantaBatch).Name;

        public override Task HandleMessage(IncomingNodeConnection connection, IncomingMessage message)
        {
            _ = Context.Catchup.AddAuditorState(connection.PubKey, (CatchupQuantaBatch)message.Envelope.Message);
            return Task.CompletedTask;
        }
    }
}
