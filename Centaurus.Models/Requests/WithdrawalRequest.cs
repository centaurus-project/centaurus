using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class WithdrawalRequest : RequestMessage
    {
        public override MessageTypes MessageType => MessageTypes.WithdrawalRequest;
    }
}
