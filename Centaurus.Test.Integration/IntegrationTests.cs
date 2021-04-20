using Centaurus.Controllers;
using Centaurus.DAL.Mongo;
using Centaurus.Domain;
using Centaurus.Models;
using Centaurus.SDK.Models;
using Centaurus.Stellar.Models;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Test
{
    public class IntegrationTests
    {
        IntegrationTestEnvironment environment = new IntegrationTestEnvironment();

        private async Task InitConstellation()
        {
            var assetCodes = new string[] {
                "USD",
                "EURO",
                "GOLD",
                "SILVER",
                "OIL",
                "NGAS",
                "CORN",
                "SUGAR"
            };
            var initResult = await environment.ConstellationController.Init(new ConstellationInitModel
            {
                Assets = assetCodes.Select(a => $"{a}-{environment.Issuer.AccountId}-{(a.Length > 4 ? 2 : 1)}").ToArray(),
                Auditors = environment.AuditorStartups.Select(a => a.Context.Settings.KeyPair.AccountId).ToArray(),
                MinAccountBalance = 1000,
                MinAllowedLotSize = 100,
                RequestRateLimits = new RequestRateLimitsModel { HourLimit = int.MaxValue, MinuteLimit = int.MaxValue }
            });
            var result = (ConstellationController.InitResult)((JsonResult)initResult).Value;

            Assert.IsTrue(result.IsSuccess, "Init result.");
        }

        private async Task AssertConstellationState(ApplicationState targetState)
        {
            Func<Task<bool>> func = () =>
            {
                return Task.FromResult(environment.ConstellationController.Info().State == targetState);
            };

            await AssertDuringPeriod(
                func,
                TimeSpan.FromSeconds(10000),
                $"Unable to rich {targetState} state."
            );
        }

        private async Task AssertDuringPeriod(Func<Task<bool>> predicate, TimeSpan timeout, string failMessage)
        {
            var waitStartDate = DateTime.UtcNow;
            while (DateTime.UtcNow - waitStartDate < timeout)
            {
                var result = await predicate();
                if (result)
                    return;
                Thread.Sleep(50);
            }
            Assert.Fail(failMessage);
        }

        private async Task AssertClientsCount(int clientsCount, TimeSpan timeout)
        {
            Func<Task<bool>> func = () =>
            {
                return Task.FromResult(environment.AlphaStartup.Context.AccountStorage.Count == clientsCount);
            };

            await AssertDuringPeriod(
                func,
                timeout,
                $"Client count is not equal to expected."
            );
        }

        private async Task<List<SDK.CentaurusClient>> ConnectClients(List<KeyPair> clients, SDK.Models.ConstellationInfo info)
        {

            var clientConnections = new List<SDK.CentaurusClient>();
            foreach (var client in clients)
            {
                var clientConnection = new SDK.CentaurusClient(
                    new Uri(IntegrationTestEnvironment.AlphaAddress),
                    client,
                    info,
                    environment.StellarProvider,
                    environment.GetClientConnectionWrapper
                );

                await clientConnection.Connect();

                clientConnections.Add(clientConnection);
            }
            return clientConnections;
        }

        private void AsserFinalize(MessageEnvelope resultMessage)
        {
            Assert.IsTrue(resultMessage.AreSignaturesValid(), "Signatures validation.");
            Assert.IsTrue(resultMessage.Signatures.Count >= environment.AlphaStartup.Context.GetMajorityCount(), "Majority validation.");
        }

        private void AsserResult(MessageEnvelope resultMessage, ResultStatusCodes targetResult)
        {
            if (resultMessage.Message is ResultMessage result)
            {
                Assert.AreEqual(result.Status, targetResult, "Result message status assertion.");
                return;
            }
            Assert.Fail("Specified message is not result message.");
        }

        private async Task AssertPayment(SDK.CentaurusClient client, KeyPair keyPair, int assetId, long amount)
        {
            var balance = client.AccountData.GetBalances().First(a => a.AssetId == assetId);
            var balanceAmount = balance.Amount;
            try
            {
                var result = await client.MakePayment(keyPair, amount, environment.SDKConstellationInfo.Assets.First(a => a.Id == assetId));
                AsserFinalize(result);
                AsserResult(result, ResultStatusCodes.Success);
                await AssertBalance(client, assetId, balanceAmount - amount, 0);
            }
            catch (Exception exc)
            {
                Assert.Fail(exc.Message, "Error on payment.");
            }
        }

        private async Task AssertBalance(SDK.CentaurusClient client, int assetId, long amount, long liabilities)
        {
            Func<Task<bool>> func = async () =>
            {
                await client.UpdateAccountData();
                var balance = client.AccountData.GetBalances().First(a => a.AssetId == assetId);
                return balance.Amount == amount && balance.Liabilities == liabilities;
            };

            await AssertDuringPeriod(
                func,
                TimeSpan.FromSeconds(1),
                $"Balance is not equal to expected."
            );
        }

        [Test]
        [TestCase(1)]
        //[TestCase(2)]
        //[TestCase(10)]
        public async Task BaseTest(int auditorsCount)
        {
            await environment.Init(auditorsCount);

            await InitConstellation();

            await AssertConstellationState(ApplicationState.Ready);

            var clientsCount = 1;

            environment.GenerateCliens(clientsCount);

            await AssertClientsCount(clientsCount, TimeSpan.FromSeconds(5));

            var connectedClients = await ConnectClients(environment.Clients, environment.SDKConstellationInfo);

            var client = connectedClients.First();
            await AssertPayment(client, KeyPair.Random(), 0, environment.SDKConstellationInfo.MinAccountBalance);

            await AssertClientsCount(clientsCount + 1, TimeSpan.FromSeconds(5)); //client should be created on payment
        }
    }
}