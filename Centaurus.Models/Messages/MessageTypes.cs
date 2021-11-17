using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Centaurus.Models
{
    public enum MessageTypes
    {
        /// <summary>
        /// A client requested funds withdrawal.
        /// </summary>
        WithdrawalRequest = 2,
        /// <summary>
        /// A client requested funds transfer to another account. 
        /// </summary>
        PaymentRequest = 3,
        /// <summary>
        /// A client requested order creation or update.
        /// </summary>
        OrderRequest = 4,
        /// <summary>
        /// A client requested existing order cancellation.
        /// </summary>
        OrderCancellationRequest = 5,
        /// <summary>
        /// A client requested data about it's account.
        /// </summary>
        AccountDataRequest = 10,
        /// <summary>
        /// Wrapper for client account data request. Created by Alpha.
        /// </summary>
        AccountDataRequestQuantum = 46,
        /// <summary>
        /// Wrapper for client request messages that contains withdrawal. Created by Alpha.
        /// </summary>
        WithdrawalRequestQuantum = 47,
        /// <summary>
        /// Wrapper for client request messages. Created by Alpha.
        /// </summary>
        RequestQuantum = 48,
        /// <summary>
        /// Quantum created by Alpha server that contains aggregated deposits provided by <see cref="DepositNotification"/>.
        /// </summary>
        DepositQuantum = 49,
        /// <summary>
        /// A client requested account's effects.
        /// </summary>
        QuantumInfoRequest = 50,
        /// <summary>
        /// Initiate connection handshake.
        /// </summary>
        HandshakeRequest = 100,
        /// <summary>
        /// Signed handshake data.
        /// </summary>
        HandshakeResponse = 101,
        /// <summary>
        /// Signed handshake data and last known apex.
        /// </summary>
        AuditorHandshakeResponse = 102,
        /// <summary>
        /// Operation result, containing original message and processing status.
        /// </summary>
        ResultMessage = 103,
        /// <summary>
        /// Operation result, containing original quantum and processing status.
        /// </summary>
        QuantumResultMessage = 104,
        /// <summary>
        /// ITransaction operation result, containing transaction signature, original quantum and processing status.
        /// </summary>
        ITransactionResultMessage = 105,
        /// <summary>
        /// Message from Alpha to a client with all effects that affects the client, and wasn't triggered by the client (Trade for example).
        /// </summary>
        EffectsNotification = 106,
        /// <summary>
        /// Auditor's performance statistics.
        /// </summary>
        AuditorPerfStatistics = 107, 
        /// <summary>
        /// Internal message. Contains quantum message envelope and effects signatures.
        /// </summary>
        PendingQuantum = 108,
        /// <summary>
        /// Internal message. Contains new Auditor state.
        /// </summary>
        StateUpdate = 109,
        /// <summary>
        /// Account data request result.
        /// </summary>
        AccountDataResponse = 151,
        /// <summary>
        /// Account's effects request result.
        /// </summary>
        QuantumInfoResponse = 152,
        /// <summary>
        /// Update quorum, add/remove auditors, apply new settings etc.
        /// </summary>
        ConstellationUpdate = 201,
        /// <summary>
        /// Wrapper for constellation requests.
        /// </summary>
        ConstellationQuantum = 205,
        /// <summary>
        /// Contains last saved Apex
        /// </summary>
        CatchupQuantaBatchRequest = 211,
        /// <summary>
        /// Contains batch of quanta, that was requested by Alpha
        /// </summary>
        CatchupQuantaBatch = 212,
        /// <summary>
        /// Contains cursors for quanta and signatures
        /// </summary>
        SyncCursorReset = 213,
        /// <summary>
        /// Contains batch of quanta, that were processed by Alpha
        /// </summary>
        AlphaQuantaBatch = 214,
        /// <summary>
        /// Contains majority of signatures for quanta (except Alpha signatures, it will be send with quantum)
        /// </summary>
        QuantumMajoritySignaturesBatch = 215,
        /// <summary>
        /// Contains an array of the current auditor's quantum signatures
        /// </summary>
        AuditorSignaturesBatch = 216
    }
}
