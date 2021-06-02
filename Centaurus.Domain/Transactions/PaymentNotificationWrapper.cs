using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class PaymentNotificationWrapper
    {
        public PaymentNotificationWrapper(PaymentNotification payment, DateTime paymentTime)
        {
            Payment = payment ?? throw new ArgumentNullException(nameof(payment));
            PaymentTime = paymentTime;
        }

        public PaymentNotification Payment { get; }

        public DateTime PaymentTime { get; }
    }
}
