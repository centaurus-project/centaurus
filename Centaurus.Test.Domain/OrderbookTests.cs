using Centaurus.DAL;
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
            var settings = new AlphaSettings
            {
                HorizonUrl = "https://horizon-testnet.stellar.org",
                NetworkPassphrase = "Test SDF Network ; September 2015",
                CWD = "AppData"
            };
            Global.Setup(settings, new MockStorage()).Wait();

            account1 = new Models.Account()
            {
                Id = 1,
                Pubkey = new RawPubKey() { Data = KeyPair.Random().PublicKey },
                Balances = new List<Balance>()
            };

            account1.CreateBalance(0);
            account1.GetBalance(0).UpdateBalance(10000000000);
            account1.CreateBalance(1);
            account1.GetBalance(1).UpdateBalance(10000000000);

            account2 = new Models.Account()
            {
                Id = 2,
                Pubkey = new RawPubKey() { Data = KeyPair.Random().PublicKey },
                Balances = new List<Balance>()
            };

            account2.CreateBalance(0);
            account2.GetBalance(0).UpdateBalance(10000000000);
            account2.CreateBalance(1);
            account2.GetBalance(1).UpdateBalance(10000000000);

            Global.Setup(new Snapshot
            {
                Accounts = new List<Models.Account> { account1, account2 },
                Apex = 0,
                TxCursor = 1,
                Orders = new List<Order>(),
                Settings = new ConstellationSettings
                {
                    Vault = KeyPair.Random().PublicKey,
                    Assets = new List<AssetSettings> { new AssetSettings { Id = 1, Code = "X", Issuer = new RawPubKey() } },
                    RequestRateLimits = new RequestRateLimits { HourLimit = 1000, MinuteLimit = 100 }
                },
            }).Wait();
        }

        [TearDown]
        public void TearDown()
        {
            Global.Exchange.Clear();
            //Global.AccountStorage.Clear();
        }

        [Test]
        [TestCase(OrderSide.Buy)]
        [TestCase(OrderSide.Sell)]
        public void SimpleMatchingTest(OrderSide side)
        {
            var acc1XlmLiabilities = account1.Balances[0].Liabilities;
            var acc1XlmAmount = account1.Balances[0].Amount;
            var acc1AssetLiabilities = account1.Balances[1].Liabilities;
            var acc1AssetAmount = account1.Balances[1].Amount;

            var acc2XlmLiabilities = account2.Balances[0].Liabilities;
            var acc2XlmAmount = account2.Balances[0].Amount;
            var acc2AssetLiabilities = account2.Balances[1].Liabilities;
            var acc2AssetAmount = account2.Balances[1].Amount;

            var account = Global.AccountStorage.GetAccount(account1.Pubkey);

            var orderRequest1 = new OrderRequest
            {
                Account = account.Account.Id,
                Nonce = 1,
                Amount = 10,
                Asset = 1,
                Price = 2.5,
                Side = side,
                AccountWrapper = account
            };

            var order = new RequestQuantum
            {
                Apex = 1,
                RequestEnvelope = new MessageEnvelope
                {
                    Message = orderRequest1,
                    Signatures = new List<Ed25519Signature>()
                },
                Timestamp = DateTime.UtcNow.Ticks
            };

            var diffObject = new DiffObject();

            var orderEffectsContainer = new EffectProcessorsContainer(order.CreateEnvelope(), diffObject);
            Global.Exchange.ExecuteOrder(orderEffectsContainer);
            var effects = orderEffectsContainer.Effects;
            Assert.AreEqual(effects.Count, 2);
            Assert.AreEqual(effects[0].EffectType, EffectTypes.UpdateLiabilities);
            Assert.AreEqual(effects[1].EffectType, EffectTypes.OrderPlaced);
            if (side == OrderSide.Sell)
            {
                Assert.AreEqual(account1.Balances[1].Liabilities, acc1AssetLiabilities + orderRequest1.Amount);
                Assert.AreEqual(account1.Balances[1].Amount, acc1AssetAmount);
            }
            else
            {
                Assert.AreEqual(account1.Balances[0].Liabilities, acc1XlmLiabilities + orderRequest1.Amount * orderRequest1.Price);
                Assert.AreEqual(account1.Balances[0].Amount, acc1XlmAmount);
            }

            account = Global.AccountStorage.GetAccount(account2.Pubkey);

            var orderRequest2 = new OrderRequest
            {
                Account = account.Account.Id,
                Nonce = 2,
                Amount = 20,
                Asset = 1,
                Price = 2,
                Side = side.Inverse(),
                AccountWrapper = account
            };

            var conterOrder = new RequestQuantum
            {
                Apex = 2,
                RequestEnvelope = new MessageEnvelope
                {
                    Message = orderRequest2,
                    Signatures = new List<Ed25519Signature>()
                },
                Timestamp = DateTime.UtcNow.Ticks
            };

            var conterOrderEffectsContainer = new EffectProcessorsContainer(conterOrder.CreateEnvelope(), diffObject);
            Global.Exchange.ExecuteOrder(conterOrderEffectsContainer);
            if (orderRequest2.Side == OrderSide.Sell)
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
            var account = Global.AccountStorage.GetAccount(account2.Pubkey);

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
                            Account = account.Account.Id,
                            Nonce = i,
                            Amount = rnd.Next(1, 20),
                            Asset = 1,
                            Price = Math.Round(price * 10) / 10,
                            Side = rnd.NextDouble() >= 0.5 ? OrderSide.Buy : OrderSide.Sell,
                            AccountWrapper = account
                        },
                        Signatures = new List<Ed25519Signature>()
                    },
                    Timestamp = DateTime.UtcNow.Ticks
                };
                var diffObject = new DiffObject();
                var conterOrderEffectsContainer = new EffectProcessorsContainer(trade.CreateEnvelope(), diffObject);
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
