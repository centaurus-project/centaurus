using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain
{
    public class AccountDataRequestHandler : AlphaBaseQuantumHandler
    {
        public AccountDataRequestHandler(ExecutionContext context) 
            : base(context)
        {
        }

        public override MessageTypes SupportedMessageType => MessageTypes.AccountDataRequest;
    }
}
