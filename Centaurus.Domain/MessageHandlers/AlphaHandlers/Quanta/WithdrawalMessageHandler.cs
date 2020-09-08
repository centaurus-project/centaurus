using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class WithdrawalMessageHandler : AlphaBaseQuantumHandler
    {
        public override MessageTypes SupportedMessageType { get; } = MessageTypes.WithdrawalRequest;
    }
}
