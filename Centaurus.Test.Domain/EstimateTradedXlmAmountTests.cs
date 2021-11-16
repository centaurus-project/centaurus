using Centaurus.Domain;
using Centaurus.Models;
using NUnit.Framework;

namespace Centaurus.Test
{
    public class EstimateTradedXlmAmountTests
    {
        [TestCase(1ul, 1, OrderSide.Buy, 2ul)]
        [TestCase(1ul, 1, OrderSide.Sell, 0ul)]
        [TestCase(1ul, 2, OrderSide.Buy, 3ul)]
        [TestCase(1ul, 2, OrderSide.Sell, 1ul)]
        [TestCase(1000ul, 2, OrderSide.Buy, 2002ul)]
        [TestCase(1000ul, 2, OrderSide.Sell, 1998ul)]
        [TestCase(1ul, 0.5, OrderSide.Buy, 1ul)]
        [TestCase(1000ul, 0.003, OrderSide.Buy, 4ul)]
        [TestCase(1000ul, 0.003, OrderSide.Sell, 2ul)]
        public void EstimateTradedXlmAmountTest(ulong assetAmount, double price, OrderSide side, ulong estimatedQuoteAmount)
        {
            Assert.AreEqual(estimatedQuoteAmount, OrderMatcher.EstimateQuoteAmount(assetAmount, price, side));
        }
    }
}