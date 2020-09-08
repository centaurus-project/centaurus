using Centaurus.Models;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class WithdrawalCleanupProcessorContext : TransactionProcessorContext
    {
        public WithdrawalCleanupProcessorContext(EffectProcessorsContainer effectProcessorsContainer)
            : base(effectProcessorsContainer)
        {
        }

        public List<Withdrawal> Withdrawals { get; set; } = new List<Withdrawal>();
    }
}
