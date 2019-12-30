using Centaurus.Domain;
using Centaurus.Models;
using NUnit.Framework;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Text;
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
            try
            {
                long apex = Global.QuantumStorage.CurrentApex;

                var client1StartBalanceAmount = (long)0;
                var clientAccountBalance = Global.AccountStorage.GetAccount(TestEnvironment.Client1KeyPair).GetBalance(asset);

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
                        TransactionXdr = txXdr
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

                await Global.QuantumHandler.HandleAsync(ledgerCommit.CreateEnvelope());

                Assert.AreEqual(Global.LedgerManager.Ledger, ledgerNotification.LedgerTo);

                var account1 = Global.AccountStorage.GetAccount(TestEnvironment.Client1KeyPair);

                Assert.AreEqual(account1.GetBalance(asset).Liabilities, 0);
                Assert.AreEqual(account1.GetBalance(asset).Amount, client1StartBalanceAmount - amount + depositeAmount); //acc balance + deposit - withdrawal
            }
            catch (Exception exc)
            {
                //throw if we don't expect this type of exception
                if (excpectedException == null || excpectedException != exc.GetType())
                    throw;
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
            try
            {
                var order = new OrderRequest
                {
                    Account = TestEnvironment.Client1KeyPair,
                    Nonce = (ulong)nonce,
                    Amount = amount,
                    Asset = asset,
                    Price = 100,
                    Side = side
                };

                var envelope = order.CreateEnvelope();

                if (!Global.IsAlpha)
                {
                    var quantum = new RequestQuantum { Apex = Global.QuantumStorage.CurrentApex + 1, RequestEnvelope = envelope };
                    envelope = quantum.CreateEnvelope();
                }

                await Global.QuantumHandler.HandleAsync(envelope);

                var currentMarket = Global.Exchange.GetMarket(asset);
                Assert.IsTrue(currentMarket != null);

                var requests = side == OrderSides.Buy ? currentMarket.Bids : currentMarket.Asks;
                Assert.AreEqual(requests.Count, 1);

                Assert.AreEqual(requests.TotalAmount, amount);
                Assert.AreEqual(requests.Volume, amount * order.Price);

            }
            catch (Exception exc)
            {
                //throw if we don't expect this type of exception
                if (excpectedException == null || excpectedException != exc.GetType())
                    throw;
            }
        }
    }
}
