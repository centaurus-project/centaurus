using Centaurus.Domain;
using Centaurus.Models;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using stellar_dotnet_sdk;
using System.Linq;
using System.Runtime;
using Centaurus.DAL.Mongo;

namespace Centaurus.Test
{
    [TestFixture]
    public class SnapshotPerformanceTests
    {

        [SetUp]
        public void Setup()
        {
            EnvironmentHelper.SetTestEnvironmentVariable();
            var settings = new AlphaSettings 
            { 
                CWD = "AppData"
            };
            Global.Init(settings, new MongoStorage());
        }

        [TearDown]
        public void TearDown()
        {
            Global.Exchange.Clear();
            //AccountStorage.Clear();
        }

        //[Test]
        //[Explicit]
        //[Category("Performance")]
        //[TestCase(10, 10000, 4, 50)]
        //[TestCase(100, 1000, 50, 10)]
        //[TestCase(100, 10, 50, 20)]
        //public void SnapshotPerformanceTest(int iterations, int totalAccounts, int totalMarkets, int totalOrdersPerAccountPerMarket)
        //{

        //    Global.Setup(new Snapshot
        //    {
        //        Accounts = new List<Models.Account>(),
        //        Apex = 0,
        //        Ledger = 1,
        //        Orders = new List<Order>(),
        //        VaultSequence = 1,
        //        Withdrawals = new List<RequestQuantum>(),
        //        Settings = new ConstellationSettings()
        //        {
        //            Auditors = Enumerable.Repeat(0, 11).Select(_ => (RawPubKey)KeyPair.Random()).ToList(),
        //            MinAccountBalance = 1_000_000_000L,
        //            MinAllowedLotSize = 100L,
        //            Assets = Enumerable.Range(1, totalMarkets).Select(i => new AssetSettings { Id = i, Code = i.ToString().PadLeft(4), Issuer = new RawPubKey() }).ToList(),
        //            Vault = KeyPair.Random()
        //        }
        //    });

        //    var accs = new List<Models.Account>();
        //    var rnd = new Random();
        //    ulong orderCounter = 1;
        //    for (int i = 0; i < totalAccounts; i++)
        //    {
        //        var pk = new byte[32];
        //        rnd.NextBytes(pk);
        //        var balances = new List<Balance>();
        //        for (var m = 1; m <= totalMarkets; m++)
        //        {
        //            balances.Add(new Balance { Amount = (long)rnd.Next() * 10_000_000, Asset = m });
        //            var market = Global.Exchange.GetMarket(m);
        //            for (var o = 0; o < totalOrdersPerAccountPerMarket; o++)
        //            {
        //                market.Asks.InsertOrder(new Order
        //                {
        //                    Amount = rnd.Next(),
        //                    OrderId = ++orderCounter,
        //                    Price = rnd.NextDouble() * 100,
        //                    Pubkey = pk
        //                });
        //            }
        //        }
        //        var acc = Global.AccountStorage.CreateAccount(new RawPubKey() { Data = pk });
        //    }

        //    Global.LedgerManager.SetLedger(1000);
        //    PerfCounter.MeasureTime(() =>
        //    {
        //        for (var i = 0; i < iterations; i++)
        //        {
        //            var snapshot = Global.SnapshotManager.InitSnapshot();
        //            snapshot.Confirmation = new MessageEnvelope()
        //            {
        //                Message = new ResultMessage()
        //                {
        //                    Effects = new List<Effect>(),
        //                    Status = ResultStatusCodes.Success,
        //                    OriginalMessage = new MessageEnvelope()
        //                    {
        //                        Message = new SnapshotQuantum
        //                        {
        //                            Apex = 1,
        //                            Hash = 32.RandomBytes(),
        //                            IsProcessed = true,
        //                            Timestamp = 12123
        //                        },
        //                        Signatures = new List<Ed25519Signature>
        //                    {
        //                        FakeModelsFactory.RandomSignature()
        //                    }
        //                    }
        //                },
        //                Signatures = new List<Ed25519Signature>{
        //                FakeModelsFactory.RandomSignature(),
        //                FakeModelsFactory.RandomSignature(),
        //                FakeModelsFactory.RandomSignature(),
        //            }
        //            };
        //            snapshot.ComputeHash();
        //            Global.SnapshotManager.AbortPendingSnapshot();
        //        }
        //    }, () => $"snapshot.InitSnapshot() + snapshot.ComputeHash() ({iterations} iterations, {totalAccounts} totalAccounts, {totalMarkets} totalMarkets, {totalOrdersPerAccountPerMarket} totalOrdersPerAccount)");
        //}
    }
}
