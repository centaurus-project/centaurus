using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    internal class WithdrawalMessageHandler : QuantumHandlerBase
    {
        public WithdrawalMessageHandler(ExecutionContext context) 
            : base(context)
        {
        }

        public override string SupportedMessageType { get; } = typeof(WithdrawalRequest).Name;
    }
}