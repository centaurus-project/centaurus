using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class WithrawalsCleanupQuantum : Message
    {
        public override MessageTypes MessageType => MessageTypes.WithrawalsCleanup;

        [XdrField(0)]
        public List<byte[]> ExpiredWithdrawals { get; set; }
    }
}
