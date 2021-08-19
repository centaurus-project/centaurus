using System;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    [XdrContract]
    [XdrUnion((int)MessageTypes.HandshakeRequest, typeof(HandshakeRequest))]
    [XdrUnion((int)MessageTypes.HandshakeResponse, typeof(HandshakeResponse))]
    [XdrUnion((int)MessageTypes.AuditorHandshakeResponse, typeof(AuditorHandshakeResponse))]
    [XdrUnion((int)MessageTypes.ClientConnectionSuccess, typeof(ClientConnectionSuccess))]
    [XdrUnion((int)MessageTypes.WithdrawalRequest, typeof(WithdrawalRequest))]
    [XdrUnion((int)MessageTypes.AccountDataRequest, typeof(AccountDataRequest))]
    [XdrUnion((int)MessageTypes.AccountDataRequestQuantum, typeof(AccountDataRequestQuantum))]
    [XdrUnion((int)MessageTypes.ResultMessage, typeof(ResultMessage))]
    [XdrUnion((int)MessageTypes.QuantumResultMessage, typeof(QuantumResultMessage))]
    [XdrUnion((int)MessageTypes.AccountDataResponse, typeof(AccountDataResponse))]
    [XdrUnion((int)MessageTypes.EffectsNotification, typeof(EffectsNotification))]
    [XdrUnion((int)MessageTypes.PaymentRequest, typeof(PaymentRequest))]
    [XdrUnion((int)MessageTypes.OrderRequest, typeof(OrderRequest))]
    [XdrUnion((int)MessageTypes.OrderCancellationRequest, typeof(OrderCancellationRequest))]
    [XdrUnion((int)MessageTypes.RequestQuantum, typeof(RequestQuantum))]
    [XdrUnion((int)MessageTypes.WithdrawalRequestQuantum, typeof(WithdrawalRequestQuantum))]
    [XdrUnion((int)MessageTypes.DepositQuantum, typeof(DepositQuantum))]
    [XdrUnion((int)MessageTypes.AuditorPerfStatistics, typeof(AuditorPerfStatistics))]
    [XdrUnion((int)MessageTypes.StateUpdate, typeof(StateUpdateMessage))]
    [XdrUnion((int)MessageTypes.AuditorResultsBatch, typeof(AuditorResultsBatch))]
    [XdrUnion((int)MessageTypes.ConstellationUpdate, typeof(ConstellationUpdate))]
    [XdrUnion((int)MessageTypes.ConstellationQuantum, typeof(ConstellationQuantum))]
    [XdrUnion((int)MessageTypes.QuantaBatchRequest, typeof(QuantaBatchRequest))]
    [XdrUnion((int)MessageTypes.QuantaBatch, typeof(QuantaBatch))]
    [XdrUnion((int)MessageTypes.EffectsRequest, typeof(EffectsRequest))]
    [XdrUnion((int)MessageTypes.EffectsResponse, typeof(QuantumInfoResponse))]
    [XdrUnion((int)MessageTypes.PendingQuantum, typeof(PendingQuantum))]
    public abstract class Message 
    {
        public virtual long MessageId { get { return 0; } }
    }
}
