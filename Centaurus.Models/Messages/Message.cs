using System;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    [XdrContract]
    [XdrUnion((int)MessageTypes.HandshakeInit, typeof(HandshakeInit))]
    [XdrUnion((int)MessageTypes.HandshakeResult, typeof(HandshakeResult))]
    [XdrUnion((int)MessageTypes.WithdrawalRequest, typeof(WithdrawalRequest))]
    [XdrUnion((int)MessageTypes.AccountDataRequest, typeof(AccountDataRequest))]
    [XdrUnion((int)MessageTypes.AccountDataResponse, typeof(AccountDataResponse))]
    [XdrUnion((int)MessageTypes.ITransactionResultMessage, typeof(ITransactionResultMessage))]
    [XdrUnion((int)MessageTypes.EffectsNotification, typeof(EffectsNotification))]
    [XdrUnion((int)MessageTypes.PaymentRequest, typeof(PaymentRequest))]
    [XdrUnion((int)MessageTypes.OrderRequest, typeof(OrderRequest))]
    [XdrUnion((int)MessageTypes.OrderCancellationRequest, typeof(OrderCancellationRequest))]
    [XdrUnion((int)MessageTypes.RequestQuantum, typeof(RequestQuantum))]
    [XdrUnion((int)MessageTypes.TxCommitQuantum, typeof(PaymentCommitQuantum))]
    [XdrUnion((int)MessageTypes.ResultMessage, typeof(ResultMessage))]
    [XdrUnion((int)MessageTypes.AuditorState, typeof(AuditorState))]
    [XdrUnion((int)MessageTypes.AuditorPerfStatistics, typeof(AuditorPerfStatistics))]
    [XdrUnion((int)MessageTypes.SetApexCursor, typeof(SetApexCursor))]
    [XdrUnion((int)MessageTypes.AlphaState, typeof(AlphaState))]
    [XdrUnion((int)MessageTypes.AuditorStateRequest, typeof(AuditorStateRequest))]
    [XdrUnion((int)MessageTypes.AuditorResultsBatch, typeof(AuditorResultsBatch))]
    [XdrUnion((int)MessageTypes.ConstellationInitRequest, typeof(ConstellationInitRequest))]
    [XdrUnion((int)MessageTypes.ConstellationUpgradeRequest, typeof(ConstellationUpgradeQuantum))]
    [XdrUnion((int)MessageTypes.ConstellationQuantum, typeof(ConstellationQuantum))]
    [XdrUnion((int)MessageTypes.QuantaBatch, typeof(QuantaBatch))]
    [XdrUnion((int)MessageTypes.EffectsRequest, typeof(EffectsRequest))]
    [XdrUnion((int)MessageTypes.EffectsResponse, typeof(EffectsResponse))]
    [XdrUnion((int)MessageTypes.WithrawalsCleanup, typeof(WithrawalsCleanupQuantum))]
    public abstract class Message 
    {
        public abstract MessageTypes MessageType { get; }

        public virtual long MessageId { get { return 0; } }
    }
}
