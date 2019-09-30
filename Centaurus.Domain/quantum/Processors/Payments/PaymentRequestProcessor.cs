using System;
using System.Collections.Generic;
using System.Text;
using Centaurus.Models;

namespace Centaurus.Domain
{
    public class PaymentRequestProcessor : PaymentRequestProcessorBase
    {
        public override MessageTypes SupportedMessageType { get; } = MessageTypes.PaymentRequest;
    }
}
