using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class PaymentMessageHandler : AlphaBaseQuantumHandler
    {
        public override MessageTypes SupportedMessageType { get; } = MessageTypes.PaymentRequest;

        public override ConnectionState[] ValidConnectionStates { get; } = new ConnectionState[] { ConnectionState.Ready };
    }
}
