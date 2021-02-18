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

        private void ExecuteWithOrderbook(int iterations, bool useNormalDistribution, Action<Action> executor)
        {
            var rnd = new Random();

            var a1 = Global.AccountStorage.GetAccount(account1.Pubkey);
            var a2 = Global.AccountStorage.GetAccount(account2.Pubkey);

            var market = Global.Exchange.GetMarket(1);

            var testTradeResults = new Dictionary<RequestQuantum, EffectProcessorsContainer>();
            for (var i = 1; i < iterations; i++)
            {
                var price = useNormalDistribution ? rnd.NextNormallyDistributed() + 50 : rnd.NextDouble() * 100;
                var request = new OrderRequest
                {
                    Nonce = i,
                    Amount = rnd.Next(1, 20),
                    Asset = 1,
                    Price = Math.Round(price * 27) / 13
                };
                if (rnd.NextDouble() >= 0.5)
                {
                    request.Account = a1.Id;
                    request.AccountWrapper = a1;
                    request.Side = OrderSide.Buy;
                }
                else
                {
                    request.Account = a2.Id;
                    request.AccountWrapper = a2;
                    request.Side = OrderSide.Sell;
                }

                var trade = new RequestQuantum
                {
                    Apex = i,
                    RequestEnvelope = new MessageEnvelope
                    {
                        Message = request,
                        Signatures = new List<Ed25519Signature>()
                    },
                    Timestamp = DateTime.UtcNow.Ticks
                };
                var diffObject = new DiffObject();
                var conterOrderEffectsContainer = new EffectProcessorsContainer(trade.CreateEnvelope(), diffObject);
                testTradeResults.Add(trade, conterOrderEffectsContainer);
            }

            var xlmStartBalance = account1.GetBalance(0).Amount + account2.GetBalance(0).Amount;
            var assetStartBalance = account1.GetBalance(1).Amount + account2.GetBalance(1).Amount;

            executor(() =>
            {
                foreach (var trade in testTradeResults)
                {
                    Global.Exchange.ExecuteOrder(trade.Value);
                }
            });

            //cleanup orders
            foreach (var account in new[] { account1, account2 })
            {
                var activeOrders = Global.Exchange.OrderMap.GetAllAccountOrders(account.Id);
                foreach (var order in activeOrders)
                {
                    var orderInfo = order.ToOrderInfo();
                    //unlock order reserve
                    if (orderInfo.Side == OrderSide.Buy)
                    {
                        new UpdateLiabilitiesEffectProcessor(new UpdateLiabilitiesEffect
                        {
                            Amount = -order.QuoteAmount,
                            Asset = 0,
                            Account = account.Id
                        }, account).CommitEffect();
                    }
                    else
                    {
                        new UpdateLiabilitiesEffectProcessor(new UpdateLiabilitiesEffect
                        {
                            Amount = -order.Amount,
                            Asset = orderInfo.Market,
                            Account = account.Id
                        }, account).CommitEffect();
                    }
                    order.Amount = 0;
                    order.QuoteAmount = 0;

                    new OrderRemovedEffectProccessor(new OrderRemovedEffect { OrderId = order.OrderId, Amount = order.Amount, QuoteAmount = order.QuoteAmount, Price = order.Price, Asset = orderInfo.Market, Account = account.Id }, market.GetOrderbook(orderInfo.Side), account).CommitEffect();
                }
            }
            Assert.AreEqual(xlmStartBalance, account1.GetBalance(0).Amount + account2.GetBalance(0).Amount);
            Assert.AreEqual(assetStartBalance, account1.GetBalance(1).Amount + account2.GetBalance(1).Amount);
            Assert.AreEqual(0, account1.GetBalance(0).Liabilities);
            Assert.AreEqual(0, account1.GetBalance(1).Liabilities);
            Assert.AreEqual(0, account2.GetBalance(0).Liabilities);
            Assert.AreEqual(0, account2.GetBalance(1).Liabilities);
        }

        [Test]
        public void MatchingTest()
        {
            ExecuteWithOrderbook(10, false, executeOrders => executeOrders());
        }

        [Test]
        [Explicit]
        [Category("Performance")]
        [TestCase(10000)]
        [TestCase(100000, true)]
        public void OrderbookPerformanceTest(int iterations, bool useNormalDistribution = false)
        {
            ExecuteWithOrderbook(iterations, useNormalDistribution, executeOrders => PerfCounter.MeasureTime(executeOrders, () =>
            {
                var market = Global.Exchange.GetMarket(1);
                return $"{iterations} iterations, orderbook size: {market.Bids.Count} bids,  {market.Asks.Count} asks, {market.Bids.GetBestPrice().ToString("G3")}/{market.Asks.GetBestPrice().ToString("G3")} spread.";
            }));
        }
    }
}
