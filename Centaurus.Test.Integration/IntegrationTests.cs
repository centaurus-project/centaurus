using Centaurus.Domain;
using Centaurus.Models;
using Centaurus.Xdr;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Test
{
    public class IntegrationTests
    {
        [Test]
        [Explicit]
        [TestCase(2, 0)]
        [TestCase(10, 0)]
        [TestCase(2, 10)]
        [TestCase(2, 100)]
        [TestCase(3, 100)]
        [TestCase(3, 10)]
        [TestCase(10, 10)]
        public async Task BaseTest(int auditorsCount, int clientsCount)
        {
            ThreadPool.SetMinThreads(1000, 1000);

            TestContext.Out.WriteLine("BaseTest started");
            var environment = new IntegrationTestEnvironment();

            await environment.PrepareConstellation(auditorsCount, clientsCount);

            var connectedClients = await environment.ConnectClients(environment.Clients, environment.SDKConstellationInfo);

            if (connectedClients.Count > 0)
            {
                var client = connectedClients.First();
                await environment.AssertPayment(
                    client,
                    KeyPair.Random(),
                    environment.SDKConstellationInfo.QuoteAsset.Code,
                    environment.SDKConstellationInfo.MinAccountBalance
                );

                await environment.AssertClientsCount(clientsCount + 1, TimeSpan.FromSeconds(15)); //client should be created on payment

                await environment.AssertWithdrawal(
                    client,
                    environment.AlphaWrapper.ProviderFactory.Provider.Id,
                    client.Config.ClientKeyPair,
                    environment.SDKConstellationInfo.QuoteAsset.Code,
                    1
                );
            }

            environment.Dispose();
        }

        [Test]
        [Explicit]
        public async Task AuditorRestartTest()
        {
            var environment = new IntegrationTestEnvironment();

            await environment.PrepareConstellation(3, 0);

            var auditorStartup = environment.AuditorWrappers.Values.Skip(1).First(); //first is Alpha
            auditorStartup.Shutdown();

            Assert.AreEqual(1, environment.AlphaWrapper.Context.NodesManager.GetRemoteNodes().Count(n => n.State != State.Undefined), "Auditors count assertion.");
            await environment.AssertConstellationState(TimeSpan.FromSeconds(5), State.Ready);

            var clientsCount = 100;
            environment.GenerateCliens(clientsCount);

            await environment.AssertClientsCount(clientsCount, TimeSpan.FromSeconds(15));

            auditorStartup.Run(environment.AuditorWrappers);

            await IntegrationTestEnvironmentExtensions.AssertState(auditorStartup.Startup, State.Ready, TimeSpan.FromSeconds(10));
            await IntegrationTestEnvironmentExtensions.AssertDuringPeriod(
                () =>
                {
                    return Task.FromResult(auditorStartup.Context.QuantumHandler.CurrentApex == environment.AlphaWrapper.Context.QuantumHandler.CurrentApex);
                },
                TimeSpan.FromSeconds(5),
                "Apexes are not equal"
            );
        }


        [Test]
        [Explicit]
        public async Task AlphaRestartTest()
        {
            var environment = new IntegrationTestEnvironment();

            await environment.PrepareConstellation(3, 0);

            environment.AlphaWrapper.Shutdown();

            await Task.WhenAll(environment.AuditorWrappers.Select(a => IntegrationTestEnvironmentExtensions.AssertState(a.Value.Startup, State.Running, TimeSpan.FromSeconds(10))));

            environment.AlphaWrapper.Run(environment.AuditorWrappers);

            await environment.AssertConstellationState(TimeSpan.FromSeconds(15), State.Ready);

            await Task.WhenAll(environment.AuditorWrappers.Select(a => IntegrationTestEnvironmentExtensions.AssertState(a.Value.Startup, State.Ready, TimeSpan.FromSeconds(10))));
        }

        [Test]
        [Explicit]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public async Task AlphaRestartWithQuantaDelayTest(bool invalidHash, bool invalidClientSignature)
        {
            var environment = new IntegrationTestEnvironment();

            await environment.PrepareConstellation(1, 3);

            var lastApex = environment.AlphaWrapper.Context.QuantumHandler.CurrentApex;
            var lastHash = environment.AlphaWrapper.Context.QuantumHandler.LastQuantumHash;

            var clientPk = environment.Clients.First();
            var client = environment.AlphaWrapper.Context.AccountStorage.GetAccount(clientPk);

            //wait while all auditors will process all available quanta
            await environment.AssertConstellationApex(lastApex, TimeSpan.FromSeconds(5));

            //generate quantum that will not be processed by Alpha
            var request = new AccountDataRequest
            {
                Account = client.Pubkey,
                RequestId = DateTime.UtcNow.Ticks
            }
                .CreateEnvelope()
                .Sign(clientPk);

            var quantum = new ClientRequestQuantum
            {
                Apex = lastApex + 1,
                PrevHash = lastHash,
                RequestEnvelope = request,
                Timestamp = DateTime.UtcNow.Ticks
            };

            var result = await environment.ProcessQuantumIsolated(quantum);

            environment.AlphaWrapper.Shutdown();

            await Task.WhenAll(environment.AuditorWrappers.Select(a => IntegrationTestEnvironmentExtensions.AssertState(a.Value.Startup, State.Running, TimeSpan.FromSeconds(10))));

            //handle quantum
            await Task.WhenAll(environment.AuditorWrappers.Select(a =>
            {
                var rawQuantum = quantum.ToByteArray();
                var auditorsQuantum = XdrConverter.Deserialize<Quantum>(rawQuantum);
                return a.Value.Context.QuantumHandler.HandleAsync(auditorsQuantum, Task.FromResult(true)).OnProcessed;
            }));

            //change quantum
            environment.AuditorWrappers.Values.ToList().ForEach(a =>
            {
                var quanta = a.Context.SyncStorage.GetQuanta(lastApex, 1);
                var quantum = quanta.First();
                if (invalidHash)
                    ((Quantum)quantum.Quantum).Timestamp = DateTime.UtcNow.Ticks;
                if (invalidClientSignature)
                {
                    var request = (ClientRequestQuantum)quantum.Quantum;
                    ((MessageEnvelope)request.RequestEnvelope).Signature = new TinySignature { Data = new byte[64] };
                    request.RequestEnvelope.Sign(KeyPair.Random());
                }
                //if (invalidAlphaSignature)
                //{
                //    quantum.Quantum.Signatures.Clear();
                //    quantum.Quantum.Sign(KeyPair.Random());
                //}
            });

            environment.AlphaWrapper.Run(environment.AuditorWrappers);

            var expectedState = invalidHash || invalidClientSignature ? State.Failed : State.Ready;

            await IntegrationTestEnvironmentExtensions.AssertState(environment.AlphaWrapper.Startup, expectedState, TimeSpan.FromSeconds(30));

            if (expectedState == State.Failed)
                return;

            await Task.WhenAll(environment.AuditorWrappers.Select(a => IntegrationTestEnvironmentExtensions.AssertState(a.Value.Startup, State.Ready, TimeSpan.FromSeconds(10))));

            await environment.AssertConstellationApex(lastApex + 1, TimeSpan.FromSeconds(5));
        }

        [Test]
        [Explicit]
        [TestCase(false, false, false)]
        [TestCase(true, false, false)]
        [TestCase(false, true, false)]
        [TestCase(false, false, true)]
        public async Task ScamQuantaTest(bool useFakeClient, bool useFakeAlpha, bool invalidBalance)
        {
            var environment = new IntegrationTestEnvironment();

            await environment.PrepareConstellation(3, 1);

            var clientPk = environment.Clients.First();
            var client = environment.AlphaWrapper.Context.AccountStorage.GetAccount(clientPk);
            var syncStorage = environment.AlphaWrapper.Context.SyncStorage;

            EnvironmentHelper.SetTestEnvironmentVariable();

            var amount = invalidBalance
                ? client.GetBalance(environment.SDKConstellationInfo.QuoteAsset.Code).Amount + 1
                : environment.AlphaWrapper.Context.ConstellationSettingsManager.Current.MinAllowedLotSize + 1;
            var sqamRequest = new OrderRequest
            {
                Account = client.Pubkey,
                Amount = amount,
                Price = 1,
                Asset = environment.AlphaWrapper.Context.ConstellationSettingsManager.Current.Assets[1].Code,
                RequestId = 1,
                Side = OrderSide.Buy
            }.CreateEnvelope().Sign(useFakeClient ? KeyPair.Random() : clientPk);

            var apex = environment.AlphaWrapper.Context.QuantumHandler.CurrentApex + 1;
            var lastQuantumHash = environment.AlphaWrapper.Context.QuantumHandler.LastQuantumHash;
            var requestQuantum = new ClientRequestQuantum
            {
                Apex = apex,
                EffectsProof = new byte[] { },
                PrevHash = lastQuantumHash,
                RequestEnvelope = sqamRequest,
                Timestamp = DateTime.UtcNow.Ticks
            };

            syncStorage.AddQuantum(new SyncQuantaBatchItem { Quantum = requestQuantum });

            var expectedState = useFakeClient || useFakeAlpha || invalidBalance ? State.Failed : State.Ready;

            if (expectedState == State.Ready)
                await environment.AssertConstellationApex(apex, TimeSpan.FromSeconds(10));

            await Task.WhenAll(environment.AuditorWrappers.Select(a => IntegrationTestEnvironmentExtensions.AssertState(a.Value.Startup, expectedState, TimeSpan.FromSeconds(10))));
        }
    }
}
