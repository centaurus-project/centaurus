# Quantum structure and flow

### Quantum structure

All quantum requests are inherited from **RequestMessage**, and **RequestMessage** is inherited 
from [**Message**](docs/messages.md). 

Once the quantum is validated by Alpha, it wraps client's quantum request with **RequestQuantum**, 
and assigns the apex and timestamp upon execution, before propagating it further to auditors.
An auditor in turn validates **RequestQuantum** alongside wit the original request and sends 
an audit result back to Alpha.

When the result majority is reached, Alpha create an envelop containing the result message and 
all auditor signatures. The envelope is delivered back to the client, which verifies the signatures 
and marks the quantum as finalized.

**RequestMessage** structure: 

- **`account`** (`Array<byte>`) - Public key of the client's account - 32 bytes.
- **`nonce`** (`Int64`) - A sequential nonce provided by the client to prevent replay attacks.

**QuantumTypes**

- `2` **Withdrawal** – Request funds withdrawal.   
- `3` **Payment** – Transfer funds to another account. 
- `4` **Order** – Create/update order.
- `5` **OrderCancellation** – Cancel existing order.
- `48` **RequestQuantum** – Wrapper for client request messages. Created by Alpha.
- `49` **LedgerCommitQuantum** – Quantum created by Alpha server that contains aggregated ledger 
updates provided by **LedgerUpdateNotification**
- `50` **SnapshotQuantum** – Snapshot initialization quantum request. Created by Alpha.
- `201` **ConstellationUpgrade** – Upgrade quorum, add/remove auditors, apply new settings etc.

**RequestQuantum** structure:

- **`apex`** (`Int64`) - The most recent quantum apex (sequence).
- **`timestamp`** (`Int64`) - Alpha server timestamp when the quantum has been fully executed 
on Alpha.
- **`requestEnvelope`** (`MessageEnvelope`) - Original operation request received from a 
client ([**MessageEnvelope**](docs/messages.md)).


**Payment** and **Withdrawal** has same structure. The only difference is type. Structure:

- **`asset`** (`Int32`) - Centaurus asset id.
- **`amount`** (`Int64`) - Payment amount.
- **`destination`** (`Array<byte>`) - Destination public key.
- **`memo`** (`String`) - Memo field.
- **`transactionHash`** (`Array<byte>`) - Stellar transaction hash..
- **`transactionXdr`** (`Array<byte>`) - Stellar transaction.
	
**Order** structure:

- **`asset`** (`Int32`) - Centaurus asset id.
- **`side`** (`Int32`) - Order side. `Sell = 1` and `Buy = 2`
- **`amount`** (`Int64`) - Order amount.
- **`price`** (`Float64`) - Asset price.
- **`timeInForce`** (`Int32`) - Order type. `GoodTillExpire = 0, ImmediateOrCancel = 1`.
	
**OrderCancellation** structure:

- **`orderId`** (`UInt32`) - Order to cancel id.
	
	
**LedgerCommitQuantum** structure:

- **`ledger`** (`UInt64`) - Ledger sequence number.
- **`payments`** (`Array<Payment>`) - List of payments witnessed by an auditor 
([**Payment**](docs/messages.md)).
	
**SnapshotQuantum** structure:

- **`snapshotHash`** (`Array<byte>`) - Alpha snapshot hash.
