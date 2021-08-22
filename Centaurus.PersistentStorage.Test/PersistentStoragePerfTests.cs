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
                var aboveApexQuanta = storage.Find<QuantumPersistentModel>(ApexConverter.EncodeApex(1));
                Assert.AreEqual(quantaBatch.Count - 1, aboveApexQuanta.Count());
            }
        }
    }
}