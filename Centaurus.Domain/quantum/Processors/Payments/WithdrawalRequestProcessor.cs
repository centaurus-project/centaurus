using System;
using System.Collections.Generic;
using System.Text;
using Centaurus.Models;

namespace Centaurus.Domain
{
    public class WithdrawalRequestProcessor : PaymentRequestProcessorBase
    {
        public override MessageTypes SupportedMessageType { get; } = MessageTypes.WithdrawalRequest;
    }
}
