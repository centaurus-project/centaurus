## Effects

Each quantum affects constellation. To make it easier to commit, revert and validate operation that were 
initiated by the quantum, this changes grouped to atomic objects that named effects. 

### Effect types

All effects are inherited from **Effect**. Structure:

- **`apex`** (`UInt64`) - Quantum apex. 

#### Constellation effects

**CursorUpdateEffect** - payment provider cursor is updated. Structure:

- **`provider`** (`string`) - Provider id.
- **`cursor`** (`string`) - New provider cursor.
- **`prevCursor`** (`string`) - Previous cursor. 


**ConstellationUpdateEffect** - constellation updated/initialized. Structure:

- **`settings`** (`ConstellationSettings`) - New constellation settings.
- **`prevSettings`** (`ConstellationSettings`) - Previous constellation settings. Could be null when it's initial effect.


**ConstellationSettings** structure:

- **`apex`** (`string`) - Quantum apex.
- **`providers`** (`Array<ProviderSettings>`) - Array of providers settings.
- **`auditors`** (`Array<Auditor>`) - Array of auditor settings.
- **`minAccountBalance`** (`UInt64`) - Minimal account balance.
- **`minAllowedLotSize`** (`UInt64`) - Minimal order size.
- **`assets`** (`Array<AssetSettings>`) - Array of asset settings.
- **`requestRateLimits`** (`RequestRateLimits`) - Default client request rate limit settings.
- **`alpha`** (`Array<byte>`) - Alpha public ed25519 key.


**ProviderSettings** structure:

- **`provider`** (`string`) - Provider type (Stellar, Bitcoin, Ethereum etc).
- **`name`** (`string`) - Provider name. Same provider type can have several configurations.
- **`vault`** (`string`) - Providers vault account.
- **`initCursor`** (`string`) - Cursor to start listening payments from.
- **`assets`** (`Array<ProviderAsset>`) - Provider's assets settings.
- **`paymentSubmitDelay`** (`Int32`) - Payment submit delay in seconds. How long to wait before notify alpha about payment.


**ProviderAsset** structure:

- **`token`** (`string`) - Token code. Could be null for native assets.
- **`centaurusAsset`** (`string`) - Centaurus asset code to map to.
- **`isVirtual`** (`Boolean`) - Is non native asset.


**Auditor** structure:

- **`pubKey`** (`Array<byte>`) - Auditor public ed25519 key.
- **`address`** (`string`) - Domain address. Could be null for auditors with participation level `Auditor`


**AssetSettings** structure:

- **`code`** (`string`) - Centaurus asset code.
- **`isSuspended`** (`Boolean`) - Is asset suspended for any operations (except deposit).
- **`isQuoteAsset`** (`Boolean`) - Is current asset is a quote asset.


**RequestRateLimits** structure:

- **`minuteLimit`** (`UInt32`) - Maximum requests for a client in a minute.
- **`hourLimit`** (`UInt32`) - Maximum requests for a client in an hour.

#### Account effects

All effects that affect account inherit **AccountEffect**. Structure:

- **`account`** (`UInt64`) - Account id.


**AccountCreateEffect** structure:

- **`pubkey`** (`Array<byte>`) - Accounts public key.


**NonceUpdateEffect** structure:

- **`nonce`** (`UInt64`) - New account nonce.
- **`prevNonce`** (`UInt64`) - Previous account nonce.

**BalanceCreateEffect** structure:

- **`asset`** (`string`) - Centaurus asset code.

**BalanceUpdateEffect** structure:

- **`asset`** (`string`) - Centaurus asset code.
- **`sign`** (`string`) - Update direction. `Plus` = `0` and `Minus` = `1`.
- **`amount`** (`UInt64`) - Update amount.


Order place/removed effects inherit **BaseOrderEffect**:

- **`price`** (`Float64`) - Asset price.
- **`asset`** (`string`) - Centaurus asset code.
- **`side`** (`Int32`) - Order side. `Sell = 1` and `Buy = 2`
- **`amount`** (`UInt64`) - Order amount.
- **`quoteAmount`** (`UInt64`) - Quote asset order amount.


**OrderPlacedEffect** doesn't have any additional fields.

**OrderRemovedEffect** structure:

- **`orderId`** (`UInt64`) - Order id to remove.

**TradeEffect** structure:

- **`orderId`** (`UInt64`) - Order id that was crossed.
- **`asset`** (`string`) - Centaurus asset code.
- **`side`** (`Int32`) - Order side. `Sell = 1` and `Buy = 2`
- **`assetAmount`** (`UInt64`) - Order asset amount.
- **`quoteAmount`** (`UInt64`) - Quote asset order amount.
- **`isNewOrder`** (`Boolean`) - Is it new order or not.