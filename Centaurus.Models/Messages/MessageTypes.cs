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
        /// Wrapper for client request messages that contains transaction. Created by Alpha.
        /// </summary>
        RequestTransactionQuantum = 47,
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
        /// Operation result, containing original message and processing status.
        /// </summary>
        ResultMessage = 102,
        /// <summary>
        /// Operation result, containing original quantum and processing status.
        /// </summary>
        QuantumResultMessage = 103,
        /// <summary>
        /// ITransaction operation result, containing transaction signature, original quantum and processing status.
        /// </summary>
        ITransactionResultMessage = 104,
        /// <summary>
        /// Message from Alpha to a client with all effects that affects the client, and wasn't triggered by the client (Trade for example).
        /// </summary>
        EffectsNotification = 105,
        /// <summary>
        /// Auditor current state (the last snapshot, and all quanta after the last snapshot).
        /// </summary>
        AuditorState = 106,
        /// <summary>
        /// Auditor's performance statistics.
        /// </summary>
        AuditorPerfStatistics = 107,
        /// <summary>
        /// Set auditor apex cursor.
        /// </summary>
        SetApexCursor = 110,
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
        /// Contains data for init. It will be created by alpha on init.
        /// </summary>
        ConstellationInitRequest = 200,
        /// <summary>
        /// Upgrade quorum, add/remove auditors, apply new settings etc.
        /// </summary>
        ConstellationUpgradeRequest = 201,
        /// <summary>
        /// Alpha state message. It contains Alpha state and last snapshot.
        /// </summary>
        AlphaState = 202,
        /// <summary>
        /// Alpha requests auditor's state for the specified apex
        /// </summary>
        AuditorStateRequest = 203,
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
