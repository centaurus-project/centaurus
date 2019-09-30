using Centaurus.Domain;
using NUnit.Framework;

namespace Centaurus.Test
{
    public class EstimateTradedXlmAmountTests
    {
        [TestCase(1L, 1, 1L)]
        [TestCase(1L, 2, 2L)]
        [TestCase(1L, 0.5, 1L)]
        [TestCase(1000L, 0.003, 3L)]
        public void EstimateTradedXlmAmountTest(long assetAmount, double price, long estimatedXlmAmount)
        {
            Assert.AreEqual(estimatedXlmAmount, OrderMatcher.EstimateTradedXlmAmount(assetAmount, price));
        }
    }
}