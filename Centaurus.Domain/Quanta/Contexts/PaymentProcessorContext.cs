using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class PaymentProcessorContext : RequestContext
    {
        public PaymentProcessorContext(EffectProcessorsContainer effectProcessors) 
            : base(effectProcessors)
        {
            Payment = (PaymentRequest)Request.RequestEnvelope.Message;
            SourceAccount = Payment.AccountWrapper.Account;
        }

        public PaymentRequest Payment { get; }

        public Account SourceAccount { get; }

        private Account destinationAccount;
        public Account DestinationAccount
        {
            get
            {
                if (destinationAccount == null)
                    destinationAccount = Global.AccountStorage.GetAccount(Payment.Destination)?.Account;

                return destinationAccount;
            }
        }
    }
}
