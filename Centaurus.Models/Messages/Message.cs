﻿using System;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    [XdrContract]
    [XdrUnion((int)MessageTypes.HandshakeInit, typeof(HandshakeInit))]
    [XdrUnion((int)MessageTypes.Heartbeat, typeof(Heartbeat))]
    [XdrUnion((int)MessageTypes.WithdrawalRequest, typeof(WithdrawalRequest))]
    [XdrUnion((int)MessageTypes.PaymentRequest, typeof(PaymentRequest))]
    [XdrUnion((int)MessageTypes.OrderRequest, typeof(OrderRequest))]
    [XdrUnion((int)MessageTypes.RequestQuantum, typeof(RequestQuantum))]
    [XdrUnion((int)MessageTypes.LedgerCommitQuantum, typeof(LedgerCommitQuantum))]
    [XdrUnion((int)MessageTypes.SnapshotQuantum, typeof(SnapshotQuantum))]
    [XdrUnion((int)MessageTypes.ResultMessage, typeof(ResultMessage))]
    [XdrUnion((int)MessageTypes.LedgerUpdateNotification, typeof(LedgerUpdateNotification))]
    [XdrUnion((int)MessageTypes.AuditorState, typeof(AuditorState))]
    [XdrUnion((int)MessageTypes.SetApexCursor, typeof(SetApexCursor))]
    [XdrUnion((int)MessageTypes.AlphaState, typeof(AlphaState))]
    public abstract class Message 
    {
        public abstract MessageTypes MessageType { get; }

        public virtual ulong MessageId { get { return 0; } }
    }
}
