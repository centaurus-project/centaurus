# StellarLink

StellarLink module monitors Vault transactions, processes on-chain deposits and withdrawals.

### Deposits

Users deposit their funds to the constellation by transferring them to Vault account. 
The source account of the payment operation is considered a user address. To start working with 
the constellation, a user has to deposit enough XLM to meet the minimum reserve requirements. 
Constellation accounts that don't have enough balance are considered underfunded and cannot 
submit quantum requests.

When an auditor acknowledges the incoming deposit transaction, it sends the notification to 
Alpha server. Alpha aggregates all deposit notifications, and once it receives the notifications 
from the majority of auditors, it updates the account balance locally and broadcasts the deposit 
quantum confirmation containing the list of deposited balances and Stellar transaction hash 
to all auditors. 
Each auditor verifies that the quantum message envelope contains the majority of auditor signatures. 

If the signatures validation passed, the deposit considered confirmed – auditor applies the 
deposit to the constellation account balance and sends the confirmation back to Alpha which 
in turn notifies the client about the updated balance.

### Withdrawals

Clients request withdrawals by sending a quantum request to Alpha server. A withdrawal quantum 
contains the destination address and list of asset-amount pairs specifying the amount of each 
asset to withdraw. Upon the request validation, Alpha locks the requested amount on the 
constellation account balance and generates Stellar transaction containing corresponding 
payment operations.

To support parallel withdrawals, each constellation member tracks Vault account sequence, 
incrementing it on every withdrawal request. As such, even if one of the transactions fails, 
it doesn't affect the subsequent withdrawal transactions. TimeBounds preconditions are configured 
to allow a 5-minute validity period. 

Alpha broadcasts the XDR-serialized transaction alongside with the original withdrawal quantum 
to auditors. Every auditor verifies the input, adds the hash of the withdrawal transaction to 
the local cache, and sends back to Alpha the transaction signature.
Once Alpha receives the majority of signatures, it prepares the transaction envelope, appends 
signatures received from auditors and submits it to the Stellar Network.

Each auditor listens to all Vault account transactions and performs a transaction lookup in 
the pending withdrawals list by its hash. If the corresponding hash is found, the auditor sends 
the operation acknowledgment to Alpha. Once the majority of confirmations received, 
the process considered finalized – Alpha updates local account balance, notifies a client about 
the successful withdrawal, and broadcasts withdrawal confirmation quantum to other constellation 
members, which in turn update local account balances correspondingly.

A client can have at most one pending withdrawal request at a time; subsequent withdrawal requests 
are automatically rejected by Alpha. At the same time, a client may submit other quantum requests, 
namely payments and trades. 

When the requested XLM amount lowers the constellation balance to the minimum account reserve, 
Alpha rejects the request. In such a situation, a client may request a complete funds withdrawal, 
which results in the constellation account closing; the client will have to deposit a minimum 
required balance again to resume the operations on the constellation.

### Stellar transactions monitoring

Immediately after startup, each auditor server fetches recent Vault account transactions history 
from the `/transactions` Horizon endpoint and subscribes to updates in streaming mode. 
Every time a new transaction arrives, an auditor examines and validates included operations.

- When an incoming payment found, the server runs a deposit processing routine.
- When an outgoing payment found, the server checks for the pending withdrawal by the 
transaction hash and finalizes it.
- Operations like `SetOptions`, `AccountMerge`, and `BumpSequence` are treated as 
constellation upgrade and validated following the constellation upgrade routines.
