using Centaurus.Controllers;
using Centaurus.Domain.Models;
using Centaurus.Models;
using Centaurus.NetSDK;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
                "SLVR",
                "OIL",
                "NGAS",
                "CORN",
                "SGR"
            };

            var assets = new List<AssetSettings>();
            for (var i = 0; i < assetCodes.Length; i++)
                assets.Add(new AssetSettings { Code = assetCodes[i] });

            var auditors = environment.AuditorWrappers.Select(a =>
                new Auditor
                {
                    PubKey = a.Value.Settings.KeyPair,
                    Address = a.Key
                }).ToList();

            var providers = new List<ProviderSettings> {
                new ProviderSettings {
                    Provider = "mock",
                    InitCursor = "0",
                    Name = "test",
                    PaymentSubmitDelay = 0,
                    Vault = KeyPair.Random().AccountId,
                    Assets = assets.Select(a => new ProviderAsset { CentaurusAsset = a.Code, IsVirtual = true, Token = a.Code }).ToList()
                }
            };

            var constellationInit = new ConstellationUpdate
            {
                Assets = assets,
                Auditors = auditors,
                Providers = providers,
                MinAccountBalance = 1000,
                MinAllowedLotSize = 100,
                RequestRateLimits = new RequestRateLimits { HourLimit = int.MaxValue, MinuteLimit = int.MaxValue },
                Alpha = environment.AlphaWrapper.Settings.KeyPair
            }.CreateEnvelope<ConstellationMessageEnvelope>();

            foreach (var a in environment.AuditorWrappers.Values)
                constellationInit.Sign(a.Context.Settings.KeyPair);

            var res = await environment.AlphaWrapper.ConstellationController.Init(constellationInit);

            var result = (ConstellationController.InitResult)((JsonResult)res).Value;

            Assert.IsTrue(result.IsSuccess, "Init result.");
        }

        public static async Task AssertConstellationState(this IntegrationTestEnvironment environment, TimeSpan timeOut, params State[] validStates)
        {
            Func<Task<bool>> func = () =>
            {
                var state = environment.AlphaWrapper.ConstellationController.Info().State;
                Debug.WriteLine(state);
                return Task.FromResult(validStates.Contains(state));
            };

            await AssertDuringPeriod(
                func,
                timeOut,
                $"Unable to reach {string.Join(',', validStates.Select(s => s.ToString()))} state."
            );
        }

        public static async Task AssertState(Startup startup, State targetState, TimeSpan timeOut)
        {
            Func<Task<bool>> func = () =>
            {
                TestContext.Out.WriteLine(startup.Context.StateManager.State);
                return Task.FromResult(startup.Context.StateManager.State == targetState);
            };

            await AssertDuringPeriod(
                func,
                timeOut,
                $"Unable to reach auditor {targetState} state."
            );
        }

        public static async Task AssertConstellationApex(this IntegrationTestEnvironment environment, ulong apex, TimeSpan timeOut)
        {
            Func<Task<bool>> func = () =>
            {
                return Task.FromResult(
                    environment.AlphaWrapper.Context.QuantumStorage.CurrentApex == apex
                    && environment.AuditorWrappers.Values.All(a => a.Context.QuantumStorage.CurrentApex == apex)
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

        public static async Task<List<CentaurusClient>> ConnectClients(this IntegrationTestEnvironment environment, List<KeyPair> clients, NetSDK.ConstellationInfo info)
        {
            var connectionFactory = new MockOutgoingConnectionFactory(environment.AuditorWrappers); 
            var clientConnections = new List<CentaurusClient>();
            var address = environment.AuditorWrappers.First().Key;
            foreach (var client in clients)
            {
                var clientConfig = new MockClientConstellationConfig(address,
                    client.SeedBytes, connectionFactory, environment.SDKConstellationInfo);
                var clientConnection = new CentaurusClient(clientConfig);

                await clientConnection.Connect();

                clientConnections.Add(clientConnection);
            }
            return clientConnections;
        }

        public static async Task AssertPayment(this IntegrationTestEnvironment environment, CentaurusClient client, KeyPair keyPair, string asset, ulong amount)
        {
            var balance = client.AccountState.GetBalances().First(a => a.Asset == asset);
            var balanceAmount = balance.Amount;
            try
            {
                var result = await client.Pay(keyPair.PublicKey, asset, amount);
                await result.OnAcknowledged;
                await AssertBalance(client, asset, balanceAmount - amount, 0);
            }
            catch (Exception exc)
            {
                Assert.Fail(exc.Message, "Error on payment.");
            }
        }

        public static async Task AssertWithdrawal(this IntegrationTestEnvironment environment, CentaurusClient client, string provider, KeyPair keyPair, string asset, ulong amount)
        {
            var balance = client.AccountState.GetBalances().First(a => a.Asset == asset);
            var balanceAmount = balance.Amount;
            try
            {
                var result = await client.Withdraw(provider, keyPair.PublicKey, asset, amount);
                await AssertBalance(client, asset, balanceAmount - amount, 0);
            }
            catch (Exception exc)
            {
                Assert.Fail(exc.Message, "Error on withdrawal.");
            }
        }

        public static async Task AssertBalance(CentaurusClient client, string asset, ulong amount, ulong liabilities)
        {
            Func<Task<bool>> func = async () =>
            {
                await client.UpdateAccountData();
                var balance = client.AccountState.GetBalances().First(a => a.Asset == asset);
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

            environment.RunAuditors();

            await environment.InitConstellation();

            await environment.AssertConstellationState(TimeSpan.FromSeconds(5), State.Running, State.Ready);

            await environment.AssertConstellationState(TimeSpan.FromSeconds(5), State.Ready);

            environment.GenerateCliens(clientsCount);

            await environment.AssertClientsCount(clientsCount, TimeSpan.FromSeconds(5));
        }

        public static async Task<QuantumResultMessageBase> ProcessQuantumIsolated(this IntegrationTestEnvironment environment, Quantum quantum)
        {
            var context = new Domain.ExecutionContext(environment.AlphaWrapper.Context.Settings, new MockStorage(), new MockPaymentProviderFactory(), new MockOutgoingConnectionFactory(new Dictionary<string, StartupWrapper>()));

            //wait while all pending updates will be saved
            while (environment.AlphaWrapper.Context.DataProvider.GetLastApex() != environment.AlphaWrapper.Context.QuantumStorage.CurrentApex)
                Thread.Sleep(100);

            context.Setup(environment.AlphaWrapper.Context.DataProvider.GetSnapshot(environment.AlphaWrapper.Context.QuantumStorage.CurrentApex));

            var messageType = quantum.GetType().Name;
            var account = default(Account);
            if (quantum is RequestQuantum requestQuantum)
            {
                messageType = requestQuantum.RequestMessage.GetMessageType();
                account = context.AccountStorage.GetAccount(requestQuantum.RequestMessage.Account);
            }

            context.QuantumProcessor.TryGetValue(messageType, out var processor);

            var processContext = processor.GetContext(quantum, account);

            var res = await processor.Process(processContext);

            return res;
        }



        public static async Task<QuantumResultMessageBase> ProcessQuantumWithoutValidation(Domain.ExecutionContext context, Quantum quantum)
        {
            var messageType = quantum.GetType().Name;
            var account = default(Account);
            if (quantum is RequestQuantum requestQuantum)
            {
                messageType = requestQuantum.RequestMessage.GetMessageType();
                account = context.AccountStorage.GetAccount(requestQuantum.RequestMessage.Account);
            }

            context.QuantumProcessor.TryGetValue(messageType, out var processor);

            var processContext = processor.GetContext(quantum, account);

            var res = await processor.Process(processContext);

            context.QuantumStorage.AddQuantum(new PendingQuantum { 
                Quantum = quantum, 
                Signatures = new List<AuditorSignatureInternal>() }, 
            quantum.ComputeHash());

            return res;
        }
    }
}
