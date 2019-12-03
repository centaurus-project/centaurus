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

            var clientConnection = new AlphaWebSocketConnection(new FakeWebSocket());

            var envelope = new HandshakeInit { HandshakeData = clientConnection.HandshakeData }.CreateEnvelope();
            envelope.Sign(TestEnvironment.Client1KeyPair);
            var isHandled = await MessageHandlers<AlphaWebSocketConnection>.HandleMessage(clientConnection, envelope);

            Assert.IsTrue(isHandled);
            Assert.AreEqual(clientConnection.ClientPubKey, new RawPubKey(TestEnvironment.Client1KeyPair.AccountId));
            Assert.AreEqual(clientConnection.ConnectionState, ConnectionState.Ready);
        }

        [Test]
        public void HandshakeInvalidDataTest()
        {
            Global.AppState.State = ApplicationState.Ready;

            var clientConnection = new AlphaWebSocketConnection(new FakeWebSocket());

            var handshake = new HandshakeData();
            handshake.Randomize();

            var envelope = new HandshakeInit { HandshakeData = handshake }.CreateEnvelope();
            envelope.Sign(TestEnvironment.Client1KeyPair);

            Assert.ThrowsAsync<ConnectionCloseException>(async () => await MessageHandlers<AlphaWebSocketConnection>.HandleMessage(clientConnection, envelope));
        }

        [Test]
        public async Task HeartbeatTest()
        {
            Global.AppState.State = ApplicationState.Ready;

            var clientConnection = new AlphaWebSocketConnection(new FakeWebSocket());

            var envelope = new Heartbeat().CreateEnvelope();
            envelope.Sign(TestEnvironment.Client1KeyPair);
            var isHandled = await MessageHandlers<AlphaWebSocketConnection>.HandleMessage(clientConnection, envelope);

            Assert.IsTrue(isHandled);
        }

        static object[] SetApexCursorTestCases =
        {
            new object[] { TestEnvironment.Client1KeyPair, ConnectionState.Validated, typeof(UnauthorizedException) },
            new object[] { TestEnvironment.Auditor1KeyPair, ConnectionState.Connected, typeof(InvalidStateException) },
            new object[] { TestEnvironment.Auditor1KeyPair, ConnectionState.Validated, null }
        };

        [Test]
        [TestCaseSource(nameof(SetApexCursorTestCases))]
        public async Task SetApexCursorTest(KeyPair clientKeyPair, ConnectionState state, Type excpectedException)
        {
            try
            {
                Global.AppState.State = ApplicationState.Ready;

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

        static object[] LedgerUpdateTestCases =
        {
            new object[] { TestEnvironment.Client1KeyPair, ConnectionState.Validated, typeof(UnauthorizedException) },
            new object[] { TestEnvironment.Auditor1KeyPair, ConnectionState.Validated, typeof(InvalidStateException) },
            new object[] { TestEnvironment.Auditor1KeyPair, ConnectionState.Ready, null }
        };

        [Test]
        [TestCaseSource(nameof(LedgerUpdateTestCases))]
        public async Task LedgerUpdateTest(KeyPair clientKeyPair, ConnectionState state, Type excpectedException)
        {
            try
            {
                Global.AppState.State = ApplicationState.Ready;

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

        static object[] AuditorStateTestCases =
        {
            new object[] { TestEnvironment.Client1KeyPair, ConnectionState.Validated, typeof(UnauthorizedException) },
            new object[] { TestEnvironment.Auditor1KeyPair, ConnectionState.Connected, typeof(InvalidStateException) },
            new object[] { TestEnvironment.Auditor1KeyPair, ConnectionState.Ready, null }
        };

        [Test]
        [TestCaseSource(nameof(AuditorStateTestCases))]
        public async Task AuditorStateTest(KeyPair clientKeyPair, ConnectionState state, Type excpectedException)
        {
            try
            {
                Global.AppState.State = ApplicationState.Rising;

                var clientConnection = new AlphaWebSocketConnection(new FakeWebSocket())
                {
                    ClientPubKey = clientKeyPair.PublicKey,
                    ConnectionState = state
                };

                var envelope = new AuditorState
                {
                    PendingQuantums = new List<MessageEnvelope>(),
                    Snapshot = await SnapshotManager.GetSnapshot(),
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

        static object[] OrderTestCases =
        {
            new object[] { ConnectionState.Validated, typeof(InvalidStateException) },
            new object[] { ConnectionState.Ready, null }
        };

        [Test]
        [TestCaseSource(nameof(OrderTestCases))]
        public async Task OrderTest(ConnectionState state, Type excpectedException)
        {
            try
            {
                Global.AppState.State = ApplicationState.Ready;

                var clientConnection = new AlphaWebSocketConnection(new FakeWebSocket())
                {
                    ClientPubKey = TestEnvironment.Client1KeyPair.PublicKey,
                    ConnectionState = state
                };

                var envelope = new OrderRequest
                {
                    Account = TestEnvironment.Client1KeyPair
                }.CreateEnvelope();
                envelope.Sign(TestEnvironment.Client1KeyPair);

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
