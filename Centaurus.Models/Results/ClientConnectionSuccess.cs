﻿using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    public class ClientConnectionSuccess : ResultMessage
    {
        public override MessageTypes MessageType => MessageTypes.ClientConnectionSuccess;

        [XdrField(0)]
        public ulong AccountId { get; set; }
    }
}