using Centaurus.PaymentProvider.Models;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Centaurus.Stellar.PaymentProvider.Tests
{
    public class StellarPaymentProviderTests
    {
        [Test]
        public void InitTest()
        {
            Debugger.Launch();
            var provider = new StellarPaymentProvider(new SettingsModel
            {
                Cursor = "6180462233923584",
                Name = "TestNet",
                Provider = "Stellar",
                PaymentSubmitDelay = 20,
                Vault = "GCNCAGVEMEKJKT7TDFI4S6IBQOFOF57SZE2C765NO5WCGT3ZNRDELOAX",
                Assets = new List<AssetModel>
                {
                    new AssetModel
                    {
                        CentaurusAsset = "XLM",
                        Token = "native"
                    },
                    new AssetModel
                    {
                        CentaurusAsset = "ast0",
                        Token = "ast0:GCPUAY2Y6KLU7O2AQJVRE36OQ3IKI6OAJYJQGRRP7WZHKM6AKASFCY5U",
                        IsVirtual = true
                    },
                    new AssetModel
                    {
                        CentaurusAsset = "ast1",
                        Token = "ast1:GCPUAY2Y6KLU7O2AQJVRE36OQ3IKI6OAJYJQGRRP7WZHKM6AKASFCY5U",
                        IsVirtual = true
                    },
                    new AssetModel
                    {
                        CentaurusAsset = "ast2",
                        Token = "ast2:GCPUAY2Y6KLU7O2AQJVRE36OQ3IKI6OAJYJQGRRP7WZHKM6AKASFCY5U",
                        IsVirtual = true
                    },
                    new AssetModel
                    {
                        CentaurusAsset = "ast3",
                        Token = "ast3:GCPUAY2Y6KLU7O2AQJVRE36OQ3IKI6OAJYJQGRRP7WZHKM6AKASFCY5U",
                        IsVirtual = true
                    },
                    new AssetModel
                    {
                        CentaurusAsset = "ast4",
                        Token = "ast4:GCPUAY2Y6KLU7O2AQJVRE36OQ3IKI6OAJYJQGRRP7WZHKM6AKASFCY5U",
                        IsVirtual = true
                    },
                    new AssetModel
                    {
                        CentaurusAsset = "ast5",
                        Token = "ast5:GCPUAY2Y6KLU7O2AQJVRE36OQ3IKI6OAJYJQGRRP7WZHKM6AKASFCY5U",
                        IsVirtual = true
                    },
                    new AssetModel
                    {
                        CentaurusAsset = "ast6",
                        Token = "ast6:GCPUAY2Y6KLU7O2AQJVRE36OQ3IKI6OAJYJQGRRP7WZHKM6AKASFCY5U",
                        IsVirtual = true
                    },
                    new AssetModel
                    {
                        CentaurusAsset = "ast7",
                        Token = "ast7:GCPUAY2Y6KLU7O2AQJVRE36OQ3IKI6OAJYJQGRRP7WZHKM6AKASFCY5U",
                        IsVirtual = true
                    },
                    new AssetModel
                    {
                        CentaurusAsset = "ast8",
                        Token = "ast8:GCPUAY2Y6KLU7O2AQJVRE36OQ3IKI6OAJYJQGRRP7WZHKM6AKASFCY5U",
                        IsVirtual = true
                    },
                    new AssetModel
                    {
                        CentaurusAsset = "ast9",
                        Token = "ast9:GCPUAY2Y6KLU7O2AQJVRE36OQ3IKI6OAJYJQGRRP7WZHKM6AKASFCY5U",
                        IsVirtual = true
                    }
                }
            }, JsonSerializer.Serialize(new
            {
                Horizon = "https://horizon-testnet.stellar.org",
                Secret = "SAP3AXLCYPIR4I4HJCIVO67Q52ITPI5O5FSEDZFNJNFZRMYQRCRFFZBI",
                PassPhrase = "Test SDF Network ; September 2015"
            }));
        }
    }
}
