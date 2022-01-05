using System;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    [XdrContract]
    [XdrUnion((int)MessageTypes.HandshakeRequest, typeof(HandshakeRequest))]
    [XdrUnion((int)MessageTypes.HandshakeResponse, typeof(HandshakeResponse))]
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
    [XdrUnion((int)MessageTypes.RequestQuantum, typeof(ClientRequestQuantum))]
    [XdrUnion((int)MessageTypes.WithdrawalRequestQuantum, typeof(WithdrawalRequestQuantum))]
    [XdrUnion((int)MessageTypes.DepositQuantum, typeof(DepositQuantum))]
    [XdrUnion((int)MessageTypes.StateUpdate, typeof(StateMessage))]
    [XdrUnion((int)MessageTypes.SingleNodeSignaturesBatch, typeof(SingleNodeSignaturesBatch))]
    [XdrUnion((int)MessageTypes.AlphaUpdate, typeof(AlphaUpdate))]
    [XdrUnion((int)MessageTypes.ConstellationUpdate, typeof(ConstellationUpdate))]
    [XdrUnion((int)MessageTypes.ConstellationQuantum, typeof(ConstellationQuantum))]
    [XdrUnion((int)MessageTypes.SyncCursorReset, typeof(SyncCursorReset))]
    [XdrUnion((int)MessageTypes.MajoritySignaturesBatch, typeof(MajoritySignaturesBatch))]
    [XdrUnion((int)MessageTypes.SyncQuantaBatch, typeof(SyncQuantaBatch))]
    [XdrUnion((int)MessageTypes.RequestQuantaBatch, typeof(RequestQuantaBatch))]
    [XdrUnion((int)MessageTypes.CatchupQuantaBatchRequest, typeof(CatchupQuantaBatchRequest))]
    [XdrUnion((int)MessageTypes.CatchupQuantaBatch, typeof(CatchupQuantaBatch))]
    [XdrUnion((int)MessageTypes.QuantumInfoRequest, typeof(QuantumInfoRequest))]
    [XdrUnion((int)MessageTypes.QuantumInfoResponse, typeof(QuantumInfoResponse))]
    public abstract class Message 
    {
        public virtual long MessageId { get { return 0; } }
    }
}
