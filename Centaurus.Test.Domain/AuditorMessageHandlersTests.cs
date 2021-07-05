using Centaurus.Domain;
using Centaurus.Models;
using Centaurus.Xdr;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Centaurus.Test
{
    public class AuditorMessageHandlersTests : BaseMessageHandlerTests
    {

        private ExecutionContext context;

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

            var clientConnection = GetOutgoingConnection(context);

            var hd = new HandshakeData();
            hd.Randomize();

            var envelope = new HandshakeRequest { HandshakeData = hd }.CreateEnvelope();
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

            var clientConnection = GetOutgoingConnection(context);

            var envelope = new AlphaState
            {
                State = alphaState
            }
            .CreateEnvelope()
            .Sign(TestEnvironment.AlphaKeyPair);

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

            var clientConnection = GetOutgoingConnection(context, state);

            var ledgerNotification = new DepositNotification
            {
                Cursor = "0",
                Items = new List<Deposit>()
            };

            var envelope = new DepositQuantum
            {
                Source = ledgerNotification
            }.CreateEnvelope();
            envelope.Sign(alphaKeyPair);
            using var writer = new XdrBufferWriter();
            var inMessage = envelope.ToIncomingMessage(writer);
            await AssertMessageHandling(clientConnection, inMessage, excpectedException);
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
            context.AppState.State = ApplicationState.Ready;

            var clientConnection = GetOutgoingConnection(context, state);
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
