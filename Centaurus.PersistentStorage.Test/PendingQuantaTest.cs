using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace Centaurus.PersistentStorage.Test
{
    public class PendingQuantaTest
    {
        private string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "db");
        private PersistentStorage storage;

        [OneTimeSetUp]
        public void Setup()
        {
            Console.WriteLine("Opening db " + path);
            storage = new PersistentStorage(path);
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            storage?.Dispose();
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }

        const int pendingQuantaCount = 100;

        [Test, Order(1)]
        public void SavePendingQuantaTest()
        {
            var quanta = new List<PendingQuantumPersistentModel>();
            for (int i = 1; i <= pendingQuantaCount; i++)
            {
                quanta.Add(new PendingQuantumPersistentModel { 
                    RawQuantum = new byte[0],
                    Signatures = new List<SignatureModel>()
                });
            }
            var pendingQuanta = new PendingQuantaPersistentModel { Quanta = quanta };
            storage.SaveBatch(new List<IPersistentModel> { pendingQuanta });
            Assert.IsTrue(true);
        }

        [Test, Order(2)]
        public void LoadPendingQuantaTest()
        {
            var pendingQuanta = storage.First<PendingQuantaPersistentModel>();
            Assert.NotNull(pendingQuanta, "No pending quanta in db.");
            Assert.AreEqual(pendingQuantaCount, pendingQuanta.Quanta.Count, "Fetched quanta count not equal to expected one.");
        }

        [Test, Order(3)]
        public void DeletePendingQuantaTest()
        {
            storage.Delete<PendingQuantaPersistentModel>(PendingQuantaPersistentModel.KeyValue);
            var pendingQuanta = storage.First<PendingQuantaPersistentModel>();
            Assert.IsNull(pendingQuanta, "Pending quanta wasn't deleted from db.");
        }
    }
}