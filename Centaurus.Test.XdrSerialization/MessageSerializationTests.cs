using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Centaurus.Xdr;
using NUnit.Framework;
using stellar_dotnet_sdk.xdr;

namespace Centaurus.Test
{
    public class MessageSerializationTests
    {
        [SetUp]
        public void Setup()
        {
            EnvironmentHelper.SetTestEnvironmentVariable();
            DynamicSerializersInitializer.Init();
        }

        [Test]
        public void OrderSerializationTest()
        {
            var pubkey = new byte[32];
            var signature = new byte[64];
            Array.Fill(pubkey, (byte)10);
            Array.Fill(signature, (byte)64);
            var original = new OrderRequest()
            {
                Account = pubkey,
                Amount = 2131231,
                Nonce = 1,
                TimeInForce = TimeInForce.ImmediateOrCancel,
                Asset = 5,
                Price = 23423.4325
            };
            var message = new MessageEnvelope()
            {
                Message = original,
                Signatures = new List<Ed25519Signature> { new Ed25519Signature() { Signer = pubkey, Signature = signature } }
            };
            var deserializedMessage = XdrConverter.Deserialize<MessageEnvelope>(XdrConverter.Serialize(message));
            var deserialized = deserializedMessage.Message as OrderRequest;
            Assert.AreEqual(signature, deserializedMessage.Signatures[0].Signature);
            Assert.IsTrue(original.Account.Data.SequenceEqual(deserialized.Account.Data));
            Assert.AreEqual(original.Amount, deserialized.Amount);
            Assert.AreEqual(original.TimeInForce, deserialized.TimeInForce);
            Assert.AreEqual(original.Asset, deserialized.Asset);
            Assert.AreEqual(original.Nonce, deserialized.Nonce);
            Assert.AreEqual(original.Price, deserialized.Price);
        }

        [Test]
        public void AssetsNullValueSerializationTest()
        {
            var asset = new AssetSettings { Code = "XLM" };
            var rawData = XdrConverter.Serialize(asset);
            asset = XdrConverter.Deserialize<AssetSettings>(rawData);
            Assert.AreEqual(null, asset.Issuer);
        }

        [Test]
        public void XdrCompatibilityTest()
        {
            var testArray = new byte[32];
            Array.Fill(testArray, (byte)100);

            //forward compatibility
            var legacyXdrSerializationStream = new XdrDataOutputStream();
            legacyXdrSerializationStream.WriteInt(435);
            legacyXdrSerializationStream.WriteUInt(435);
            legacyXdrSerializationStream.WriteLong(43546345634657565L);
            legacyXdrSerializationStream.WriteDoubleArray(new double[] { 435.15, 64656.11 });
            legacyXdrSerializationStream.WriteString("oiewurouqwe");
            legacyXdrSerializationStream.WriteVarOpaque(32, testArray);

            var fastXdrReader = new XdrReader(legacyXdrSerializationStream.ToArray());
            Assert.AreEqual(435, fastXdrReader.ReadInt32());
            Assert.AreEqual((uint)435, fastXdrReader.ReadUInt32());
            Assert.AreEqual(43546345634657565L, fastXdrReader.ReadInt64());
            {
                var length = fastXdrReader.ReadInt32();
                var value = new double[length];
                for (var i = 0; i < length; i++)
                {
                    value[i] = fastXdrReader.ReadDouble();
                }
                Assert.AreEqual(new double[] { 435.15, 64656.11 }, value);
            }
            Assert.AreEqual("oiewurouqwe", fastXdrReader.ReadString());
            Assert.AreEqual(testArray, fastXdrReader.ReadVariable());

            //backward compatibility
            var fastWriter = new XdrWriter();
            fastWriter.WriteInt32(435);
            fastWriter.WriteUInt32((uint)435);
            {
                var arr = new double[] { 435.15, 64656.11 };
                fastWriter.WriteInt32(arr.Length);
                foreach (var d in arr)
                {
                    fastWriter.WriteDouble(d);
                }
            }
            fastWriter.WriteString("oiewurouqwe");
            fastWriter.WriteVariable(testArray);

            var legacyXdrReader = new XdrDataInputStream(fastWriter.ToArray());
            Assert.AreEqual(435, legacyXdrReader.ReadInt());
            Assert.AreEqual((uint)435, legacyXdrReader.ReadUInt());
            Assert.AreEqual(new double[] { 435.15, 64656.11 }, legacyXdrReader.ReadDoubleArray());
            Assert.AreEqual("oiewurouqwe", legacyXdrReader.ReadString());
            Assert.AreEqual(testArray, legacyXdrReader.ReadVarOpaque(32));
        }

        [TestCase(1, 1000000)]
        [TestCase(10000, 100)]
        [Explicit]
        [Category("Performance")]
        public void XdrOtputStreamPerformanceTest(int rounds, int iterations)
        {
            var testArray = new byte[32];
            Array.Fill(testArray, (byte)100);
            PerfCounter.MeasureTime(() =>
            {
                for (var r = 0; r < rounds; r++)
                {
                    var stream = new XdrDataOutputStream();
                    for (var i = 0; i < iterations; i++)
                    {
                        stream.WriteInt(435);
                        stream.WriteString("oiewurouqwe");
                        stream.WriteVarOpaque(32, testArray);
                    }
                    stream.ToArray();
                }
            }, () => $"({rounds} rounds, {iterations} iterations)");
        }

        [TestCase(1, 10000000)]
        [TestCase(100000, 100)]
        [Explicit]
        [Category("Performance")]
        public void XdrInputStreamPerformanceTest(int rounds, int iterations)
        {
            var testArray = new byte[32];
            Array.Fill(testArray, (byte)100);

            var stream = new XdrDataOutputStream();
            for (var i = 0; i < iterations; i++)
            {
                stream.WriteInt(435);
                stream.WriteString("oiewurouqwe");
                stream.WriteVarOpaque(32, testArray);
            }
            var serialized = stream.ToArray();

            PerfCounter.MeasureTime(() =>
            {
                for (var r = 0; r < rounds; r++)
                {
                    var reader = new XdrDataInputStream(serialized);
                    for (var i = 0; i < iterations; i++)
                    {
                        reader.ReadInt();
                        reader.ReadString();
                        reader.ReadVarOpaque(32);
                    }
                }
            }, () => $"({rounds} rounds, {iterations} iterations)");
        }

        [TestCase(1, 10000000)]
        [TestCase(100000, 100)]
        [Explicit]
        [Category("Performance")]
        public void XdrWriterPerformanceTest(int rounds, int iterations)
        {
            var testArray = new byte[32];
            Array.Fill(testArray, (byte)100);

            PerfCounter.MeasureTime(() =>
            {
                for (var r = 0; r < rounds; r++)
                {
                    var stream = new XdrWriter();
                    for (var i = 0; i < iterations; i++)
                    {
                        stream.WriteInt32(435);
                        stream.WriteString("oiewurouqwe");
                        stream.WriteVariable(testArray);
                    }
                    stream.ToArray();
                }
            }, () => $"({rounds} rounds, {iterations} iterations)");
        }

        [TestCase(1, 10000000)]
        [TestCase(100000, 100)]
        [Explicit]
        [Category("Performance")]
        public void XdrReaderPerformanceTest(int rounds, int iterations)
        {
            var testArray = new byte[32];
            Array.Fill(testArray, (byte)100);

            var stream = new XdrWriter();
            for (var i = 0; i < iterations; i++)
            {
                stream.WriteInt32(435);
                stream.WriteString("oiewurouqwe");
                stream.WriteVariable(testArray);
            }
            var serialized = stream.ToArray();

            PerfCounter.MeasureTime(() =>
            {
                for (var r = 0; r < rounds; r++)
                {
                    var reader = new XdrReader(serialized);
                    for (var i = 0; i < iterations; i++)
                    {
                        reader.ReadInt32();
                        reader.ReadString();
                        reader.ReadVariableAsSpan();
                    }
                }
            }, () => $"({rounds} rounds, {iterations} iterations)");
        }
    }
}