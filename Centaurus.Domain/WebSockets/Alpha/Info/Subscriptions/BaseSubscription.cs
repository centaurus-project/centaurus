using Centaurus.Analytics;
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
        static BaseSubscription()
        {
            var subscriptions = new Dictionary<SubscriptionType, Type>();
            var allTypes = Assembly.GetExecutingAssembly().GetTypes();
            foreach (var t in allTypes)
            {
                if (t.IsAbstract || t.IsInterface || !typeof(BaseSubscription).IsAssignableFrom(t))
                    continue;
                var commandAttribute = t.GetCustomAttribute<SubscriptionAttribute>();
                if (commandAttribute == null)
                    continue;
                subscriptions.Add(commandAttribute.SubscriptionType, t);
            }
            BaseSubscription.subscriptions = subscriptions.ToImmutableDictionary();
        }

        private static ImmutableDictionary<SubscriptionType, Type> subscriptions;

        public static BaseSubscription GetBySubscriptionName(string subscriptionName)
        {
            if (string.IsNullOrWhiteSpace(subscriptionName))
                throw new ArgumentNullException(nameof(subscriptionName));
            var subscriptionValues = subscriptionName.Split('_');
            var subscriptionTypeStr = subscriptionValues[0];
            if (!Enum.TryParse<SubscriptionType>(subscriptionTypeStr, out var subscriptionType) || !subscriptions.ContainsKey(subscriptionType))
                throw new NotSupportedException($"Subscription {subscriptionTypeStr} is not supported.");
            var subscription = (BaseSubscription)Activator.CreateInstance(subscriptions[subscriptionType]);
            subscription.SetValues(subscriptionValues.Skip(1).ToArray());
            return subscription;
        }

        public abstract void SetValues(string[] values);

        protected virtual StringBuilder GetNameBuilder()
        {
            return new StringBuilder().Append(subscriptions.FirstOrDefault(s => s.Value == GetType()).Key);
        }

        public string Name { get; protected set; }
    }

    public abstract class BaseMarketSubscription : BaseSubscription
    {
        public int Market { get; set; }

        public override void SetValues(string[] values)
        {
            if (values.Length < 1 || !int.TryParse(values[0], out var market))
                throw new ArgumentException($"{(values.Length > 0 ? values[0]: "null")} is not valid market");
            Market = market;
        }

        protected override StringBuilder GetNameBuilder()
        {
            return base.GetNameBuilder().Append("_").Append(Market);
        }
    }
}
