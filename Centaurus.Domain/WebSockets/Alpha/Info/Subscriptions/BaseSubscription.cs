using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Centaurus.Domain
{
    public abstract class BaseSubscription
    {
        public static BaseSubscription GetBySubscriptionName(string subscriptionName)
        {
            if (string.IsNullOrWhiteSpace(subscriptionName))
                throw new ArgumentNullException(nameof(subscriptionName));
            var subscriptionValues = subscriptionName.Split('@');

            var subscriptionTypeStr = subscriptionValues[0];
            var subscription = default(BaseSubscription);
            switch (subscriptionTypeStr)
            {
                case nameof(AllMarketTickersSubscription):
                    subscription = new AllMarketTickersSubscription();
                    break;
                case nameof(DepthsSubscription):
                    subscription = new DepthsSubscription();
                    break;
                case nameof(MarketTickerSubscription):
                    subscription = new MarketTickerSubscription();
                    break;
                case nameof(PriceHistorySubscription):
                    subscription = new PriceHistorySubscription();
                    break;
                case nameof(TradesFeedSubscription):
                    subscription = new TradesFeedSubscription();
                    break;
                default:
                    throw new ArgumentException($"Subscription {subscriptionTypeStr} is not supported.");
            }
            subscription.SetValues(subscriptionValues.Length > 1 ? subscriptionValues[1] : null);
            return subscription;
        }

        public abstract void SetValues(string values);

        public abstract string Name { get; }
    }

    public abstract class BaseMarketSubscription : BaseSubscription
    {
        public int Market { get; set; }

        protected void SetMarket(string marketStr)
        {
            if (!int.TryParse(marketStr, out var market))
                throw new ArgumentException($"{(string.IsNullOrEmpty(marketStr) ? "null": marketStr)} is not valid market value.");
            Market = market;
        }
    }
}
