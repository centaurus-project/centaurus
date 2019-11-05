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
        [TestCase(2, 0, 0, 0, typeof(InvalidOperationException))]
        [TestCase(2, 63, 0, 0, typeof(InvalidOperationException))]
        [TestCase(2, 63, 100, 10, typeof(InvalidOperationException))]
        [TestCase(2, 63, 100, 0, null)]
        public async Task LedgerQuantumTest(int ledgerFrom, int ledgerTo, int amount, int asset, Type excpectedException)
        {
            try
            {
                var client1KeyPair = KeyPair.FromSecretSeed(TestEnvironment.Client1Secret);
                var client2KeyPair = KeyPair.FromSecretSeed(TestEnvironment.Client2Secret);
                var auditorKeyPair = KeyPair.FromSecretSeed(TestEnvironment.Auditor1Secret);

                var client1StartBalanceAmount = (long)0;
                var clientAccountBalance = Global.AccountStorage.GetAccount(client1KeyPair).GetBalance(asset);
                if (clientAccountBalance != null && amount > 0)
                {
                    client1StartBalanceAmount = clientAccountBalance.Amount;
                    clientAccountBalance.LockLiabilities(amount);//emulate lock liabilities for the withdrawal
                }

                var ledgerNotification = new LedgerUpdateNotification
                {
                    LedgerFrom = (uint)ledgerFrom,
                    LedgerTo = (uint)ledgerTo,
                    Payments = new List<PaymentBase>
                    {
                        new Deposit
                        {
                            Amount = amount,
                            Destination = client1KeyPair,
                            Asset = asset
                        },
                        new Withdrawal
                        {
                            Amount = amount,
                            Destination = client2KeyPair,
                            Source = client1KeyPair,
                            Asset = asset,
                            PaymentResult = PaymentResults.Success
                        }
                    }
                };
                var ledgerNotificationEnvelope = ledgerNotification.CreateEnvelope();
                ledgerNotificationEnvelope.Sign(auditorKeyPair);

                var ledgerCommit = new LedgerCommitQuantum
                {
                    Source = ledgerNotificationEnvelope,
                    Apex = 1
                };

                await Global.QuantumHandler.HandleAsync(ledgerCommit.CreateEnvelope());

                Assert.AreEqual(Global.LedgerManager.Ledger, ledgerNotification.LedgerTo);

                var account1 = Global.AccountStorage.GetAccount(client1KeyPair);

                Assert.AreEqual(account1.GetBalance(asset).Liabilities, 0);
                Assert.AreEqual(account1.GetBalance(asset).Amount, client1StartBalanceAmount); //acc balance + deposit - withdrawal
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
        [TestCase(1, 1, 0, OrderSides.Sell, typeof(ArgumentException))]
        [TestCase(1, 1, 1000000000, OrderSides.Sell, typeof(BadRequestException))]
        [TestCase(1, 1, 10000, OrderSides.Sell, null)]
        [TestCase(1, 1, 10000, OrderSides.Buy, null)]
        public async Task OrderQuantumTest(int nonce, int asset, int amount, OrderSides side, Type excpectedException)
        {
            try
            {
                var clientKeyPair = KeyPair.FromSecretSeed(TestEnvironment.Client1Secret);
                var order = new OrderRequest
                {
                    Account = clientKeyPair,
                    Nonce = (ulong)nonce,
                    Amount = amount,
                    Asset = asset,
                    Price = 100,
                    Side = side
                };

                var envelope = order.CreateEnvelope();

                if (!Global.IsAlpha)
                {
                    var quantum = new RequestQuantum { Apex = 1, RequestEnvelope = envelope };
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
