using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class TradeEffectProcessor : EffectProcessor<TradeEffect>
    {
        private Order order;

        public TradeEffectProcessor(TradeEffect effect, Order order)
            : base(effect)
        {
            this.order = order;
        }

        public override UpdatedObject[] CommitEffect()
        {
            order.Amount -= Effect.AssetAmount;
            return new UpdatedObject[] { new UpdatedObject(order) };
        }

        public override void RevertEffect()
        {
            order.Amount += Effect.AssetAmount;
        }

        public static TradeEffectProcessor GetProcessor(ulong apex, Order order, int asset, long assetAmount, long xlmAmount, double price, OrderSides side)
        {
            var trade = new TradeEffect
            {
                Apex = apex,
                Pubkey = order.Pubkey,
                Asset = asset,
                AssetAmount = assetAmount,
                XlmAmount = xlmAmount,
                Price = price,
                OrderId = order.OrderId,
                OrderSide = side
            };

            return GetProcessor(trade, order);
        }

        public static TradeEffectProcessor GetProcessor(TradeEffect effect, Order order)
        {
            return new TradeEffectProcessor(effect, order);
        }
    }
}
