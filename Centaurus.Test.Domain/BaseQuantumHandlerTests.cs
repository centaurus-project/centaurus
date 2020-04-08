using Centaurus.Domain;
using Centaurus.Models;
using NUnit.Framework;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Centaurus.Test
{
    public abstract class BaseQuantumHandlerTests
    {

        [Test]
        [TestCase(1, 0, 0, 0, typeof(InvalidOperationException))]
        [TestCase(3, 0, 0, 0, typeof(InvalidOperationException))]
        [TestCase(3, 63, 0, 0, typeof(InvalidOperationException))]
        [TestCase(3, 63, 100, 10, typeof(InvalidOperationException))]
        [TestCase(3, 63, 100, 0, null)]
        public async Task LedgerQuantumTest(int ledgerFrom, int ledgerTo, int amount, int asset, Type excpectedException)
        {
            long apex = Global.QuantumStorage.CurrentApex;

            var client1StartBalanceAmount = (long)0;

            var account1 = Global.AccountStorage.GetAccount(TestEnvironment.Client1KeyPair).Account;

            var clientAccountBalance = account1.GetBalance(asset);

            var withdrawalDest = KeyPair.Random();
            var txHash = new byte[] { };
            if (clientAccountBalance != null && amount > 0)
            {
                client1StartBalanceAmount = clientAccountBalance.Amount;

                var transactionBuilder = new Transaction.Builder(Global.VaultAccount.GetAccount());
                transactionBuilder.AddOperation(
                    new PaymentOperation.Builder(
                        withdrawalDest,
                        new AssetTypeNative(),
                        amount.ToString()
                ).Build());
                var transaction = transactionBuilder.Build();

                var txXdr = transaction.ToRawEnvelopeXdr();

                txHash = txXdr.ComputeHash();

                var withdrawal = new WithdrawalRequest
                {
                    Account = TestEnvironment.Client1KeyPair,
                    Destination = withdrawalDest.PublicKey,
                    Amount = amount,
                    Asset = asset,
                    Nonce = (ulong)DateTime.UtcNow.Ticks,
                    TransactionHash = txHash,
                    TransactionXdr = txXdr,
                    AccountWrapper = Global.AccountStorage.GetAccount(TestEnvironment.Client1KeyPair)
                };

                Message quantum = withdrawal;
                if (!Global.IsAlpha)
                    quantum = new RequestQuantum { Apex = ++apex, RequestEnvelope = withdrawal.CreateEnvelope(), Timestamp = DateTime.UtcNow.Ticks };

                //create withdrawal
                await Global.QuantumHandler.HandleAsync(quantum.CreateEnvelope());
            }

            var depositeAmount = new Random().Next(10, 1000);

            var ledgerNotification = new LedgerUpdateNotification
            {
                LedgerFrom = (uint)ledgerFrom,
                LedgerTo = (uint)ledgerTo,
                Payments = new List<PaymentBase>
                    {
                        new Deposit
                        {
                            Amount = depositeAmount,
                            Destination = TestEnvironment.Client1KeyPair,
                            Asset = asset
                        },
                        new Withdrawal
                        {
                            Amount = amount,
                            Destination = withdrawalDest,
                            Source = TestEnvironment.Client1KeyPair,
                            Asset = asset,
                            TransactionHash = txHash,
                            PaymentResult = PaymentResults.Success
                        }
                    }
            };
            var ledgerNotificationEnvelope = ledgerNotification.CreateEnvelope();
            ledgerNotificationEnvelope.Sign(TestEnvironment.Auditor1KeyPair);

            var ledgerCommit = new LedgerCommitQuantum
            {
                Source = ledgerNotificationEnvelope,
                Apex = ++apex
            };

            await AssertQuantumHandling(ledgerCommit.CreateEnvelope(), excpectedException);
            if (excpectedException == null)
            {
                Assert.AreEqual(Global.LedgerManager.Ledger, ledgerNotification.LedgerTo);

                Assert.AreEqual(account1.GetBalance(asset).Liabilities, 0);
                Assert.AreEqual(account1.GetBalance(asset).Amount, client1StartBalanceAmount - amount + depositeAmount); //acc balance + deposit - withdrawal
            }
        }

        [Test]
        [TestCase(0, 0, 0, OrderSides.Sell, typeof(UnauthorizedException))]
        [TestCase(1, 0, 0, OrderSides.Sell, typeof(InvalidOperationException))]
        [TestCase(1, 1, 0, OrderSides.Sell, typeof(BadRequestException))]
        [TestCase(1, 1, 1000000000, OrderSides.Sell, typeof(BadRequestException))]
        [TestCase(1, 1, 100, OrderSides.Sell, null)]
        [TestCase(1, 1, 100, OrderSides.Buy, null)]
        public async Task OrderQuantumTest(int nonce, int asset, int amount, OrderSides side, Type excpectedException)
        {
            var order = new OrderRequest
            {
                Account = TestEnvironment.Client1KeyPair,
                Nonce = (ulong)nonce,
                Amount = amount,
                Asset = asset,
                Price = 100,
                Side = side,
                AccountWrapper = Global.AccountStorage.GetAccount(TestEnvironment.Client1KeyPair)
            };

            var envelope = order.CreateEnvelope();

            if (!Global.IsAlpha)
            {
                var quantum = new RequestQuantum { Apex = Global.QuantumStorage.CurrentApex + 1, RequestEnvelope = envelope };
                envelope = quantum.CreateEnvelope();
            }

            await AssertQuantumHandling(envelope, excpectedException);
            if (excpectedException == null)
            {
                var currentMarket = Global.Exchange.GetMarket(asset);
                Assert.IsTrue(currentMarket != null);

                var requests = side == OrderSides.Buy ? currentMarket.Bids : currentMarket.Asks;
                Assert.AreEqual(requests.Count, 1);

                Assert.AreEqual(requests.TotalAmount, amount);
                Assert.AreEqual(requests.Volume, amount * order.Price);
            }
        }

        [Test]
        [TestCase(0, typeof(UnauthorizedException))]
        [TestCase(1, null)]
        public async Task AccountDataRequestTest(int nonce, Type excpectedException)
        {
            var order = new AccountDataRequest
            {
                Account = TestEnvironment.Client1KeyPair,
                Nonce = (ulong)nonce,
                AccountWrapper = Global.AccountStorage.GetAccount(TestEnvironment.Client1KeyPair)
            };

            var envelope = order.CreateEnvelope();
            envelope.Sign(TestEnvironment.Client1KeyPair);

            if (!Global.IsAlpha)
            {
                var quantum = new RequestQuantum { Apex = Global.QuantumStorage.CurrentApex + 1, RequestEnvelope = envelope };
                envelope = quantum.CreateEnvelope();
                envelope.Sign(TestEnvironment.AlphaKeyPair);
            }

            var res = await AssertQuantumHandling(envelope, excpectedException);
            if (excpectedException == null)
                Assert.IsInstanceOf<AccountDataResponse>(res);
        }

        [Test]
        [TestCaseSource("AccountRequestRateLimitsCases")]
        public async Task AccountRequestRateLimitTest(KeyPair clientKeyPair, int? requestLimit)
        {
            Global.AppState.State = ApplicationState.Ready;

            var account = Global.AccountStorage.GetAccount(clientKeyPair);
            if (requestLimit.HasValue)
                account.Account.RequestRateLimits = new RequestRateLimits { HourLimit = (uint)requestLimit.Value, MinuteLimit = (uint)requestLimit.Value };

            var minuteLimit = (account.Account.RequestRateLimits ?? Global.Constellation.RequestRateLimits).MinuteLimit;
            var minuteIterCount = minuteLimit + 1;
            for (var i = 0; i < minuteIterCount; i++)
            {
                var envelope = new AccountDataRequest
                {
                    Account = clientKeyPair,
                    Nonce = (ulong)(i + 1),
                    AccountWrapper = Global.AccountStorage.GetAccount(clientKeyPair)
                }.CreateEnvelope();
                envelope.Sign(clientKeyPair);
                if (!Global.IsAlpha)
                {
                    var quantum = new RequestQuantum { Apex = Global.QuantumStorage.CurrentApex + 1, RequestEnvelope = envelope };
                    envelope = quantum.CreateEnvelope();
                    envelope.Sign(TestEnvironment.AlphaKeyPair);
                }

                if (i + 1 > minuteLimit)
                    await AssertQuantumHandling(envelope, typeof(TooManyRequests));
                else
                    await AssertQuantumHandling(envelope, null);
            }
        }

        protected async Task<ResultMessage> AssertQuantumHandling(MessageEnvelope quantum, Type excpectedException = null)
        {
            try
            {
                return await Global.QuantumHandler.HandleAsync(quantum);
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
