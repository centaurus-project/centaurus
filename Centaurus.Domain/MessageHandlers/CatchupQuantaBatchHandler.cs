using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    internal class CatchupQuantaBatchHandler: MessageHandlerBase
    {
        public CatchupQuantaBatchHandler(ExecutionContext context)
            :base(context)
        {
        }

        public override string SupportedMessageType => typeof(CatchupQuantaBatch).Name;

        public override bool IsAuditorOnly => true;

        public override bool IsAuthenticatedOnly => true;

        public override Task HandleMessage(ConnectionBase connection, IncomingMessage message)
        {
            _ = Context.Catchup.AddNodeBatch(connection.PubKey, (CatchupQuantaBatch)message.Envelope.Message);
            return Task.CompletedTask;
        }
    }
}
