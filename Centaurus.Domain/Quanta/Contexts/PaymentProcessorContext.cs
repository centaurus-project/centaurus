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
            SourceAccount = Payment.AccountWrapper;
        }

        public PaymentRequest Payment { get; }

        public AccountWrapper SourceAccount { get; }

        private AccountWrapper destinationAccount;
        public AccountWrapper DestinationAccount
        {
            get
            {
                if (destinationAccount == null)
                    destinationAccount = EffectProcessors.Context.AccountStorage.GetAccount(Payment.Destination);

                return destinationAccount;
            }
        }
    }
}
