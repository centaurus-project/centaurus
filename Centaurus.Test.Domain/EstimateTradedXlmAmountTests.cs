using Centaurus.Domain;
using Centaurus.Models;
using NUnit.Framework;

namespace Centaurus.Test
{
    public class EstimateTradedXlmAmountTests
    {
        [TestCase(1L, 1, OrderSide.Buy, 2L)]
        [TestCase(1L, 1, OrderSide.Sell, 0L)]
        [TestCase(1L, 2, OrderSide.Buy, 3L)]
        [TestCase(1L, 2, OrderSide.Sell, 1L)]
        [TestCase(1000L, 2, OrderSide.Buy, 2002L)]
        [TestCase(1000L, 2, OrderSide.Sell, 1998L)]
        [TestCase(1L, 0.5, OrderSide.Buy, 1L)]
        [TestCase(1000L, 0.003, OrderSide.Buy, 4L)]
        [TestCase(1000L, 0.003, OrderSide.Sell, 2L)]
        public void EstimateTradedXlmAmountTest(long assetAmount, double price, OrderSide side, long estimatedQuoteAmount)
        {
            Assert.AreEqual(estimatedQuoteAmount, OrderMatcher.EstimateQuoteAmount(assetAmount, price, side));
        }
    }
}