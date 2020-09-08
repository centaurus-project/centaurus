using Centaurus.Models;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class WithdrawalProcessorContext : TransactionProcessorContext
    {
        public WithdrawalProcessorContext(EffectProcessorsContainer effectProcessorsContainer)
            : base(effectProcessorsContainer)
        {
        }

        public List<WithdrawalItem> WithdrawalItems { get; set; } = new List<WithdrawalItem>();
    }
}
