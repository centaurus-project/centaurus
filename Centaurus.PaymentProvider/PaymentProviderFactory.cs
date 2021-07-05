using Centaurus.Models;
using System;
using System.Collections.Generic;

namespace Centaurus.PaymentProvider
{
    public abstract class PaymentProviderFactoryBase
    {
        public abstract PaymentProviderBase GetProvider(ProviderSettings providerSettings, string config);

        public static PaymentProviderFactoryBase Default { get; } = new PaymentProviderFactoryDefault();
    }

    public class PaymentProviderFactoryDefault : PaymentProviderFactoryBase
    {
        public override PaymentProviderBase GetProvider(ProviderSettings providerSettings, string config)
        {
            if (!providerTypes.TryGetValue(providerSettings.Provider, out var providerType))
                providerType = ProviderDiscoverer.DiscoverProvider(providerSettings.Provider);

            return (PaymentProviderBase)Activator.CreateInstance(providerType, new object[] { providerSettings, config });
        }

        private static Dictionary<string, Type> providerTypes = new Dictionary<string, Type>();
    }
}