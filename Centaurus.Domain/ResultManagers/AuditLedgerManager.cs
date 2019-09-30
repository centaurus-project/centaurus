using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public class AuditLedgerManager : MajorityManager
    {
        public LedgerCommitQuantum Add(MessageEnvelope envelope)
        {
            var confirmation = Aggregate(envelope);
            if (confirmation == null) return null;
            //we have consensus
            return new LedgerCommitQuantum
            {
                Source = confirmation
            };
        }
    }
}
