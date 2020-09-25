using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class WithrawalsCleanupQuantum : Quantum
    {
        public override MessageTypes MessageType => MessageTypes.WithrawalsCleanup;

        [XdrField(0)]
        public byte[] ExpiredWithdrawal { get; set; }
    }
}