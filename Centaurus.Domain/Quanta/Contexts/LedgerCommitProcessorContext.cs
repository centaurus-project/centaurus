using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class LedgerCommitProcessorContext : ProcessorContext
    {
        public LedgerCommitProcessorContext(EffectProcessorsContainer effectProcessors) 
            : base(effectProcessors)
        {
        }

        public Dictionary<Withdrawal, WithdrawalWrapper> Withdrawals { get; } = new Dictionary<Withdrawal, WithdrawalWrapper>();
    }
}
