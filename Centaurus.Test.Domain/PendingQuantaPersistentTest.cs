using Centaurus.Domain;
using Centaurus.Models;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Test
{
    public class PendingQuantaPersistentTest
    {
        private MockStorage storage;
        private Settings settings;
        private ExecutionContext context;

        [SetUp]
        public async Task Setup()
        {
            settings = GlobalInitHelper.GetAlphaSettings();
            storage = new MockStorage();
            context = await GlobalInitHelper.DefaultAlphaSetup(storage, settings);
        }

        [TearDown]
        public void TearDown()
        {
            context?.Dispose();
        }

        [Test]
        public async Task PendingQuantaTest()
        {
            var result = await context.QuantumHandler.HandleAsync(new AccountDataRequestQuantum
            {
                RequestEnvelope = new AccountDataRequest
                {
                    Account = TestEnvironment.Client1KeyPair,
                    RequestId = DateTime.UtcNow.Ticks
                }.CreateEnvelope().Sign(TestEnvironment.Client1KeyPair)
            }, Task.FromResult(true)).OnProcessed;

            //run it in separate thread to avoid deadlock
            await Task.Factory.StartNew(() =>
            {
                context.Complete();
                context.Dispose();
            });

            //verify that quantum is in the storage
            var quantumModel = storage.LoadPendingQuanta().Quanta.LastOrDefault();

            Assert.NotNull(quantumModel);

            var item = quantumModel.ToCatchupQuantaBatchItem();
            var quantum = (Quantum)item.Quantum;

            Assert.IsTrue(context.Settings.KeyPair.Verify(quantum.GetPayloadHash(), item.Signatures.First().PayloadSignature.Data));

            Assert.AreEqual(quantum.Apex, result.Apex);
            Assert.IsInstanceOf<AccountDataRequestQuantum>(item.Quantum);

            context = new ExecutionContext(settings, storage, new MockPaymentProviderFactory(), new DummyConnectionWrapperFactory());
            Assert.AreEqual(State.Rising, context.NodesManager.CurrentNode.State);

            await context.Catchup.AddNodeBatch(TestEnvironment.Auditor1KeyPair, new CatchupQuantaBatch { Quanta = new List<CatchupQuantaBatchItem>(), HasMore = false });

            Assert.AreEqual(State.Running, context.NodesManager.CurrentNode.State);

            var targetQuantum = context.SyncStorage.GetQuanta(quantum.Apex - 1, 1).FirstOrDefault();

            Assert.NotNull(targetQuantum);
            var quantumFromStorage = (Quantum)targetQuantum.Quantum;
            Assert.IsTrue(context.Settings.KeyPair.Verify(quantumFromStorage.GetPayloadHash(), item.Signatures.First().PayloadSignature.Data));

            Assert.AreEqual(quantumFromStorage.Apex, result.Apex);
            Assert.IsInstanceOf<AccountDataRequestQuantum>(targetQuantum.Quantum);

            Assert.IsNull(context.PersistentStorage.LoadPendingQuanta());
        }
    }
}
