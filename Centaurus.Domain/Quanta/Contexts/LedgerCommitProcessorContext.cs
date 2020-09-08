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

        public Dictionary<Models.Withdrawal, Withdrawal> Withdrawals { get; } = new Dictionary<Models.Withdrawal, Withdrawal>();
    }
}
