using Centaurus.Models;
using Centaurus.PaymentProvider.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class WithdrawalRequestModelExtensions
    {
        public static WithdrawalRequestModel ToProviderModel(this WithdrawalRequest withdrawal)
        {
            if (withdrawal == null)
                throw new ArgumentNullException(nameof(withdrawal));

            return new WithdrawalRequestModel
            {
                Amount = withdrawal.Amount,
                Asset = withdrawal.Asset,
                Destination = withdrawal.Destination,
                Fee = withdrawal.Fee,
                PaymentProvider = withdrawal.PaymentProvider
            };
        }
    }
}
