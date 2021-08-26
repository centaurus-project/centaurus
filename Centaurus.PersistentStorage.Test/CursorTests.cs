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
                    new QuantumRefPersistentModel { Apex = 1, AccountId = 1 },
                    new QuantumRefPersistentModel { Apex = 2, AccountId = 1 },
                    new QuantumRefPersistentModel { Apex = 3, AccountId = 1 },
                    new QuantumRefPersistentModel { Apex = 4, AccountId = 2 },
                    new QuantumRefPersistentModel { Apex = 101, AccountId = 3 },
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
        [TestCase(QueryOrder.Asc, 3, 1ul, 0ul, null)]
        [TestCase(QueryOrder.Asc, 2, 1ul, 1ul, null)]
        [TestCase(QueryOrder.Asc, 1, 1ul, 1ul, 2ul)]
        [TestCase(QueryOrder.Asc, 5, null, 0ul, null)]
        [TestCase(QueryOrder.Asc, 1, 3ul, 100ul, 110ul)]
        [TestCase(QueryOrder.Desc, 3, 1ul, 100ul, null)]
        [TestCase(QueryOrder.Desc, 2, 1ul, 110ul, null)]
        [TestCase(QueryOrder.Desc, 1, 3ul, 110ul, 100ul)]
        [TestCase(QueryOrder.Desc, 0, 1ul, 100ul, 110ul)]
        [TestCase(QueryOrder.Desc, 5, null, 0ul, null)]
        public void QuantaRefStorageIteratorTest(QueryOrder order, int expectedCount, ulong? account, ulong from, ulong? to)
        {
            //var startFrom = new QuantumRefPersistentModel
            //{ AccountId = account, Apex = 1 }.Key;
            var cursor = account.HasValue
                ? storage.Find<QuantumRefPersistentModel>(UlongConverter.Encode(account.Value), order)
                : storage.Find<QuantumRefPersistentModel>(order);

            cursor.From(UlongConverter.Encode(from));

            if (to.HasValue)
            {
                cursor.To(UlongConverter.Encode(to.Value));
            }
            var aboveApexQuanta = cursor.ToList();

            if (account.HasValue)
                aboveApexQuanta.ForEach(q => Assert.AreEqual(q.AccountId, account.Value));

            Assert.AreEqual(expectedCount, aboveApexQuanta.Count);
        }
    }
}
