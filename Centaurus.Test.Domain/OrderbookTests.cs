using Centaurus.DAL;
using Centaurus.DAL.Mongo;
using Centaurus.Domain;
using Centaurus.Models;
using Centaurus.Xdr;
using NUnit.Framework;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Centaurus.Test
{
    [TestFixture(true)]
    [TestFixture(false)]
    public class OrderbookTests
    {
        public OrderbookTests(bool useLegacyOrderbook)
        {
            this.useLegacyOrderbook = useLegacyOrderbook;
        }

        AccountWrapper account1;
        AccountWrapper account2;
        private bool useLegacyOrderbook;
        private CentaurusContext context;

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
            context = new AlphaContext(settings, new MockStorage(), useLegacyOrderbook);
            context.Init().Wait();

            var requestRateLimits = new RequestRateLimits { HourLimit = 1000, MinuteLimit = 100 };

            var account1 = new AccountWrapper(new Models.Account
            {
                Id = 1,
                Pubkey = new RawPubKey() { Data = KeyPair.Random().PublicKey },
                Balances = new List<Balance>()
            }, requestRateLimits);

            account1.Account.CreateBalance(0);
            account1.Account.GetBalance(0).UpdateBalance(10000000000);
            account1.Account.CreateBalance(1);
            account1.Account.GetBalance(1).UpdateBalance(10000000000);

            var account2 = new AccountWrapper(new Models.Account
            {
                Id = 2,
                Pubkey = new RawPubKey() { Data = KeyPair.Random().PublicKey },
                Balances = new List<Balance>()
            }, requestRateLimits);

            account2.Account.CreateBalance(0);
            account2.Account.GetBalance(0).UpdateBalance(10000000000);
            account2.Account.CreateBalance(1);
            account2.Account.GetBalance(1).UpdateBalance(10000000000);

            context.Setup(new Snapshot
            {
                Accounts = new List<AccountWrapper> { account1, account2 },
                Apex = 0,
                TxCursor = 1,
                Orders = new List<Order>(),
                Settings = new ConstellationSettings
                {
                    Vault = KeyPair.Random().PublicKey,
                    Assets = new List<AssetSettings> { new AssetSettings { Id = 1, Code = "X", Issuer = new RawPubKey() } },
                    RequestRateLimits = requestRateLimits
                },
            }).Wait();

            this.account1 = context.AccountStorage.GetAccount(account1.Id);
            this.account2 = context.AccountStorage.GetAccount(account2.Id);
        }

        [TearDown]
        public void TearDown()
        {
            context.Exchange.Clear();
            //CentaurusContext.Current.AccountStorage.Clear();
        }

        private void ExecuteWithOrderbook(int iterations, bool useNormalDistribution, Action<Action> executor)
        {
            var rnd = new Random();

            var market = context.Exchange.GetMarket(1);

            var testTradeResults = new Dictionary<RequestQuantum, EffectProcessorsContainer>();
            for (var i = 1; i < iterations; i++)
            {
                var price = useNormalDistribution ? rnd.NextNormallyDistributed() + 50 : rnd.NextDouble() * 100;
                var request = new OrderRequest
                {
                    RequestId = i,
                    Amount = rnd.Next(1, 20),
                    Asset = 1,
                    Price = Math.Round(price * 27) / 13
                };
                if (rnd.NextDouble() >= 0.5)
                {
                    request.Account = account1.Id;
                    request.AccountWrapper = account1;
                    request.Side = OrderSide.Buy;
                }
                else
                {
                    request.Account = account2.Id;
                    request.AccountWrapper = account2;
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
                var conterOrderEffectsContainer = new EffectProcessorsContainer(context, trade.CreateEnvelope(), diffObject);
                testTradeResults.Add(trade, conterOrderEffectsContainer);
            }

            var xlmStartBalance = account1.Account.GetBalance(0).Amount + account2.Account.GetBalance(0).Amount;
            var assetStartBalance = account1.Account.GetBalance(1).Amount + account2.Account.GetBalance(1).Amount;

            executor(() =>
            {
                foreach (var trade in testTradeResults)
                {
                    context.Exchange.ExecuteOrder(trade.Value);
                }
            });

            //cleanup orders
            foreach (var account in new[] { account1, account2 })
            {
                var activeOrders = context.Exchange.OrderMap.GetAllAccountOrders(account);
                foreach (var order in activeOrders)
                {
                    var decodedOrderId = OrderIdConverter.Decode(order.OrderId);

                    new OrderRemovedEffectProccessor(new OrderRemovedEffect
                    {
                        Account = account.Id,
                        OrderId = order.OrderId,
                        Amount = order.Amount,
                        QuoteAmount = order.QuoteAmount,
                        Price = order.Price,
                        Asset = decodedOrderId.Asset,
                        AccountWrapper = account
                    }, market.GetOrderbook(decodedOrderId.Side)).CommitEffect();
                }
            }
            Assert.AreEqual(xlmStartBalance, account1.Account.GetBalance(0).Amount + account2.Account.GetBalance(0).Amount);
            Assert.AreEqual(assetStartBalance, account1.Account.GetBalance(1).Amount + account2.Account.GetBalance(1).Amount);
            Assert.AreEqual(0, account1.Account.GetBalance(0).Liabilities);
            Assert.AreEqual(0, account1.Account.GetBalance(1).Liabilities);
            Assert.AreEqual(0, account2.Account.GetBalance(0).Liabilities);
            Assert.AreEqual(0, account2.Account.GetBalance(1).Liabilities);
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
                var market = context.Exchange.GetMarket(1);
                return $"Is legacy orderbook: {useLegacyOrderbook}, {iterations} iterations, orderbook size: {market.Bids.Count} bids,  {market.Asks.Count} asks, {market.Bids.GetBestPrice().ToString("G3")}/{market.Asks.GetBestPrice().ToString("G3")} spread.";
            }));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void OrderbookRemoveTest(bool isOrderedByPrice)
        {
            var orders = new List<Order>();
            var random = new Random();
            var price = 1D;
            var side = OrderSide.Buy;
            var asset = 1;

            var orderbook = context.Exchange.GetOrderbook(asset, side);
            var ordersCount = 1000;
            for (var i = 1; i <= ordersCount; i++)
            {
                if (isOrderedByPrice)
                    price = price * 1.01;
                else
                    price = 1 + random.NextDouble();
                var orderId = OrderIdConverter.Encode((ulong)i, asset, side);
                var amount = 1000;
                var order = new Order { OrderId = orderId, Amount = 1000, Price = price, QuoteAmount = OrderMatcher.EstimateQuoteAmount(amount, price, side) };
                orders.Add(order);
                orderbook.InsertOrder(order);
            }

            Func<int> getOrdersCount = () =>
            {
                var ordersCounter = 0;
                foreach (var o in orderbook)
                    ordersCounter++;
                return ordersCounter;
            };

            var count = getOrdersCount();
            Assert.AreEqual(orderbook.Count, count, "Orderbook.Count and order-book items count are not equal.");
            Assert.AreEqual(ordersCount, count);

            foreach (var order in orders)
            {
                orderbook.RemoveOrder(order.OrderId, out _);
                ordersCount--;
                Assert.AreEqual(ordersCount, orderbook.Count);
                Assert.AreEqual(ordersCount, getOrdersCount());
            }
            Assert.IsNull(orderbook.Head);
            Assert.IsNull(orderbook.Tail);
        }
    }
}
