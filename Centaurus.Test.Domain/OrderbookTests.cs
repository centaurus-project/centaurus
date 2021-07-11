﻿using Centaurus.DAL;
using Centaurus.Domain;
using Centaurus.Domain.Models;
using Centaurus.Models;
using NUnit.Framework;
using System;
using System.Collections.Generic;

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
        private ExecutionContext context;
        string baseAsset = "XLM";
        string secondAsset = "USD";

        [SetUp]
        public void Setup()
        {
            EnvironmentHelper.SetTestEnvironmentVariable();
            var settings = GlobalInitHelper.GetAlphaSettings();

            context = new ExecutionContext(settings, new MockStorage(), new MockPaymentProviderFactory(), useLegacyOrderbook);

            var requestRateLimits = new RequestRateLimits { HourLimit = 1000, MinuteLimit = 100 };

            var account1 = new AccountWrapper(new Account
            {
                Id = 1,
                Pubkey = new RawPubKey() { Data = KeyPair.Random().PublicKey },
                Balances = new List<Balance>()
            }, requestRateLimits);

            account1.Account.CreateBalance(baseAsset);
            account1.Account.GetBalance(baseAsset).UpdateBalance(10000000000, UpdateSign.Plus);
            account1.Account.CreateBalance(secondAsset);
            account1.Account.GetBalance(secondAsset).UpdateBalance(10000000000, UpdateSign.Plus);

            var account2 = new AccountWrapper(new Account
            {
                Id = 2,
                Pubkey = new RawPubKey() { Data = KeyPair.Random().PublicKey },
                Balances = new List<Balance>()
            }, requestRateLimits);

            account2.Account.CreateBalance(baseAsset);
            account2.Account.GetBalance(baseAsset).UpdateBalance(10000000000, UpdateSign.Plus);
            account2.Account.CreateBalance(secondAsset);
            account2.Account.GetBalance(secondAsset).UpdateBalance(10000000000, UpdateSign.Plus);

            context.Setup(new Snapshot
            {
                Accounts = new List<AccountWrapper> { account1, account2 },
                Apex = 0,
                Orders = new List<OrderWrapper>(),
                Settings = new ConstellationSettings
                {
                    Providers = new List<ProviderSettings> {
                        new ProviderSettings {
                            Assets = new List<ProviderAsset> { new ProviderAsset {  CentaurusAsset = baseAsset, Token = "native" } },
                            InitCursor = "0",
                            Name = "Main",
                            PaymentSubmitDelay = 0,
                            Provider = "Stellar",
                            Vault = KeyPair.Random().AccountId
                        }
                    },
                    Assets = new List<AssetSettings> { new AssetSettings { Code = baseAsset }, new AssetSettings { Code = secondAsset } },
                    Auditors = new List<RawPubKey> { TestEnvironment.AlphaKeyPair, TestEnvironment.Auditor1KeyPair },
                    MinAccountBalance = 1,
                    MinAllowedLotSize = 1,
                    RequestRateLimits = requestRateLimits
                }
            });

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
            var asset = context.Constellation.Assets[1].Code;
            var market = context.Exchange.GetMarket(asset);

            var testTradeResults = new Dictionary<RequestQuantum, EffectProcessorsContainer>();
            for (var i = 1; i < iterations; i++)
            {
                var price = useNormalDistribution ? rnd.NextNormallyDistributed() + 50 : rnd.NextDouble() * 100;
                var request = new OrderRequest
                {
                    RequestId = i,
                    Amount = (ulong)rnd.Next(1, 20),
                    Asset = asset,
                    Price = Math.Round(price * 27) / 13
                };
                var initiator = default(AccountWrapper);
                if (rnd.NextDouble() >= 0.5)
                {
                    initiator = account1;
                    request.Account = account1.Id;
                    request.Side = OrderSide.Buy;
                }
                else
                {
                    initiator = account2;
                    request.Account = account2.Id;
                    request.Side = OrderSide.Sell;
                }

                var trade = new RequestQuantum
                {
                    Apex = (ulong)i,
                    RequestEnvelope = new MessageEnvelope
                    {
                        Message = request,
                        Signatures = new List<Ed25519Signature>()
                    },
                    Timestamp = DateTime.UtcNow.Ticks
                };
                var diffObject = new DiffObject();
                var conterOrderEffectsContainer = new EffectProcessorsContainer(context, trade.CreateEnvelope(), diffObject, initiator);
                testTradeResults.Add(trade, conterOrderEffectsContainer);
            }
            var baseAsset = context.Constellation.GetBaseAsset();
            var xlmStartBalance = account1.Account.GetBalance(baseAsset).Amount + account2.Account.GetBalance(baseAsset).Amount;
            var assetStartBalance = account1.Account.GetBalance(asset).Amount + account2.Account.GetBalance(asset).Amount;

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
                    new OrderRemovedEffectProccessor(new OrderRemovedEffect
                    {
                        Account = account.Id,
                        OrderId = order.OrderId,
                        Amount = order.Order.Amount,
                        QuoteAmount = order.Order.QuoteAmount,
                        Price = order.Order.Price,
                        Asset = order.Order.Asset,
                        Side = order.Order.Side
                    }, account, market.GetOrderbook(order.Order.Side), baseAsset).CommitEffect();
                }
            }
            Assert.AreEqual(xlmStartBalance, account1.Account.GetBalance(baseAsset).Amount + account2.Account.GetBalance(baseAsset).Amount);
            Assert.AreEqual(assetStartBalance, account1.Account.GetBalance(asset).Amount + account2.Account.GetBalance(asset).Amount);
            Assert.AreEqual(0, account1.Account.GetBalance(baseAsset).Liabilities);
            Assert.AreEqual(0, account1.Account.GetBalance(asset).Liabilities);
            Assert.AreEqual(0, account2.Account.GetBalance(baseAsset).Liabilities);
            Assert.AreEqual(0, account2.Account.GetBalance(asset).Liabilities);
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
                var market = context.Exchange.GetMarket(context.Constellation.Assets[1].Code);
                return $"Is legacy orderbook: {useLegacyOrderbook}, {iterations} iterations, orderbook size: {market.Bids.Count} bids,  {market.Asks.Count} asks, {market.Bids.GetBestPrice().ToString("G3")}/{market.Asks.GetBestPrice().ToString("G3")} spread.";
            }));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void OrderbookRemoveTest(bool isOrderedByPrice)
        {
            var orders = new List<OrderWrapper>();
            var random = new Random();
            var price = 1D;
            var side = OrderSide.Buy;
            var asset = context.Constellation.Assets[1].Code;

            var orderbook = context.Exchange.GetOrderbook(asset, side);
            var ordersCount = 1000;
            var fakeAccountWrapper = new AccountWrapper(new Account(), new RequestRateLimits());
            for (var i = 1; i <= ordersCount; i++)
            {
                if (isOrderedByPrice)
                    price = price * 1.01;
                else
                    price = 1 + random.NextDouble();
                var amount = 1000ul;
                var order = new OrderWrapper(new Order { OrderId = (ulong)i, Amount = 1000, Price = price, QuoteAmount = OrderMatcher.EstimateQuoteAmount(amount, price, side) }, fakeAccountWrapper);
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
