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
        const string clientSecret = "SBUYSHHKTX32A2Z3P2WYLT7EGFG2O7NCFK3DQYJ3O2UKC4X6R64CUI22";
        const string auditorSecret = "SC4E2JXDOYKQYAZWKHRRNXYOJUYUJUPCU6LAWQ7JSJIV6AFHHW2M3LQU";


        [SetUp]
        public void Setup()
        {
            var settings = new AlphaSettings
            {
                HorizonUrl = "https://horizon-testnet.stellar.org",
                NetworkPassphrase = "Test SDF Network ; September 2015",
                Secret = KeyPair.Random().SecretSeed
            };
            settings.Build();

            GlobalInitHelper.Setup(new string[] { clientSecret }, new string[] { auditorSecret }, settings);

            MessageHandlers<AlphaWebSocketConnection>.Init();
        }

        [Test]
        public async Task HandshakeTest()
        {
            Global.AppState.State = ApplicationState.Ready;

            var clientKeyPair = KeyPair.FromSecretSeed(clientSecret);
            var clientConnection = new AlphaWebSocketConnection(new FakeWebSocket());

            var envelope = new HandshakeInit { HandshakeData = clientConnection.HandshakeData }.CreateEnvelope();
            envelope.Sign(clientKeyPair);
            var isHandled = await MessageHandlers<AlphaWebSocketConnection>.HandleMessage(clientConnection, envelope);

            Assert.IsTrue(isHandled);
        }

        [Test]
        public async Task HeartbeatTest()
        {
            Global.AppState.State = ApplicationState.Ready;

            var clientKeyPair = KeyPair.FromSecretSeed(clientSecret);
            var clientConnection = new AlphaWebSocketConnection(new FakeWebSocket());

            var envelope = new Heartbeat().CreateEnvelope();
            envelope.Sign(clientKeyPair);
            var isHandled = await MessageHandlers<AlphaWebSocketConnection>.HandleMessage(clientConnection, envelope);

            Assert.IsTrue(isHandled);
        }

        [Test]
        [TestCase(clientSecret, ConnectionState.Validated, typeof(UnauthorizedException))]
        [TestCase(auditorSecret, ConnectionState.Connected, typeof(InvalidStateException))]
        [TestCase(auditorSecret, ConnectionState.Validated, null)]
        public async Task SetApexCursorTest(string clientSecret, ConnectionState state, Type excpectedException)
        {
            try
            {
                Global.AppState.State = ApplicationState.Ready;

                var clientKeyPair = KeyPair.FromSecretSeed(clientSecret);
                var clientConnection = new FakeAlphaWebSocketConnection();
                clientConnection.SetClientPubKey(clientKeyPair.PublicKey);
                clientConnection.SetState(state);

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
        [TestCase(clientSecret, ConnectionState.Validated, typeof(UnauthorizedException))]
        [TestCase(auditorSecret, ConnectionState.Validated, typeof(InvalidStateException))]
        [TestCase(auditorSecret, ConnectionState.Ready, null)]
        public async Task LedgerUpdateTest(string clientSecret, ConnectionState state, Type excpectedException)
        {
            try
            {
                Global.AppState.State = ApplicationState.Ready;

                var clientKeyPair = KeyPair.FromSecretSeed(clientSecret);
                var clientConnection = new FakeAlphaWebSocketConnection();
                clientConnection.SetClientPubKey(clientKeyPair.PublicKey);
                clientConnection.SetState(state);

                var envelope = new LedgerUpdateNotification { LedgerFrom = 0, LedgerTo = 64, Payments = new List<PaymentBase>() }.CreateEnvelope();
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
        [TestCase(clientSecret, ConnectionState.Validated, typeof(UnauthorizedException))]
        [TestCase(auditorSecret, ConnectionState.Connected, typeof(InvalidStateException))]
        [TestCase(auditorSecret, ConnectionState.Ready, null)]
        public async Task AuditorStateTest(string clientSecret, ConnectionState state, Type excpectedException)
        {
            try
            {
                Global.AppState.State = ApplicationState.Rising;

                var clientKeyPair = KeyPair.FromSecretSeed(clientSecret);
                var clientConnection = new FakeAlphaWebSocketConnection();
                clientConnection.SetClientPubKey(clientKeyPair.PublicKey);
                clientConnection.SetState(state);

                var envelope = new AuditorState { 
                    PendingQuantums = new List<MessageEnvelope>(),
                    LastSnapshot = Global.SnapshotManager.LastSnapshot,
                    State = ApplicationState.Running
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

        [Test]
        [TestCase(ConnectionState.Validated, typeof(InvalidStateException))]
        [TestCase(ConnectionState.Ready, null)]
        public async Task OrderTest(ConnectionState state, Type excpectedException)
        {
            try
            {
                Global.AppState.State = ApplicationState.Ready;

                var clientKeyPair = KeyPair.FromSecretSeed(clientSecret);
                var clientConnection = new FakeAlphaWebSocketConnection();
                clientConnection.SetClientPubKey(clientKeyPair.PublicKey);
                clientConnection.SetState(state);

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
