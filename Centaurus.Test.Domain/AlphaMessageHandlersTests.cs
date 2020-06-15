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
    public class AlphaMessageHandlersTests : BaseMessageHandlerTests
    {
        [SetUp]
        public void Setup()
        {
            EnvironmentHelper.SetTestEnvironmentVariable();
            GlobalInitHelper.DefaultAlphaSetup();
            MessageHandlers<AlphaWebSocketConnection>.Init();
        }



        static object[] HandshakeTestCases =
        {
            new object[] { TestEnvironment.Client1KeyPair, ApplicationState.Rising, typeof(ConnectionCloseException) },
            new object[] { TestEnvironment.Client1KeyPair, ApplicationState.Ready, null },
            new object[] { TestEnvironment.Auditor1KeyPair, ApplicationState.Rising, null },
            new object[] { TestEnvironment.Auditor1KeyPair,  ApplicationState.Ready, null }
        };

        [Test]
        [TestCaseSource(nameof(HandshakeTestCases))]
        public async Task HandshakeTest(KeyPair clientKeyPair, ApplicationState alphaState, Type expectedException)
        {
            Global.AppState.State = alphaState;

            var clientConnection = new AlphaWebSocketConnection(new FakeWebSocket(), "127.0.0.1");

            var message = new HandshakeInit { HandshakeData = clientConnection.HandshakeData };
            var envelope = message.CreateEnvelope();
            envelope.Sign(clientKeyPair);
            if (expectedException == null)
            {
                var isHandled = await MessageHandlers<AlphaWebSocketConnection>.HandleMessage(clientConnection, envelope);

                Assert.IsTrue(isHandled);
                Assert.AreEqual(clientConnection.ClientPubKey, new RawPubKey(clientKeyPair.AccountId));
                if (clientConnection.ClientPubKey.Equals((RawPubKey)TestEnvironment.Auditor1KeyPair))
                    Assert.AreEqual(clientConnection.ConnectionState, ConnectionState.Validated);
                else
                    Assert.AreEqual(clientConnection.ConnectionState, ConnectionState.Ready);
            }
            else
                Assert.ThrowsAsync(expectedException, async () => await MessageHandlers<AlphaWebSocketConnection>.HandleMessage(clientConnection, envelope));
        }

        [Test]
        public void HandshakeInvalidDataTest()
        {
            Global.AppState.State = ApplicationState.Ready;

            var clientConnection = new AlphaWebSocketConnection(new FakeWebSocket(), "127.0.0.1");

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

            var clientConnection = new AlphaWebSocketConnection(new FakeWebSocket(), "127.0.0.1");

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
            Global.AppState.State = ApplicationState.Ready;

            var clientConnection = new AlphaWebSocketConnection(new FakeWebSocket(), "127.0.0.1")
            {
                ClientPubKey = clientKeyPair.PublicKey,
                ConnectionState = state
            };

            var envelope = new SetApexCursor { Apex = 1 }.CreateEnvelope();
            envelope.Sign(clientKeyPair);

            await AssertMessageHandling(clientConnection, envelope, excpectedException);
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
            Global.AppState.State = ApplicationState.Ready;

            var clientConnection = new AlphaWebSocketConnection(new FakeWebSocket(), "127.0.0.1")
            {
                ClientPubKey = clientKeyPair.PublicKey,
                ConnectionState = state
            };

            var ledgerTo = 63;
            var envelope = new LedgerUpdateNotification
            {
                LedgerFrom = Global.LedgerManager.Ledger + 1,
                LedgerTo = (uint)ledgerTo,
                Payments = new List<PaymentBase>()
            }.CreateEnvelope();
            envelope.Sign(clientKeyPair);

            await AssertMessageHandling(clientConnection, envelope, excpectedException);
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
            Global.AppState.State = ApplicationState.Rising;

            var clientConnection = new AlphaWebSocketConnection(new FakeWebSocket(), "127.0.0.1")
            {
                ClientPubKey = clientKeyPair.PublicKey,
                ConnectionState = state
            };

            var envelope = new AuditorState
            {
                PendingQuantums = new List<MessageEnvelope>(),
                State = ApplicationState.Running
            }.CreateEnvelope();
            envelope.Sign(clientKeyPair);


            await AssertMessageHandling(clientConnection, envelope, excpectedException);
            if (excpectedException == null)
                Assert.AreEqual(Global.AppState.State, ApplicationState.Running);
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
            Global.AppState.State = ApplicationState.Ready;

            var clientConnection = new AlphaWebSocketConnection(new FakeWebSocket(), "127.0.0.1")
            {
                ClientPubKey = TestEnvironment.Client1KeyPair.PublicKey,
                ConnectionState = state
            };

            var envelope = new OrderRequest
            {
                Account = TestEnvironment.Client1KeyPair,
                Nonce = 1
            }.CreateEnvelope();
            envelope.Sign(TestEnvironment.Client1KeyPair);

            await AssertMessageHandling(clientConnection, envelope, excpectedException);
        }

        static object[] AccountDataTestRequestCases =
        {
            new object[] { ConnectionState.Validated, typeof(InvalidStateException) },
            new object[] { ConnectionState.Ready, null }
        };

        [Test]
        [TestCaseSource(nameof(AccountDataTestRequestCases))]
        public async Task AccountDataRequestTest(ConnectionState state, Type excpectedException)
        {
            Global.AppState.State = ApplicationState.Ready;

            var clientConnection = new AlphaWebSocketConnection(new FakeWebSocket(), "127.0.0.1")
            {
                ClientPubKey = TestEnvironment.Client1KeyPair.PublicKey,
                ConnectionState = state
            };

            var envelope = new AccountDataRequest
            {
                Account = TestEnvironment.Client1KeyPair,
                Nonce = 1
            }.CreateEnvelope();
            envelope.Sign(TestEnvironment.Client1KeyPair);

            await AssertMessageHandling(clientConnection, envelope, excpectedException);
        }

        static object[] AccountRequestRateLimitsCases =
        {
            new object[] { TestEnvironment.Client2KeyPair, 10 }
        };

        [Test]
        [TestCaseSource(nameof(AccountRequestRateLimitsCases))]
        public async Task AccountRequestRateLimitTest(KeyPair clientKeyPair, int? requestLimit)
        {
            Global.AppState.State = ApplicationState.Ready;

            var account = Global.AccountStorage.GetAccount(clientKeyPair);
            if (requestLimit.HasValue)
            {
                //TODO: replace it with quantum
                var effect = new RequestRateLimitUpdateEffect { 
                    Pubkey = clientKeyPair, 
                    RequestRateLimits = new RequestRateLimits { 
                        HourLimit = (uint)requestLimit.Value, 
                        MinuteLimit = (uint)requestLimit.Value }
                };
                var effectProcessor = new RequestRateLimitUpdateEffectProcessor(effect, account, Global.Constellation.RequestRateLimits);
                effectProcessor.CommitEffect();
            }
            var clientConnection = new AlphaWebSocketConnection(new FakeWebSocket(), "127.0.0.1")
            {
                ClientPubKey = clientKeyPair,
                ConnectionState = ConnectionState.Ready,
                Account = account
            };

            var minuteLimit = (account.Account.RequestRateLimits ?? Global.Constellation.RequestRateLimits).MinuteLimit;
            var minuteIterCount = minuteLimit + 1;
            for (var i = 0; i < minuteIterCount; i++)
            {
                var envelope = new AccountDataRequest
                {
                    Account = clientKeyPair,
                    Nonce = (ulong)(i + 1)
                }.CreateEnvelope();
                envelope.Sign(clientKeyPair);
                if (i + 1 > minuteLimit)
                    await AssertMessageHandling(clientConnection, envelope, typeof(TooManyRequests));
                else
                    await AssertMessageHandling(clientConnection, envelope);
            }
        }
    }
}
