using Centaurus.Domain;
using Centaurus.Models;
using NUnit.Framework;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Test
{
    public class AuditorMessageHandlersTests
    {
        [SetUp]
        public void Setup()
        {
            GlobalInitHelper.DefaultAuditorSetup();

            MessageHandlers<AuditorWebSocketConnection>.Init();
        }


        [Test]
        public async Task HandshakeTest()
        {
            Global.AppState.State = ApplicationState.Running;

            var clientConnection = new AuditorWebSocketConnection(new FakeWebSocket());

            var hd = new HandshakeData();
            hd.Randomize();

            var alphaKeyPair = KeyPair.FromSecretSeed(TestEnvironment.AlphaSecret);
            var envelope = new HandshakeInit { HandshakeData = hd }.CreateEnvelope();
            envelope.Sign(alphaKeyPair);
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

            var clientConnection = new AuditorWebSocketConnection(new FakeWebSocket());

            var alphaKeyPair = KeyPair.FromSecretSeed(TestEnvironment.AlphaSecret);
            var envelope = new AlphaState
            {
                LastSnapshot = Global.SnapshotManager.LastSnapshot,
                State = alphaState
            }.CreateEnvelope();
            envelope.Sign(alphaKeyPair);

            var isHandled = await MessageHandlers<AuditorWebSocketConnection>.HandleMessage(clientConnection, envelope);

            Assert.IsTrue(isHandled);
        }

        [Test]
        [TestCase(TestEnvironment.Client1Secret, ConnectionState.Ready, typeof(UnauthorizedException))]
        [TestCase(TestEnvironment.AlphaSecret, ConnectionState.Connected, typeof(InvalidStateException))]
        [TestCase(TestEnvironment.AlphaSecret, ConnectionState.Ready, null)]
        public async Task SnapshotQuantumTest(string alphaSecret, ConnectionState state, Type excpectedException)
        {
            try
            {
                Global.AppState.State = ApplicationState.Ready;

                var clientConnection = new AuditorWebSocketConnection(new FakeWebSocket()) { ConnectionState = state };

                var currentSnapshotId = Global.SnapshotManager.LastSnapshot.Id;
                var alphaKeyPair = KeyPair.FromSecretSeed(alphaSecret);
                var snapshotQuantumEnvelope = new SnapshotQuantum { Hash = new byte[32] }.CreateEnvelope();
                snapshotQuantumEnvelope.Sign(alphaKeyPair);

                var isHandled = await MessageHandlers<AuditorWebSocketConnection>.HandleMessage(clientConnection, snapshotQuantumEnvelope);

                Assert.IsTrue(isHandled);
            }
            catch (Exception exc)
            {
                //throw if we don't expect this type of exception
                if (excpectedException == null || excpectedException != exc.GetType())
                    throw;
            }
        }

        [Test]
        [TestCase(TestEnvironment.Client1Secret, ConnectionState.Ready, typeof(UnauthorizedException))]
        [TestCase(TestEnvironment.AlphaSecret, ConnectionState.Connected, typeof(InvalidStateException))]
        [TestCase(TestEnvironment.AlphaSecret, ConnectionState.Ready, null)]
        public async Task LedgerQuantumTest(string alphaSecret, ConnectionState state, Type excpectedException)
        {
            try
            {
                Global.AppState.State = ApplicationState.Ready;

                var clientConnection = new AuditorWebSocketConnection(new FakeWebSocket()) { ConnectionState = state };

                var alphaKeyPair = KeyPair.FromSecretSeed(alphaSecret);

                var ledgerNotification = new LedgerUpdateNotification
                {
                    LedgerFrom = 0,
                    LedgerTo = 63,
                    Payments = new List<PaymentBase>()
                };

                var snapshotQuantumEnvelope = new LedgerCommitQuantum
                {
                    Source = ledgerNotification.CreateEnvelope()
                }.CreateEnvelope();
                snapshotQuantumEnvelope.Sign(alphaKeyPair);

                var isHandled = await MessageHandlers<AuditorWebSocketConnection>.HandleMessage(clientConnection, snapshotQuantumEnvelope);

                Assert.IsTrue(isHandled);
            }
            catch (Exception exc)
            {
                //throw if we don't expect this type of exception
                if (excpectedException == null || excpectedException != exc.GetType())
                    throw;
            }
        }

        [Test]
        [TestCase(TestEnvironment.AlphaSecret, TestEnvironment.Client1Secret, ConnectionState.Ready, typeof(UnauthorizedException))]
        [TestCase(TestEnvironment.Client1Secret, TestEnvironment.Client1Secret, ConnectionState.Ready, typeof(UnauthorizedException))]
        [TestCase(TestEnvironment.Client1Secret, TestEnvironment.AlphaSecret, ConnectionState.Connected, typeof(InvalidStateException))]
        [TestCase(TestEnvironment.Client1Secret, TestEnvironment.AlphaSecret, ConnectionState.Ready, null)]
        public async Task OrderQuantumTest(string client1Secret, string alphaSecret, ConnectionState state, Type excpectedException)
        {
            try
            {
                Global.AppState.State = ApplicationState.Ready;

                var clientConnection = new AuditorWebSocketConnection(new FakeWebSocket()) { ConnectionState = state };

                var alphaKeyPair = KeyPair.FromSecretSeed(alphaSecret);
                var clientKeyPair = KeyPair.FromSecretSeed(client1Secret);

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

                var isHandled = await MessageHandlers<AuditorWebSocketConnection>.HandleMessage(clientConnection, orderQuantumEnvelope);

                Assert.IsTrue(isHandled);
            }
            catch (Exception exc)
            {
                //throw if we don't expect this type of exception
                if (excpectedException == null || excpectedException != exc.GetType())
                    throw;
            }
        }
    }
}
