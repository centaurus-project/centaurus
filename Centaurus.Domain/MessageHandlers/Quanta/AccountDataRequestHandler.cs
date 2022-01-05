﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain
{
    internal class AccountDataRequestHandler : QuantumHandlerBase
    {
        public AccountDataRequestHandler(ExecutionContext context) 
            : base(context)
        {
        }

        public override string SupportedMessageType => typeof(AccountDataRequest).Name;
    }
}