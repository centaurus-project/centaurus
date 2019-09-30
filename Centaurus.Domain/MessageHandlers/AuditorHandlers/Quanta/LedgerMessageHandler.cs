using System;
using System.Collections.Generic;
using System.Text;
using Centaurus.Models;

namespace Centaurus.Domain
{
    public class LedgerMessageHandler : AuditorBaseQuantumHandler
    {
        public override MessageTypes SupportedMessageType { get; } = MessageTypes.LedgerCommitQuantum;
    }
}
