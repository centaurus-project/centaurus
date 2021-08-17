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
        EffectsRequest = 50,
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
        /// The message is send after success handshake. Contains account id.
        /// </summary>
        ClientConnectionSuccess = 150,
        /// <summary>
        /// Account data request result.
        /// </summary>
        AccountDataResponse = 151,
        /// <summary>
        /// Account's effects request result.
        /// </summary>
        EffectsResponse = 152,
        /// <summary>
        /// Update quorum, add/remove auditors, apply new settings etc.
        /// </summary>
        ConstellationUpdate = 201,
        /// <summary>
        /// Quanta batch request
        /// </summary>
        QuantaBatchRequest = 203,
        /// <summary>
        /// Contains batch of quanta
        /// </summary>
        QuantaBatch = 204,
        /// <summary>
        /// Wrapper for constellation requests.
        /// </summary>
        ConstellationQuantum = 205,
        /// <summary>
        /// Contains array of auditor's quantum processing results
        /// </summary>
        AuditorResultsBatch = 210
    }
}
