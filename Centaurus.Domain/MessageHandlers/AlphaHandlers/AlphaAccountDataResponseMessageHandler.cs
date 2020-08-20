﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;
using NLog;

namespace Centaurus.Domain.Handlers.AlphaHandlers
{
    public class AlphaAccountDataResponseMessageHandler : AlphaResultMessageHandler
    {
        public override MessageTypes SupportedMessageType { get; } = MessageTypes.AccountDataResponse;
    }
}