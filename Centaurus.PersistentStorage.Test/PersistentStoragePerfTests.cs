using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Centaurus.PersistentStorage
{
    public class PersistentStoragePerfTests
    {
        [Test, Explicit]
        public void MeasurePersistentStorageWritePerformance()
        {
            var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "db");
            Console.WriteLine("Opening db " + path);

            using (var storage = new PersistentStorage(path))
            {
                var sw = Stopwatch.StartNew();
                var ssw = Stopwatch.StartNew();
                for (var i = 0; i < 10000; i++)
                {
                    var data = TestDataGenerator.GenerateTestData(i);
                    storage.SaveBatch(data);
                    if (i % 100 == 99)
                    {
                        ssw.Stop();
                        Console.WriteLine("100 batches in " + ssw.Elapsed.TotalSeconds.ToString("0.00") + "s   => avg " + Math.Floor(100 * TestDataGenerator.QuantaPerBatch / ssw.Elapsed.TotalSeconds) + " q/s");
                        ssw.Restart();
                    }
                }
                sw.Stop();
                ssw.Stop();
                //Console.WriteLine("--- db stats ---");
                //Console.WriteLine(storage.GetStats());
                Console.WriteLine("Total time: " + sw.Elapsed.TotalSeconds.ToString("0") + "s");
            }

        }

        [Test, Explicit]
        public void QuantaLoadTest()
        {
            var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "db");
            Console.WriteLine("Opening db " + path);

            using (var storage = new PersistentStorage(path))
            {
                var quantaBatch = new List<QuantumPersistentModel> {
                    new QuantumPersistentModel { Apex = 1 },
                    new QuantumPersistentModel { Apex = 2 },
                    new QuantumPersistentModel { Apex = 101 },
                };

                storage.SaveBatch(quantaBatch.Cast<IPersistentModel>().ToList());

                var allQuanta = storage.Find<QuantumPersistentModel>();
                Assert.AreEqual(quantaBatch.Count, allQuanta.Count());
                var aboveApexQuanta = storage.Find<QuantumPersistentModel>(QueryOrder.Desc).From(UlongConverter.Encode(1)).ToList();
                Assert.AreEqual(0, aboveApexQuanta.Count);
                aboveApexQuanta = storage.Find<QuantumPersistentModel>(QueryOrder.Desc).From(UlongConverter.Encode(2)).ToList();
                Assert.AreEqual(1, aboveApexQuanta.Count);
                aboveApexQuanta = storage.Find<QuantumPersistentModel>(QueryOrder.Desc).From(UlongConverter.Encode(99)).ToList();
                Assert.AreEqual(2, aboveApexQuanta.Count);
                aboveApexQuanta = storage.Find<QuantumPersistentModel>(QueryOrder.Desc).From(UlongConverter.Encode(150)).ToList();
                Assert.AreEqual(3, aboveApexQuanta.Count);
            }
        }



        [Test]
        [TestCase(QueryOrder.Desc, 0, 1ul, null)]
        [TestCase(QueryOrder.Desc, 3, ulong.MaxValue, null)]
        [TestCase(QueryOrder.Desc, 1, 2ul, null)]
        [TestCase(QueryOrder.Desc, 2, 99ul, null)]
        [TestCase(QueryOrder.Asc, 2, 1ul, null)]
        [TestCase(QueryOrder.Asc, 0, 150ul, null)]
        [TestCase(QueryOrder.Asc, 1, 2ul, null)]
        [TestCase(QueryOrder.Asc, 1, 99ul, null)]
        public void QuantaStorageIteratorTest(QueryOrder order, int expectedCount, ulong from, ulong? to)
        {
            var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "db");
            Console.WriteLine("Opening db " + path);

            using (var storage = new PersistentStorage(path))
            {
                var quantaBatch = new List<QuantumPersistentModel> {
                    new QuantumPersistentModel { Apex = 1 },
                    new QuantumPersistentModel { Apex = 2 },
                    new QuantumPersistentModel { Apex = 101 },
                };

                storage.SaveBatch(quantaBatch.Cast<IPersistentModel>().ToList());

                var cursor = storage.Find<QuantumPersistentModel>(order).From(UlongConverter.Encode(from));
                if (to.HasValue)
                {
                    cursor.To(UlongConverter.Encode(to.Value));
                }
                var aboveApexQuanta = cursor.ToList();

                Assert.AreEqual(expectedCount, aboveApexQuanta.Count);
            }
        }
    }
}