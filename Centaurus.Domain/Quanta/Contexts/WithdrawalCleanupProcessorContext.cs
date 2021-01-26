using Centaurus.Models;
using System.Collections.Generic;

namespace Centaurus.Domain
{
    public class WithdrawalCleanupProcessorContext : ProcessorContext
    {
        public WithdrawalCleanupProcessorContext(EffectProcessorsContainer effectProcessorsContainer)
            : base(effectProcessorsContainer)
        {
        }

        public WithdrawalWrapper Withdrawal { get; set; }
    }
}
