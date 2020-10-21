using System.Collections.Generic;

namespace Centaurus.Domain
{
    public class WithdrawalCleanupProcessorContext : ProcessorContext
    {
        public WithdrawalCleanupProcessorContext(EffectProcessorsContainer effectProcessorsContainer)
            : base(effectProcessorsContainer)
        {
        }

        public Withdrawal Withdrawal { get; set; }
    }
}
