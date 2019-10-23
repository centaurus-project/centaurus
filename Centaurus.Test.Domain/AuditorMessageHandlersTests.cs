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

        const string auditorSecret = "SC4E2JXDOYKQYAZWKHRRNXYOJUYUJUPCU6LAWQ7JSJIV6AFHHW2M3LQU";
        const string alphaSecret = "SD7DAXWXRCVQKGPCATD3JSCWYUEDV2KTH6PLKPSRJZABH4TW4ZDLMNI5";
        const string clientSecret = "SBYLNNI2HPQZIFL7K5HFN53XBBLBS4T6L7J7I33XFTEX3BP4H6AACYB4";

        [SetUp]
        public void Setup()
        {
            var genesisQuorum = new string[] { auditorSecret };
            var settings = new AuditorSettings
            {
                HorizonUrl = "https://horizon-testnet.stellar.org",
                NetworkPassphrase = "Test SDF Network ; September 2015",
                Secret = auditorSecret,
                AlphaPubKey = KeyPair.FromSecretSeed(alphaSecret).AccountId,
                GenesisQuorum = genesisQuorum.Select(s => KeyPair.FromSecretSeed(s).AccountId)
            };
            settings.Build();

            GlobalInitHelper.Setup(new string[] { }, genesisQuorum, settings);

            MessageHandlers<AuditorWebSocketConnection>.Init();
        }


        [Test]
        public async Task HandshakeTest()
        {
            Global.AppState.State = ApplicationState.Running;

            var clientConnection = new FakeAuditorWebSocketConnection();

            var hd = new HandshakeData();
            hd.Randomize();

            var alphaKeyPair = KeyPair.FromSecretSeed(alphaSecret);
            var envelope = new HandshakeInit { HandshakeData = hd }.CreateEnvelope();
            envelope.Sign(alphaKeyPair);
            var isHandled = await MessageHandlers<AuditorWebSocketConnection>.HandleMessage(clientConnection, envelope);

            Assert.IsTrue(isHandled);
        }

        [Test]
        //[TestCase(ApplicationState.Rising)]//TODO: add fake snapshot manager provider. After that we will have an opportunity to test this case
        [TestCase(ApplicationState.Running)]
        [TestCase(ApplicationState.Ready)]
        public async Task AlphaStateTest(ApplicationState alphaState)
        {
            Global.AppState.State = ApplicationState.Running;

            var clientConnection = new FakeAuditorWebSocketConnection();

            var alphaKeyPair = KeyPair.FromSecretSeed(alphaSecret);
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
        [TestCase(clientSecret, ConnectionState.Ready, typeof(UnauthorizedException))]
        [TestCase(alphaSecret, ConnectionState.Connected, typeof(InvalidStateException))]
        [TestCase(alphaSecret, ConnectionState.Ready, null)]
        public async Task SnapshotQuantumTest(string alphaSecret, ConnectionState state, Type excpectedException)
        {
            try
            {
                Global.AppState.State = ApplicationState.Ready;

                var clientConnection = new FakeAuditorWebSocketConnection();
                clientConnection.SetState(state);

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
        [TestCase(clientSecret, ConnectionState.Ready, typeof(UnauthorizedException))]
        [TestCase(alphaSecret, ConnectionState.Connected, typeof(InvalidStateException))]
        [TestCase(alphaSecret, ConnectionState.Ready, null)]
        public async Task LedgerQuantumTest(string alphaSecret, ConnectionState state, Type excpectedException)
        {
            try
            {
                Global.AppState.State = ApplicationState.Ready;

                var clientConnection = new FakeAuditorWebSocketConnection();
                clientConnection.SetState(state);

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
        [TestCase(alphaSecret, clientSecret, ConnectionState.Ready, typeof(UnauthorizedException))]
        [TestCase(clientSecret, clientSecret, ConnectionState.Ready, typeof(UnauthorizedException))]
        [TestCase(clientSecret, alphaSecret, ConnectionState.Connected, typeof(InvalidStateException))]
        [TestCase(clientSecret, alphaSecret, ConnectionState.Ready, null)]
        public async Task OrderQuantumTest(string clientSecret, string alphaSecret, ConnectionState state, Type excpectedException)
        {
            try
            {
                Global.AppState.State = ApplicationState.Ready;

                var clientConnection = new FakeAuditorWebSocketConnection();
                clientConnection.SetState(state);

                var alphaKeyPair = KeyPair.FromSecretSeed(alphaSecret);
                var clientKeyPair = KeyPair.FromSecretSeed(clientSecret);

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
