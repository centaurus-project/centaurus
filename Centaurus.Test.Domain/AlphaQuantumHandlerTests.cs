using Centaurus.Domain;
using Centaurus.Models;
using NUnit.Framework;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Test
{
    public class AlphaQuantumHandlerTests: BaseQuantumHandlerTests
    {
        [SetUp]
        public void Setup()
        {
            GlobalInitHelper.DefaultAlphaSetup();

            Global.QuantumHandler.Start();
        }

        [Test]
        public async Task SnapshotQuantumTest()
        {
            var snapshot = new SnapshotQuantum();
            var envelope = snapshot.CreateEnvelope();
            await Global.QuantumHandler.HandleAsync(envelope);

            //emulate quorum
            var res = envelope.CreateResult(ResultStatusCodes.Success).CreateEnvelope();
            res.Sign(TestEnvironment.Auditor1KeyPair);

            Global.SnapshotManager.SetResult(res);

            Assert.AreEqual(Global.SnapshotManager.LastSnapshot.Id, 2);
        }

        [Test]
        public async Task SnapshotFailQuantumTest()
        {
            var snapshot = new SnapshotQuantum();
            var envelope = snapshot.CreateEnvelope();
            await Global.QuantumHandler.HandleAsync(envelope);

            snapshot = new SnapshotQuantum();
            envelope = snapshot.CreateEnvelope();
            Assert.ThrowsAsync<InvalidOperationException>(async () => await Global.QuantumHandler.HandleAsync(envelope));
        }
    }
}
