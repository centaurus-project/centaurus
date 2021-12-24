using Centaurus.Domain;
using Centaurus.Domain.Models;
using Centaurus.Models;
using Centaurus.PaymentProvider;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Centaurus.Test.Exchange.Analytics
{
    public class UpdatesTest
    {
        Account account1;
        Account account2;
        private ExecutionContext context;

        [SetUp]
        public void Setup()
        {
            EnvironmentHelper.SetTestEnvironmentVariable();
            var settings = GlobalInitHelper.GetAlphaSettings();

            context = new ExecutionContext(settings, new MockStorage(), new MockPaymentProviderFactory(), new DummyConnectionWrapperFactory());
            var requestsLimit = new RequestRateLimits();

            account1 = new Account(requestsLimit)
            {
                Pubkey = new RawPubKey() { Data = KeyPair.Random().PublicKey },
                Balances = new Dictionary<string, Balance>(),
                Orders = new Dictionary<ulong, Order>()
            };

            account1.CreateBalance("XLM");
            account1.GetBalance("XLM").UpdateBalance(10000000000, UpdateSign.Plus);

            account1.CreateBalance("USD");
            account1.GetBalance("USD").UpdateBalance(10000000000, UpdateSign.Plus);

            account2 = new Account(requestsLimit)
            {
                Pubkey = new RawPubKey() { Data = KeyPair.Random().PublicKey },
                Balances = new Dictionary<string, Balance>(),
                Orders = new Dictionary<ulong, Order>()
            };

            account2.CreateBalance("XLM");
            account2.GetBalance("XLM").UpdateBalance(10000000000, UpdateSign.Plus);

            account2.CreateBalance("USD");
            account2.GetBalance("USD").UpdateBalance(10000000000, UpdateSign.Plus);

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

            var constellation = new ConstellationSettings
            {
                Providers = new List<ProviderSettings> { stellarPaymentProvider },
                Assets = new List<AssetSettings> { new AssetSettings { Code = "XLM", IsQuoteAsset = true }, new AssetSettings { Code = "USD" } },
                RequestRateLimits = new RequestRateLimits { HourLimit = 1000, MinuteLimit = 100 },
                Alpha = TestEnvironment.AlphaKeyPair,
                Auditors = new[] { TestEnvironment.AlphaKeyPair, TestEnvironment.Auditor1KeyPair }
                        .Select(pk => new Auditor
                        {
                            PubKey = pk,
                            Address = $"{pk.AccountId}.com"
                        })
                        .ToList()
            };

            context.ConstellationSettingsManager.Update(constellation);

            context.Init(new Snapshot
            {
                Accounts = new List<Account> { account1, account2 },
                Apex = 0,
                Orders = new List<OrderWrapper>(),
                ConstellationSettings = constellation,
                Cursors = new[] { stellarPaymentProvider }.ToDictionary(p => PaymentProviderBase.GetProviderId(p.Provider, p.Name), p => p.InitCursor)
            });
        }

        public void OrderbookPerformanceTest(int iterations, bool useNormalDistribution = false)
        {
            var rnd = new Random();
            var asset = "USD";
            var orderRequestProcessor = new OrderRequestProcessor(context);
            var testTradeResults = new Dictionary<ClientRequestQuantum, QuantumProcessingItem>();
            for (var i = 1; i < iterations; i++)
            {
                var price = useNormalDistribution ? rnd.NextNormallyDistributed() + 50 : rnd.NextDouble() * 100;
                var trade = new ClientRequestQuantum
                {
                    Apex = (ulong)i,
                    RequestEnvelope = new MessageEnvelope
                    {
                        Message = new OrderRequest
                        {
                            Account = account1.Pubkey,
                            RequestId = i,
                            Amount = (ulong)rnd.Next(1, 20),
                            Asset = asset,
                            Price = Math.Round(price * 10) / 10,
                            Side = rnd.NextDouble() >= 0.5 ? OrderSide.Buy : OrderSide.Sell
                        },
                        Signature = new TinySignature { Data = new byte[64] }
                    },
                    Timestamp = DateTime.UtcNow.Ticks
                };

                testTradeResults.Add(trade, new QuantumProcessingItem(trade, System.Threading.Tasks.Task.FromResult(true)) { Initiator = account1 });
            }

            PerfCounter.MeasureTime(() =>
            {
                foreach (var trade in testTradeResults)
                    context.Exchange.ExecuteOrder(asset, context.ConstellationSettingsManager.Current.QuoteAsset.Code, trade.Value);
            }, () =>
            {
                var market = context.Exchange.GetMarket(asset);
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
                foreach (var asset in context.ConstellationSettingsManager.Current.Assets)
                {
                    if (asset.IsQuoteAsset) //base asset
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
