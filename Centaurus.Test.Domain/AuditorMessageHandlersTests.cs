using NUnit.Framework;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Centaurus.Domain;
using Centaurus.Models;
using Centaurus.Xdr;

namespace Centaurus.Test
{
    public class AuditorMessageHandlersTests: BaseMessageHandlerTests
    {
        [SetUp]
        public void Setup()
        {
            EnvironmentHelper.SetTestEnvironmentVariable();
            context = GlobalInitHelper.DefaultAuditorSetup().Result;
        }


        [Test]
        public async Task HandshakeTest()
        {
            context.AppState.State = ApplicationState.Running;

            var clientConnection = new AuditorWebSocketConnection(context, new FakeAuditorConnectionInfo(new FakeWebSocket()));

            var hd = new HandshakeData();
            hd.Randomize();

            var envelope = new HandshakeInit { HandshakeData = hd }.CreateEnvelope();
            envelope.Sign(TestEnvironment.AlphaKeyPair);
            using var writer = new XdrBufferWriter();
            var inMessage = envelope.ToIncomingMessage(writer);
            var isHandled = await context.MessageHandlers.HandleMessage(clientConnection, inMessage);

            Assert.IsTrue(isHandled);
        }

        [Test]
        [TestCase(ApplicationState.Rising)]
        [TestCase(ApplicationState.Running)]
        [TestCase(ApplicationState.Ready)]
        public async Task AlphaStateTest(ApplicationState alphaState)
        {
            context.AppState.State = ApplicationState.Running;

            var clientConnection = new AuditorWebSocketConnection(context, new FakeAuditorConnectionInfo(new FakeWebSocket()));

            var envelope = new AlphaState
            {
                State = alphaState
            }.CreateEnvelope();
            envelope.Sign(TestEnvironment.AlphaKeyPair);

            using var writer = new XdrBufferWriter();
            var inMessage = envelope.ToIncomingMessage(writer);
            var isHandled = await context.MessageHandlers.HandleMessage(clientConnection, inMessage);

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
        public async Task TxCommitQuantumTest(KeyPair alphaKeyPair, ConnectionState state, Type excpectedException)
        {
            context.AppState.State = ApplicationState.Ready;

            var clientConnection = new AuditorWebSocketConnection(context, new FakeAuditorConnectionInfo(new FakeWebSocket())) { ConnectionState = state };

            var ledgerNotification = new TxNotification
            {
                TxCursor = 0,
                Payments = new List<PaymentBase>()
            };

            var envelope = new TxCommitQuantum
            {
                Source = ledgerNotification.CreateEnvelope()
            }.CreateEnvelope();
            envelope.Sign(alphaKeyPair);
            using var writer = new XdrBufferWriter();
            var inMessage = envelope.ToIncomingMessage(writer);
            await AssertMessageHandling(clientConnection, inMessage, excpectedException);
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
            context.AppState.State = ApplicationState.Ready;

            var clientConnection = new AuditorWebSocketConnection(context, new FakeAuditorConnectionInfo(new FakeWebSocket())) { ConnectionState = state };
            var account = context.AccountStorage.GetAccount(clientKeyPair);
            var orderEnvelope = new OrderRequest
            {
                Account = account?.Account.Id ?? 0
            }.CreateEnvelope();
            orderEnvelope.Sign(clientKeyPair);

            var orderQuantumEnvelope = new RequestQuantum
            {
                RequestEnvelope = orderEnvelope
            }.CreateEnvelope();
            orderQuantumEnvelope.Sign(alphaKeyPair);
            using var writer = new XdrBufferWriter();
            var inMessage = orderQuantumEnvelope.ToIncomingMessage(writer);


            await AssertMessageHandling(clientConnection, inMessage, excpectedException);
        }

        static object[] QuantaBatchTestCases =
        {
            new object[] { TestEnvironment.Client1KeyPair, ConnectionState.Ready, typeof(UnauthorizedException) },
            new object[] { TestEnvironment.AlphaKeyPair, ConnectionState.Validated, typeof(InvalidStateException) },
            new object[] { TestEnvironment.AlphaKeyPair, ConnectionState.Ready, null },
            new object[] { TestEnvironment.AlphaKeyPair, ConnectionState.Connected, null },
        };
        private AuditorContext context;

        [Test]
        [TestCaseSource(nameof(QuantaBatchTestCases))]
        public async Task QuantaBatchTest(KeyPair alphaKeyPair, ConnectionState state, Type excpectedException)
        {
            context.AppState.State = ApplicationState.Ready;

            var clientConnection = new AuditorWebSocketConnection(context, new FakeAuditorConnectionInfo(new FakeWebSocket())) { ConnectionState = state };
            var orderEnvelope = new QuantaBatch
            {
                Quanta = new List<MessageEnvelope>()
            }.CreateEnvelope();
            orderEnvelope.Sign(alphaKeyPair);
            using var writer = new XdrBufferWriter();
            var inMessage = orderEnvelope.ToIncomingMessage(writer);

            await AssertMessageHandling(clientConnection, inMessage, excpectedException);
        }
    }
}
