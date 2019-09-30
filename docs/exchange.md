# Exchange

### Orderbook

An orderbook is a record of outstanding orders between XLM and a given asset. 
There are a few major differences between Centaurus exchange and Stellar DEX:

- Only `{ASSET}/XLM` trading pairs allowed to provide maximum liquidity and simplify path payments.
- All asset prices  are designated in XLM. 
- Tick size (the smallest unit of price change) depends on the market, by default 0.001XLM.
- There are a limited number of active markets. 
Constellation members decide which asset to list with the majority of votes.

### Orders

Orders are the requests to buy/sell tokens for other tokens on Centaurus exchange. 
The exchange supports `BID` (buy the asset for XLM) and `ASK` (sell the asset for XLM) orders of 
the following types:

- Good Till Canceled (`GTC`, default) – an order would stay effective until it is filled in full 
or the user cancels it.
- Immediate or Cancel (`IOC`) – orders would be matched against existing orderbook orders without 
creating a new order on the orderbook.

Each order contains the following fields:

- **`symbol`** (`String`) – the symbol of the asset the user wants to trade.
- **`side`** (`Enum`) – buy or sell.
- **`amount`** (`Int64`) – the number of tokens a user wants to buy or sell (multiplied 
by 10,000,000 to match Stellar standard 7-digit precision convention). The minimum amount eligible 
for trade equals 0.1XLM.
- **`price`** (`Double`) – the price user would like to pay in terms of quote asset (XLM), 
presented as a 32-bit floating-point number. The price is rounded by tick size. 
- **`timeInForce`** (`Enum`) – currently supported "Good Till Expire" and "Immediate or Cancel".
- **`orderId`** (`Int64`) – the identifier of the order to modify. 0 for new orders.

### Matching logic

- When an account makes an exchange proposal, the order is checked against the existing orderbook 
for that asset pair.
- If the order crosses an existing order, it is filled at the price of the existing order. 
The match only happens when the bid and ask prices are "crossed," i.e., bid price >= ask price.
- If the order has not been executed entirely and more crossing orders exist, they are executed 
one by one. 
The next order with a better price is filled first. For orderbook orders placed at the same price, 
earlier timestamped orders receive fills before later timestamped orders (FIFO method).
- Orders can be filled partially.
- If the trade order doesn't cross an existing order, a new order is created on the orderbook 
until it is either taken by another order or canceled by the account that created it. 
Order id of the newly created order is always equal to the quantum apex. 
In the case of IOC orders (Immediate or Cancel), new order creation is skipped.
- Each order contributes locked liabilities to the balance of the account creating the order. 
This guarantees that any order in the orderbook can be executed entirely.
- The match occurs when the engine starts new order quantum processing.
 All orders are processed strictly sequentially, according to the FIFO principle.
- Orders would be rejected when the account does not possess enough token to buy or sell 
(excluding its current liabilities), the asset symbol orderbook not found, or orderId is invalid.