using Centaurus.Domain;
using Centaurus.Models;
using Centaurus.Xdr;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Centaurus.Test
{
    internal class AuditorMessageHandlersTests : BaseMessageHandlerTests
    {

        private ExecutionContext context;

        [OneTimeSetUp]
        public void Setup()
        {
            EnvironmentHelper.SetTestEnvironmentVariable();
            context = GlobalInitHelper.DefaultAuditorSetup().Result;
        }


        [Test]
        public async Task HandshakeTest()
        {
            context.SetState(State.Running);

            var clientConnection = GetOutgoingConnection(context, TestEnvironment.AlphaKeyPair);

            var hd = new HandshakeData().Randomize();

            var envelope = new HandshakeRequest { HandshakeData = hd }.CreateEnvelope();
            envelope.Sign(TestEnvironment.AlphaKeyPair);

            using var writer = new XdrBufferWriter();
            var inMessage = envelope.ToIncomingMessage(writer);
            var isHandled = await context.MessageHandlers.HandleMessage(clientConnection, inMessage);

            Assert.IsTrue(isHandled);
        }

        static object[] QuantaBatchTestCases =
        {
            new object[] { TestEnvironment.AlphaKeyPair, null }
        };

        [Test]
        [TestCaseSource(nameof(QuantaBatchTestCases))]
        public async Task QuantaBatchTest(KeyPair alphaKeyPair, Type excpectedException)
        {
            context.SetState(State.Ready);

            var connection = default(ConnectionBase);
            try
            {
                connection = GetOutgoingConnection(context, alphaKeyPair);
            }
            catch (UnauthorizedException)
            {
                if (excpectedException == typeof(UnauthorizedException))
                    return;
                throw;
            }
            var batch = new SyncQuantaBatch
            {
                Quanta = new List<SyncQuantaBatchItem>()
            }.CreateEnvelope();
            batch.Sign(alphaKeyPair);

            using var writer = new XdrBufferWriter();
            var inMessage = batch.ToIncomingMessage(writer);

            await AssertMessageHandling(connection, inMessage, excpectedException);
        }
    }
}