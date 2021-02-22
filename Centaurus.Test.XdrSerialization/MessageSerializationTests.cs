using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Centaurus.Xdr;
using NUnit.Framework;
using stellar_dotnet_sdk.xdr;
using System.IO;

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
                Account = 1,
                Amount = 2131231,
                RequestId = 1,
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
            Assert.AreEqual(pubkey, deserializedMessage.Signatures[0].Signer.Data);
            Assert.AreEqual(signature, deserializedMessage.Signatures[0].Signature);
            Assert.AreEqual(original.Account, deserialized.Account);
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

            var bufferReader = new XdrBufferReader(legacyXdrSerializationStream.ToArray());
            Assert.AreEqual(435, bufferReader.ReadInt32());
            Assert.AreEqual((uint)435, bufferReader.ReadUInt32());
            Assert.AreEqual(43546345634657565L, bufferReader.ReadInt64());
            {
                var length = bufferReader.ReadInt32();
                var value = new double[length];
                for (var i = 0; i < length; i++)
                {
                    value[i] = bufferReader.ReadDouble();
                }
                Assert.AreEqual(new double[] { 435.15, 64656.11 }, value);
            }
            Assert.AreEqual("oiewurouqwe", bufferReader.ReadString());
            Assert.AreEqual(testArray, bufferReader.ReadVariable());

            using var streamReader = new XdrStreamReader(new MemoryStream(legacyXdrSerializationStream.ToArray()));
            Assert.AreEqual(435, streamReader.ReadInt32());
            Assert.AreEqual((uint)435, streamReader.ReadUInt32());
            Assert.AreEqual(43546345634657565L, streamReader.ReadInt64());
            {
                var length = streamReader.ReadInt32();
                var value = new double[length];
                for (var i = 0; i < length; i++)
                {
                    value[i] = streamReader.ReadDouble();
                }
                Assert.AreEqual(new double[] { 435.15, 64656.11 }, value);
            }
            Assert.AreEqual("oiewurouqwe", streamReader.ReadString());
            Assert.AreEqual(testArray, streamReader.ReadVariable());

            //backward compatibility
            var bufferWriter = new XdrBufferWriter();
            bufferWriter.WriteInt32(435);
            bufferWriter.WriteUInt32((uint)435);
            {
                var arr = new double[] { 435.15, 64656.11 };
                bufferWriter.WriteInt32(arr.Length);
                foreach (var d in arr)
                {
                    bufferWriter.WriteDouble(d);
                }
            }
            bufferWriter.WriteString("oiewurouqwe");
            bufferWriter.WriteVariable(testArray);

            var legacyXdrReader = new XdrDataInputStream(bufferWriter.ToArray());
            Assert.AreEqual(435, legacyXdrReader.ReadInt());
            Assert.AreEqual((uint)435, legacyXdrReader.ReadUInt());
            Assert.AreEqual(new double[] { 435.15, 64656.11 }, legacyXdrReader.ReadDoubleArray());
            Assert.AreEqual("oiewurouqwe", legacyXdrReader.ReadString());
            Assert.AreEqual(testArray, legacyXdrReader.ReadVarOpaque(32));


            using var memoryStream = new MemoryStream();
            var streamWriter = new XdrStreamWriter(memoryStream);
            streamWriter.WriteInt32(435);
            streamWriter.WriteUInt32((uint)435);
            {
                var arr = new double[] { 435.15, 64656.11 };
                streamWriter.WriteInt32(arr.Length);
                foreach (var d in arr)
                {
                    streamWriter.WriteDouble(d);
                }
            }
            streamWriter.WriteString("oiewurouqwe");
            streamWriter.WriteVariable(testArray);
            var res = memoryStream.ToArray();
            var reference = bufferWriter.ToArray();
            CollectionAssert.AreEqual(reference, res);

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

        [TestCase(1, 1000000)]
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
                    var stream = new XdrBufferWriter();
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

            var stream = new XdrBufferWriter();
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
                    var reader = new XdrBufferReader(serialized);
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