# Administration

## Installation Instructions

To install Centaurus you need to download the latest release for your operating system or download source code 
and build an application on your own. Also you need to prepare you payment providers libraries. Make sure that 
application has rights to access to  
For UNIX operation systems, you need to install  [RocksDB](https://github.com/facebook/rocksdb/blob/master/INSTALL.md). 

## Node launching

To start the node you need to pass valid arguments on Centaurus startup. 

Arguments for a startup:

- **`s, secret`** - Current node ed25519 secret seed. **Required**.
- **`cwd`** - Working directory. **Required**.
- **`participation_level`** - Node participation level. `1` = `Prime` and `2` = `Auditor`. **Required**.
- **`connection_string`** - Path to the database folder. **Required**.
- **`payment_config`** - Path to the payment configuration file. **Required**.
- **`auditor`** - This parameter is only required for a new constellation. It is required for an initial connection to alpha server.
- **`listening_port`** - Port the node will listen on.
- **`cert_path`** - Certificate path.
- **`cert_pk_path`** - Certificate private key file path.
- **`use_secure_connection`** - Use wss/https to connect to auditors.
- **`sync_batch_size`** - Maximum quanta per batch on sync between auditors. Default: 500.
- **`verbose`** - Add this argument to log in verbose mode.
- **`silent`** - Add this argument to log only errors.
- **`extensions_config_file_path`** - Path to extensions configuration file.
- **`help`** - Display help screen.
- **`version`** - Display version information.
 
All specified paths must be an absolute path or relative path to the working directory folder.

Alpha server launch example:

`./Centaurus --secret SBY...ER --participation_level 1 --connection_string db --cwd appData --payment_config payment-providers.config --listening_port 5000` 

And auditor launch will look like this:

`./Centaurus --secret SD6...G3  --participation_level 2 --connection_string db --cwd appData --payment_config payment-providers.config --auditor GC8...BJ=alpha.domain:5000` 

## Constellation initialization

Constellation must consist of 5-19 nodes. One of them is elected to be alpha. Each node must have the same 
Centaurus version and the same set of payment providers. After all servers are started, auditors connect to 
alpha and await for initialization quantum. Auditors create constellation update quantum and post it to alpha. 
If quantum is valid it's processed and broadcasted to all auditors. After auditors process the quantum the 
constellation becomes ready to process quantum requests. 

## Constellation restart

### Alpha restart

On alpha restart, it loads the last snapshot from the database and applies it. Then alpha requests auditors for 
quanta with apex above last alpha apex. If there is no majority of connected auditors, alpha shuts down. After 
auditors sent their quanta, alpha starts the aggregation process. All quanta group by the apex. Each quantum in 
the group must have alpha signature, if it doesn't these quantum signatures are not aggregated. If quantum with 
same apex has different payload hash and do have valid alpha signature, than alpha secret key is compromised, 
and alpha shuts down. If all quanta aggregated successfully, alpha processes it one by one. After quanta are 
processed constellation switches to `Ready` state.

### Auditor restart

Auditor restart procedure is much simpler. On the restart, the auditor sends to alpha his quantum and result 
cursors. Alpha starts broadcasting all quanta and results that have greater apex than the specified by the 
auditor. If the auditor is far behind alpha, it switches to `Chasing` mode. After the auditor reaches alpha's 
apex (with some delay threshold), it becomes `Ready`. 

## Changing constellation settings

At some point auditors can decide that they need to change constellation settings: add/suspend assets, register 
new payment providers, change auditors, assign a new alpha, etc. Centaurus provides an easy way to do it. All 
you need is to compound [**ConstellationUpdate**](quantum-flow.md) request, sign it with majority of auditors 
and submit. If the quantum is valid, constellation will apply new settings.