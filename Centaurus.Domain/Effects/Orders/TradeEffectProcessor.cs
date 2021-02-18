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
            this.order = order ?? throw new ArgumentNullException(nameof(order));
        }

        public override void CommitEffect()
        {
            MarkAsProcessed();
            order.Amount -= Effect.AssetAmount;
            order.QuoteAmount -= Effect.QuoteAmount;
        }

        public override void RevertEffect()
        {
            MarkAsProcessed();
            order.Amount += Effect.AssetAmount;
            order.QuoteAmount += Effect.QuoteAmount;
        }
    }
}
