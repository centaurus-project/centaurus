using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models.Extensions
{
    public static class BalanceExtensions
    {
        public static Balance Clone(this Balance source)
        {
            return new Balance
            {
                Amount = source.Amount,
                Asset = source.Asset,
                Liabilities = source.Liabilities
            };
        }
    }
}
