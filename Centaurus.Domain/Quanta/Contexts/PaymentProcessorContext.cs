using Centaurus.Domain.Models;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class PaymentProcessorContext : RequestContext
    {
        public PaymentProcessorContext(ExecutionContext context, Quantum quantum, Account account) 
            : base(context, quantum, account)
        {
            Payment = (PaymentRequest)Request.RequestEnvelope.Message;
        }

        public PaymentRequest Payment { get; }

        private Account destinationAccount;
        public Account DestinationAccount
        {
            get
            {
                if (destinationAccount == null)
                    destinationAccount = Context.AccountStorage.GetAccount(Payment.Destination);

                return destinationAccount;
            }
        }
    }
}
