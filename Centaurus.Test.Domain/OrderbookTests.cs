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
        Models.Account account1;
        Models.Account account2;

        [SetUp]
        public void Setup()
        {
            EnvironmentHelper.SetTestEnvironmentVariable();
            var settings = new AlphaSettings { 
                HorizonUrl = "https://horizon-testnet.stellar.org", 
                NetworkPassphrase = "Test SDF Network ; September 2015",
                CWD = "AppData"
            };
            Global.Init(settings, new MockStorage());

            account1 = new Models.Account()
            {
                Pubkey = new RawPubKey() { Data = KeyPair.Random().PublicKey },
                Balances = new List<Balance>()
            };

            account1.Balances.Add(new Balance() { Asset = 0, Amount = 10000000000, Account = account1 });
            account1.Balances.Add(new Balance() { Asset = 1, Amount = 10000000000, Account = account1 });

            account2 = new Models.Account()
            {
                Pubkey = new RawPubKey() { Data = KeyPair.Random().PublicKey },
                Balances = new List<Balance>()
            };

            account2.Balances.Add(new Balance() { Asset = 0, Amount = 10000000000, Account = account2 });
            account2.Balances.Add(new Balance() { Asset = 1, Amount = 10000000000, Account = account2 });
            Global.Setup(new Snapshot
            {
                Accounts = new List<Models.Account> { account1, account2 },
                Apex = 0,
                Ledger = 1,
                Orders = new List<Order>(),
                VaultSequence = 1,
                Settings = new ConstellationSettings
                {
                    Vault = KeyPair.Random().PublicKey,
                    Assets = new List<AssetSettings> { new AssetSettings { Id = 1, Code = "X", Issuer = new RawPubKey() } },
                    RequestRateLimits = new RequestRateLimits { HourLimit = 1000, MinuteLimit = 100 }
                },
            });
        }

        [TearDown]
        public void TearDown()
        {
            Global.Exchange.Clear();
            //Global.AccountStorage.Clear();
        }

        [Test]
        [TestCase(OrderSides.Buy)]
        [TestCase(OrderSides.Sell)]
        public void SimpleMatchingTest(OrderSides side)
        {
            var acc1XlmLiabilities = account1.Balances[0].Liabilities;
            var acc1XlmAmount = account1.Balances[0].Amount;
            var acc1AssetLiabilities = account1.Balances[1].Liabilities;
            var acc1AssetAmount = account1.Balances[1].Amount;

            var acc2XlmLiabilities = account2.Balances[0].Liabilities;
            var acc2XlmAmount = account2.Balances[0].Amount;
            var acc2AssetLiabilities = account2.Balances[1].Liabilities;
            var acc2AssetAmount = account2.Balances[1].Amount;

            var orderRequest1 = new OrderRequest
            {
                Account = account1.Pubkey,
                Nonce = 1,
                Amount = 10,
                Asset = 1,
                Price = 2.5,
                Side = side,
                AccountWrapper = Global.AccountStorage.GetAccount(account1.Pubkey)
            };

            var order = new RequestQuantum
            {
                Apex = 1,
                RequestEnvelope = new MessageEnvelope
                {
                    Message = orderRequest1
                }
            };

            var orderEffectsContainer = new EffectProcessorsContainer(order.CreateEnvelope());
            Global.Exchange.ExecuteOrder(orderEffectsContainer);
            var effects = orderEffectsContainer.GetEffects();
            Assert.AreEqual(effects.Length, 2);
            Assert.AreEqual(effects[0].EffectType, EffectTypes.LockLiabilities);
            Assert.AreEqual(effects[1].EffectType, EffectTypes.OrderPlaced);
            if (side == OrderSides.Sell)
            {
                Assert.AreEqual(account1.Balances[1].Liabilities, acc1AssetLiabilities + orderRequest1.Amount);
                Assert.AreEqual(account1.Balances[1].Amount, acc1AssetAmount);
            }
            else
            {
                Assert.AreEqual(account1.Balances[0].Liabilities, acc1XlmLiabilities + orderRequest1.Amount * orderRequest1.Price);
                Assert.AreEqual(account1.Balances[0].Amount, acc1XlmAmount);
            }

            var orderRequest2 = new OrderRequest
            {
                Account = account2.Pubkey,
                Nonce = 2,
                Amount = 20,
                Asset = 1,
                Price = 2,
                Side = side.Inverse(),
                AccountWrapper = Global.AccountStorage.GetAccount(account2.Pubkey)
            };

            var conterOrder = new RequestQuantum
            {
                Apex = 2,
                RequestEnvelope = new MessageEnvelope
                {
                    Message = orderRequest2
                }
            };

            var conterOrderEffectsContainer = new EffectProcessorsContainer(conterOrder.CreateEnvelope());
            Global.Exchange.ExecuteOrder(conterOrderEffectsContainer);
            if (orderRequest2.Side == OrderSides.Sell)
            {
                Assert.AreEqual(account2.Balances[1].Liabilities, acc2AssetLiabilities + (orderRequest2.Amount - orderRequest1.Amount));
                Assert.AreEqual(account2.Balances[1].Amount, acc2AssetAmount - orderRequest1.Amount);
            }
            else
            {
                Assert.AreEqual(account2.Balances[0].Liabilities, acc2XlmLiabilities + orderRequest2.Amount * orderRequest2.Price);
                Assert.AreEqual(account2.Balances[0].Amount, acc2XlmAmount);
            }
        }

        [Test]
        [Explicit]
        [Category("Performance")]
        [TestCase(100000)]
        [TestCase(1000000, true)]
        public void OrderbookPerformanceTest(int iterations, bool useNormalDistribution = false)
        {
            var rnd = new Random();

            var testTradeResults = new Dictionary<RequestQuantum, EffectProcessorsContainer>();
            for (var i = 1; i < iterations; i++)
            {
                var price = useNormalDistribution ? rnd.NextNormallyDistributed() + 50 : rnd.NextDouble() * 100;
                var trade = new RequestQuantum
                {
                    Apex = i,
                    RequestEnvelope = new MessageEnvelope
                    {
                        Message = new OrderRequest
                        {
                            Account = account1.Pubkey,
                            Nonce = (ulong)i,
                            Amount = rnd.Next(1, 20),
                            Asset = 1,
                            Price = Math.Round(price * 10) / 10,
                            Side = rnd.NextDouble() >= 0.5 ? OrderSides.Buy : OrderSides.Sell,
                            AccountWrapper = Global.AccountStorage.GetAccount(account1.Pubkey)
                        }
                    }
                };

                var conterOrderEffectsContainer = new EffectProcessorsContainer(trade.CreateEnvelope());
                testTradeResults.Add(trade, conterOrderEffectsContainer);
            }

            PerfCounter.MeasureTime(() =>
            {
                foreach (var trade in testTradeResults)
                    Global.Exchange.ExecuteOrder(trade.Value);
            }, () =>
            {
                var market = Global.Exchange.GetMarket(1);
                return $"{iterations} iterations, orderbook size: {market.Bids.Count} bids,  {market.Asks.Count} asks, {market.Bids.GetBestPrice().ToString("G3")}/{market.Asks.GetBestPrice().ToString("G3")} spread.";
            });
        }
    }
}
