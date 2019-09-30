# Administration

## Constellation bootstrap 

**1. Configure Centaurus.Alpha**

`app.settings` example:

```
{
  "IsAlpha": true,
  "Auditors": [ "GC2Z2GVUXJFGU3SIC6S2RVVH35TUSNL7ZQPZXGTHEBFFNMMEGPIBTLTT" ], //should contain at least one auditor
  "AppUrls": [ "http://*:5000", "https://*:5001" ], //addresses to bind to
  "StellarNetwork": {
    "Horizon": "https://horizon-testnet.stellar.org",
    "Passphrase": "Test SDF Network ; September 2015"
  },
  "Vault": "GB2C7ZJZDDOCAA75E5JZVC343WCWJUIJZCXQXW4T7IKR4TYLUA6FZT6Z", //valid and active Stellar account address
  "SupportedAssets": { //an asset key (except native, it should be just "XLM") //symbol in format {assetCode}:{assetIssuer} and value should be an integer
    "XLM": 0,
    "USD:GAWJ5J3GRGHDRTQSJSUNLIBBRB47YMUWTS7TCNGIUWMJUML22Q2BJYFJ": 1,
    "TEST1:GA7JKJ5AKYBA3KW4EYULFNUJUBSXT2VUXAPSTTSRQDRRXSGTM5ZL66A7": 2
  },
  "MinAccountBalance": 100,
  "SnapshotsDirectory": "/snapshots", //should be a valid path to snapshot directory
  "GenesisLedger": 1457416 //ledger sequence from which we will start listening to Stellar network updates
}
```

**2. Start Centaurus.Alpha**

**3. Configure Centaurus.Auditor**

`app.settings` example:

```
{
  "Secret": "SCT2UXYJUV5R2OVP5SPTYFSDZZY24EADL3XGABWLYQBGYZMMIUA5SHDA", //current auditor secret. Public key should be in Auditors array
  "AlphaAddress": "ws://localhost:5000/ws", //Alpha server url
  "Vault": "GB2C7ZJZDDOCAA75E5JZVC343WCWJUIJZCXQXW4T7IKR4TYLUA6FZT6Z", 
  "StellarNetwork": {
    "Horizon": "https://horizon-testnet.stellar.org",
    "Passphrase": "Test SDF Network ; September 2015"
  },
  "Auditors": [ "GC2Z2GVUXJFGU3SIC6S2RVVH35TUSNL7ZQPZXGTHEBFFNMMEGPIBTLTT" ], 
  "SnapshotsDirectory": "//auditor-snapshots", 
  "GenesisLedger": 1457416, 
  "SupportedAssets": { 
    "XLM": 0,
    "USD:GAWJ5J3GRGHDRTQSJSUNLIBBRB47YMUWTS7TCNGIUWMJUML22Q2BJYFJ": 1,
    "TEST1:GA7JKJ5AKYBA3KW4EYULFNUJUBSXT2VUXAPSTTSRQDRRXSGTM5ZL66A7": 2
  }
}
```

**4. Start Centaurus.Auditor**

Once auditors majority is connected to Alpha, the constellation is ready to process requests.

**5. Configure Centaurus.Test.Client**

`app.settings` example:

```
{
  "Secret": "SCR6C6STGV7RKURFGV7XA76WRUZYAKRYFICLWBHTI7NAW3VXVRA5T75E", //current secret
  "AlphaAddress": "ws://localhost:5000/ws" //alpha server url
}
```

**6. Start Centaurus.Test.Client**

For now it only supports place order operations. Details will be displayed in console after the start.
