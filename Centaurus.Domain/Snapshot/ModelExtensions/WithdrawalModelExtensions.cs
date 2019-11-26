using Centaurus.DAL.Models;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class WithdrawalModelExtensions
    {
        public static WithdrawalModel FromWithdrawal(RequestQuantum withdrawal)
        {
            return new WithdrawalModel
            {
                Apex = withdrawal.Apex,
                TransactionHash = ((PaymentRequestBase)withdrawal.RequestEnvelope.Message).TransactionHash
            };
        }
    }
}
