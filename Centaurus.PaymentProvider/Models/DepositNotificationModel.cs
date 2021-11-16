using System;
using System.Collections.Generic;

namespace Centaurus.PaymentProvider.Models
{
    public class DepositNotificationModel
    {
        public string ProviderId { get; set; }

        public string Cursor { get; set; }

        public List<DepositModel> Items { get; set; } = new List<DepositModel>();

        public DateTime DepositTime { get; set; }

        public bool IsSend { get; set; }
    }
}