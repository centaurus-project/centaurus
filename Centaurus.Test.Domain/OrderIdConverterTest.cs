using Centaurus.Domain;
using Centaurus.Models;
using NUnit.Framework;
using System;

namespace Centaurus.Test
{
    public class OrderIdConverterTest
    {
        [TestCase((ulong)123412, 345, OrderSides.Buy)]
        [TestCase((ulong)281474976710655, 32767, OrderSides.Buy)]
        [TestCase((ulong)1, 1, OrderSides.Sell)]
        public void OrderIdConverterSimpleTest(ulong apex, int market, OrderSides side)
        {
            var decoded = OrderIdConverter.Decode(OrderIdConverter.Encode(apex, market, side));
            Assert.AreEqual(apex, decoded.Apex);
            Assert.AreEqual(market, decoded.Asset);
            Assert.AreEqual(side, decoded.Side);
        }
    }
}