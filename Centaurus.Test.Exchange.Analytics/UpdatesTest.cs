using Centaurus.DAL;
using Centaurus.Domain;
using Centaurus.Exchange.Analytics;
using Centaurus.Models;
using NUnit.Framework;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Centaurus.Test.Exchange.Analytics
{
    public class UpdatesTest
    {
        AccountWrapper account1;
        AccountWrapper account2;

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

            account1 = new AccountWrapper(new Models.Account
            {
                Id = 1,
                Pubkey = new RawPubKey() { Data = KeyPair.Random().PublicKey },
                Balances = new List<Balance>()
            }, Global.Constellation.RequestRateLimits);

            account1.Account.CreateBalance(0);
            account1.Account.GetBalance(0).UpdateBalance(10000000000);

            account1.Account.CreateBalance(1);
            account1.Account.GetBalance(1).UpdateBalance(10000000000);

            account2 = new AccountWrapper(new Models.Account
            {
                Id = 2,
                Pubkey = new RawPubKey() { Data = KeyPair.Random().PublicKey },
                Balances = new List<Balance>()
            }, Global.Constellation.RequestRateLimits);

            account2.Account.CreateBalance(0);
            account2.Account.GetBalance(0).UpdateBalance(10000000000);

            account2.Account.CreateBalance(1);
            account2.Account.GetBalance(1).UpdateBalance(10000000000);
            Global.Setup(new Snapshot
            {
                Accounts = new List<AccountWrapper> { account1, account2 },
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

        public void OrderbookPerformanceTest(int iterations, bool useNormalDistribution = false)
        {
            var rnd = new Random();

            var testTradeResults = new Dictionary<RequestQuantum, EffectProcessorsContainer>();
            for (var i = 1; i < iterations; i++)
            {
                var price = useNormalDistribution ? rnd.NextNormallyDistributed() + 50 : rnd.NextDouble() * 100;
                var accountWrapper = Global.AccountStorage.GetAccount(account1.Account.Pubkey);
                var trade = new RequestQuantum
                {
                    Apex = i,
                    RequestEnvelope = new MessageEnvelope
                    {
                        Message = new OrderRequest
                        {
                            Account = accountWrapper.Account.Id,
                            RequestId = i,
                            Amount = rnd.Next(1, 20),
                            Asset = 1,
                            Price = Math.Round(price * 10) / 10,
                            Side = rnd.NextDouble() >= 0.5 ? OrderSide.Buy : OrderSide.Sell,
                            AccountWrapper = Global.AccountStorage.GetAccount(account1.Account.Pubkey)
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

        [Test]
        public void OnUpdates()
        {
            Global.AnalyticsManager.OnUpdate += AnalyticsManager_OnUpdate;
            OrderbookPerformanceTest(10_000);
        }

        private async void AnalyticsManager_OnUpdate()
        {
            try
            {
                foreach (var asset in Global.Constellation.Assets)
                {
                    if (asset.IsXlm)
                        continue;
                    foreach (var precision in DepthsSubscription.Precisions)
                    {
                        Global.AnalyticsManager.MarketDepthsManager.GetDepth(asset.Id, precision);
                    }

                    var periods = Enum.GetValues(typeof(PriceHistoryPeriod)).Cast<PriceHistoryPeriod>();
                    foreach (var period in periods)
                    {
                        await Global.AnalyticsManager.PriceHistoryManager.GetPriceHistory(0, asset.Id, period);
                    }

                    Global.AnalyticsManager.TradesHistoryManager.GetTrades(asset.Id);

                    Global.AnalyticsManager.MarketTickersManager.GetAllTickers();

                    Global.AnalyticsManager.MarketTickersManager.GetMarketTicker(asset.Id);
                }
            }
            catch (Exception exc)
            {
                Assert.Fail(exc.Message);
            }
        }
    }
}
