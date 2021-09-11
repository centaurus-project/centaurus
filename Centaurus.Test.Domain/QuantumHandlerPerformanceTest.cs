using Centaurus.Domain;
using Centaurus.Models;
using Centaurus.Xdr;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Test
{
    public class QuantumHandlerPerformanceTest : BaseMessageHandlerTests
    {
        private ExecutionContext context;

        [SetUp]
        public void Setup()
        {
            EnvironmentHelper.SetTestEnvironmentVariable();
            context = GlobalInitHelper.DefaultAlphaSetup().Result;
        }


        [Test]
        public async Task PerformanceTest()
        {
            context.SetState(State.Ready);

            var connection = GetIncomingConnection(context, TestEnvironment.Client1KeyPair);

            var accountId = context.AccountStorage.GetAccount(TestEnvironment.Client1KeyPair);
            var messages = new Dictionary<Task<bool>, RequestQuantum>();
            for (var i = 0; i < 1_00_000; i++)
            {
                var quantumRequest = new RequestQuantum
                {
                    RequestEnvelope = new PaymentRequest
                    {
                        Account = accountId.Pubkey,
                        Amount = 1,
                        Asset = "XLM",
                        Destination = TestEnvironment.Client2KeyPair,
                        RequestId = DateTime.UtcNow.Ticks
                    }.CreateEnvelope().Sign(TestEnvironment.Client1KeyPair)
                };
                messages.Add(QuantumSignatureValidator.Validate(quantumRequest), quantumRequest);
            }

            var sw = new Stopwatch();
            sw.Start();
            var tasks = new List<Task>();
            foreach (var q in messages)
            {

                tasks.Add(context.QuantumHandler.HandleAsync(q.Value, q.Key));
            }

            await Task.WhenAll(tasks);

            sw.Stop();

            TestContext.Out.Write($"{messages.Count} processed in {sw.ElapsedMilliseconds}. {messages.Count / (sw.ElapsedMilliseconds / 1000)} quanta per second.");
            Assert.IsTrue(true);
        }
    }
}
