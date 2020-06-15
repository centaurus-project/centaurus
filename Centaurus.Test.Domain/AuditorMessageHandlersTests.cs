using NUnit.Framework;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Centaurus.Domain;
using Centaurus.Models;

namespace Centaurus.Test
{
    public class AuditorMessageHandlersTests: BaseMessageHandlerTests
    {
        [SetUp]
        public void Setup()
        {
            EnvironmentHelper.SetTestEnvironmentVariable();
            GlobalInitHelper.DefaultAuditorSetup();
            MessageHandlers<AuditorWebSocketConnection>.Init();
        }


        [Test]
        public async Task HandshakeTest()
        {
            Global.AppState.State = ApplicationState.Running;

            var clientConnection = new AuditorWebSocketConnection(new FakeWebSocket(), null);

            var hd = new HandshakeData();
            hd.Randomize();

            var envelope = new HandshakeInit { HandshakeData = hd }.CreateEnvelope();
            envelope.Sign(TestEnvironment.AlphaKeyPair);
            var isHandled = await MessageHandlers<AuditorWebSocketConnection>.HandleMessage(clientConnection, envelope);

            Assert.IsTrue(isHandled);
        }

        [Test]
        [TestCase(ApplicationState.Rising)]
        [TestCase(ApplicationState.Running)]
        [TestCase(ApplicationState.Ready)]
        public async Task AlphaStateTest(ApplicationState alphaState)
        {
            Global.AppState.State = ApplicationState.Running;

            var clientConnection = new AuditorWebSocketConnection(new FakeWebSocket(), null);

            var envelope = new AlphaState
            {
                State = alphaState
            }.CreateEnvelope();
            envelope.Sign(TestEnvironment.AlphaKeyPair);

            var isHandled = await MessageHandlers<AuditorWebSocketConnection>.HandleMessage(clientConnection, envelope);

            Assert.IsTrue(isHandled);
        }

        static object[] LedgerQuantumTestCases =
        {
            new object[] { TestEnvironment.Client1KeyPair, ConnectionState.Ready, typeof(UnauthorizedException) },
            new object[] { TestEnvironment.AlphaKeyPair, ConnectionState.Connected, typeof(InvalidStateException) },
            new object[] { TestEnvironment.AlphaKeyPair, ConnectionState.Ready, null }
        };

        [Test]
        [TestCaseSource(nameof(LedgerQuantumTestCases))]
        public async Task LedgerQuantumTest(KeyPair alphaKeyPair, ConnectionState state, Type excpectedException)
        {
            Global.AppState.State = ApplicationState.Ready;

            var clientConnection = new AuditorWebSocketConnection(new FakeWebSocket(), null) { ConnectionState = state };

            var ledgerNotification = new LedgerUpdateNotification
            {
                LedgerFrom = 0,
                LedgerTo = 63,
                Payments = new List<PaymentBase>()
            };

            var envelope = new LedgerCommitQuantum
            {
                Source = ledgerNotification.CreateEnvelope()
            }.CreateEnvelope();
            envelope.Sign(alphaKeyPair);

            await AssertMessageHandling(clientConnection, envelope, excpectedException);
        }

        static object[] OrderQuantumTestCases =
        {
            new object[] { TestEnvironment.AlphaKeyPair, TestEnvironment.Client1KeyPair, ConnectionState.Ready, typeof(UnauthorizedException) },
            new object[] { TestEnvironment.Client1KeyPair, TestEnvironment.Client1KeyPair, ConnectionState.Ready, typeof(UnauthorizedException) },
            new object[] { TestEnvironment.Client1KeyPair, TestEnvironment.AlphaKeyPair, ConnectionState.Connected, typeof(InvalidStateException) },
            new object[] { TestEnvironment.Client1KeyPair, TestEnvironment.AlphaKeyPair, ConnectionState.Ready, null }
        };

        [Test]
        [TestCaseSource(nameof(OrderQuantumTestCases))]
        public async Task OrderQuantumTest(KeyPair clientKeyPair, KeyPair alphaKeyPair, ConnectionState state, Type excpectedException)
        {
            Global.AppState.State = ApplicationState.Ready;

            var clientConnection = new AuditorWebSocketConnection(new FakeWebSocket(), null) { ConnectionState = state };

            var orderEnvelope = new OrderRequest
            {
                Account = clientKeyPair
            }.CreateEnvelope();
            orderEnvelope.Sign(clientKeyPair);

            var orderQuantumEnvelope = new RequestQuantum
            {
                RequestEnvelope = orderEnvelope
            }.CreateEnvelope();
            orderQuantumEnvelope.Sign(alphaKeyPair);


            await AssertMessageHandling(clientConnection, orderQuantumEnvelope, excpectedException);
        }

        static object[] QuantaBatchTestCases =
        {
            new object[] { TestEnvironment.Client1KeyPair, ConnectionState.Ready, typeof(UnauthorizedException) },
            new object[] { TestEnvironment.AlphaKeyPair, ConnectionState.Validated, typeof(InvalidStateException) },
            new object[] { TestEnvironment.AlphaKeyPair, ConnectionState.Ready, null },
            new object[] { TestEnvironment.AlphaKeyPair, ConnectionState.Connected, null },
        };

        [Test]
        [TestCaseSource(nameof(QuantaBatchTestCases))]
        public async Task QuantaBatchTest(KeyPair alphaKeyPair, ConnectionState state, Type excpectedException)
        {
            Global.AppState.State = ApplicationState.Ready;

            var clientConnection = new AuditorWebSocketConnection(new FakeWebSocket(), null) { ConnectionState = state };
            var orderEnvelope = new QuantaBatch
            {
                Quanta = new List<MessageEnvelope>()
            }.CreateEnvelope();
            orderEnvelope.Sign(alphaKeyPair);

            await AssertMessageHandling(clientConnection, orderEnvelope, excpectedException);
        }
    }
}
