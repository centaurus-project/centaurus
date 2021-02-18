using Centaurus.Domain;
using Centaurus.Models;
using NUnit.Framework;
using stellar_dotnet_sdk;
using stellar_dotnet_sdk.responses;
using stellar_dotnet_sdk.xdr;
using System;
using System.Collections.Generic;
using System.Globalization;
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

                var accountWrapper = Global.AccountStorage.GetAccount(TestEnvironment.Client1KeyPair);

                var withdrawal = new WithdrawalRequest
                {
                    Account = accountWrapper.Account.Id,
                    TransactionXdr = txStream.ToArray(),
                    Nonce = DateTime.UtcNow.Ticks,
                    AccountWrapper = accountWrapper
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

            var depositAmount = new Random().Next(10, 1000);

            var ledgerNotification = new TxNotification
            {
                TxCursor = (uint)cursor,
                Payments = new List<PaymentBase>
                    {
                        new Deposit
                        {
                            Amount = depositAmount,
                            Destination = TestEnvironment.Client1KeyPair,
                            Asset = asset
                        },
                        new Withdrawal
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
                var msg = ((TxCommitQuantum)ledgerCommitEnv.Message);
                msg.Timestamp = DateTime.UtcNow.Ticks;
                ledgerCommitEnv = msg.CreateEnvelope().Sign(TestEnvironment.AlphaKeyPair);
            }

            await AssertQuantumHandling(ledgerCommitEnv, excpectedException);
            if (excpectedException == null)
            {
                Assert.AreEqual(Global.TxCursorManager.TxCursor, ledgerNotification.TxCursor);

                Assert.AreEqual(account1.GetBalance(asset).Liabilities, 0);
                Assert.AreEqual(account1.GetBalance(asset).Amount, client1StartBalanceAmount - amount + depositAmount); //acc balance + deposit - withdrawal
            }
        }

        [Test]
        [TestCase(0, 0, 0, OrderSide.Sell, typeof(UnauthorizedException))]
        [TestCase(1, 0, 0, OrderSide.Sell, typeof(InvalidOperationException))]
        [TestCase(1, 1, 0, OrderSide.Sell, typeof(BadRequestException))]
        [TestCase(1, 1, 1000000000, OrderSide.Sell, typeof(BadRequestException))]
        [TestCase(1, 1, 100, OrderSide.Sell, null)]
        [TestCase(1, 1, 100, OrderSide.Buy, typeof(BadRequestException))]
        [TestCase(1, 1, 98, OrderSide.Buy, null)]
        public async Task OrderQuantumTest(int nonce, int asset, int amount, OrderSide side, Type excpectedException)
        {
            var accountWrapper = Global.AccountStorage.GetAccount(TestEnvironment.Client1KeyPair);
            var order = new OrderRequest
            {
                Account = accountWrapper.Account.Id,
                Nonce = nonce,
                Amount = amount,
                Asset = asset,
                Price = 100,
                Side = side,
                AccountWrapper = accountWrapper
            };

            var envelope = order.CreateEnvelope();
            envelope.Sign(TestEnvironment.Client1KeyPair);

            if (!Global.IsAlpha)
            {
                var quantum = new RequestQuantum { Apex = Global.QuantumStorage.CurrentApex + 1, RequestEnvelope = envelope, Timestamp = DateTime.UtcNow.Ticks };
                envelope = quantum.CreateEnvelope();
                envelope.Sign(TestEnvironment.AlphaKeyPair);
            }

            var res = await AssertQuantumHandling(envelope, excpectedException);
            if (excpectedException == null)
            {
                var currentMarket = Global.Exchange.GetMarket(asset);
                Assert.IsTrue(currentMarket != null);

                var requests = side == OrderSide.Buy ? currentMarket.Bids : currentMarket.Asks;
                Assert.AreEqual(1, requests.Count);

                Assert.AreEqual(requests.TotalAmount, amount);
                Assert.AreEqual((res.Effects.Find(e => e.EffectType == EffectTypes.OrderPlaced) as OrderPlacedEffect).QuoteAmount, requests.Volume);
            }
        }

        [Test]
        [TestCase(1, 100, OrderSide.Sell, 111, false, typeof(BadRequestException))]
        [TestCase(1, 98, OrderSide.Buy, 111, false, typeof(BadRequestException))]
        [TestCase(1, 100, OrderSide.Sell, 0, true, typeof(UnauthorizedAccessException))]
        [TestCase(1, 100, OrderSide.Sell, 0, false, null)]
        [TestCase(1, 98, OrderSide.Buy, 0, false, null)]
        public async Task OrderCancellationQuantumTest(int asset, int amount, OrderSide side, int apexMod, bool useFakeSigner, Type excpectedException)
        {
            var acc = Global.AccountStorage.GetAccount(TestEnvironment.Client1KeyPair);

            var order = new OrderRequest
            {
                Account = acc.Account.Id,
                Nonce = 1,
                Amount = amount,
                Asset = asset,
                Price = 100,
                Side = side,
                AccountWrapper = Global.AccountStorage.GetAccount(TestEnvironment.Client1KeyPair)
            };

            var envelope = order.CreateEnvelope();
            envelope.Sign(useFakeSigner ? TestEnvironment.Client2KeyPair : TestEnvironment.Client1KeyPair);

            if (!Global.IsAlpha)
            {
                var quantum = new RequestQuantum { Apex = Global.QuantumStorage.CurrentApex + 1, RequestEnvelope = envelope, Timestamp = DateTime.UtcNow.Ticks };
                envelope = quantum.CreateEnvelope();
                envelope.Sign(TestEnvironment.AlphaKeyPair);
            }

            var submitResult = await AssertQuantumHandling(envelope, excpectedException);

            var apex = ((Quantum)submitResult.OriginalMessage.Message).Apex + apexMod;

            var orderCancellation = new OrderCancellationRequest
            {
                Account = acc.Account.Id,
                Nonce = 2,
                OrderId = OrderIdConverter.Encode((ulong)apex, asset, side),
                AccountWrapper = Global.AccountStorage.GetAccount(TestEnvironment.Client1KeyPair)
            };

            envelope = orderCancellation.CreateEnvelope();
            envelope.Sign(TestEnvironment.Client1KeyPair);

            if (!Global.IsAlpha)
            {
                var quantum = new RequestQuantum { Apex = Global.QuantumStorage.CurrentApex + 1, RequestEnvelope = envelope, Timestamp = DateTime.UtcNow.Ticks };
                envelope = quantum.CreateEnvelope();
                envelope.Sign(TestEnvironment.AlphaKeyPair);
            }

            var cancelResult = await AssertQuantumHandling(envelope, excpectedException);

            if (excpectedException == null)
            {
                var currentMarket = Global.Exchange.GetMarket(asset);
                Assert.IsTrue(currentMarket != null);

                var requests = side == OrderSide.Buy ? currentMarket.Bids : currentMarket.Asks;
                Assert.AreEqual(requests.Count, 1);

                Assert.AreEqual(amount, requests.TotalAmount);
                var effect = cancelResult.Effects.Find(e => e.EffectType == EffectTypes.OrderRemoved) as OrderRemovedEffect;
                Assert.AreEqual(effect.QuoteAmount, requests.Volume);
            }
        }



        [Test]
        [TestCase(100, false, true, typeof(BadRequestException))]
        [TestCase(100, false, false, typeof(BadRequestException))]
        [TestCase(1000000000000, false, false, typeof(BadRequestException))]
        [TestCase(100, true, false, typeof(BadRequestException))]
        [TestCase(100, false, true, typeof(BadRequestException))]
        [TestCase(100, false, false, typeof(UnauthorizedAccessException))]
        [TestCase(100, false, false, null)]
        public async Task WithdrawalQuantumTest(double amount, bool hasWithdrawal, bool useFakeSigner, Type excpectedException)
        {
            var outputStream = new XdrDataOutputStream();
            var txBuilder = new TransactionBuilder(new AccountResponse(TestEnvironment.Client1KeyPair.AccountId, 1));
            txBuilder.SetFee(10_000);

            txBuilder.AddOperation(new PaymentOperation.Builder(TestEnvironment.Client1KeyPair, new AssetTypeNative(), (amount / AssetsHelper.StroopsPerAsset).ToString("0.##########", CultureInfo.InvariantCulture)).SetSourceAccount(TestEnvironment.AlphaKeyPair).Build());
            txBuilder.AddTimeBounds(new stellar_dotnet_sdk.TimeBounds(maxTime: DateTimeOffset.UtcNow.AddSeconds(60)));
            var tx = txBuilder.Build();
            stellar_dotnet_sdk.xdr.Transaction.Encode(outputStream, tx.ToXdrV1());

            var account = Global.AccountStorage.GetAccount(TestEnvironment.Client1KeyPair);
            var withdrawal = new WithdrawalRequest
            {
                Account = account.Account.Id,
                Nonce = 1,
                TransactionXdr = outputStream.ToArray(),
                AccountWrapper = account
            };

            var envelope = withdrawal.CreateEnvelope();
            envelope.Sign(useFakeSigner ? TestEnvironment.Client2KeyPair : TestEnvironment.Client1KeyPair);

            if (!Global.IsAlpha)
            {
                var quantum = new RequestQuantum { Apex = Global.QuantumStorage.CurrentApex + 1, RequestEnvelope = envelope, Timestamp = DateTime.UtcNow.Ticks };
                envelope = quantum.CreateEnvelope();
                envelope.Sign(TestEnvironment.AlphaKeyPair);
            }

            var result = await AssertQuantumHandling(envelope, excpectedException);

            if (excpectedException == null)
            {
                Assert.IsTrue(account.HasPendingWithdrawal);

                Assert.IsTrue(account.Account.GetBalance(0).Liabilities == amount);
            }
        }


        [Test]
        [TestCase(100, true, typeof(InvalidOperationException))]
        [TestCase(100, false, null)]
        public async Task WithdrawalCleanupQuantumTest(double amount, bool useFakeHash, Type excpectedException)
        {
            var outputStream = new XdrDataOutputStream();
            var txBuilder = new TransactionBuilder(new AccountResponse(TestEnvironment.Client1KeyPair.AccountId, 1));
            txBuilder.SetFee(10_000);

            txBuilder.AddOperation(new PaymentOperation.Builder(TestEnvironment.Client1KeyPair, new AssetTypeNative(), (amount / AssetsHelper.StroopsPerAsset).ToString("0.##########", CultureInfo.InvariantCulture)).SetSourceAccount(TestEnvironment.AlphaKeyPair).Build());
            txBuilder.AddTimeBounds(new stellar_dotnet_sdk.TimeBounds(maxTime: DateTimeOffset.UtcNow.AddSeconds(60)));
            var tx = txBuilder.Build();
            stellar_dotnet_sdk.xdr.Transaction.Encode(outputStream, tx.ToXdrV1());

            var account = Global.AccountStorage.GetAccount(TestEnvironment.Client1KeyPair);

            var acc = Global.AccountStorage.GetAccount(TestEnvironment.Client1KeyPair);
            var withdrawal = new WithdrawalRequest
            {
                Account = acc.Account.Id,
                Nonce = 1,
                TransactionXdr = outputStream.ToArray(),
                AccountWrapper = account
            };

            var envelope = withdrawal.CreateEnvelope();
            envelope.Sign(TestEnvironment.Client1KeyPair);

            if (!Global.IsAlpha)
            {
                var quantum = new RequestQuantum { Apex = Global.QuantumStorage.CurrentApex + 1, RequestEnvelope = envelope, Timestamp = DateTime.UtcNow.Ticks };
                envelope = quantum.CreateEnvelope();
                envelope.Sign(TestEnvironment.AlphaKeyPair);
            }

            var result = await AssertQuantumHandling(envelope, null);

            if (result.Status != ResultStatusCodes.Success)
                throw new Exception("Withdrawal creation failed.");

            var cleanup = new WithrawalsCleanupQuantum
            {
                ExpiredWithdrawal = useFakeHash ? new byte[] { } : tx.Hash(),
                Apex = Global.QuantumStorage.CurrentApex + 1
            };

            envelope = cleanup.CreateEnvelope();


            if (!Global.IsAlpha)
            {
                cleanup.Timestamp = DateTime.UtcNow.Ticks;
                envelope = cleanup.CreateEnvelope();
                envelope.Sign(TestEnvironment.AlphaKeyPair);
            }

            await AssertQuantumHandling(envelope, excpectedException);

            if (excpectedException == null)
            {
                Assert.IsTrue(!account.HasPendingWithdrawal);

                Assert.AreEqual(account.Account.GetBalance(0).Liabilities, 0);
            }
        }

        [Test]
        [TestCase(100, false, typeof(BadRequestException))]
        [TestCase(100, false, typeof(BadRequestException))]
        [TestCase(1000000000000, false, typeof(BadRequestException))]
        [TestCase(100, true, typeof(BadRequestException))]
        [TestCase(100, false, null)]
        public async Task PaymentQuantumTest(long amount, bool useFakeSigner, Type excpectedException)
        {
            var account = Global.AccountStorage.GetAccount(TestEnvironment.Client1KeyPair);

            var withdrawal = new PaymentRequest
            {
                Account = account.Account.Id,
                Nonce = 1,
                Asset = 0,
                Destination = TestEnvironment.Client2KeyPair,
                Amount = amount,
                AccountWrapper = account
            };

            var envelope = withdrawal.CreateEnvelope();
            envelope.Sign(useFakeSigner ? TestEnvironment.Client2KeyPair : TestEnvironment.Client1KeyPair);

            if (!Global.IsAlpha)
            {
                var quantum = new RequestQuantum { Apex = Global.QuantumStorage.CurrentApex + 1, RequestEnvelope = envelope, Timestamp = DateTime.UtcNow.Ticks };
                envelope = quantum.CreateEnvelope();
                envelope.Sign(TestEnvironment.AlphaKeyPair);
            }

            var expextedBalance = account.Account.GetBalance(0).Amount - amount;

            await AssertQuantumHandling(envelope, excpectedException);

            if (excpectedException == null)
            {
                Assert.AreEqual(account.Account.GetBalance(0).Amount, expextedBalance);
            }
        }

        [Test]
        [TestCase(0, typeof(UnauthorizedException))]
        [TestCase(1, null)]
        public async Task AccountDataRequestTest(int nonce, Type excpectedException)
        {
            Global.AppState.State = ApplicationState.Ready;
            var accountWrapper = Global.AccountStorage.GetAccount(TestEnvironment.Client1KeyPair);
            var order = new AccountDataRequest
            {
                Account = accountWrapper.Account.Id,
                Nonce = nonce,
                AccountWrapper = accountWrapper
            };

            var envelope = order.CreateEnvelope();
            envelope.Sign(TestEnvironment.Client1KeyPair);

            if (!Global.IsAlpha)
            {
                var quantum = new RequestQuantum { Apex = Global.QuantumStorage.CurrentApex + 1, RequestEnvelope = envelope, Timestamp = DateTime.UtcNow.Ticks };
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
                    Account = account.Account.Id,
                    Nonce = i + 1,
                    AccountWrapper = account
                }.CreateEnvelope();
                envelope.Sign(clientKeyPair);
                if (!Global.IsAlpha)
                {
                    var quantum = new RequestQuantum { Apex = Global.QuantumStorage.CurrentApex + 1, RequestEnvelope = envelope, Timestamp = DateTime.UtcNow.Ticks };
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
