# Messages and message envelope

## Message

Message class is a base structure for all data streams inside the constellation.

**Message** structure:

- **`messsageType`** (`Int32`) - Message type discriminator.
- **`messageId`** (`UInt64`) - Message identifier. Message results refer to original messages 
using those identifiers.

Additional data depends on the message type.

**MessageTypes**

Message types and it's structures:

- `0` **Hardbeat** – a message to keep connection alive. Doesn't have any additional fields.
- `100` **HandshakeInit** - initiates connection handshake.
- `102` **ResultMessage** - operation result, containing original quantum and processing status.
- `105` **LedgerUpdateNotification** - message from an auditor to Alpha server that contains all 
Stellar payments included into the recent ledger obtained from the Horizon.
- `106` **AuditorState** - transmits auditor current state (the last snapshot and all quanta since 
the last snapshot).
- `110` **SetApexCursor** - message that sets auditor sequence counter to the specified position.
- `202` **AlphaState** - alpha state message, contains current Alpha state and last snapshot.
	
**HandshakeInit** structure:

- **`handshakeData`** (`Array<byte>`) - random 32 byte array sent by Alpha. 

**ResultMessage** structure:

- **`originalMessage`** (`Message`) - original message.
- **`status`** (`Int32`) - result status code.
- **`effects`** (`Array<Int32>`) - message effects.

**ResultStatusCodes**

- `200` - Success.
- `401` - Unauthorized.
- `500` - Internal Error.
- `551` - Unexpected Message.

**MessageEffects**

- `0` - Undefined.
- `1` - Asset Sent.
- `2` - Asset Received.
- `10` - Order Placed.
- `11` - Order Removed.
- `12` - Trade.
	
**LedgerUpdateNotification** structure:

- **`ledger`** (`UInt64`) - ledger sequence number.
- **`payments`** (`Array<Payment>`) - list of payments witnessed by an auditor.
	
**Payment** structure:

- **`type`** (`Int32`) - payment type `Deposit = 1` and `Withdrawal = 2`.
- **`asset`** (`Int32`) - asset id, unique within a constellation.
- **`amount`** (`Int64`) - payment amount.
- **`destination`** (`Array<byte>`) - destination account public key.
- **`transactionHash`** (`Array<byte>`) - Stellar transaction hash
- **`paymentResult`** (`Int32`) - `Success = 0` and `Failed = 1`.
	
**AlphaState** structure:
	
- **`state`** (`Int32`) - current Alpha server state.
- **`lastSnapshot`** (`Snapshot`) - the most recent valid snapshot.
	
**AuditorState** structure:

- **`state`** (`Int32`) - current auditor state.
- **`lastSnapshot`** (`Snapshot`) - the most recent valid snapshot.
- **`pendingQuantums`** (`Array<MessageEnvelope>`) - quanta processed since the last snapshot.
	
**ApplicationState**

- `0` - Undefined.
- `1` - Waiting for init.
- `2` - Running.
- `4` - Ready.
- `5` - Rising.
- `10` - Failed.
	
**SetApexCursor** structure:

- **`apex`** (`UInt64`) - apex sequence to set.

## Message envelope

Every message is wrapped with envelope, containing the message itself and alpha/auditor signatures. 

**MessageEnvelope** structure:

- **`message`** (`Message`) - nested message.
- **`signatures`** (`Array<Ed25519Signature>`) - ed25519 message signatures.
	
**Ed25519Signature** structure:
	
- **`signer`** (`Array<byte>`) - signer public key.
- **`signature`** (`Array<byte>`) - signature itself.
