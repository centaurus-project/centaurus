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
            context.SetState(State.Running);

            var clientConnection = GetOutgoingConnection(context, KeyPair.Random());

            var hd = new HandshakeData();
            hd.Randomize();

            var envelope = new HandshakeRequest { HandshakeData = hd }.CreateEnvelope();
            envelope.Sign(TestEnvironment.AlphaKeyPair);
            using var writer = new XdrBufferWriter();
            var inMessage = envelope.ToIncomingMessage(writer);
            var isHandled = await context.MessageHandlers.HandleMessage(clientConnection, inMessage);

            Assert.IsTrue(isHandled);
        }

        static object[] QuantaBatchTestCases =
        {
            new object[] { TestEnvironment.Client1KeyPair, ConnectionState.Ready, typeof(UnauthorizedException) },
            new object[] { TestEnvironment.AlphaKeyPair, ConnectionState.Connected, typeof(InvalidStateException) },
            new object[] { TestEnvironment.AlphaKeyPair, ConnectionState.Validated, null },
            new object[] { TestEnvironment.AlphaKeyPair, ConnectionState.Ready, null }
        };

        [Test]
        [TestCaseSource(nameof(QuantaBatchTestCases))]
        public async Task QuantaBatchTest(KeyPair alphaKeyPair, ConnectionState state, Type excpectedException)
        {
            context.SetState(State.Ready);

            var clientConnection = GetIncomingConnection(context, alphaKeyPair, state);
            var batch = new QuantaBatch
            {
                Quanta = new List<PendingQuantum>()
            }.CreateEnvelope();
            batch.Sign(alphaKeyPair);

            using var writer = new XdrBufferWriter();
            var inMessage = batch.ToIncomingMessage(writer);

            await AssertMessageHandling(clientConnection, inMessage, excpectedException);
        }
    }
}