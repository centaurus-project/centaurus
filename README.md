![](docs/centaurus256.png)

# Project Centaurus

Project Centaurus is a second-layer payment network and exchange for 
[Stellar Network](https://www.stellar.org/).

## Features

- Payments and trades with instant confirmation and 5 seconds finality.
- Built-in assets exchange.
- Decentralized consensus with the trust model resembling a simplified SCP protocol.
- Funds secured directly on Stellar multisig account.
- Designed to handle more than 10,000 tps per cluster.
- Zero transaction fees.
- Ready-to-use private networks backed by public Stellar ledger.
- Open-source (because the best things in the world are free of charge).
- Low hardware requirements.

Sounds good? But wait, the platform also allows to build even more fascinating things in the future:

- Sharding and infinite scaling with cross-cluster communication.
- 2-hop path payments.
- Base reserve sponsorship for enterprise accounts.
- Sponsored accounts with reserved funds for seamless users on-boarding.
- Margin trading and lending.
- Dark pool and anonymous transactions.
- Assets with custom precision, non-divisible assets, NFT tokens.
- Cross-chain swaps.
- Auctions.

## Concept

A deployed cluster (**constellation**) consists of one leading server (**alpha**) and 5-19 
**auditor** servers. All servers are deployed by independent organizations or business entities, 
and each party publicly confirms its identity with a standard Stellar ed25519 public key. 
The organizations forming the constellation (and their public keys) won't be changed frequently, 
therefore it makes sense to ship public keys with client software to allow direct signature 
verification on the client side.

Clients communicate directly with the alpha server by sending atomic operation requests (account 
funding, payment, exchange order, or withdrawal request). Each operation (**quantum**) is processed 
and acknowledged by alpha which assigns a sequence number (**apex**) based on a FIFO principle, 
and then asynchronously verified by all auditors. Alpha maintains all account records, balances, 
and orderbooks. When the new quantum arrives, it validates the client's signature, executes 
operation, and broadcasts processed quantum to all auditors. Each auditor verifies and applies 
the quantum, sending back a signature, which essentially means its approval. 
When alpha receives the majority of auditors' signatures, it aggregates them and sends to 
the client. The client checks all the signatures and acknowledges the finality of the operation.

Funds received from clients are stored in a single Stellar account (**vault**) protected by 
M-of-N multisig, where N is the total number of independent servers forming a constellation, 
and M is the majority of votes (>50%) plus 1. Any withdrawal operation requires the majority of 
signatures from the quorum participants and ensured by the underlying Stellar ledger.

Every 5 seconds alpha initiates state reconciliation (**checkpoint**). It calculates the hash 
of current internal status, including all balances and orderbooks, signs it and sends to auditors. 
Each auditor also calculates the hash for the internal state and compares it with the value 
received from the alpha server. If the hashes don't match, auditor immediately halts processing 
operations queue and falls out of consensus. 
When the alpha can't reach consensus with the constellation, it stops all operations, rollbacks 
to the last quantum confirmed by the majority, and starts processing only withdrawal requests. 
In case if alpha is down, auditors may choose to deploy a new alpha server and proceed from the 
last checkpoint or initiate an emergency refund, returning all funds back to the client accounts.

Initial quantum acknowledgment takes only a fraction of a second. It guarantees that the account 
state was updated on the alpha server, and can be viewed as final for all subsequent operations 
inside the constellation (like trading, transfers, etc.) 

The **finality** (100% confidence) is ensured within a maximum of 5 seconds (one snapshot period). 
Typically it takes only 1-3 seconds to receive the final confirmation depending on the workload on 
auditor servers. Finality means that the quantum has been acknowledged and processed by 
the majority of the auditors, so a user will be able to withdraw funds even if the constellation 
is not operational. A quantum without a final confirmation potentially can be lost in case of the 
alpha server reboot or problems with consensus. Therefore it is strongly recommended to wait for 
the final confirmation before releasing the liabilities (goods, services, bank transfers, 
payments on the blockchain, etc.)

Cross-cluster and cluster-ledger communication can be implemented based on simple on-chain swaps 
between clusters or using payment channels as it's much easier to organize payment channels 
between a few highly available clusters. 
With such a design, clusters will be able to trade p2p or execute arbitrage path payments on 
Stellar DEX, crossing the open Stellar offers with internal orders.

### Use cases

Combination of characteristics described above opens a wide range of possible use cases: 
- Remittance and settlement networks may use Centaurus for their services as a private settlement 
layer secured by Stellar ledger. Instant acknowledgments and high throughput allow building 
something like SWIFT with minimum efforts. Correspondent banks run auditor nodes while domestic 
banks and other financial institutions operate as clients. The setup is flexible enough to 
handle payments from tens of thousands of banks alongside with automatic currency conversion 
through the built-in exchange. The ability to specify custom permissioned logic and extended 
compliance rules on the auditor level is also an important feature for enterprise networks.
- One of the most common problems with Stellar adoption is the need to match the underlying 
protocol requirements: trustlines, base reserve, transaction fees. Various B2C services 
(like loyalty programs, games, financial apps) strive to provide the abstraction on top of 
blockchain to simplify onboarding and UX. With Centaurus, companies can deploy their private 
protected clusters without transaction fees or base reserve requirements. 
Users will have a comfortable UX while performing regular tasks on the constellation backed by 
Stellar pubnet. At the same time, they will be able to withdraw their tokens/community points 
to any Stellar wallet and trade or use them outside the sandbox provided by the company.
- A Centaurus cluster may serve as a trusted distributed custodian for other services like 
cross-chain swaps, sidechains settlement, escrow, etc.
- Finally, deploying one large multipurpose constellation supported by all current Stellar 
validators opens the way to scale pubnet throughput well beyond the current level, especially in 
cases that require instant payments and HFT trading. Trading user experience will be very similar 
to the traditional centralized exchanges. As soon as only ASSET-XLM trades pairs allowed, 
markets liquidity should be higher. Besides that, it will be much safer, as the majority of 
auditors must agree on the new token listing before it will be added to the orderbooks.

## Rationale

Stellar SCP protocol is brilliant, and it proved the ability to work perfectly under the high load. 
However, we all can agree that we need a second layer sharding solution that will gradually 
decrease the workload on the mainnet and provide the overall 100x throughput increase in order 
to be able to compete with traditional payment systems and exchanges.

We conducted detailed research on state channels that recently become increasingly fashionable 
thanks to Bitcoin Lightning network concept. This technology may be a breakthrough for direct 
p2p payment channels with high volumes, but it does not allow building the payment/exchange 
network out of the box. Besides that, the concept underneath the Lightning technology itself has 
a lot of [flaws](https://www.scipioerp.com/2018/12/12/is-lightning-ready-for-commerce/).

After a series of experiments with Stellar payment channels, we decided to return to the core 
principles of all blockchains and Stellar in particular. When we talk about ledger sharding, 
the most straightforward approach that comes to mind is to deploy one more blockchain and operate 
on it, periodically synchronizing account states with the mainnet. Despite the seeming simplicity 
of such an approach, a shard will have the same problems: limited throughput (due to the very 
nature of blockchain), discrete confirmation time (clients have to wait for the new block to 
arrive), high resources overhead (fast synchronous consensus is expensive). An ideal solution 
should combine the reliability and decentralization of blockchain with performance and 
responsiveness of centralized systems.

### Async consensus principle

The cornerstone principle of the async consensus paradigm based on the assumption that consensus 
participants may safely delay the operation validation if the assets are securely locked on the 
account balance in the trusted system. Stellar provides the trusted environment and all required 
security primitives out of the box: transactions, multi-sig accounts, configurable thresholds. 
With such an approach, we need to get the majority of signatures from the vault account signers 
to process deposits and withdrawals while the inter-cluster operations (like trading, internal 
transfers) may be confirmed and processed by the alpha server almost in real-time and then 
verified by auditors post-factum. 

In most cases, the 99.99% confidence provided after the first confirmation from the alpha server 
is enough, and clients may enjoy ultra-fast operations and user experience that can be achieved 
only with centralized systems. Bad actors cannot manipulate operations or steal user's funds 
unless they have the quorum majority. Of course, independent organizations that deploy auditor 
nodes and establish a constellation are crucial for the overall system security. 
This part closely resembles Stellar consensus model.

In the case of constellation crash or quorum conflicts, the client acknowledges unconfirmed 
operations and recognizes them as failed. In the worst case, a few most recent operations may 
be lost after the constellation cluster recovery, but it's not a problem since the balances are 
preserved on Stellar ledger. Сonsidering the fact that 100% finality (confirmation from the 
majority of auditors) can be reached in 1-3 seconds, the client may choose to wait for 
the operation finality or treat the first confirmation as final depending on preconditions, 
which is essential for enterprise-grade systems.

### Simplicity principle

The simplicity is another basic principle of the proposed concept. It supports only basic atomic 
operations, namely deposits/withdrawals, trades (submit or cancel order), and payments. 
Due to the absence of preconditions, transactions, and fees, the operation flow is quite simple 
and straightforward. The engine executes payments and trades strictly sequentially, following 
the natural first-arrived-first-processed order. Account balances and orderbooks reside entirely 
in memory to provide the maximum possible performance. The state can be easily restored from 
checkpoint snapshots after the node restart or unexpected crash.

Auditors communicate directly with the alpha server, which means that they can be deployed behind 
the firewall without public IP, and thus automatically protected from DDoS and flooding attacks. 
In a setup with a single leading node, there is no nomination/voting/flooding network overhead. 
Auditors simply agree or disagree with the next arrived operation sent by the alpha. 
In the latter case, an auditor node just falls out of consensus and stops validating. 
We aim to make auditor nodes deployment and administration as effortless as possible, ideally – 
deploy and forget.

Binary XDR format used for communication and a superset of Stellar XDR data contracts ensures 
maximum compatibility with Stellar Network and Stellar SDKs. The communication protocol itself 
is minimalistic, as well as a FIFO matching engine. Fundamentally, all components are designed 
to be plain and straightforward to provide reliability and predictable performance while exposing 
minimal attack surface.


## Reference

- [Roadmap](docs/roadmap.md)
- [Architecture](docs/architecture.md)
- [Security](docs/security.md)
- [Exchange](docs/exchange.md)
- [StellarLink](docs/stellar-link.md)
- [Message types](docs/messages.md)
- [Quantum structure](docs/quantum-flow.md)
- [Snapshots](docs/snapshots.md)
- [Administration](docs/administration.md)
- [Extensions](docs/extensions.md)

---

#### Why Centaurus? 

Because it just as a mythical creature combines the reliability and security of Stellar ledger 
with high throughput and performance of centralized systems. And of course, Alpha Centauri 
(which is a triple star system by itself) is the closest star to the Solar System. 
Centaurus architecture is tightly integrated with Stellar, so we decided to employ distinctive 
names for major logical entities to prevent possible confusion.