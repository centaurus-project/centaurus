using Centaurus.Controllers;
using Centaurus.DAL;
using Centaurus.DAL.Mongo;
using Centaurus.Domain;
using Centaurus.Models;
using Centaurus.SDK.Models;
using Centaurus.Stellar.Models;
using Centaurus.Xdr;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Test
{
    public class IntegrationTests
    {
        [Test]
        [Explicit]
        [TestCase(2, 0)]
        [TestCase(2, 100)]
        [TestCase(3, 100)]
        [TestCase(10, 10)]
        public async Task BaseTest(int auditorsCount, int clientsCount)
        {
            var environment = new IntegrationTestEnvironment();

            await environment.PrepareConstellation(auditorsCount, clientsCount);

            var connectedClients = await environment.ConnectClients(environment.Clients, environment.SDKConstellationInfo);

            if (connectedClients.Count > 0)
            {
                var client = connectedClients.First();
                await environment.AssertPayment(client, KeyPair.Random(), 0, environment.SDKConstellationInfo.MinAccountBalance);

                await environment.AssertClientsCount(clientsCount + 1, TimeSpan.FromSeconds(15)); //client should be created on payment

                await environment.AssertWithdrawal(client, client.KeyPair, 0, 1.ToString());
            }

            environment.Dispose();
        }

        [Test]
        [Explicit]
        public async Task AuditorRestartTest()
        {
            var environment = new IntegrationTestEnvironment();

            await environment.PrepareConstellation(3, 0);

            var auditorStartup = environment.AuditorWrappers.First();
            await auditorStartup.Shutdown();

            Assert.AreEqual(1, environment.AlphaWrapper.Context.AppState.ConnectedAuditorsCount, "Auditors count assertion.");
            await environment.AssertConstellationState(State.Ready, TimeSpan.FromSeconds(5));

            var clientsCount = 100;
            environment.GenerateCliens(clientsCount);

            await environment.AssertClientsCount(clientsCount, TimeSpan.FromSeconds(15));

            await auditorStartup.Run();

            await IntegrationTestEnvironmentExtensions.AssertState(auditorStartup.Startup, State.Ready, TimeSpan.FromSeconds(10));
            await IntegrationTestEnvironmentExtensions.AssertDuringPeriod(
                () =>
                {
                    return Task.FromResult(auditorStartup.Context.QuantumStorage.CurrentApex == environment.AlphaWrapper.Context.QuantumStorage.CurrentApex);
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

            await environment.AlphaWrapper.Shutdown();

            await Task.WhenAll(environment.AuditorWrappers.Select(a => IntegrationTestEnvironmentExtensions.AssertState(a.Startup, State.Running, TimeSpan.FromSeconds(10))));

            await environment.AlphaWrapper.Run();

            await environment.AssertConstellationState(State.Ready, TimeSpan.FromSeconds(15));

            await Task.WhenAll(environment.AuditorWrappers.Select(a => IntegrationTestEnvironmentExtensions.AssertState(a.Startup, State.Ready, TimeSpan.FromSeconds(10))));
        }

        [Test]
        [Explicit]
        [TestCase(false, false, false)]
        [TestCase(true, false, false)]
        [TestCase(false, true, false)]
        [TestCase(false, false, true)]
        public async Task AlphaRestartWithQuantaDelayTest(bool invalidHash, bool invalidClientSignature, bool invalidAlphaSignature)
        {
            var environment = new IntegrationTestEnvironment();

            await environment.PrepareConstellation(1, 3);

            var lastApex = environment.AlphaWrapper.Context.QuantumStorage.CurrentApex;
            var lastHash = environment.AlphaWrapper.Context.QuantumStorage.LastQuantumHash;

            var clientPk = environment.Clients.First();
            var client = environment.AlphaWrapper.Context.AccountStorage.GetAccount(clientPk);

            //wait while all auditors will process all available quanta
            await environment.AssertConstellationApex(lastApex, TimeSpan.FromSeconds(5));

            //generate quantum that will not be processed by Alpha
            var request = new AccountDataRequest
            {
                Account = client.Id,
                RequestId = DateTime.UtcNow.Ticks
            }
                .CreateEnvelope()
                .Sign(clientPk);

            var quantum = new RequestQuantum
            {
                Apex = lastApex + 1,
                PrevHash = lastHash,
                RequestEnvelope = request,
                Timestamp = DateTime.UtcNow.Ticks
            };
            var quantumEnvelope = quantum
                .CreateEnvelope();

            var result = await environment.ProcessQuantumIsolated(quantumEnvelope);

            quantum.EffectsHash = result.effectsHash;
            quantumEnvelope.Sign(environment.AlphaWrapper.Settings.KeyPair);

            await environment.AlphaWrapper.Shutdown();

            await Task.WhenAll(environment.AuditorWrappers.Select(a => IntegrationTestEnvironmentExtensions.AssertState(a.Startup, State.Running, TimeSpan.FromSeconds(10))));

            //handle quantum
            await Task.WhenAll(environment.AuditorWrappers.Select(a =>
            {
                var rawQuantum = quantumEnvelope.ToByteArray();
                var auditorsQuantum = XdrConverter.Deserialize<MessageEnvelope>(rawQuantum);
                return a.Context.QuantumHandler.HandleAsync(auditorsQuantum);
            }));

            //change quantum
            environment.AuditorWrappers.ForEach(a =>
            {
                a.Context.QuantumStorage.GetQuantaBacth(lastApex + 1, 1, out var quanta);
                var quantum = quanta.First();
                if (invalidHash)
                    ((Quantum)quantum.Message).Timestamp = DateTime.UtcNow.Ticks;
                if (invalidClientSignature)
                {
                    var request = (RequestQuantum)quantum.Message;
                    request.RequestEnvelope.Signatures.Clear();
                    request.RequestEnvelope.Sign(KeyPair.Random());
                }
                if (invalidAlphaSignature)
                {
                    quantum.Signatures.Clear();
                    quantum.Sign(KeyPair.Random());
                }
            });

            await environment.AlphaWrapper.Run();

            var expectedState = invalidHash || invalidClientSignature || invalidAlphaSignature ? State.Failed : State.Ready;

            await IntegrationTestEnvironmentExtensions.AssertState(environment.AlphaWrapper.Startup, expectedState, TimeSpan.FromSeconds(30));

            if (expectedState == State.Failed)
                return;

            await Task.WhenAll(environment.AuditorWrappers.Select(a => IntegrationTestEnvironmentExtensions.AssertState(a.Startup, State.Ready, TimeSpan.FromSeconds(10))));

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
            var quantaStorage = environment.AlphaWrapper.Context.QuantumStorage;

            EnvironmentHelper.SetTestEnvironmentVariable();

            var amount = invalidBalance
                ? client.Account.Balances[0].Amount + 1
                : environment.AlphaWrapper.Context.Constellation.MinAllowedLotSize + 1;
            var sqamRequest = new OrderRequest
            {
                Account = client.Id,
                AccountWrapper = client,
                Amount = amount,
                Price = 1,
                Asset = 1,
                RequestId = 1,
                Side = OrderSide.Buy
            }.CreateEnvelope().Sign(useFakeClient ? KeyPair.Random() : clientPk);

            var apex = quantaStorage.CurrentApex + 1;
            var requestQuantum = new RequestQuantum
            {
                Apex = quantaStorage.CurrentApex + 1,
                EffectsHash = new byte[] { },
                PrevHash = quantaStorage.LastQuantumHash,
                RequestEnvelope = sqamRequest,
                Timestamp = DateTime.UtcNow.Ticks
            }.CreateEnvelope().Sign(useFakeAlpha ? KeyPair.Random() : environment.AlphaWrapper.Settings.KeyPair);

            quantaStorage.AddQuantum(requestQuantum, requestQuantum.ComputeMessageHash());

            var expectedState = useFakeClient || useFakeAlpha || invalidBalance ? State.Failed : State.Ready;

            if (expectedState == State.Ready)
                await environment.AssertConstellationApex(apex, TimeSpan.FromSeconds(10));

            await Task.WhenAll(environment.AuditorWrappers.Select(a => IntegrationTestEnvironmentExtensions.AssertState(a.Startup, expectedState, TimeSpan.FromSeconds(10))));
        }
    }
}
