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
        public void Setup()
        {
            settings = GlobalInitHelper.GetAlphaSettings();
            storage = new MockStorage();
            context = GlobalInitHelper.DefaultAlphaSetup(storage, settings).Result;
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

            context.Complete();

            //verify that quantum is in the storage
            var quantumModel = storage.LoadPendingQuanta().Quanta.LastOrDefault();

            Assert.NotNull(quantumModel);

            var quantum = quantumModel.ToDomainModel();

            Assert.IsTrue(context.Settings.KeyPair.Verify(quantum.Quantum.GetPayloadHash(), quantum.Signatures.First().PayloadSignature.Data));

            Assert.AreEqual(quantum.Quantum.Apex, result.Apex);
            Assert.IsInstanceOf<AccountDataRequestQuantum>(quantum.Quantum);

            context = new ExecutionContext(settings, storage, new MockPaymentProviderFactory(), new DummyConnectionWrapperFactory());
            Assert.AreEqual(State.Rising, context.StateManager.State);

            var targetQuantum = context.QuantumStorage.GetQuanta(quantum.Quantum.Apex - 1, 1).FirstOrDefault();

            Assert.NotNull(targetQuantum);

            Assert.IsTrue(context.Settings.KeyPair.Verify(targetQuantum.Quantum.GetPayloadHash(), quantum.Signatures.First().PayloadSignature.Data));

            Assert.AreEqual(targetQuantum.Quantum.Apex, result.Apex);
            Assert.IsInstanceOf<AccountDataRequestQuantum>(targetQuantum.Quantum);

            context.StateManager.Rised();
            Assert.AreEqual(State.Running, context.StateManager.State);

            Assert.IsNull(context.PersistentStorage.LoadPendingQuanta());
        }
    }
}
