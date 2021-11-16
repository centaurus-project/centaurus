using Centaurus.PaymentProvider;
using Centaurus.PaymentProvider.Models;
using System;
using System.Collections.Generic;

namespace Centaurus.Domain
{
    public abstract class PaymentProvidersFactoryBase
    {
        public abstract PaymentProviderBase GetProvider(SettingsModel providerSettings, string assemblyPath, string config);

        public static PaymentProvidersFactoryDefault Default { get; } = new PaymentProvidersFactoryDefault();
    }

    public class PaymentProvidersFactoryDefault : PaymentProvidersFactoryBase
    {
        public override PaymentProviderBase GetProvider(SettingsModel providerSettings, string assemblyPath, string config)
        {
            if (!providerTypes.TryGetValue(providerSettings.Provider, out var providerType))
                providerType = PaymentProvidersDiscoverer.DiscoverProvider(providerSettings.Provider, assemblyPath);

            return (PaymentProviderBase)Activator.CreateInstance(providerType, providerSettings, config);
        }

        private static Dictionary<string, Type> providerTypes = new Dictionary<string, Type>();
    }
}