# Security

## Internal malicious actors

Due to the multisig scheme applied to the vault Stellar account, any attack on the consensus 
mechanism requires more than a half of all constellation auditors to agree on the operation. 
A client can't transfer, trade, or withdraw funds without confirmation from the majority of 
auditors. Since the auditors will be run by public entities, we can assume that it would be 
extremely hard to convince the majority of constellation members to participate in the open 
coordinate theft of users' funds.

**Malicious auditor**

If a particular auditor starts to behave maliciously (say, refusing to confirm valid operations, 
sending malformed data to the alpha server, flooding alpha with invalid requests), it can be 
expelled from the consensus by the alpha server without any damage to the constellation 
performance. Of course, we assume here that the constellation still has the majority + 1 of votes, 
which is required to sign client requests and process withdrawals. 
For example, a setup with 5 auditor nodes can survive 1 node failing out of consensus. 
A constellation with 7 auditors will keep functioning despite the outage of 2 nodes, and so on.

Auditor servers can't forge/mutate a quantum received from alpha or submit fake quantum requests 
on behalf of a client, as each auditor always verifies cryptographic client signature. One auditor 
also can't flood or DDoS another constellation auditors because they do not communicate directly 
and the malicious server doesn't know IP addresses of another constellation members except 
the alpha server.

**Malicious alpha**

If the alpha server is compromised, an attacker still can't tamper client requests protected by 
the cryptographic signatures and initiate fraudulent withdrawals. Any attempt to mutate internal 
accounts or orderbook state will result in a consensus breakdown and halt of all operations. 
Similarly, the alliance with compromised auditors still won't be enough to get the majority 
required for a confirmation.

In case if a client does not receive the majority of auditor confirmations after 5 seconds 
after the first acknowledgment response from the alpha server, it stops processing and waits 
for the correct confirmations. The quantum is considered non-final until it receives the majority 
of signatures from auditors.

Temporary service disruption is the only possible problem in this case. In the case of 
the prolonged alpha downtime, the constellation members may choose to deploy another alpha server 
(due to the frequent periodical full state snapshots it may be a matter of minutes) and notify 
clients about the new alpha endpoint.

## External threats

**Denial-of-service and flooding attacks**

In the situation when clients do not pay transaction fees, the only possible way to prevent 
DDoS/flooding attacks is to enforce strict rate-limiting rules on the alpha server 
(for example, 5 requests per second with configurable burst window) and request a substantial 
amount of XLM reserve, say 1000 XLM locked on the account. Both parameters can be changed over 
time using the constellation members voting mechanism. Also each client will have a limit of 
open orders, approximately 100 per account. 

**Man-in-the-middle and replay attacks**

Clients use ed25519-based cryptography to sign the requests. The private key is stored 
exclusively on the client side.  Each quantum request sent by a client contains a sequential 
`nonce` (Int64) which is a part of hashed and signed data. For the quantum to be valid, 
the nonce must be exactly one greater than the nonce of the previously processed quantum. 
As the quantum is applied, the account state on the server is updated with the new value. 
Every constellation server tracks and verifies the order of the account nonce independently. 
MitM attackers can't modify the signed quantum contents without access to the client private key. 
The connection with clients is established over the secure WebSocket protocol, protected by 
TLS certificate.