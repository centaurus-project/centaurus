using Centaurus.Domain;
using Centaurus.Domain.Models;
using Centaurus.Models;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Centaurus.Test.Exchange.Analytics
{
    public class UpdatesTest
    {
        AccountWrapper account1;
        AccountWrapper account2;
        private ExecutionContext context;

        [SetUp]
        public void Setup()
        {
            EnvironmentHelper.SetTestEnvironmentVariable();
            var settings = GlobalInitHelper.GetAlphaSettings();

            context = new ExecutionContext(settings, new MockStorage(), new MockPaymentProviderFactory());
            var requestsLimit = new RequestRateLimits();

            account1 = new AccountWrapper(new Models.Account
            {
                Id = 1,
                Pubkey = new RawPubKey() { Data = KeyPair.Random().PublicKey },
                Balances = new List<Balance>()
            }, requestsLimit);

            account1.Account.CreateBalance("XLM");
            account1.Account.GetBalance("XLM").UpdateBalance(10000000000, UpdateSign.Plus);

            account1.Account.CreateBalance("USD");
            account1.Account.GetBalance("USD").UpdateBalance(10000000000, UpdateSign.Plus);

            account2 = new AccountWrapper(new Models.Account
            {
                Id = 2,
                Pubkey = new RawPubKey() { Data = KeyPair.Random().PublicKey },
                Balances = new List<Balance>()
            }, requestsLimit);

            account2.Account.CreateBalance("XLM");
            account2.Account.GetBalance("XLM").UpdateBalance(10000000000, UpdateSign.Plus);

            account2.Account.CreateBalance("USD");
            account2.Account.GetBalance("USD").UpdateBalance(10000000000, UpdateSign.Plus);

            var stellarPaymentProviderVault = KeyPair.Random().AccountId;
            var stellarPaymentProvider = new ProviderSettings
            {
                Provider = "Stellar",
                Name = "Test",
                Vault = stellarPaymentProviderVault,
                Assets = new List<ProviderAsset>
                {
                    new ProviderAsset { CentaurusAsset = "XLM", Token = "native"},
                    new ProviderAsset { CentaurusAsset = "USD", IsVirtual = true, Token = $"USD-{stellarPaymentProviderVault}" }
                },
                InitCursor = "1",
                PaymentSubmitDelay = 0
            };

            context.Setup(new Snapshot
            {
                Accounts = new List<AccountWrapper> { account1, account2 },
                Apex = 0,
                Orders = new List<OrderWrapper>(),
                Settings = new ConstellationSettings
                {
                    Providers = new List<ProviderSettings> { stellarPaymentProvider },
                    Assets = new List<AssetSettings> { new AssetSettings { Code = "XLM" }, new AssetSettings { Code = "USD" } },
                    RequestRateLimits = new RequestRateLimits { HourLimit = 1000, MinuteLimit = 100 }
                }
            });
        }

        public void OrderbookPerformanceTest(int iterations, bool useNormalDistribution = false)
        {
            var rnd = new Random();

            var orderRequestProcessor = new OrderRequestProcessor(context);
            var testTradeResults = new Dictionary<RequestQuantum, RequestContext>();
            for (var i = 1; i < iterations; i++)
            {
                var price = useNormalDistribution ? rnd.NextNormallyDistributed() + 50 : rnd.NextDouble() * 100;
                var accountWrapper = context.AccountStorage.GetAccount(account1.Account.Pubkey);
                var trade = new RequestQuantum
                {
                    Apex = (ulong)i,
                    RequestEnvelope = new MessageEnvelope
                    {
                        Message = new OrderRequest
                        {
                            Account = accountWrapper.Account.Id,
                            RequestId = i,
                            Amount = (ulong)rnd.Next(1, 20),
                            Asset = "USD",
                            Price = Math.Round(price * 10) / 10,
                            Side = rnd.NextDouble() >= 0.5 ? OrderSide.Buy : OrderSide.Sell
                        },
                        Signatures = new List<Ed25519Signature>()
                    },
                    Timestamp = DateTime.UtcNow.Ticks
                };

                var orderContext = (RequestContext)orderRequestProcessor.GetContext(trade.CreateEnvelope(), context.AccountStorage.GetAccount(account1.Account.Pubkey));
                testTradeResults.Add(trade, orderContext);
            }

            PerfCounter.MeasureTime(() =>
            {
                foreach (var trade in testTradeResults)
                    context.Exchange.ExecuteOrder(trade.Value);
            }, () =>
            {
                var market = context.Exchange.GetMarket("USD");
                return $"{iterations} iterations, orderbook size: {market.Bids.Count} bids,  {market.Asks.Count} asks, {market.Bids.GetBestPrice().ToString("G3")}/{market.Asks.GetBestPrice().ToString("G3")} spread.";
            });
        }

        [Test]
        public void OnUpdates()
        {
            context.AnalyticsManager.OnUpdate += AnalyticsManager_OnUpdate;
            OrderbookPerformanceTest(10_000);
        }

        private void AnalyticsManager_OnUpdate()
        {
            try
            {
                foreach (var asset in context.Constellation.Assets)
                {
                    if (asset.Code == "XLM") //base asset
                        continue;
                    foreach (var precision in DepthsSubscription.Precisions)
                    {
                        context.AnalyticsManager.MarketDepthsManager.GetDepth(asset.Code, precision);
                    }

                    var periods = Enum.GetValues(typeof(PriceHistoryPeriod)).Cast<PriceHistoryPeriod>();
                    foreach (var period in periods)
                    {
                        context.AnalyticsManager.PriceHistoryManager.GetPriceHistory(0, asset.Code, period);
                    }

                    context.AnalyticsManager.TradesHistoryManager.GetTrades(asset.Code);

                    context.AnalyticsManager.MarketTickersManager.GetAllTickers();

                    context.AnalyticsManager.MarketTickersManager.GetMarketTicker(asset.Code);
                }
            }
            catch (Exception exc)
            {
                Assert.Fail(exc.Message);
            }
        }
    }
}
