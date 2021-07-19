using Centaurus.PaymentProvider.Models;
using System;
using System.Collections.Generic;

namespace Centaurus.PaymentProvider
{
    public abstract class PaymentProviderFactoryBase
    {
        public abstract PaymentProviderBase GetProvider(SettingsModel providerSettings, string config);

        public static PaymentProviderFactoryBase Default { get; } = new PaymentProviderFactoryDefault();
    }

    public class PaymentProviderFactoryDefault : PaymentProviderFactoryBase
    {
        public override PaymentProviderBase GetProvider(SettingsModel providerSettings, string config)
        {
            if (!providerTypes.TryGetValue(providerSettings.Provider, out var providerType))
                providerType = ProviderDiscoverer.DiscoverProvider(providerSettings.Provider);

            return (PaymentProviderBase)Activator.CreateInstance(providerType, providerSettings, config);
        }

        private static Dictionary<string, Type> providerTypes = new Dictionary<string, Type>();
    }
}