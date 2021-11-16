using Centaurus.Domain;
using Centaurus.Models;
using Centaurus.Xdr;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Centaurus.Test
{
    public class AlphaMessageHandlersTests : BaseMessageHandlerTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            EnvironmentHelper.SetTestEnvironmentVariable();
            context = GlobalInitHelper.DefaultAlphaSetup().Result;
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            context?.Dispose();
        }

        private ExecutionContext context;


        static object[] HandshakeTestCases =
        {
            new object[] { TestEnvironment.Client1KeyPair, State.Rising, typeof(ConnectionCloseException) },
            new object[] { TestEnvironment.Client1KeyPair, State.Ready, null },
            new object[] { TestEnvironment.Auditor1KeyPair, State.Rising, typeof(UnauthorizedException) },
            new object[] { TestEnvironment.Auditor1KeyPair,  State.Ready, typeof(UnauthorizedException) }
        };

        [Test]
        [TestCaseSource(nameof(HandshakeTestCases))]
        public async Task HandshakeTest(KeyPair clientKeyPair, State alphaState, Type expectedException)
        {
            context.SetState(alphaState);

            var connection = GetIncomingConnection(context, clientKeyPair);

            var handshakeData = (HandshakeData)typeof(IncomingConnectionBase)
                .GetField("handshakeData", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(connection);

            var message = new HandshakeResponse { HandshakeData = handshakeData };
            var envelope = message.CreateEnvelope();
            envelope.Sign(clientKeyPair);

            using var writer = new XdrBufferWriter();
            var inMessage = envelope.ToIncomingMessage(writer);
            if (expectedException == null)
            {
                var isHandled = await context.MessageHandlers.HandleMessage(connection, inMessage);

                Assert.IsTrue(isHandled);
                Assert.AreEqual(connection.ConnectionState, ConnectionState.Ready);
            }
            else
                Assert.ThrowsAsync(expectedException, async () => await context.MessageHandlers.HandleMessage(connection, inMessage));
        }
        static object[] AuditorHandshakeTestCases =
        {
            new object[] { TestEnvironment.Client1KeyPair, State.Rising, typeof(UnauthorizedException) },
            new object[] { TestEnvironment.Client1KeyPair, State.Ready, typeof(UnauthorizedException) },
            new object[] { TestEnvironment.Auditor1KeyPair, State.Rising, null },
            new object[] { TestEnvironment.Auditor1KeyPair,  State.Ready, null }
        };

        [Test]
        [TestCaseSource(nameof(AuditorHandshakeTestCases))]
        public async Task AuditorHandshakeTest(KeyPair clientKeyPair, State alphaState, Type expectedException)
        {
            context.SetState(alphaState);

            var connection = GetIncomingConnection(context, clientKeyPair);

            var handshakeData = (HandshakeData)typeof(IncomingConnectionBase)
                .GetField("handshakeData", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(connection);

            var message = new AuditorHandshakeResponse { HandshakeData = handshakeData };
            var envelope = message.CreateEnvelope();
            envelope.Sign(clientKeyPair);

            using var writer = new XdrBufferWriter();
            var inMessage = envelope.ToIncomingMessage(writer);
            if (expectedException == null)
            {
                var isHandled = await context.MessageHandlers.HandleMessage(connection, inMessage);

                Assert.IsTrue(isHandled);
                Assert.AreEqual(connection.ConnectionState, ConnectionState.Ready);
            }
            else
                Assert.ThrowsAsync(expectedException, async () => await context.MessageHandlers.HandleMessage(connection, inMessage));
        }

        [Test]
        public void HandshakeInvalidDataTest()
        {
            context.SetState(State.Ready);

            var clientConnection = GetIncomingConnection(context, TestEnvironment.Client1KeyPair);

            var handshake = new HandshakeData();
            handshake.Randomize();

            var envelope = new HandshakeResponse { HandshakeData = handshake }.CreateEnvelope();
            envelope.Sign(TestEnvironment.Client1KeyPair);

            using var writer = new XdrBufferWriter();
            var inMessage = envelope.ToIncomingMessage(writer);

            Assert.ThrowsAsync<ConnectionCloseException>(async () => await context.MessageHandlers.HandleMessage(clientConnection, inMessage));
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


            var auditorConnection = GetIncomingConnection(context, TestEnvironment.Auditor1KeyPair.PublicKey, ConnectionState.Ready);

            using var writer = new XdrBufferWriter();
            var inMessage = envelope.ToIncomingMessage(writer);

            await AssertMessageHandling(auditorConnection, inMessage, null);

        }

        static object[] QuantaBatchTestCases =
        {
            new object[] { TestEnvironment.Client1KeyPair, ConnectionState.Ready, typeof(UnauthorizedException) },
            new object[] { TestEnvironment.Auditor1KeyPair, ConnectionState.Ready, null }
        };

        [Test]
        [TestCaseSource(nameof(QuantaBatchTestCases))]
        public async Task CatchupQuantaBatchTest(KeyPair clientKeyPair, ConnectionState state, Type excpectedException)
        {
            context.SetState(State.Rising);

            var clientConnection = GetIncomingConnection(context, clientKeyPair.PublicKey, state);

            var envelope = new CatchupQuantaBatch
            {
                Quanta = new List<CatchupQuantaBatchItem>()
            }.CreateEnvelope();
            envelope.Sign(clientKeyPair);

            using var writer = new XdrBufferWriter();
            var inMessage = envelope.ToIncomingMessage(writer);

            await AssertMessageHandling(clientConnection, inMessage, excpectedException);
        }

        static object[] QuantumMajoritySignaturesBatchCases =
        {
            new object[] { TestEnvironment.Client1KeyPair, ConnectionState.Ready, typeof(UnauthorizedException) },
            new object[] { TestEnvironment.Auditor1KeyPair, ConnectionState.Connected, typeof(InvalidStateException) },
            new object[] { TestEnvironment.Auditor1KeyPair, ConnectionState.Ready, null }
        };
        public async Task QuantumMajoritySignaturesBatchTest(KeyPair clientKeyPair, ConnectionState state, Type excpectedException)
        {
            context.SetState(State.Ready);

            var clientConnection = GetIncomingConnection(context, clientKeyPair.PublicKey, state);

            var envelope = new QuantumMajoritySignaturesBatch
            {
                Signatures = new List<QuantumSignatures>()
            }.CreateEnvelope();
            envelope.Sign(clientKeyPair);

            using var writer = new XdrBufferWriter();
            var inMessage = envelope.ToIncomingMessage(writer);

            await AssertMessageHandling(clientConnection, inMessage, excpectedException);
            if (excpectedException == null)
                Assert.AreEqual(context.StateManager.State, State.Running);
        }

        static object[] OrderTestCases =
        {
            new object[] { ConnectionState.Connected, typeof(InvalidStateException) },
            new object[] { ConnectionState.Ready, null }
        };

        [Test]
        [TestCaseSource(nameof(OrderTestCases))]
        public async Task OrderTest(ConnectionState state, Type excpectedException)
        {
            context.SetState(State.Ready);

            var clientConnection = GetIncomingConnection(context, TestEnvironment.Client1KeyPair.PublicKey, state);

            var account = context.AccountStorage.GetAccount(TestEnvironment.Client1KeyPair);

            var envelope = new OrderRequest
            {
                Account = account.Pubkey,
                RequestId = DateTime.UtcNow.Ticks,
                Amount = 100,
                Asset = context.Constellation.Assets.First(a => !a.IsQuoteAsset).Code,
                Price = 1,
                Side = OrderSide.Buy
            }.CreateEnvelope();
            envelope.Sign(TestEnvironment.Client1KeyPair);
            using var writer = new XdrBufferWriter();
            var inMessage = envelope.ToIncomingMessage(writer);

            await AssertMessageHandling(clientConnection, inMessage, excpectedException);
        }

        static object[] AccountDataTestRequestCases =
        {
            new object[] { ConnectionState.Connected, typeof(InvalidStateException) },
            new object[] { ConnectionState.Ready, null }
        };

        [Test]
        [TestCaseSource(nameof(AccountDataTestRequestCases))]
        public async Task AccountDataRequestTest(ConnectionState state, Type excpectedException)
        {
            context.SetState(State.Ready);

            var clientConnection = (IncomingClientConnection)GetIncomingConnection(context, TestEnvironment.Client1KeyPair.PublicKey, state);

            var envelope = new AccountDataRequest
            {
                Account = clientConnection.Account.Pubkey,
                RequestId = 1
            }.CreateEnvelope();
            envelope.Sign(TestEnvironment.Client1KeyPair);
            using var writer = new XdrBufferWriter();
            var inMessage = envelope.ToIncomingMessage(writer);

            await AssertMessageHandling(clientConnection, inMessage, excpectedException);
        }

        //static object[] AccountRequestRateLimitsCases =
        //{
        //    new object[] { TestEnvironment.Client2KeyPair, 10 }
        //};

        //[Test]
        //[TestCaseSource(nameof(AccountRequestRateLimitsCases))]
        //public async Task AccountRequestRateLimitTest(KeyPair clientKeyPair, int? requestLimit)
        //{
        //    context.SetState(State.Ready);

        //    var account = context.AccountStorage.GetAccount(clientKeyPair);
        //    if (requestLimit.HasValue)
        //    {
        //        //TODO: replace it with quantum
        //        var effect = new RequestRateLimitUpdateEffect
        //        {
        //            Account = account.Pubkey,
        //            RequestRateLimits = new RequestRateLimits
        //            {
        //                HourLimit = (uint)requestLimit.Value,
        //                MinuteLimit = (uint)requestLimit.Value
        //            }
        //        };
        //        var effectProcessor = new RequestRateLimitUpdateEffectProcessor(effect, account, context.Constellation.RequestRateLimits);
        //        effectProcessor.CommitEffect();
        //    }
        //    var clientConnection = GetIncomingConnection(context, clientKeyPair.PublicKey, ConnectionState.Ready);

        //    var minuteLimit = (account.RequestRateLimits ?? context.Constellation.RequestRateLimits).MinuteLimit;
        //    var minuteIterCount = minuteLimit + 1;
        //    for (var i = 0; i < minuteIterCount; i++)
        //    {
        //        var envelope = new AccountDataRequest
        //        {
        //            Account = account.Pubkey,
        //            RequestId = i + 1
        //        }.CreateEnvelope();
        //        envelope.Sign(clientKeyPair);
        //        using var writer = new XdrBufferWriter();
        //        var inMessage = envelope.ToIncomingMessage(writer);
        //        if (i + 1 > minuteLimit)
        //            await AssertMessageHandling(clientConnection, inMessage, typeof(TooManyRequestsException));
        //        else
        //            await AssertMessageHandling(clientConnection, inMessage);
        //    }
        //}



        static object[] EffectsRequestTestCases =
        {
            new object[] { TestEnvironment.Client1KeyPair, ConnectionState.Connected, typeof(InvalidStateException) },
            new object[] { TestEnvironment.Client1KeyPair, ConnectionState.Ready, null }
        };

        [Test]
        [TestCaseSource(nameof(EffectsRequestTestCases))]
        public async Task EffectsRequestTest(KeyPair client, ConnectionState state, Type excpectedException)
        {
            context.SetState(State.Ready);

            var account = context.AccountStorage.GetAccount(client);

            var clientConnection = GetIncomingConnection(context, client.PublicKey, state);

            var envelope = new QuantumInfoRequest { Account = account.Pubkey }.CreateEnvelope();
            envelope.Sign(client);

            using var writer = new XdrBufferWriter();
            var inMessage = envelope.ToIncomingMessage(writer);
            await AssertMessageHandling(clientConnection, inMessage, excpectedException);
        }
    }
}
