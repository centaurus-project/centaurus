using Centaurus.Domain;
using Centaurus.Models;
using Centaurus.PaymentProvider;
using Centaurus.PaymentProvider.Models;
using Centaurus.Xdr;
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
        [OneTimeSetUp]
        public void Setup()
        {
            EnvironmentHelper.SetTestEnvironmentVariable();
            context = GlobalInitHelper.DefaultAlphaSetup().Result;
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            context?.Dispose();
        }

        protected ExecutionContext context;

        [Test]
        [TestCase(10, "XLM", null)]
        public async Task DepositQuantumTest(int cursor, string asset, Type excpectedException)
        {
            context.SetState(State.Ready);

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
        [TestCase(true, 1, "USD", 98ul, OrderSide.Buy, null)]
        [TestCase(false, 1, "USD", 100ul, OrderSide.Sell, null)]
        [TestCase(true, 0, "XLM", 0ul, OrderSide.Sell, typeof(UnauthorizedException))]
        [TestCase(true, 1, "XLM", 0ul, OrderSide.Sell, typeof(BadRequestException))]
        [TestCase(true, 1, "USD", 0ul, OrderSide.Sell, typeof(BadRequestException))]
        [TestCase(true, 1, "USD", ulong.MaxValue, OrderSide.Sell, typeof(BadRequestException))]
        [TestCase(true, 1, "XLM", 100ul, OrderSide.Buy, typeof(BadRequestException))]
        public async Task OrderQuantumTest(bool useFirstAccount, int nonceInc, string asset, ulong amount, OrderSide side, Type excpectedException)
        {
            var accountKeypair = useFirstAccount ? TestEnvironment.Client1KeyPair : TestEnvironment.Client2KeyPair;
            var account = context.AccountStorage.GetAccount(accountKeypair);
            var order = new OrderRequest
            {
                Account = account.Pubkey,
                RequestId = (long)account.Nonce + nonceInc,
                Amount = amount,
                Asset = asset,
                Price = 100,
                Side = side
            };

            var envelope = order.CreateEnvelope();
            envelope.Sign(accountKeypair);
            var quantum = new RequestQuantum
            {
                RequestEnvelope = envelope
            };
            var ordersCount = account.Orders.Count;
            await AssertQuantumHandling(quantum, excpectedException);
            if (excpectedException == null)
                Assert.AreEqual(ordersCount + 1, account.Orders.Count);
        }

        [Test]
        [TestCase("USD", 98ul, OrderSide.Buy, 0ul, false, null)]
        [TestCase("USD", 100ul, OrderSide.Sell, 111ul, false, typeof(BadRequestException))]
        [TestCase("USD", 98ul, OrderSide.Buy, 111ul, false, typeof(BadRequestException))]
        [TestCase("USD", 100ul, OrderSide.Sell, 0ul, true, typeof(UnauthorizedException))]
        [TestCase("USD", 100ul, OrderSide.Sell, 0ul, false, null)]
        public async Task OrderCancellationQuantumTest(string asset, ulong amount, OrderSide side, ulong apexMod, bool useFakeSigner, Type excpectedException)
        {
            var acc = context.AccountStorage.GetAccount(TestEnvironment.Client1KeyPair);

            var order = new OrderRequest
            {
                Account = acc.Pubkey,
                RequestId = (long)acc.Nonce + 1,
                Amount = amount,
                Asset = asset,
                Price = 100,
                Side = side
            };

            var envelope = order.CreateEnvelope().Sign(TestEnvironment.Client1KeyPair);
            var quantum = new RequestQuantum { RequestEnvelope = envelope };

            await AssertQuantumHandling(quantum);

            var orderId = quantum.Apex + apexMod;

            var orderCancellation = new OrderCancellationRequest
            {
                Account = acc.Pubkey,
                RequestId = (long)acc.Nonce + 1,
                OrderId = orderId
            };

            var ordersCount = acc.Orders.Count;

            envelope = orderCancellation.CreateEnvelope().Sign(useFakeSigner ? TestEnvironment.Client2KeyPair : TestEnvironment.Client1KeyPair);
            quantum = new RequestQuantum { RequestEnvelope = envelope };

            await AssertQuantumHandling(quantum, excpectedException);
            if (excpectedException != null)
            {
                //remove created order
                orderCancellation.OrderId = orderId - apexMod;
                envelope = orderCancellation.CreateEnvelope().Sign(TestEnvironment.Client1KeyPair);
                quantum = new RequestQuantum { RequestEnvelope = envelope };
                await AssertQuantumHandling(quantum);
                return;
            }

            Assert.AreEqual(ordersCount - 1, acc.Orders.Count);
        }



        [Test]
        [TestCase(100ul, true, typeof(UnauthorizedException))]
        [TestCase(ulong.MaxValue, false, typeof(BadRequestException))]
        [TestCase(100ul, false, null)]
        public async Task WithdrawalQuantumTest(ulong amount, bool useFakeSigner, Type excpectedException)
        {
            var account = context.AccountStorage.GetAccount(TestEnvironment.Client1KeyPair);
            var providerSettings = context.Constellation.Providers.First();
            var withdrawal = new WithdrawalRequest
            {
                Account = account.Pubkey,
                RequestId = (long)account.Nonce + 1,
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
        [TestCase(ulong.MaxValue, false, typeof(BadRequestException))]
        [TestCase(100ul, true, typeof(UnauthorizedException))]
        [TestCase(100ul, false, null)]
        public async Task PaymentQuantumTest(ulong amount, bool useFakeSigner, Type excpectedException)
        {
            var account = context.AccountStorage.GetAccount(TestEnvironment.Client1KeyPair);

            var withdrawal = new PaymentRequest
            {
                Account = account.Pubkey,
                RequestId = (long)account.Nonce + 1,
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
        [TestCase(1ul, null)]
        public async Task AccountDataRequestTest(ulong nonceInc, Type excpectedException)
        {
            context.SetState(State.Ready);
            var account = context.AccountStorage.GetAccount(TestEnvironment.Client1KeyPair);
            var order = new AccountDataRequest
            {
                Account = account.Pubkey,
                RequestId = (long)(nonceInc + account.Nonce)
            };

            var envelope = order.CreateEnvelope();
            envelope.Sign(TestEnvironment.Client1KeyPair);
            var quantum = new AccountDataRequestQuantum { RequestEnvelope = envelope };

            var result = await AssertQuantumHandling(quantum, excpectedException);
            if (excpectedException == null)
            {
                Assert.IsInstanceOf<AccountDataResponse>(result);
                var adr = (AccountDataResponse)result;
                var payloadHash = adr.ComputePayloadHash();
                var accountDataRequestQuantum = XdrConverter.Deserialize<AccountDataRequestQuantum>(adr.Request.Data);
                Assert.AreEqual(payloadHash, accountDataRequestQuantum.PayloadHash);
            }
        }

        //[Test]
        //[TestCaseSource("AccountRequestRateLimitsCases")]
        //public async Task AccountRequestRateLimitTest(KeyPair clientKeyPair, int? requestLimit)
        //{
        //    context.SetState(State.Ready);

        //    var account = context.AccountStorage.GetAccount(clientKeyPair);
        //    if (requestLimit.HasValue)
        //        account.RequestRateLimits = new RequestRateLimits { HourLimit = (uint)requestLimit.Value, MinuteLimit = (uint)requestLimit.Value };

        //    var minuteLimit = (account.RequestRateLimits ?? context.Constellation.RequestRateLimits).MinuteLimit;
        //    var minuteIterCount = minuteLimit + 1;
        //    for (var i = 0; i < minuteIterCount; i++)
        //    {
        //        var envelope = new AccountDataRequest
        //        {
        //            Account = account.Pubkey,
        //            RequestId = i + 1
        //        }.CreateEnvelope();
        //        envelope.Sign(clientKeyPair);
        //        var quantum = new AccountDataRequestQuantum { RequestEnvelope = envelope };

        //        if (i + 1 > minuteLimit)
        //            await AssertQuantumHandling(quantum, typeof(TooManyRequestsException));
        //        else
        //            await AssertQuantumHandling(quantum, null);
        //    }
        //}

        protected async Task<QuantumResultMessageBase> AssertQuantumHandling(Quantum quantum, Type excpectedException = null)
        {
            try
            {
                var result = await context.QuantumHandler.HandleAsync(quantum, QuantumSignatureValidator.Validate(quantum)).OnProcessed;

                if (excpectedException != null)
                    Assert.Fail($"{excpectedException.Name} was expected, but wasn't occurred.");

                Assert.AreEqual(result.Status.ToString(), ResultStatusCode.Success.ToString());
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
