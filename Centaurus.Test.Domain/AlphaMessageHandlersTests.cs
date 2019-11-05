using Centaurus.Domain;
using Centaurus.Models;
using NUnit.Framework;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Test
{
    public class AlphaMessageHandlersTests
    {
        [SetUp]
        public void Setup()
        {
            GlobalInitHelper.DefaultAlphaSetup();
            MessageHandlers<AlphaWebSocketConnection>.Init();
        }

        [Test]
        public async Task HandshakeTest()
        {
            Global.AppState.State = ApplicationState.Ready;

            var clientKeyPair = KeyPair.FromSecretSeed(TestEnvironment.Client1Secret);
            var clientConnection = new AlphaWebSocketConnection(new FakeWebSocket());

            var envelope = new HandshakeInit { HandshakeData = clientConnection.HandshakeData }.CreateEnvelope();
            envelope.Sign(clientKeyPair);
            var isHandled = await MessageHandlers<AlphaWebSocketConnection>.HandleMessage(clientConnection, envelope);

            Assert.IsTrue(isHandled);
            Assert.AreEqual(clientConnection.ClientPubKey, new RawPubKey(clientKeyPair.AccountId));
            Assert.AreEqual(clientConnection.ConnectionState, ConnectionState.Ready);
        }

        [Test]
        public void HandshakeInvalidDataTest()
        {
            Global.AppState.State = ApplicationState.Ready;

            var clientKeyPair = KeyPair.FromSecretSeed(TestEnvironment.Client1Secret);
            var clientConnection = new AlphaWebSocketConnection(new FakeWebSocket());

            var handshake = new HandshakeData();
            handshake.Randomize();

            var envelope = new HandshakeInit { HandshakeData = handshake }.CreateEnvelope();
            envelope.Sign(clientKeyPair);

            Assert.ThrowsAsync<ConnectionCloseException>(async () => await MessageHandlers<AlphaWebSocketConnection>.HandleMessage(clientConnection, envelope));
        }

        [Test]
        public async Task HeartbeatTest()
        {
            Global.AppState.State = ApplicationState.Ready;

            var clientKeyPair = KeyPair.FromSecretSeed(TestEnvironment.Client1Secret);
            var clientConnection = new AlphaWebSocketConnection(new FakeWebSocket());

            var envelope = new Heartbeat().CreateEnvelope();
            envelope.Sign(clientKeyPair);
            var isHandled = await MessageHandlers<AlphaWebSocketConnection>.HandleMessage(clientConnection, envelope);

            Assert.IsTrue(isHandled);
        }

        [Test]
        [TestCase(TestEnvironment.Client1Secret, ConnectionState.Validated, typeof(UnauthorizedException))]
        [TestCase(TestEnvironment.Auditor1Secret, ConnectionState.Connected, typeof(InvalidStateException))]
        [TestCase(TestEnvironment.Auditor1Secret, ConnectionState.Validated, null)]
        public async Task SetApexCursorTest(string client1Secret, ConnectionState state, Type excpectedException)
        {
            try
            {
                Global.AppState.State = ApplicationState.Ready;

                var clientKeyPair = KeyPair.FromSecretSeed(client1Secret);
                var clientConnection = new AlphaWebSocketConnection(new FakeWebSocket())
                {
                    ClientPubKey = clientKeyPair.PublicKey,
                    ConnectionState = state
                };

                var envelope = new SetApexCursor { Apex = 1 }.CreateEnvelope();
                envelope.Sign(clientKeyPair);

                var isHandled = await MessageHandlers<AlphaWebSocketConnection>.HandleMessage(clientConnection, envelope);

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
        [TestCase(TestEnvironment.Client1Secret, ConnectionState.Validated, typeof(UnauthorizedException))]
        [TestCase(TestEnvironment.Auditor1Secret, ConnectionState.Validated, typeof(InvalidStateException))]
        [TestCase(TestEnvironment.Auditor1Secret, ConnectionState.Ready, null)]
        public async Task LedgerUpdateTest(string clientSecret, ConnectionState state, Type excpectedException)
        {
            try
            {
                Global.AppState.State = ApplicationState.Ready;

                var clientKeyPair = KeyPair.FromSecretSeed(clientSecret);
                var clientConnection = new AlphaWebSocketConnection(new FakeWebSocket())
                {
                    ClientPubKey = clientKeyPair.PublicKey,
                    ConnectionState = state
                };

                var ledgerTo = 63;
                var envelope = new LedgerUpdateNotification { LedgerFrom = 0, LedgerTo = (uint)ledgerTo, Payments = new List<PaymentBase>() }.CreateEnvelope();
                envelope.Sign(clientKeyPair);

                var isHandled = await MessageHandlers<AlphaWebSocketConnection>.HandleMessage(clientConnection, envelope);

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
        [TestCase(TestEnvironment.Client1Secret, ConnectionState.Validated, typeof(UnauthorizedException))]
        [TestCase(TestEnvironment.Auditor1Secret, ConnectionState.Connected, typeof(InvalidStateException))]
        [TestCase(TestEnvironment.Auditor1Secret, ConnectionState.Ready, null)]
        public async Task AuditorStateTest(string clientSecret, ConnectionState state, Type excpectedException)
        {
            try
            {
                Global.AppState.State = ApplicationState.Rising;

                var clientKeyPair = KeyPair.FromSecretSeed(clientSecret);
                var clientConnection = new AlphaWebSocketConnection(new FakeWebSocket())
                {
                    ClientPubKey = clientKeyPair.PublicKey,
                    ConnectionState = state
                };

                var envelope = new AuditorState { 
                    PendingQuantums = new List<MessageEnvelope>(),
                    LastSnapshot = Global.SnapshotManager.LastSnapshot,
                    State = ApplicationState.Running
                }.CreateEnvelope();
                envelope.Sign(clientKeyPair);

                var isHandled = await MessageHandlers<AlphaWebSocketConnection>.HandleMessage(clientConnection, envelope);

                Assert.IsTrue(isHandled);
                Assert.AreEqual(Global.AppState.State, ApplicationState.Running);
            }
            catch (Exception exc)
            {
                //throw if we don't expect this type of exception
                if (excpectedException == null || excpectedException != exc.GetType())
                    throw;
            }
        }

        [Test]
        [TestCase(ConnectionState.Validated, typeof(InvalidStateException))]
        [TestCase(ConnectionState.Ready, null)]
        public async Task OrderTest(ConnectionState state, Type excpectedException)
        {
            try
            {
                Global.AppState.State = ApplicationState.Ready;

                var clientKeyPair = KeyPair.FromSecretSeed(TestEnvironment.Client1Secret);
                var clientConnection = new AlphaWebSocketConnection(new FakeWebSocket())
                {
                    ClientPubKey = clientKeyPair.PublicKey,
                    ConnectionState = state
                };

                var envelope = new OrderRequest
                {
                    Account = clientKeyPair
                }.CreateEnvelope();
                envelope.Sign(clientKeyPair);

                var isHandled = await MessageHandlers<AlphaWebSocketConnection>.HandleMessage(clientConnection, envelope);

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
