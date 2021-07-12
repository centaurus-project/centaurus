using Centaurus.Domain;
using Centaurus.Models;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Centaurus.Test
{
    public abstract class BaseQuantumHandlerTests
    {
        protected ExecutionContext context;

        [Test]
        [TestCase(0, "XLM", typeof(InvalidOperationException))]
        [TestCase(1, "XLM", typeof(InvalidOperationException))]
        [TestCase(10, "1000", typeof(InvalidOperationException))]
        [TestCase(10, "XLM", null)]
        public async Task TxCommitQuantumTest(int cursor, string asset, Type excpectedException)
        {
            context.AppState.State = ApplicationState.Ready;

            var apex = context.QuantumStorage.CurrentApex;

            var account1 = context.AccountStorage.GetAccount(TestEnvironment.Client1KeyPair).Account;

            var clientAccountBalance = account1.GetBalance(asset);

            var client1StartBalanceAmount = clientAccountBalance?.Amount ?? 0;

            var depositAmount = (ulong)new Random().Next(10, 1000);

            var providerSettings = context.Constellation.Providers.First();

            var paymentNotification = new DepositNotification
            {
                Cursor = cursor.ToString(),
                Items = new List<Deposit>
                    {
                        new Deposit
                        {
                            Amount = depositAmount,
                            Destination = TestEnvironment.Client1KeyPair,
                            Asset = asset
                        }
                    },
                ProviderId = providerSettings.ProviderId
            };

            if (!context.PaymentProvidersManager.TryGetManager(paymentNotification.ProviderId, out var provider))
                throw new Exception("Provider not found.");
            provider.NotificationsManager.RegisterNotification(paymentNotification);

            var ledgerCommitEnv = new DepositQuantum
            {
                Source = paymentNotification,
                Apex = ++apex
            }.CreateEnvelope();
            if (!context.IsAlpha)
            {
                var msg = ((DepositQuantum)ledgerCommitEnv.Message);
                msg.Timestamp = DateTime.UtcNow.Ticks;
                ledgerCommitEnv = msg.CreateEnvelope().Sign(TestEnvironment.AlphaKeyPair);
            }

            await AssertQuantumHandling(ledgerCommitEnv, excpectedException);
            if (excpectedException == null)
            {
                context.PaymentProvidersManager.TryGetManager(paymentNotification.ProviderId, out var paymentProvider);
                Assert.AreEqual(paymentProvider.Cursor, paymentNotification.Cursor);

                Assert.AreEqual(account1.GetBalance(asset).Amount, client1StartBalanceAmount + depositAmount);
            }
        }

        [Test]
        [TestCase(0, "XLM", 0ul, OrderSide.Sell, typeof(UnauthorizedException))]
        [TestCase(1, "XLM", 0ul, OrderSide.Sell, typeof(BadRequestException))]
        [TestCase(1, "USD", 0ul, OrderSide.Sell, typeof(BadRequestException))]
        [TestCase(1, "USD", 1000000000ul, OrderSide.Sell, typeof(BadRequestException))]
        [TestCase(1, "USD", 100ul, OrderSide.Sell, null)]
        [TestCase(1, "USD", 100ul, OrderSide.Buy, typeof(BadRequestException))]
        [TestCase(1, "USD", 98ul, OrderSide.Buy, null)]
        public async Task OrderQuantumTest(int nonce, string asset, ulong amount, OrderSide side, Type excpectedException)
        {
            var accountWrapper = context.AccountStorage.GetAccount(TestEnvironment.Client1KeyPair);
            var order = new OrderRequest
            {
                Account = accountWrapper.Account.Id,
                RequestId = nonce,
                Amount = amount,
                Asset = asset,
                Price = 100,
                Side = side
            };

            var envelope = order.CreateEnvelope();
            envelope.Sign(TestEnvironment.Client1KeyPair);

            if (!context.IsAlpha)
            {
                var quantum = new RequestQuantum { Apex = context.QuantumStorage.CurrentApex + 1, RequestEnvelope = envelope, Timestamp = DateTime.UtcNow.Ticks };
                envelope = quantum.CreateEnvelope();
                envelope.Sign(TestEnvironment.AlphaKeyPair);
            }

            var res = await AssertQuantumHandling(envelope, excpectedException);
            if (excpectedException == null)
            {
                var currentMarket = context.Exchange.GetMarket(asset);
                Assert.IsTrue(currentMarket != null);

                var requests = side == OrderSide.Buy ? currentMarket.Bids : currentMarket.Asks;
                Assert.AreEqual(1, requests.Count);
            }
        }

        [Test]
        [TestCase("USD", 100ul, OrderSide.Sell, 111ul, false, typeof(BadRequestException))]
        [TestCase("USD", 98ul, OrderSide.Buy, 111ul, false, typeof(BadRequestException))]
        [TestCase("USD", 100ul, OrderSide.Sell, 0ul, true, typeof(UnauthorizedAccessException))]
        [TestCase("USD", 100ul, OrderSide.Sell, 0ul, false, null)]
        [TestCase("USD", 98ul, OrderSide.Buy, 0ul, false, null)]
        public async Task OrderCancellationQuantumTest(string asset, ulong amount, OrderSide side, ulong apexMod, bool useFakeSigner, Type excpectedException)
        {
            var acc = context.AccountStorage.GetAccount(TestEnvironment.Client1KeyPair);

            var order = new OrderRequest
            {
                Account = acc.Account.Id,
                RequestId = 1,
                Amount = amount,
                Asset = asset,
                Price = 100,
                Side = side
            };

            var envelope = order.CreateEnvelope().Sign(useFakeSigner ? TestEnvironment.Client2KeyPair : TestEnvironment.Client1KeyPair);

            if (!context.IsAlpha)
            {
                var quantum = new RequestQuantum { Apex = context.QuantumStorage.CurrentApex + 1, RequestEnvelope = envelope, Timestamp = DateTime.UtcNow.Ticks };
                envelope = quantum.CreateEnvelope().Sign(TestEnvironment.AlphaKeyPair);
            }

            var submitResult = await AssertQuantumHandling(envelope, excpectedException);
            if (excpectedException != null)
                return;

            var apex = ((Quantum)submitResult.OriginalMessage.Message).Apex + apexMod;

            var orderCancellation = new OrderCancellationRequest
            {
                Account = acc.Account.Id,
                RequestId = 2,
                OrderId = apex
            };

            envelope = orderCancellation.CreateEnvelope().Sign(TestEnvironment.Client1KeyPair);

            if (!context.IsAlpha)
            {
                var quantum = new RequestQuantum { Apex = context.QuantumStorage.CurrentApex + 1, RequestEnvelope = envelope, Timestamp = DateTime.UtcNow.Ticks };
                envelope = quantum.CreateEnvelope().Sign(TestEnvironment.AlphaKeyPair);
            }

            var cancelResult = await AssertQuantumHandling(envelope, excpectedException);

            if (excpectedException != null)
                return;
            var currentMarket = context.Exchange.GetMarket(asset);
            Assert.IsTrue(currentMarket != null);

            var requests = side == OrderSide.Buy ? currentMarket.Bids : currentMarket.Asks;
            Assert.AreEqual(requests.Count, 0);
        }



        [Test]
        [TestCase(100ul, true, typeof(UnauthorizedAccessException))]
        [TestCase(100ul, false, typeof(BadRequestException))]
        [TestCase(1000000000000ul, false, typeof(BadRequestException))]
        [TestCase(100ul, false, null)]
        public async Task WithdrawalQuantumTest(ulong amount, bool useFakeSigner, Type excpectedException)
        {
            var account = context.AccountStorage.GetAccount(TestEnvironment.Client1KeyPair);
            var providerSettings = context.Constellation.Providers.First();
            var withdrawal = new WithdrawalRequest
            {
                Account = account.Account.Id,
                RequestId = 1,
                Amount = amount,
                Asset = context.Constellation.GetBaseAsset(),
                Destination = "some_address",
                PaymentProvider = providerSettings.ProviderId
            };

            var envelope = withdrawal
                .CreateEnvelope()
                .Sign(useFakeSigner ? TestEnvironment.Client2KeyPair : TestEnvironment.Client1KeyPair);

            await AssertQuantumHandling(new RequestTransactionQuantum { RequestEnvelope = envelope, Apex = context.QuantumStorage.CurrentApex + 1 }.CreateEnvelope(), excpectedException);
        }

        [Test]
        [TestCase(100ul, false, typeof(BadRequestException))]
        [TestCase(100ul, false, typeof(BadRequestException))]
        [TestCase(1000000000000ul, false, typeof(BadRequestException))]
        [TestCase(100ul, true, typeof(UnauthorizedAccessException))]
        [TestCase(100ul, false, null)]
        public async Task PaymentQuantumTest(ulong amount, bool useFakeSigner, Type excpectedException)
        {
            var account = context.AccountStorage.GetAccount(TestEnvironment.Client1KeyPair);

            var withdrawal = new PaymentRequest
            {
                Account = account.Account.Id,
                RequestId = 1,
                Asset = context.Constellation.GetBaseAsset(),
                Destination = TestEnvironment.Client2KeyPair,
                Amount = amount
            };

            var envelope = withdrawal.CreateEnvelope();
            envelope.Sign(useFakeSigner ? TestEnvironment.Client2KeyPair : TestEnvironment.Client1KeyPair);

            if (!context.IsAlpha)
            {
                var quantum = new RequestQuantum { Apex = context.QuantumStorage.CurrentApex + 1, RequestEnvelope = envelope, Timestamp = DateTime.UtcNow.Ticks };
                envelope = quantum.CreateEnvelope();
                envelope.Sign(TestEnvironment.AlphaKeyPair);
            }

            var baseAsset = context.Constellation.GetBaseAsset();
            var expextedBalance = account.Account.GetBalance(baseAsset).Amount - amount;

            await AssertQuantumHandling(envelope, excpectedException);

            if (excpectedException == null)
            {
                Assert.AreEqual(account.Account.GetBalance(baseAsset).Amount, expextedBalance);
            }
        }

        [Test]
        [TestCase(0, typeof(UnauthorizedException))]
        [TestCase(1, null)]
        public async Task AccountDataRequestTest(int nonce, Type excpectedException)
        {
            context.AppState.State = ApplicationState.Ready;
            var accountWrapper = context.AccountStorage.GetAccount(TestEnvironment.Client1KeyPair);
            var order = new AccountDataRequest
            {
                Account = accountWrapper.Account.Id,
                RequestId = nonce
            };

            var envelope = order.CreateEnvelope();
            envelope.Sign(TestEnvironment.Client1KeyPair);

            if (!context.IsAlpha)
            {
                var quantum = new RequestQuantum { Apex = context.QuantumStorage.CurrentApex + 1, RequestEnvelope = envelope, Timestamp = DateTime.UtcNow.Ticks };
                envelope = quantum.CreateEnvelope();
                envelope.Sign(TestEnvironment.AlphaKeyPair);
            }

            var res = await AssertQuantumHandling(envelope, excpectedException);
            if (excpectedException == null)
                Assert.IsInstanceOf<Models.AccountDataResponse>(res);
        }

        [Test]
        [TestCaseSource("AccountRequestRateLimitsCases")]
        public async Task AccountRequestRateLimitTest(KeyPair clientKeyPair, int? requestLimit)
        {
            context.AppState.State = ApplicationState.Ready;

            var account = context.AccountStorage.GetAccount(clientKeyPair);
            if (requestLimit.HasValue)
                account.Account.RequestRateLimits = new RequestRateLimits { HourLimit = (uint)requestLimit.Value, MinuteLimit = (uint)requestLimit.Value };

            var minuteLimit = (account.Account.RequestRateLimits ?? context.Constellation.RequestRateLimits).MinuteLimit;
            var minuteIterCount = minuteLimit + 1;
            for (var i = 0; i < minuteIterCount; i++)
            {
                var envelope = new AccountDataRequest
                {
                    Account = account.Account.Id,
                    RequestId = i + 1
                }.CreateEnvelope();
                envelope.Sign(clientKeyPair);
                if (!context.IsAlpha)
                {
                    var quantum = new RequestQuantum { Apex = context.QuantumStorage.CurrentApex + 1, RequestEnvelope = envelope, Timestamp = DateTime.UtcNow.Ticks };
                    envelope = quantum.CreateEnvelope();
                    envelope.Sign(TestEnvironment.AlphaKeyPair);
                }

                if (i + 1 > minuteLimit)
                    await AssertQuantumHandling(envelope, typeof(TooManyRequestsException));
                else
                    await AssertQuantumHandling(envelope, null);
            }
        }

        protected async Task<ResultMessage> AssertQuantumHandling(MessageEnvelope quantum, Type excpectedException = null)
        {
            try
            {
                var result = await context.QuantumHandler.HandleAsync(quantum);

                context.PendingUpdatesManager.ApplyUpdates(true);

                //check that processed quanta is saved to the storage
                var lastApex = context.PermanentStorage.GetLastApex();
                Assert.AreEqual(context.QuantumStorage.CurrentApex, lastApex);

                return result;
            }
            catch (Exception exc)
            {
                if (excpectedException == null || excpectedException.FullName != exc.GetType().FullName)
                    throw;
                return null;
            }
        }
    }
}
