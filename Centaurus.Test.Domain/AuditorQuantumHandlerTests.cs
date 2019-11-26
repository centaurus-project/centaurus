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
    public class AuditorQuantumHandlerTests: BaseQuantumHandlerTests
    {
        [SetUp]
        public void Setup()
        {
            GlobalInitHelper.DefaultAuditorSetup();

            Global.QuantumHandler.Start();
        }

        //[Test]
        //public async Task SnapshotQuantumTest()
        //{
        //    var snapshot = Global.SnapshotManager.InitSnapshot();
        //    Global.SnapshotManager.AbortPendingSnapshot();

        //    var snapshotQuantum = new SnapshotQuantum() { Hash = snapshot.ComputeHash(), Apex = 1 };
        //    var envelope = snapshotQuantum.CreateEnvelope();
        //    await Global.QuantumHandler.HandleAsync(envelope);

        //    Assert.AreEqual(Global.SnapshotManager.LastSnapshot.Apex, 1);
        //}

        //[Test]
        //public void SnapshotFailedQuantumTest()
        //{
        //    var snapshot = new SnapshotQuantum { Apex = 1 };
        //    var envelope = snapshot.CreateEnvelope();

        //    //TODO: it should throw BadRequestExcetiop
        //    Assert.ThrowsAsync<Exception>(async () => await Global.QuantumHandler.HandleAsync(envelope));
        //}
    }
}
