using Centaurus.Test;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Centaurus.PersistentStorage.Test
{
    public class CursorTests
    {
        private PersistentStorage storage;
        private string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "db");

        private byte[][] accounts = { 
            Enumerable.Range(0, 32).Select(i => (byte)i).ToArray(),
            Enumerable.Range(1, 32).Select(i => (byte)i).ToArray(),
            Enumerable.Range(2, 32).Select(i => (byte)i).ToArray() 
        };

        [OneTimeSetUp]
        public void Setup()
        {
            Console.WriteLine("Opening db " + path);
            storage = new PersistentStorage(path);

            var quantaBatch = new List<QuantumPersistentModel> {
                    new QuantumPersistentModel { Apex = 1 },
                    new QuantumPersistentModel { Apex = 2 },
                    new QuantumPersistentModel { Apex = 101 },
                };

            var quantumRefsBatch = new List<QuantumRefPersistentModel> {
                    new QuantumRefPersistentModel { Apex = 1, Account = accounts[0] },
                    new QuantumRefPersistentModel { Apex = 2, Account = accounts[0] },
                    new QuantumRefPersistentModel { Apex = 3, Account = accounts[0] },
                    new QuantumRefPersistentModel { Apex = 4, Account = accounts[1] },
                    new QuantumRefPersistentModel { Apex = 101, Account = accounts[2] },
                };

            storage.SaveBatch(quantaBatch.Cast<IPersistentModel>().Concat(quantumRefsBatch).ToList());
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            storage?.Dispose();
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }

        [Test]
        [TestCase(QueryOrder.Desc, 0, 1ul, null)]
        [TestCase(QueryOrder.Desc, 3, ulong.MaxValue, null)]
        [TestCase(QueryOrder.Desc, 1, 2ul, null)]
        [TestCase(QueryOrder.Desc, 2, 99ul, null)]
        [TestCase(QueryOrder.Desc, 1, 99ul, 2ul)]
        [TestCase(QueryOrder.Asc, 2, 1ul, null)]
        [TestCase(QueryOrder.Asc, 1, 1ul, 2ul)]
        [TestCase(QueryOrder.Asc, 0, 150ul, null)]
        [TestCase(QueryOrder.Asc, 1, 2ul, null)]
        [TestCase(QueryOrder.Asc, 1, 99ul, null)]
        public void QuantaStorageIteratorTest(QueryOrder order, int expectedCount, ulong from, ulong? to)
        {
            var cursor = storage.Find<QuantumPersistentModel>(order).From(UlongConverter.Encode(from));
            if (to.HasValue)
            {
                cursor.To(UlongConverter.Encode(to.Value));
            }
            var aboveApexQuanta = cursor.ToList();

            Assert.AreEqual(expectedCount, aboveApexQuanta.Count);
        }



        [Test]
        [TestCase(QueryOrder.Asc, 3, 0, 0ul, null)]
        [TestCase(QueryOrder.Asc, 2, 0, 1ul, null)]
        [TestCase(QueryOrder.Asc, 1, 0, 1ul, 2ul)]
        [TestCase(QueryOrder.Asc, 5, null, 0ul, null)]
        [TestCase(QueryOrder.Asc, 1, 2, 100ul, 110ul)]
        [TestCase(QueryOrder.Desc, 3, 0, 100ul, null)]
        [TestCase(QueryOrder.Desc, 2, 0, 110ul, null)]
        [TestCase(QueryOrder.Desc, 1, 2, 110ul, 100ul)]
        [TestCase(QueryOrder.Desc, 0, 0, 100ul, 110ul)]
        [TestCase(QueryOrder.Desc, 5, null, 0ul, null)]
        public void QuantaRefStorageIteratorTest(QueryOrder order, int expectedCount, int? accountIndex, ulong from, ulong? to)
        {
            var cursor = accountIndex.HasValue
                ? storage.Find<QuantumRefPersistentModel>(accounts[accountIndex.Value], order)
                : storage.Find<QuantumRefPersistentModel>(order);

            cursor.From(UlongConverter.Encode(from));

            if (to.HasValue)
            {
                cursor.To(UlongConverter.Encode(to.Value));
            }
            var aboveApexQuanta = cursor.ToList();

            if (accountIndex.HasValue)
                aboveApexQuanta.ForEach(q => Assert.IsTrue(q.Account.AsSpan().SequenceEqual(accounts[accountIndex.Value])));

            Assert.AreEqual(expectedCount, aboveApexQuanta.Count);
        }
    }
}
