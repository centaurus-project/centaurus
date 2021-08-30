## Payment providers

Centaurus gives the ability to implement your own payment providers. Each auditor have same providers 
settings, it's a part of constellation settings quantum. 

#### Processing payments

On new payment, provider adds payment notification to payments queue. After delay (`CommitDelay` is a required 
field of provider settings, we need it to make sure that all auditors have received the payment notification) 
alpha acknowledged about new payment, it generates deposit quantum and process it. During the processing each 
auditors verifies that hash of next payment in provider queue equals to provided one, and updates account 
balance.

#### Withdrawals

For withdrawal client sends request with provider id, asset and amount. Alpha validates request (if account has 
sufficient funds, that provider id can handle such withdrawal) and requests the provider to build the withdrawal
 transaction. After transaction is build, alpha assigns transaction to quantum, and send it to auditors. Each 
auditor walk trough same validation routine and additionally asks provider to validate transaction, it it's 
valid, than the auditor signs transaction and broadcasts signatures to auditors. After alpha has majority of 
signatures, it submit transaction via provider.

## Payment providers developer's guide:

- Create new NetCore 3.1 library project in VisualStudio.
- Add references to Centaurus.PaymentProvider project.
- Implement `Centaurus.PaymentProvider.PaymentProviderBase` abstract class.
- Create an payment providers [configuration file](#config-file) and pass it as argument `payment_config` on startup.
- Place all required libraries in any folder you like (it's suggested to put each provider's library in separate folder to avoid dll colisions) and put provider dll path to `assemblyPath` config field.

### Config file: 

Configuration file must valid key-value pair `json`. Where key is provider id in format 
`{provider_type}-{provider_name}`(for example `Stellar-TestNet`) and value is an object with next structure:

- **`assemblyPath`** (`string`) - Path to provider dll.
- **`config`** (`object`) - Custom json object. It will be passed as stringified json, so you can parse it to your settings class.

##### Payment provider example:
```
{
	"Stellar-TestNet": {
		"assemblyPath": "/srv/centaurus/stellar-provider/Centaurus.Stellar.PaymentProvider.dll",
		"config": {
			"Horizon": "https://horizon-testnet.stellar.org",
			"Secret": "SBYNDLIJXK5JJV3VFJA776PQTCUGAOMSRO6USEH6STE2DOPFWQ3QISER",
			"PassPhrase": "Test SDF Network ; September 2015"
		}
	}
}
```