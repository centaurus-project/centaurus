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
        private string baseAsset;

        public TradeEffectProcessor(TradeEffect effect, AccountWrapper account, OrderWrapper order, string baseAsset)
            : base(effect, account)
        {
            this.order = order ?? throw new ArgumentNullException(nameof(order));
            this.baseAsset = baseAsset ?? throw new ArgumentNullException(nameof(baseAsset));
        }

        public override void CommitEffect()
        {
            MarkAsProcessed();
            order.Order.Amount -= Effect.AssetAmount;
            if (!Effect.IsNewOrder) //new order doesn't have this value yet
                order.Order.QuoteAmount -= Effect.QuoteAmount;

            var quoteBalance = AccountWrapper.Account.GetBalance(baseAsset);
            var assetBalance = AccountWrapper.Account.GetBalance(order.Order.Asset);
            if (order.Order.Side == OrderSide.Buy)
            {
                if (!Effect.IsNewOrder) //liabilities wasn't locked for new order yet
                    quoteBalance.UpdateLiabilities(Effect.QuoteAmount, UpdateSign.Minus);
                quoteBalance.UpdateBalance(Effect.QuoteAmount, UpdateSign.Minus);
                assetBalance.UpdateBalance(Effect.AssetAmount, UpdateSign.Plus);
            }
            else
            {
                if (!Effect.IsNewOrder) //liabilities wasn't locked for new order yet
                    assetBalance.UpdateLiabilities(Effect.AssetAmount, UpdateSign.Minus);
                assetBalance.UpdateBalance(Effect.AssetAmount, UpdateSign.Minus);
                quoteBalance.UpdateBalance(Effect.QuoteAmount, UpdateSign.Plus);
            }
        }

        public override void RevertEffect()
        {
            MarkAsProcessed();

            var quoteBalance = AccountWrapper.Account.GetBalance(baseAsset);
            var assetBalance = AccountWrapper.Account.GetBalance(order.Order.Asset);
            if (order.Order.Side == OrderSide.Buy)
            {
                if (!Effect.IsNewOrder)
                    quoteBalance.UpdateLiabilities(Effect.QuoteAmount, UpdateSign.Plus);
                quoteBalance.UpdateBalance(Effect.QuoteAmount, UpdateSign.Plus);
                assetBalance.UpdateBalance(Effect.AssetAmount, UpdateSign.Minus);
            }
            else
            {
                if (!Effect.IsNewOrder)
                    assetBalance.UpdateLiabilities(Effect.AssetAmount, UpdateSign.Plus);
                assetBalance.UpdateBalance(Effect.AssetAmount, UpdateSign.Plus);
                quoteBalance.UpdateBalance(Effect.QuoteAmount, UpdateSign.Minus);
            }

            order.Order.Amount += Effect.AssetAmount; 
            if (!Effect.IsNewOrder)
                order.Order.QuoteAmount += Effect.QuoteAmount;
        }
    }
}
