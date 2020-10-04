using Centaurus.Domain;
using Centaurus.Models;
using NUnit.Framework;
using stellar_dotnet_sdk;
using stellar_dotnet_sdk.xdr;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Test
{
    public abstract class BaseQuantumHandlerTests
    {

        [Test]
        [TestCase(0, 0, 0, typeof(InvalidOperationException))]
        [TestCase(1, 0, 0, typeof(InvalidOperationException))]
        [TestCase(10, 1000, 10, typeof(InvalidOperationException))]
        [TestCase(10, 1000, 0, null)]
        public async Task TxCommitQuantumTest(int cursor, int amount, int asset, Type excpectedException)
        {
            Global.AppState.State = ApplicationState.Ready;

            long apex = Global.QuantumStorage.CurrentApex;

            var client1StartBalanceAmount = (long)0;

            var account1 = Global.AccountStorage.GetAccount(TestEnvironment.Client1KeyPair).Account;

            var clientAccountBalance = account1.GetBalance(asset);

            var withdrawalDest = KeyPair.Random();
            var txHash = new byte[] { };
            if (clientAccountBalance != null && amount > 0)
            {
                client1StartBalanceAmount = clientAccountBalance.Amount;


                Global.Constellation.TryFindAssetSettings(asset, out var assetSettings);

                var account = new stellar_dotnet_sdk.Account(TestEnvironment.Client1KeyPair.AccountId, 1);
                var txBuilder = new TransactionBuilder(account);
                txBuilder.SetFee(10_000);
                txBuilder.AddTimeBounds(new stellar_dotnet_sdk.TimeBounds(DateTimeOffset.UtcNow, new TimeSpan(0, 5, 0)));
                txBuilder.AddOperation(
                    new PaymentOperation.Builder(withdrawalDest, assetSettings.ToAsset(), Amount.FromXdr(amount).ToString())
                        .SetSourceAccount((KeyPair)Global.Constellation.Vault)
                        .Build()
                    );
                var tx = txBuilder.Build();
                txHash = tx.Hash();

                var txV1 = tx.ToXdrV1();
                var txStream = new XdrDataOutputStream();
                stellar_dotnet_sdk.xdr.Transaction.Encode(txStream, txV1);

                var withdrawal = new WithdrawalRequest
                {
                    Account = TestEnvironment.Client1KeyPair,
                    TransactionXdr = txStream.ToArray(),
                    Nonce = DateTime.UtcNow.Ticks,
                    AccountWrapper = Global.AccountStorage.GetAccount(TestEnvironment.Client1KeyPair)
                };

                MessageEnvelope quantum = withdrawal.CreateEnvelope();
                quantum.Sign(TestEnvironment.Client1KeyPair);
                if (!Global.IsAlpha)
                {
                    quantum = new RequestQuantum { Apex = ++apex, RequestEnvelope = quantum, Timestamp = DateTime.UtcNow.Ticks }.CreateEnvelope();
                    quantum.Sign(TestEnvironment.AlphaKeyPair);
                }
                //create withdrawal
                await Global.QuantumHandler.HandleAsync(quantum);
            }

            var depositeAmount = new Random().Next(10, 1000);

            var ledgerNotification = new TxNotification
            {
                TxCursor = (uint)cursor,
                Payments = new List<PaymentBase>
                    {
                        new Deposit
                        {
                            Amount = depositeAmount,
                            Destination = TestEnvironment.Client1KeyPair,
                            Asset = asset
                        },
                        new Models.Withdrawal
                        {
                            TransactionHash = txHash,
                            PaymentResult = PaymentResults.Success
                        }
                    }
            };
            var ledgerNotificationEnvelope = ledgerNotification.CreateEnvelope();
            ledgerNotificationEnvelope.Sign(TestEnvironment.Auditor1KeyPair);

            var ledgerCommitEnv = new TxCommitQuantum
            {
                Source = ledgerNotificationEnvelope,
                Apex = ++apex
            }.CreateEnvelope();
            if (!Global.IsAlpha)
            {
                ledgerCommitEnv.Sign(TestEnvironment.AlphaKeyPair);
            }

            await AssertQuantumHandling(ledgerCommitEnv, excpectedException);
            if (excpectedException == null)
            {
                Assert.AreEqual(Global.TxManager.TxCursor, ledgerNotification.TxCursor);

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
                Nonce = nonce,
                Amount = amount,
                Asset = asset,
                Price = 100,
                Side = side,
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
            Global.AppState.State = ApplicationState.Ready;

            var order = new AccountDataRequest
            {
                Account = TestEnvironment.Client1KeyPair,
                Nonce = nonce,
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
                    Nonce = i + 1,
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
                var result = await Global.QuantumHandler.HandleAsync(quantum);

                await SnapshotHelper.ApplyUpdates();

                //check that processed quanta is saved to the storage
                var lastApex = await Global.PermanentStorage.GetLastApex();
                Assert.AreEqual(Global.QuantumStorage.CurrentApex, lastApex);

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
