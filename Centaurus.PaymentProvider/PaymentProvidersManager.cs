using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Centaurus.PaymentProvider
{
    public class PaymentProvidersManager: IDisposable
    {
        public PaymentProvidersManager(PaymentProviderFactoryBase paymentProviderFactory, List<ProviderSettings> settings)
        {
            var providers = new Dictionary<string, PaymentProviderBase>();
            foreach (var provider in settings)
            {
                var providerId = PaymentProviderBase.GetProviderId(provider.Provider, provider.Provider);
                if (providers.ContainsKey(providerId))
                    throw new Exception($"Payments manager for provider {providerId} is already registered.");

                providers.Add(providerId, paymentProviderFactory.GetProvider(provider, null));
            }

            paymentProviders = providers.ToImmutableDictionary();
        }

        private ImmutableDictionary<string, PaymentProviderBase> paymentProviders;

        public bool TryGetManager(string provider, out PaymentProviderBase paymentProvider)
        {
            return paymentProviders.TryGetValue(provider, out paymentProvider);
        }

        public void Dispose()
        {
            foreach (var txM in paymentProviders.Values)
                txM.Dispose();
        }
    }
}
