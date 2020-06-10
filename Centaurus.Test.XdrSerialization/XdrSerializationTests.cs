using Centaurus.Test.Contracts;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Centaurus.Xdr;
using Centaurus.Models;

namespace Centaurus.Test
{
    public class XdrSerializationTests
    {
        [SetUp]
        public void Setup()
        {
            EnvironmentHelper.SetTestEnvironmentVariable();
            TestDynamicSerializersInitializer.Init();
        }

        [TestCaseSource(nameof(GeneratePrimitives))]
        public void XdrPrimitiveTypesSerialization(PrimitiveTypes original)
        {
            var serialized = XdrConverter.Serialize(original);
            var rehydrated = XdrConverter.Deserialize<PrimitiveTypes>(serialized);
            rehydrated.Should().BeEquivalentTo(original, opt => opt.Excluding(d => d.NotSearialized), "deserialized object should match original");
        }

        [Test]
        public void XdrInheritanceSerialization()
        {
            var mammal = GenerateMammal();

            var serializedMammal = XdrConverter.Serialize(mammal);
            XdrConverter.Deserialize<Vertebrate>(serializedMammal).Should().BeEquivalentTo(mammal);
            XdrConverter.Deserialize<Tetrapod>(serializedMammal).Should().BeEquivalentTo(mammal);
            XdrConverter.Deserialize<Mammal>(serializedMammal).Should().BeEquivalentTo(mammal);

            var fish = GenerateFish();
            var serializedFish = XdrConverter.Serialize(fish);
            XdrConverter.Deserialize<Vertebrate>(serializedFish).Should().BeEquivalentTo(fish);
            XdrConverter.Deserialize<Fish>(serializedFish).Should().BeEquivalentTo(fish);
        }


        [Test]
        [Explicit]
        [Category("Performance")]
        public void XdrPrimitiveTypesSerializationPerformance()
        {
            var iterations = 1_000_000;
            var original = GeneratePrimitives().Skip(1).First();
            PerfCounter.MeasureTime(() =>
            {
                for (var i = 0; i < iterations; i++)
                {
                    var serialized = XdrConverter.Serialize(original);
                    var rehydrated = XdrConverter.Deserialize<PrimitiveTypes>(serialized);
                }
            }, () =>
            {
                return $"{typeof(PrimitiveTypes).Name} serialization - {iterations} iterations.";
            });
        }

        [Test]
        [Explicit]
        [Category("Performance")]
        public void XdrInheritanceSerializationPerformance()
        {
            var iterations = 1_000_000;
            var original = GeneratePrimitives().First();
            var mammal = GenerateMammal();
            PerfCounter.MeasureTime(() =>
            {
                for (var i = 0; i < iterations; i++)
                {
                    var serializedMammal = XdrConverter.Serialize(mammal);
                    XdrConverter.Deserialize<Vertebrate>(serializedMammal);
                }
            }, () =>
            {
                return $"Inheritance serialization - {iterations} iterations.";
            });
        }

        #region TEST_DATA_GENERATORS

        public static IEnumerable<PrimitiveTypes> GeneratePrimitives()
        {
            yield return new PrimitiveTypes
            {
                Bool = false,
                Int32 = 10,
                Int64 = 10,
                UInt32 = 10,
                UInt64 = 10,
                Double = 1,
                String = "test",
                ByteArray = new byte[10],
                EnumInt32 = EnumInt32Values.Zero,
                Int32List = null,
                DoubleArray = new double[0],
                NotSearialized = "----"
            };
            yield return new PrimitiveTypes
            {
                Bool = true,
                Int32 = 2112312312,
                Int64 = long.MaxValue,
                UInt32 = 4283674822,
                UInt64 = ulong.MaxValue,
                Double = 234234.5324223423,
                String = "test!21ljdfsiufasrk7r92734923",
                ByteArray = 32.RandomBytes(),
                EnumInt32 = EnumInt32Values.Two,
                Int32List = new List<int> { 1, 2, 64654564 },
                DoubleArray = new double[] { 1.23467, double.MinValue },
                //Int32Nullable = 15,
            };
        }

        public Fish GenerateFish()
        {
            return new Fish
            {
                Bones = FishBoneStructure.LobeFinned,
                ColdBlooded = true,
                HasScales = true,
                Species = "Actinistia",
                Features = new List<Feature> {
                    new Feature {FeatureDescription="Living fossil" },
                    new Feature {FeatureDescription="Vestigial lung", Unique=true },
                    new Feature {FeatureDescription="Has second rudimentary tale", Unique=true }
                }
            };
        }

        public Mammal GenerateMammal()
        {
            return new Mammal()
            {
                ColdBlooded = false,
                Fur = new Fur { Color = "Black", Coverage = 92.2, Length = 5 },
                Habitat = "Rivers and Oceans",
                Species = "Otter",
                Features = new List<Feature> {
                    new Feature {FeatureDescription="Extremely cute" },
                    new Feature {FeatureDescription="Always carries a pebble in pocket", Unique=true }
                }
            };
        }
        #endregion
    }
}