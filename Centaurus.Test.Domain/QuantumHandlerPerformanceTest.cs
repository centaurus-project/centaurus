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
    internal class QuantumHandlerPerformanceTest : BaseMessageHandlerTests
    {
        private ExecutionContext context;

        [SetUp]
        public async Task Setup()
        {
            EnvironmentHelper.SetTestEnvironmentVariable();
            context = await GlobalInitHelper.DefaultAlphaSetup();
        }


        [Test]
        [Explicit]
        public async Task PerformanceTest()
        {
            context.SetState(State.Ready);

            var accountId = context.AccountStorage.GetAccount(TestEnvironment.Client1KeyPair);
            var messages = new Dictionary<RequestQuantum, Task<bool>>();
            for (var i = 0; i < 100_000; i++)
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
                messages.Add(quantumRequest, QuantumSignatureValidator.Validate(quantumRequest));
            }

            var sw = new Stopwatch();
            sw.Start();
            var tasks = new List<Task>();
            foreach (var q in messages)
            {
                tasks.Add(context.QuantumHandler.HandleAsync(q.Key, q.Value).OnProcessed);
            }

            await Task.WhenAll(tasks);

            sw.Stop();

            TestContext.Out.Write($"{messages.Count} processed in {sw.ElapsedMilliseconds}. {decimal.Divide(messages.Count, decimal.Divide(sw.ElapsedMilliseconds, 1000)).ToString(".##")} quanta per second.");
            Assert.IsTrue(true);
        }
    }
}
