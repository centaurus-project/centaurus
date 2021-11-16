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

Constellation servers share the same set of core modules. Depending on the server participation level 
(Prime or Auditor) a module can be active or inactive. 
There can be only one alpha server per cluster, and up to 19 auditors. 

|  | **Prime** | **Auditor** |
| --- | --- | --- |
| Communicates directly with clients | yes | no |
| Communicates with other constellation members | yes | yes |
| Maintains the full internal copy of the global state | yes | yes |
| Maintains the whole operations history | yes | no |
| Executes operations and applies state changes | yes | yes |
| Monitors payments | yes | yes |
| Confirms (signs) withdrawal requests | yes | yes |  

## ??Server core modules

- [**Orderbook**](exchange.md) contains active exchange orders for a particular trading pair.
- [**Matching engine**](exchange.md) executes trades.
- **Payment provider manager** launches and initializes payment providers.
- [**Payment provider**](payment-providers.md) monitors vault transactions, processes on-chain 
deposits and withdrawals.
- [**Pending updates manager**](snapshots.md) is responsible for periodical checkpoints creation. 
- **Data provider** provides access to history archives.
- **Herald** is responsible for communication between Alpha and auditors.  
- **Gateway** introduces public entry point for all client connections.
- **Chronicler** service provides queryable interface for the accounts history over WebSockets.

## Startup sequence

After server is started, it tries to get last snapshot from DB (if there is non than it will wait for 
**initialization**, see detail below). Snapshot contains all accounts, balances, orders, payment cursors 
and constellation settings. If current auditor is alpha, than it begins catchup procedure. Alpha requests 
all known quanta above last persistent apex from all available auditors. When alpha receives all quanta 
batches, it aggregates quanta signatures, validates it and process quanta. If there are any conflicts (two
 different quanta with same apex and with current auditor signature, gaps in quanta batches) or majority 
can't be reached it becomes failed. If current auditor isn't alpha than it starts sync quanta with 
constellation. When majority of auditors reached alpha quantum apex height, constellation becomes `Ready`, 
and it can process client requests.

## Initialization

For constellation initialization, auditors must compound a [**ContellationUpdate**](quantum-flow.md), sign it 
and post serialized quantum to alpha initialization endpoint (`{alpha_domain}/api/constellation/init`). 
Alpha verifies quantum signatures, validates constellation settings and, if it's valid, process it. After 
alpha initialized, it broadcasts quantum to connected auditors (auditors know about each other addresses and 
public keys from `auditor` argument passed on a startup, see [**administration**](administration.md) docs). 
Auditors follow the same validation procedure, and constellation is switched to `Ready` state.

## Deposit/registration

For deposit, a client must follow an implemented payment provider instructions. When the provider receives a new payment it notifies alpha about new pending payment. Pending payment contains public key of account to fund, asset and amount. Alpha generates [**DepositQuantum**](quantum-flow.md) and process it. To register, the client must deposit quote asset and the amount must be greater or equal to the constellation minimum account balance. If registration requirements are completed, account will be created, and the client can send quantum requests.  