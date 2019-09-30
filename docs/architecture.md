# Architecture

### Technology stack

- .NET Core for the server
- WebSockets as a communication protocol
- JavaScript for the web-based frontend
- XDR for messages encoding

We needed a cross-platform language with strong types system, automatic garbage collection, 
descent performance, and optimal development speed for the backend. 
After lengthy considerations, we have chosen .NET Core as the best match in our case. 

WebSockets became a popular standard for bidirectional web-based communication. The protocol is 
platform-agnostic and has mature implementations on most programming languages, which simplifies 
building client SDKs on any programing language. Also, it features high network speed and 
relatively low message size overhead.

There are a lot of binary serialization options out there, but XDR is a straightforward and 
unopinionated RFC standard, which suites our needs in terms of space efficiency and unified 
serialization approach.

## Server roles

Constellation servers share the same set of core modules. Depending on the server role 
(alpha or auditor) a module can be active or inactive. 
There can be only one alpha server per cluster, and up to 19 auditors. 

|  | **Alpha** | **Auditor** |
| --- | --- | --- |
| Communicates directly with clients | yes | no |
| Communicates with other constellation members | yes | no |
| Maintains the full internal copy of the global state | yes | yes |
| Executes operations and applies state changes | yes | yes |
| Monitors Stellar ledger | no | yes |
| Confirms (signs) withdrawal requests | no | yes |  

## Server core modules

- [**Orderbook**](exchange.md) contains active exchange orders for a particular trading pair.
- [**Matching engine**](exchange.md) executes trades.
- **Vault manager** controls internal balances and the multisig-protected Stellar account. 
- [**StellarLink**](stellar-link.md) monitors vault transactions, processes on-chain 
deposits and withdrawals.
- [**Snapshot manager**](snapshots.md) is responsible for periodical checkpoints creation, 
verification, and restoration. It also maintains full history archives.
- **Herald** is responsible for communication between Alpha and auditors.  
- **Gateway** introduces public entry point for all client connections.
- **Сhronicler** service provides queryable interface for the accounts history over WebSockets.

## Startup sequence

**Alpha**

- Сhronicler is started. It fetches the last known snapshot and assumes that the cluster 
hasn't been started yet if there is none. 
- Herald is up and waiting for auditors to connect.
- Snapshot manager restores the state from the snapshot and waits for auditor consensus 
on the snapshot and pending quanta. Once the consensus on current state is reached, 
Alpha applies it and sends to all auditors.
- Gateway initializes the WebSocket server which starts processing requests from clients once 
the majority of auditors are connected.

**Auditor**

- Сhronicler is started. It fetches the snapshot from the last known checkpoint and subsequent 
individual quantum records if they are available locally.
- Herald connects to Alpha, executes handshake routine, and synchronizes the checkpoint with 
Alpha server. If Alpha is in the rising state, an auditor sends its own state including current 
snapshot and pending quanta.
- Auditor restores the internal state to match the consensus state sent by Alpha and validates 
it against the most recent available checkpoint.
- StellarLink payment observer is launched. It synchronizes the state with Vault manager and 
ensures the balances.
- The auditor is up and awaits for next quantum from Alpha.