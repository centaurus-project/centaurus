using Centaurus;
using NUnit.Framework;

namespace Centauturus.Tests
{
    public class ByteArrayPrimitivesTests
    {
        static object[] EqualsTestVectors =
        {
            new object[]{ new byte[] { 1, 2, 3 }, new byte[] { 1, 2, 3 }, true },
            new object[]{ new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, true },
            new object[]{ new byte[] { 1, 2, 3 }, new byte[] { 1, 0, 3 }, false },
            new object[]{ new byte[] { 1, 2 }, new byte[] { 1, 2, 3 }, false },
            new object[]{ new byte[] { 1, 2, 3 }, new byte[] { 1, 2 }, false },
            new object[]{ new byte[] { 1, 2, 3 }, new byte[] { 1, 2 }, false },
            new object[]{ new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, new byte[] { 1, 2, 3, 4, 5, 6, 9, 8 }, false },
            new object[]{ new byte[] { 1, 2, 3 }, null, false },
            new object[]{ null, new byte[] { 1, 2, 3 }, false }
        };

        [TestCaseSource(nameof(EqualsTestVectors))]
        public void Equals(byte[] left, byte[] right, bool shouldEqual)
        {
            Assert.AreEqual(ByteArrayPrimitives.Equals(left, right), shouldEqual);
        }
    }
}