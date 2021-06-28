using Centaurus.Domain.Models;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class TradeEffectProcessor : ClientEffectProcessor<TradeEffect>
    {
        private OrderWrapper order;

        public TradeEffectProcessor(TradeEffect effect, AccountWrapper account, OrderWrapper order)
            : base(effect, account)
        {
            this.order = order ?? throw new ArgumentNullException(nameof(order));
        }

        public override void CommitEffect()
        {
            MarkAsProcessed();
            order.Order.Amount += -Effect.AssetAmount;
            if (!Effect.IsNewOrder) //new order doesn't have this value yet
                order.Order.QuoteAmount += -Effect.QuoteAmount;

            var decodedId = OrderIdConverter.Decode(Effect.OrderId);
            var quoteBalance = AccountWrapper.Account.GetBalance(0);
            var assetBalance = AccountWrapper.Account.GetBalance(decodedId.Asset);
            if (decodedId.Side == OrderSide.Buy)
            {
                if (!Effect.IsNewOrder) //liabilities wasn't locked for new order yet
                    quoteBalance.UpdateLiabilities(-Effect.QuoteAmount);
                quoteBalance.UpdateBalance(-Effect.QuoteAmount);
                assetBalance.UpdateBalance(Effect.AssetAmount);
            }
            else
            {
                if (!Effect.IsNewOrder) //liabilities wasn't locked for new order yet
                    assetBalance.UpdateLiabilities(-Effect.AssetAmount);
                assetBalance.UpdateBalance(-Effect.AssetAmount);
                quoteBalance.UpdateBalance(Effect.QuoteAmount);
            }
        }

        public override void RevertEffect()
        {
            MarkAsProcessed();

            var decodedId = OrderIdConverter.Decode(Effect.OrderId);
            var quoteBalance = AccountWrapper.Account.GetBalance(0);
            var assetBalance = AccountWrapper.Account.GetBalance(decodedId.Asset);
            if (decodedId.Side == OrderSide.Buy)
            {
                if (!Effect.IsNewOrder)
                    quoteBalance.UpdateLiabilities(Effect.QuoteAmount);
                quoteBalance.UpdateBalance(Effect.QuoteAmount);
                assetBalance.UpdateBalance(-Effect.AssetAmount);
            }
            else
            {
                if (!Effect.IsNewOrder)
                    assetBalance.UpdateLiabilities(Effect.AssetAmount);
                assetBalance.UpdateBalance(Effect.AssetAmount);
                quoteBalance.UpdateBalance(-Effect.QuoteAmount);
            }

            order.Order.Amount += Effect.AssetAmount; 
            if (!Effect.IsNewOrder)
                order.Order.QuoteAmount += Effect.QuoteAmount;
        }
    }
}
