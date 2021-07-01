using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.PaymentProvider
{
    public class DepositNotificationWrapper
    {
        public DepositNotificationWrapper(DepositNotification deposit, DateTime paymentTime)
        {
            Deposite = deposit ?? throw new ArgumentNullException(nameof(deposit));
            DepositTime = paymentTime;
        }

        public DepositNotification Deposite { get; }

        public DateTime DepositTime { get; }
    }
}