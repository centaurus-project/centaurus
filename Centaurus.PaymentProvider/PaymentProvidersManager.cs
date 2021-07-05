using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text.Json;

namespace Centaurus.PaymentProvider
{
    public class PaymentProvidersManager : IDisposable
    {
        public PaymentProvidersManager(PaymentProviderFactoryBase paymentProviderFactory, List<ProviderSettings> settings)
        {
            var config = GetConfig();
            var providers = new Dictionary<string, PaymentProviderBase>();
            foreach (var provider in settings)
            {
                var providerId = PaymentProviderBase.GetProviderId(provider.Provider, provider.Name);
                if (providers.ContainsKey(providerId))
                    throw new Exception($"Payments manager for provider {providerId} is already registered.");

                var currentProviderRawConfig = default(string);
                if (config != null && config.RootElement.TryGetProperty(providerId, out var currentProviderElement))
                    currentProviderRawConfig = currentProviderElement.GetRawText();

                providers.Add(providerId, paymentProviderFactory.GetProvider(provider, currentProviderRawConfig));
            }

            paymentProviders = providers.ToImmutableDictionary();
        }

        private ImmutableDictionary<string, PaymentProviderBase> paymentProviders;

        public bool TryGetManager(string provider, out PaymentProviderBase paymentProvider)
        {
            return paymentProviders.TryGetValue(provider, out paymentProvider);
        }

        const string configFileName = "payment-providers.config";

        private JsonDocument GetConfig()
        {
            if (!File.Exists(configFileName))
                return null;
            return JsonDocument.Parse(File.ReadAllText(configFileName));
        }

        public void Dispose()
        {
            foreach (var txM in paymentProviders.Values)
                txM.Dispose();
        }
    }
}
