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

            var clientConnection = new AuditorWebSocketConnection(new FakeWebSocket());

            var envelope = new AlphaState
            {
                LastSnapshot = await Global.SnapshotManager.GetSnapshot(),
                State = alphaState
            }.CreateEnvelope();
            envelope.Sign(TestEnvironment.AlphaKeyPair);

            var isHandled = await MessageHandlers<AuditorWebSocketConnection>.HandleMessage(clientConnection, envelope);

            Assert.IsTrue(isHandled);
        }

        //static object[] SnapshotQuantumTestCases =
        //{
        //    new object[] { TestEnvironment.Client1KeyPair, ConnectionState.Ready, typeof(UnauthorizedException) },
        //    new object[] { TestEnvironment.AlphaKeyPair, ConnectionState.Connected, typeof(InvalidStateException) },
        //    new object[] { TestEnvironment.AlphaKeyPair, ConnectionState.Ready, null }
        //};

        //[Test]
        //[TestCaseSource(nameof(SnapshotQuantumTestCases))]
        //public async Task SnapshotQuantumTest(KeyPair alphaKeyPair, ConnectionState state, Type excpectedException)
        //{
        //    try
        //    {
        //        Global.AppState.State = ApplicationState.Ready;

        //        var clientConnection = new AuditorWebSocketConnection(new FakeWebSocket()) { ConnectionState = state };

        //        var currentSnapshotId = Global.SnapshotManager.LastSnapshot.Id;
        //        var snapshotQuantumEnvelope = new SnapshotQuantum { Hash = new byte[32] }.CreateEnvelope();
        //        snapshotQuantumEnvelope.Sign(alphaKeyPair);

        //        var isHandled = await MessageHandlers<AuditorWebSocketConnection>.HandleMessage(clientConnection, snapshotQuantumEnvelope);

        //        Assert.IsTrue(isHandled);
        //    }
        //    catch (Exception exc)
        //    {
        //        //throw if we don't expect this type of exception
        //        if (excpectedException == null || excpectedException != exc.GetType())
        //            throw;
        //    }
        //}

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
            try
            {
                Global.AppState.State = ApplicationState.Ready;

                var clientConnection = new AuditorWebSocketConnection(new FakeWebSocket()) { ConnectionState = state };

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
            try
            {
                Global.AppState.State = ApplicationState.Ready;

                var clientConnection = new AuditorWebSocketConnection(new FakeWebSocket()) { ConnectionState = state };

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
