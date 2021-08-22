using Centaurus.Domain;
using Centaurus.Models;
using Centaurus.PaymentProvider;
using Centaurus.PaymentProvider.Models;
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
            context.SetState(State.Ready);

            var apex = context.QuantumStorage.CurrentApex;

            var account1 = context.AccountStorage.GetAccount(TestEnvironment.Client1KeyPair);

            var clientAccountBalance = account1.GetBalance(asset);

            var client1StartBalanceAmount = clientAccountBalance?.Amount ?? 0;

            var depositAmount = (ulong)new Random().Next(10, 1000);

            var providerSettings = context.Constellation.Providers.First();

            var paymentNotification = new DepositNotificationModel
            {
                Cursor = cursor.ToString(),
                Items = new List<DepositModel>
                    {
                        new DepositModel
                        {
                            Amount = depositAmount,
                            Destination = account1.Pubkey,
                            Asset = asset,
                            IsSuccess = true
                        }
                    },
                ProviderId = PaymentProviderBase.GetProviderId(providerSettings.Provider, providerSettings.Name),
                DepositTime = DateTime.UtcNow
            };

            if (!context.PaymentProvidersManager.TryGetManager(paymentNotification.ProviderId, out var provider))
                throw new Exception("Provider not found.");
            provider.NotificationsManager.RegisterNotification(paymentNotification);

            var ledgerCommit = new DepositQuantum
            {
                Source = paymentNotification.ToDomainModel()
            };

            await AssertQuantumHandling(ledgerCommit, excpectedException);
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
            var account = context.AccountStorage.GetAccount(TestEnvironment.Client1KeyPair);
            var order = new OrderRequest
            {
                Account = account.Id,
                RequestId = nonce,
                Amount = amount,
                Asset = asset,
                Price = 100,
                Side = side
            };

            var envelope = order.CreateEnvelope();
            envelope.Sign(TestEnvironment.Client1KeyPair);
            var quantum = new RequestQuantum
            {
                RequestEnvelope = envelope
            };

            var res = await AssertQuantumHandling(quantum, excpectedException);
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
                Account = acc.Id,
                RequestId = 1,
                Amount = amount,
                Asset = asset,
                Price = 100,
                Side = side
            };

            var envelope = order.CreateEnvelope().Sign(useFakeSigner ? TestEnvironment.Client2KeyPair : TestEnvironment.Client1KeyPair);
            var quantum = new RequestQuantum { RequestEnvelope = envelope };

            var submitResult = await AssertQuantumHandling(quantum, excpectedException);
            if (excpectedException != null)
                return;

            var apex = ((Quantum)submitResult.OriginalMessage.Message).Apex + apexMod;

            var orderCancellation = new OrderCancellationRequest
            {
                Account = acc.Id,
                RequestId = 2,
                OrderId = apex
            };

            envelope = orderCancellation.CreateEnvelope().Sign(TestEnvironment.Client1KeyPair);
            quantum = new RequestQuantum { RequestEnvelope = envelope };

            var cancelResult = await AssertQuantumHandling(quantum, excpectedException);

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
                Account = account.Id,
                RequestId = 1,
                Amount = amount,
                Asset = context.Constellation.QuoteAsset.Code,
                Destination = KeyPair.Random().PublicKey,
                Provider = PaymentProviderBase.GetProviderId(providerSettings.Provider, providerSettings.Name)
            };

            var envelope = withdrawal
                .CreateEnvelope()
                .Sign(useFakeSigner ? TestEnvironment.Client2KeyPair : TestEnvironment.Client1KeyPair);

            await AssertQuantumHandling(new WithdrawalRequestQuantum { RequestEnvelope = envelope }, excpectedException);
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
                Account = account.Id,
                RequestId = 1,
                Asset = context.Constellation.QuoteAsset.Code,
                Destination = TestEnvironment.Client2KeyPair,
                Amount = amount
            };

            var envelope = withdrawal.CreateEnvelope();
            envelope.Sign(useFakeSigner ? TestEnvironment.Client2KeyPair : TestEnvironment.Client1KeyPair);

            var quantum = new RequestQuantum { RequestEnvelope = envelope };

            var baseAsset = context.Constellation.QuoteAsset.Code;
            var expextedBalance = account.GetBalance(baseAsset).Amount - amount;

            await AssertQuantumHandling(quantum, excpectedException);

            if (excpectedException == null)
            {
                Assert.AreEqual(account.GetBalance(baseAsset).Amount, expextedBalance);
            }
        }

        [Test]
        [TestCase(0, typeof(UnauthorizedException))]
        [TestCase(1, null)]
        public async Task AccountDataRequestTest(int nonce, Type excpectedException)
        {
            context.SetState(State.Ready);
            var account = context.AccountStorage.GetAccount(TestEnvironment.Client1KeyPair);
            var order = new AccountDataRequest
            {
                Account = account.Id,
                RequestId = nonce
            };

            var envelope = order.CreateEnvelope();
            envelope.Sign(TestEnvironment.Client1KeyPair);
            var quantum = new AccountDataRequestQuantum { RequestEnvelope = envelope };

            var res = await AssertQuantumHandling(quantum, excpectedException);
            if (excpectedException == null)
            {
                Assert.IsInstanceOf<AccountDataResponse>(res);
                var adr = (AccountDataResponse)res;
                var payloadHash = adr.ComputePayloadHash();
                Assert.AreEqual(payloadHash, adr.Quantum.PayloadHash);
            }
        }

        [Test]
        [TestCaseSource("AccountRequestRateLimitsCases")]
        public async Task AccountRequestRateLimitTest(KeyPair clientKeyPair, int? requestLimit)
        {
            context.SetState(State.Ready);

            var account = context.AccountStorage.GetAccount(clientKeyPair);
            if (requestLimit.HasValue)
                account.RequestRateLimits = new RequestRateLimits { HourLimit = (uint)requestLimit.Value, MinuteLimit = (uint)requestLimit.Value };

            var minuteLimit = (account.RequestRateLimits ?? context.Constellation.RequestRateLimits).MinuteLimit;
            var minuteIterCount = minuteLimit + 1;
            for (var i = 0; i < minuteIterCount; i++)
            {
                var envelope = new AccountDataRequest
                {
                    Account = account.Id,
                    RequestId = i + 1
                }.CreateEnvelope();
                envelope.Sign(clientKeyPair);
                var quantum = new AccountDataRequestQuantum { RequestEnvelope = envelope };

                if (i + 1 > minuteLimit)
                    await AssertQuantumHandling(quantum, typeof(TooManyRequestsException));
                else
                    await AssertQuantumHandling(quantum, null);
            }
        }

        protected async Task<QuantumResultMessageBase> AssertQuantumHandling(Quantum quantum, Type excpectedException = null)
        {
            try
            {
                var result = await context.QuantumHandler.HandleAsync(quantum);

                context.PendingUpdatesManager.UpdateBatch(true);
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
