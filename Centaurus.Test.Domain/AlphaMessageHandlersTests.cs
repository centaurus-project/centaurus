﻿using Centaurus.Domain;
using Centaurus.Models;
using Centaurus.Xdr;
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
            context = GlobalInitHelper.DefaultAlphaSetup().Result;
            MessageHandlers<AlphaWebSocketConnection>.Init(context);
        }

        private AlphaContext context;


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
            context.AppState.State = alphaState;

            var clientConnection = new AlphaWebSocketConnection(context, new FakeWebSocket(), "127.0.0.1");

            var message = new HandshakeInit { HandshakeData = clientConnection.HandshakeData };
            var envelope = message.CreateEnvelope();
            envelope.Sign(clientKeyPair);

            using var writer = new XdrBufferWriter();
            var inMessage = envelope.ToIncomingMessage(writer);
            if (expectedException == null)
            {
                var isHandled = await MessageHandlers<AlphaWebSocketConnection>.HandleMessage(clientConnection, inMessage);

                Assert.IsTrue(isHandled);
                Assert.AreEqual(clientConnection.ClientPubKey, new RawPubKey(clientKeyPair.AccountId));
                if (clientConnection.ClientPubKey.Equals((RawPubKey)TestEnvironment.Auditor1KeyPair))
                    Assert.AreEqual(clientConnection.ConnectionState, ConnectionState.Validated);
                else
                    Assert.AreEqual(clientConnection.ConnectionState, ConnectionState.Ready);
            }
            else
                Assert.ThrowsAsync(expectedException, async () => await MessageHandlers<AlphaWebSocketConnection>.HandleMessage(clientConnection, inMessage));
        }

        [Test]
        public void HandshakeInvalidDataTest()
        {
            context.AppState.State = ApplicationState.Ready;

            var clientConnection = new AlphaWebSocketConnection(context, new FakeWebSocket(), "127.0.0.1");

            var handshake = new HandshakeData();
            handshake.Randomize();

            var envelope = new HandshakeInit { HandshakeData = handshake }.CreateEnvelope();
            envelope.Sign(TestEnvironment.Client1KeyPair);

            using var writer = new XdrBufferWriter();
            var inMessage = envelope.ToIncomingMessage(writer);

            Assert.ThrowsAsync<ConnectionCloseException>(async () => await MessageHandlers<AlphaWebSocketConnection>.HandleMessage(clientConnection, inMessage));
        }

        [Test]
        public async Task AuditorPerfStatisticsTest()
        {
            var auditorPerf = new AuditorPerfStatistics
            {
                BatchInfos = new List<Models.BatchSavedInfo>(),
                QuantaPerSecond = 1,
                QuantaQueueLength = 2,
                UpdateDate = DateTime.UtcNow.Ticks
            };

            var envelope = auditorPerf.CreateEnvelope();
            envelope.Sign(TestEnvironment.Auditor1KeyPair);


            var auditorConnection = new AlphaWebSocketConnection(context, new FakeWebSocket(), "127.0.0.1")
            {
                ClientPubKey = TestEnvironment.Auditor1KeyPair
            };

            using var writer = new XdrBufferWriter();
            var inMessage = envelope.ToIncomingMessage(writer);

            await AssertMessageHandling(auditorConnection, inMessage, null);

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
            context.AppState.State = ApplicationState.Ready;

            var clientConnection = new AlphaWebSocketConnection(context, new FakeWebSocket(), "127.0.0.1")
            {
                ClientPubKey = clientKeyPair.PublicKey,
                ConnectionState = state
            };

            var envelope = new SetApexCursor { Apex = 1 }.CreateEnvelope();
            envelope.Sign(clientKeyPair);
            using var writer = new XdrBufferWriter();
            var inMessage = envelope.ToIncomingMessage(writer);

            await AssertMessageHandling(clientConnection, inMessage, excpectedException);
        }

        static object[] TxNotificationTestCases =
        {
            new object[] { TestEnvironment.Client1KeyPair, ConnectionState.Validated, typeof(UnauthorizedException) },
            new object[] { TestEnvironment.Auditor1KeyPair, ConnectionState.Validated, typeof(InvalidStateException) },
            new object[] { TestEnvironment.Auditor1KeyPair, ConnectionState.Ready, null }
        };

        [Test]
        [TestCaseSource(nameof(TxNotificationTestCases))]
        public async Task TxNotificationTest(KeyPair clientKeyPair, ConnectionState state, Type excpectedException)
        {
            context.AppState.State = ApplicationState.Ready;

            var clientConnection = new AlphaWebSocketConnection(context, new FakeWebSocket(), "127.0.0.1")
            {
                ClientPubKey = clientKeyPair.PublicKey,
                ConnectionState = state
            };

            var envelope = new TxNotification
            {
                TxCursor = context.TxCursorManager.TxCursor + 1,
                Payments = new List<PaymentBase>()
            }.CreateEnvelope();
            envelope.Sign(clientKeyPair);
            using var writer = new XdrBufferWriter();
            var inMessage = envelope.ToIncomingMessage(writer);

            await AssertMessageHandling(clientConnection, inMessage, excpectedException);
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
            context.AppState.State = ApplicationState.Rising;

            var clientConnection = new AlphaWebSocketConnection(context, new FakeWebSocket(), "127.0.0.1")
            {
                ClientPubKey = clientKeyPair.PublicKey,
                ConnectionState = state
            };

            var envelope = new AuditorState
            {
                PendingQuanta = new List<MessageEnvelope>(),
                State = ApplicationState.Running
            }.CreateEnvelope();
            envelope.Sign(clientKeyPair);

            using var writer = new XdrBufferWriter();
            var inMessage = envelope.ToIncomingMessage(writer);

            await AssertMessageHandling(clientConnection, inMessage, excpectedException);
            if (excpectedException == null)
                Assert.AreEqual(context.AppState.State, ApplicationState.Running);
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
            context.AppState.State = ApplicationState.Ready;

            var clientConnection = new AlphaWebSocketConnection(context, new FakeWebSocket(), "127.0.0.1")
            {
                ClientPubKey = TestEnvironment.Client1KeyPair.PublicKey,
                ConnectionState = state
            };

            var account = context.AccountStorage.GetAccount(TestEnvironment.Client1KeyPair);

            var envelope = new OrderRequest
            {
                Account = account.Account.Id,
                RequestId = 1
            }.CreateEnvelope();
            envelope.Sign(TestEnvironment.Client1KeyPair);
            using var writer = new XdrBufferWriter();
            var inMessage = envelope.ToIncomingMessage(writer);

            await AssertMessageHandling(clientConnection, inMessage, excpectedException);
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
            context.AppState.State = ApplicationState.Ready;

            var clientConnection = new AlphaWebSocketConnection(context, new FakeWebSocket(), "127.0.0.1")
            {
                ClientPubKey = TestEnvironment.Client1KeyPair.PublicKey,
                ConnectionState = state
            };

            var account = context.AccountStorage.GetAccount(TestEnvironment.Client1KeyPair);

            var envelope = new AccountDataRequest
            {
                Account = account.Account.Id,
                RequestId = 1
            }.CreateEnvelope();
            envelope.Sign(TestEnvironment.Client1KeyPair);
            using var writer = new XdrBufferWriter();
            var inMessage = envelope.ToIncomingMessage(writer);

            await AssertMessageHandling(clientConnection, inMessage, excpectedException);
        }

        static object[] AccountRequestRateLimitsCases =
        {
            new object[] { TestEnvironment.Client2KeyPair, 10 }
        };

        [Test]
        [TestCaseSource(nameof(AccountRequestRateLimitsCases))]
        public async Task AccountRequestRateLimitTest(KeyPair clientKeyPair, int? requestLimit)
        {
            context.AppState.State = ApplicationState.Ready;

            var account = context.AccountStorage.GetAccount(clientKeyPair);
            if (requestLimit.HasValue)
            {
                //TODO: replace it with quantum
                var effect = new RequestRateLimitUpdateEffect
                {
                    Account = account.Id,
                    AccountWrapper = account,
                    RequestRateLimits = new RequestRateLimits
                    {
                        HourLimit = (uint)requestLimit.Value,
                        MinuteLimit = (uint)requestLimit.Value
                    }
                };
                var effectProcessor = new RequestRateLimitUpdateEffectProcessor(effect, context.Constellation.RequestRateLimits);
                effectProcessor.CommitEffect();
            }
            var clientConnection = new AlphaWebSocketConnection(context, new FakeWebSocket(), "127.0.0.1")
            {
                ClientPubKey = clientKeyPair,
                ConnectionState = ConnectionState.Ready,
                Account = account
            };

            var minuteLimit = (account.Account.RequestRateLimits ?? context.Constellation.RequestRateLimits).MinuteLimit;
            var minuteIterCount = minuteLimit + 1;
            for (var i = 0; i < minuteIterCount; i++)
            {
                var envelope = new AccountDataRequest
                {
                    Account = account.Account.Id,
                    RequestId = i + 1
                }.CreateEnvelope();
                envelope.Sign(clientKeyPair);
                using var writer = new XdrBufferWriter();
                var inMessage = envelope.ToIncomingMessage(writer);
                if (i + 1 > minuteLimit)
                    await AssertMessageHandling(clientConnection, inMessage, typeof(TooManyRequestsException));
                else
                    await AssertMessageHandling(clientConnection, inMessage);
            }
        }



        static object[] EffectsRequestTestCases =
        {
            new object[] { TestEnvironment.Client1KeyPair, ConnectionState.Validated, typeof(InvalidStateException) },
            new object[] { TestEnvironment.Client1KeyPair, ConnectionState.Ready, null }
        };

        [Test]
        [TestCaseSource(nameof(EffectsRequestTestCases))]
        public async Task EffectsRequestTest(KeyPair client, ConnectionState state, Type excpectedException)
        {
            context.AppState.State = ApplicationState.Ready;

            var account = context.AccountStorage.GetAccount(client);

            var clientConnection = new AlphaWebSocketConnection(context, new FakeWebSocket(), "127.0.0.1")
            {
                ClientPubKey = client,
                ConnectionState = state,
                Account = account
            };

            var envelope = new EffectsRequest { Account = account.Account.Id, AccountWrapper = account }.CreateEnvelope();
            envelope.Sign(client);

            using var writer = new XdrBufferWriter();
            var inMessage = envelope.ToIncomingMessage(writer);
            await AssertMessageHandling(clientConnection, inMessage, excpectedException);
        }
    }
}
