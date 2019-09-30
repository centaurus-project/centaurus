using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain
{
    public class OrderMessageHandler : AlphaBaseQuantumHandler
    {
        public override MessageTypes SupportedMessageType { get; } = MessageTypes.OrderRequest;

        public override ConnectionState[] ValidConnectionStates { get; } = new ConnectionState[] { ConnectionState.Ready };
    }
}
