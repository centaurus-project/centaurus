using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.DAL.Models
{
    /// <summary>
    /// Contains last processed ledger sequence and vault account sequence 
    /// </summary>
    public class StellarData
    {
        public long VaultSequence { get; set; }
        public long Ledger { get; set; }
    }
}
