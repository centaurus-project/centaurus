﻿using Centaurus.Domain;
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
    internal class AlphaMessageHandlersTests : BaseMessageHandlerTests
    {
        [OneTimeSetUp]
        public async Task Setup()
        {
            EnvironmentHelper.SetTestEnvironmentVariable();
            context = await GlobalInitHelper.DefaultAlphaSetup();
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
            new object[] { TestEnvironment.Auditor1KeyPair, State.Rising, null},
            new object[] { TestEnvironment.Auditor1KeyPair,  State.Ready, null }
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
                Assert.IsTrue(connection.IsAuthenticated);
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

        static object[] QuantaBatchTestCases =
        {
            new object[] { TestEnvironment.Client1KeyPair, true, typeof(UnauthorizedException) },
            new object[] { TestEnvironment.Auditor1KeyPair, true, null }
        };

        [Test]
        [TestCaseSource(nameof(QuantaBatchTestCases))]
        public async Task CatchupQuantaBatchTest(KeyPair clientKeyPair, bool isAuthenticated, Type excpectedException)
        {
            context.SetState(State.Rising);

            var clientConnection = GetIncomingConnection(context, clientKeyPair.PublicKey, isAuthenticated);

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
            new object[] { TestEnvironment.Client1KeyPair, true, typeof(UnauthorizedException) },
            new object[] { TestEnvironment.Auditor1KeyPair, false, typeof(UnauthorizedException) },
            new object[] { TestEnvironment.Auditor1KeyPair, true, null }
        };
        public async Task QuantumMajoritySignaturesBatchTest(KeyPair clientKeyPair, bool isAuthenticated, Type excpectedException)
        {
            context.SetState(State.Ready);

            var clientConnection = GetIncomingConnection(context, clientKeyPair.PublicKey, isAuthenticated);

            var envelope = new MajoritySignaturesBatch
            {
                Items = new List<MajoritySignaturesBatchItem>()
            }.CreateEnvelope();
            envelope.Sign(clientKeyPair);

            using var writer = new XdrBufferWriter();
            var inMessage = envelope.ToIncomingMessage(writer);

            await AssertMessageHandling(clientConnection, inMessage, excpectedException);
            if (excpectedException == null)
                Assert.AreEqual(context.NodesManager.CurrentNode.State, State.Running);
        }

        static object[] OrderTestCases =
        {
            new object[] { false, typeof(UnauthorizedException) },
            new object[] { true, null }
        };

        [Test]
        [TestCaseSource(nameof(OrderTestCases))]
        public async Task OrderTest(bool isAuthenticated, Type excpectedException)
        {
            context.SetState(State.Ready);

            var clientConnection = GetIncomingConnection(context, TestEnvironment.Client1KeyPair.PublicKey, isAuthenticated);

            var account = context.AccountStorage.GetAccount(TestEnvironment.Client1KeyPair);

            var envelope = new OrderRequest
            {
                Account = account.Pubkey,
                RequestId = DateTime.UtcNow.Ticks,
                Amount = 100,
                Asset = context.ConstellationSettingsManager.Current.Assets.First(a => !a.IsQuoteAsset).Code,
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
            new object[] { false, typeof(UnauthorizedException) },
            new object[] {true, null }
        };

        [Test]
        [TestCaseSource(nameof(AccountDataTestRequestCases))]
        public async Task AccountDataRequestTest(bool isAuthenticated, Type excpectedException)
        {
            context.SetState(State.Ready);

            var clientConnection = (IncomingClientConnection)GetIncomingConnection(context, TestEnvironment.Client1KeyPair.PublicKey, isAuthenticated);

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
        //        var effectProcessor = new RequestRateLimitUpdateEffectProcessor(effect, account, context.ConstellationSettingsManager.Current.RequestRateLimits);
        //        effectProcessor.CommitEffect();
        //    }
        //    var clientConnection = GetIncomingConnection(context, clientKeyPair.PublicKey, ConnectionState.Ready);

        //    var minuteLimit = (account.RequestRateLimits ?? context.ConstellationSettingsManager.Current.RequestRateLimits).MinuteLimit;
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
            new object[] { TestEnvironment.Client1KeyPair, false, typeof(UnauthorizedException) },
            new object[] { TestEnvironment.Client1KeyPair, true, null }
        };

        [Test]
        [TestCaseSource(nameof(EffectsRequestTestCases))]
        public async Task EffectsRequestTest(KeyPair client, bool isAuthenticated, Type excpectedException)
        {
            context.SetState(State.Ready);

            var account = context.AccountStorage.GetAccount(client);

            var clientConnection = GetIncomingConnection(context, client.PublicKey, isAuthenticated);

            var envelope = new QuantumInfoRequest { Account = account.Pubkey }.CreateEnvelope();
            envelope.Sign(client);

            using var writer = new XdrBufferWriter();
            var inMessage = envelope.ToIncomingMessage(writer);
            await AssertMessageHandling(clientConnection, inMessage, excpectedException);
        }
    }
}
