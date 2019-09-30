using System;
using System.Linq;

namespace Centaurus.Models
{
    public class PaymentRequest : PaymentRequestBase
    {
        public override MessageTypes MessageType => MessageTypes.PaymentRequest;
    }
}
