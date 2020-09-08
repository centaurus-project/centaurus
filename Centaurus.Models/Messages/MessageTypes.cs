using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Centaurus.Models
{
    public enum MessageTypes
    {
        /// <summary>
        /// Heartbeat message
        /// </summary>
        Heartbeat = 0,
        /// <summary>
        /// A client requested funds deposit.
        /// </summary>
        DepositRequest = 1,
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
        /// Wrapper for client request messages. Created by Alpha.
        /// </summary>
        RequestQuantum = 48,
        /// <summary>
        /// Quantum created by Alpha server that contains aggregated ledger updates provided by <see cref="LedgerUpdateNotification"/>.
        /// </summary>
        LedgerCommitQuantum = 49,
        /// <summary>
        /// A client requested account's effects.
        /// </summary>
        EffectsRequest = 50,
        /// <summary>
        /// Quantum created by Alpha server that contains all withdrawal transaction hashes that are expired.
        /// </summary>
        WithrawalsCleanup = 51,
        /// <summary>
        /// Initiate connection handshake.
        /// </summary>
        HandshakeInit = 100,
        /// <summary>
        /// Operation result, containing original quantum and processing status.
        /// </summary>
        ResultMessage = 102,
        /// <summary>
        /// Message from auditor to Alpha server that contains all Stellar payments included into the recent ledger (obtained from the Horizon).
        /// </summary>
        LedgerUpdateNotification = 105,
        /// <summary>
        /// Auditor current state (the last snapshot, and all quanta after the last snapshot).
        /// </summary>
        AuditorState = 106,
        /// <summary>
        /// Set auditor apex cursor.
        /// </summary>
        SetApexCursor = 110,
        /// <summary>
        /// Account data request result.
        /// </summary>
        AccountDataResponse = 150,
        /// <summary>
        /// Account's effects request result.
        /// </summary>
        EffectsResponse = 151,
        /// <summary>
        /// Contains data for init. It will be created by alpha on init.
        /// </summary>
        ConstellationInitQuantum = 200,
        /// <summary>
        /// Upgrade quorum, add/remove auditors, apply new settings etc.
        /// </summary>
        ConstellationUpgradeQuantum = 201,
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
        QuantaBatch = 204
    }
}
