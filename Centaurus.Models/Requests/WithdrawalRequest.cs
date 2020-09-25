﻿using System;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    public class WithdrawalRequest : NonceRequestMessage, ITransactionContainer
    {
        public override MessageTypes MessageType => MessageTypes.WithdrawalRequest;

        [XdrField(0)]
        public byte[] TransactionXdr { get; set; }
    }
}