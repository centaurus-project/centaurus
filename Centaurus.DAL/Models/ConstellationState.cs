using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.DAL.Models
{
    /// <summary>
    /// Contains last processed ledger sequence, vault account sequence and current apex
    /// </summary>
    public class ConstellationState
    {
        public long VaultSequence { get; set; }
        public long Ledger { get; set; }
        public long CurrentApex { get; set; }
    }
}
