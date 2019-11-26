using Centaurus.DAL.Mongo;
using Centaurus.Domain;
using Centaurus.Models;
using NUnit.Framework;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Centaurus.Test
{
    public class OrderbookTests
    {
        byte[] testPubKey = new byte[32];

        [SetUp]
        public void Setup()
        {
            var settings = new AlphaSettings { 
                HorizonUrl = "https://horizon-testnet.stellar.org", 
                NetworkPassphrase = "Test SDF Network ; September 2015",
                CWD = "AppData"
            };
            Global.Init(settings, new MockStorage());

            var account = new Models.Account()
            {
                Pubkey = new RawPubKey() { Data = testPubKey },
                Balances = new List<Balance>()
            };

            account.Balances.Add(new Balance() { Asset = 0, Amount = 10000000000, Account = account });
            account.Balances.Add(new Balance() { Asset = 1, Amount = 10000000000, Account = account });
            Global.Setup(new Snapshot
            {
                Accounts = new List<Models.Account> { account },
                Apex = 0,
                Ledger = 1,
                Orders = new List<Order>(),
                VaultSequence = 1,
                Settings = new ConstellationSettings
                {
                    Vault = KeyPair.Random().PublicKey,
                    Assets = new List<AssetSettings> { new AssetSettings { Id = 1, Code = "X", Issuer = new RawPubKey() } }
                }
            });
        }

        [TearDown]
        public void TearDown()
        {
            Global.Exchange.Clear();
            //Global.AccountStorage.Clear();
        }

        [Test]
        public void SimpleMatchingTest()
        {
            var pubkey = new RawPubKey() { Data = testPubKey };
            var order = new RequestQuantum
            {
                Apex = 1,
                RequestEnvelope = new MessageEnvelope
                {
                    Message = new OrderRequest
                    {
                        Account = pubkey,
                        Nonce = 1,
                        Amount = 10,
                        Asset = 1,
                        Price = 2,
                        Side = OrderSides.Sell
                    }
                }
            };

            var orderEffectsContainer = new EffectProcessorsContainer(new PendingUpdates(), order.CreateEnvelope());

            var orderCreatedResult = Global.Exchange.ExecuteOrder(order, orderEffectsContainer);
            Assert.AreEqual(orderCreatedResult.Count, 1);
            Assert.AreEqual(orderCreatedResult[0].EffectType, EffectTypes.OrderPlaced);

            var conterOrder = new RequestQuantum
            {
                Apex = 2,
                RequestEnvelope = new MessageEnvelope
                {
                    Message = new OrderRequest
                    {
                        Account = pubkey,
                        Nonce = 2,
                        Amount = 4,
                        Asset = 1,
                        Price = 2.5,
                        Side = OrderSides.Buy
                    }
                }
            };

            var conterOrderEffectsContainer = new EffectProcessorsContainer(new PendingUpdates(), conterOrder.CreateEnvelope());

            var tradeResult = Global.Exchange.ExecuteOrder(conterOrder, conterOrderEffectsContainer);
            Assert.AreEqual(tradeResult.Count, 2);
            Assert.AreEqual(tradeResult[0].EffectType, EffectTypes.Trade);
            Assert.AreEqual(tradeResult[1].EffectType, EffectTypes.Trade);
        }

        [Test]
        [Explicit]
        [Category("Performance")]
        [TestCase(1000000)]
        [TestCase(10000000, true)]
        public void OrderbookPerformanceTest(int iterations, bool useNormalDistribution = false)
        {
            var rnd = new Random();
            var pubkey = new RawPubKey() { Data = testPubKey };

            var testTradeReuests = new Dictionary<RequestQuantum, EffectProcessorsContainer>();
            for (var i = 1; i < iterations; i++)
            {
                var price = useNormalDistribution ? rnd.NextNormallyDistributed() + 50 : rnd.NextDouble() * 100;
                var trade = new RequestQuantum
                {
                    Apex = (ulong)i,
                    RequestEnvelope = new MessageEnvelope
                    {
                        Message = new OrderRequest
                        {
                            Account = pubkey,
                            Nonce = (ulong)i,
                            Amount = rnd.Next(1, 20),
                            Asset = 1,
                            Price = Math.Round(price * 10) / 10,
                            Side = rnd.NextDouble() >= 0.5 ? OrderSides.Buy : OrderSides.Sell
                        }
                    }
                };

                var conterOrderEffectsContainer = new EffectProcessorsContainer(new PendingUpdates(), trade.CreateEnvelope());
                testTradeReuests.Add(trade, conterOrderEffectsContainer);
            }

            PerfCounter.MeasureTime(() =>
            {
                foreach (var trade in testTradeReuests)
                    Global.Exchange.ExecuteOrder(trade.Key, trade.Value);
            }, () =>
            {
                var market = Global.Exchange.GetMarket(1);
                return $"{iterations} iterations, orderbook size: {market.Bids.Count} bids,  {market.Asks.Count} asks, {market.Bids.GetBestPrice().ToString("G3")}/{market.Asks.GetBestPrice().ToString("G3")} spread.";
            });
        }
    }
}
