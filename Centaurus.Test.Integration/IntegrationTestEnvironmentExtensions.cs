using Centaurus.Controllers;
using Centaurus.DAL;
using Centaurus.Domain;
using Centaurus.Models;
using Centaurus.SDK.Models;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Test
{
    public static class IntegrationTestEnvironmentExtensions
    {
        public static async Task InitConstellation(this IntegrationTestEnvironment environment)
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

            var assets = new List<AssetSettings>();
            for (var i = 0; i < assetCodes.Length; i++)
                assets.Add(new AssetSettings { Id = i + 1, Code = assetCodes[i], Issuer = environment.Issuer });

            var auditors = new List<KeyPair> { environment.AlphaWrapper.Settings.KeyPair };
            auditors.AddRange(environment.AuditorWrappers.Select(a => a.Settings.KeyPair));
            var constellationInit = new ConstellationInitRequest
            {
                Assets = assets,
                Auditors = auditors.Select(a => (RawPubKey)a).ToList(),
                Cursors = new List<PaymentCursor> { new PaymentCursor { Cursor = "0", Provider = PaymentProvider.Stellar } },
                MinAccountBalance = 1000,
                MinAllowedLotSize = 100,
                RequestRateLimits = new RequestRateLimits { HourLimit = int.MaxValue, MinuteLimit = int.MaxValue },
                Vaults = new List<Vault> { environment.Vault }
            }.CreateEnvelope();

            foreach (var a in auditors)
                constellationInit.Sign(a);

            var res = await environment.AlphaWrapper.ConstellationController.Init(constellationInit);

            var result = (ConstellationController.InitResult)((JsonResult)res).Value;

            Assert.IsTrue(result.IsSuccess, "Init result.");
        }

        public static async Task AssertConstellationState(this IntegrationTestEnvironment environment, State targetState, TimeSpan timeOut)
        {
            Func<Task<bool>> func = () =>
            {
                var state = environment.AlphaWrapper.ConstellationController.Info().State;
                Debug.WriteLine(state);
                return Task.FromResult(state == targetState);
            };

            await AssertDuringPeriod(
                func,
                timeOut,
                $"Unable to rich {targetState} state."
            );
        }

        public static async Task AssertState(StartupBase startup, State targetState, TimeSpan timeOut)
        {
            Func<Task<bool>> func = () =>
            {
                TestContext.Out.WriteLine((object)startup.Context.AppState.State);
                return Task.FromResult(startup.Context.AppState.SetState(= targetState);
            };

            await AssertDuringPeriod(
                func,
                timeOut,
                $"Unable to rich auditor {targetState} state."
            );
        }

        public static async Task AssertConstellationApex(this IntegrationTestEnvironment environment, long apex, TimeSpan timeOut)
        {
            Func<Task<bool>> func = () =>
            {
                return Task.FromResult(
                    environment.AlphaWrapper.Context.QuantumStorage.CurrentApex == apex
                    && environment.AuditorWrappers.All(a => a.Context.QuantumStorage.CurrentApex == apex)
                );
            };

            await AssertDuringPeriod(
                func,
                timeOut,
                $"Apexes are not equal to specified."
            );
        }

        public static async Task AssertDuringPeriod(Func<Task<bool>> predicate, TimeSpan timeout, string failMessage)
        {
            var waitStartDate = DateTime.UtcNow;
            var counter = 0;
            while (DateTime.UtcNow - waitStartDate < timeout)
            {
                var result = await predicate();
                if (result)
                    return;
                Thread.Sleep(50);
                counter++;
            }
            Assert.Fail(failMessage);
        }

        public static async Task AssertClientsCount(this IntegrationTestEnvironment environment, int clientsCount, TimeSpan timeout)
        {
            Func<Task<bool>> func = () =>
            {
                return Task.FromResult(environment.AlphaWrapper.Startup.Context.AccountStorage.Count == clientsCount);
            };

            await AssertDuringPeriod(
                func,
                timeout,
                $"Client count {environment.AlphaWrapper.Startup.Context.AccountStorage.Count} is not equal to expected {clientsCount}."
            );
        }

        public static async Task<List<SDK.CentaurusClient>> ConnectClients(this IntegrationTestEnvironment environment, List<KeyPair> clients, SDK.Models.ConstellationInfo info)
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

        public static void AssertFinalize(this IntegrationTestEnvironment environment, MessageEnvelope resultMessage)
        {
            if (resultMessage.Message.MessageType != MessageTypes.ITransactionResultMessage)
                Assert.IsTrue(resultMessage.AreSignaturesValid(), "Signatures validation.");
            Assert.IsTrue(resultMessage.Signatures.Count >= environment.AlphaWrapper.Context.GetMajorityCount(), "Majority validation.");
        }

        private static void AsserResult(MessageEnvelope resultMessage, ResultStatusCodes targetResult)
        {
            if (resultMessage.Message is ResultMessage result)
            {
                Assert.AreEqual(result.Status, targetResult, "Result message status assertion.");
                return;
            }
            Assert.Fail("Specified message is not result message.");
        }

        public static async Task AssertPayment(this IntegrationTestEnvironment environment, SDK.CentaurusClient client, KeyPair keyPair, int assetId, long amount)
        {
            var balance = client.AccountData.GetBalances().First(a => a.AssetId == assetId);
            var balanceAmount = balance.Amount;
            try
            {
                var result = await client.MakePayment(keyPair, amount, environment.SDKConstellationInfo.Assets.First(a => a.Id == assetId));
                environment.AssertFinalize(result);
                AsserResult(result, ResultStatusCodes.Success);
                await AssertBalance(client, assetId, balanceAmount - amount, 0);
            }
            catch (Exception exc)
            {
                Assert.Fail(exc.Message, "Error on payment.");
            }
        }

        public static async Task AssertWithdrawal(this IntegrationTestEnvironment environment, SDK.CentaurusClient client, KeyPair keyPair, int assetId, string amount)
        {
            var balance = client.AccountData.GetBalances().First(a => a.AssetId == assetId);
            var balanceAmount = balance.Amount;
            try
            {
                var result = await client.Withdrawal(keyPair, amount, environment.SDKConstellationInfo.Assets.First(a => a.Id == assetId));
                environment.AssertFinalize(result);
                AsserResult(result, ResultStatusCodes.Success);
                await AssertBalance(client, assetId, balanceAmount - Amount.ToXdr(amount), 0);
            }
            catch (Exception exc)
            {
                Assert.Fail(exc.Message, "Error on withdrawal.");
            }
        }

        public static async Task AssertBalance(SDK.CentaurusClient client, int assetId, long amount, long liabilities)
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


        public static async Task PrepareConstellation(this IntegrationTestEnvironment environment, int auditorsCount, int clientsCount)
        {
            environment.Init(auditorsCount);

            await environment.RunAlpha();

            await environment.InitConstellation();

            await environment.AssertConstellationState(State.Running, TimeSpan.FromSeconds(5));

            await environment.RunAuditors();

            await environment.AssertConstellationState(State.Ready, TimeSpan.FromSeconds(5));

            environment.GenerateCliens(clientsCount);

            await environment.AssertClientsCount(clientsCount, TimeSpan.FromSeconds(150));
        }

        public static async Task<(ResultMessage result, byte[] effectsHash, EffectProcessorsContainer container)> ProcessQuantumIsolated(this IntegrationTestEnvironment environment, MessageEnvelope envelope)
        {
            var context = new Domain.ExecutionContext(environment.AlphaWrapper.Context.Settings, new MockStorage(), environment.AlphaWrapper.Context.StellarDataProvider);

            await context.Init();

            //wait while all pending updates will be saved
            while (await environment.AlphaWrapper.Context.PersistenceManager.GetLastApex() != environment.AlphaWrapper.Context.QuantumStorage.CurrentApex)
                Thread.Sleep(100);
            await context.Setup(await environment.AlphaWrapper.Context.PersistenceManager.GetSnapshot(environment.AlphaWrapper.Context.QuantumStorage.CurrentApex));

            var messageType = envelope.Message.MessageType;
            if (messageType == MessageTypes.RequestQuantum)
                messageType = ((RequestQuantum)envelope.Message).RequestMessage.MessageType;

            context.QuantumProcessor.TryGetValue(messageType, out var processor);

            var container = new EffectProcessorsContainer(context, envelope, new DiffObject());
            var processContext = processor.GetContext(container);

            var res = await processor.Process(processContext);

            var effectsHash = new EffectsContainer { Effects = container.Effects }.ComputeHash();

            return (res, effectsHash, container);
        }



        public static async Task<(ResultMessage result, byte[] effectsHash, EffectProcessorsContainer container)> ProcessQuantumWithoutValidation(Domain.ExecutionContext context, MessageEnvelope envelope)
        {
            var messageType = envelope.Message.MessageType;
            if (messageType == MessageTypes.RequestQuantum)
            {
                var request = (RequestQuantum)envelope.Message;
                messageType = request.RequestMessage.MessageType;
                request.RequestMessage.AccountWrapper = context.AccountStorage.GetAccount(request.RequestMessage.Account);
            }

            context.QuantumProcessor.TryGetValue(messageType, out var processor);

            var container = new EffectProcessorsContainer(context, envelope, new DiffObject());
            var processContext = processor.GetContext(container);

            var res = await processor.Process(processContext);

            context.QuantumStorage.AddQuantum(envelope, envelope.ComputeHash());

            var effectsHash = new EffectsContainer { Effects = container.Effects }.ComputeHash();

            return (res, effectsHash, container);
        }
    }
}
