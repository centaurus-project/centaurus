using Centaurus.PaymentProvider.Models;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Centaurus.PaymentProvider
{
    public class PaymentProvidersManager : IDisposable
    {
        public PaymentProvidersManager(PaymentProviderFactoryBase paymentProviderFactory, List<SettingsModel> settings, string configPath)
        {
            var config = GetConfig(configPath);
            var providers = new Dictionary<string, PaymentProviderBase>();
            foreach (var provider in settings)
            {
                if (providers.ContainsKey(provider.Id))
                    throw new Exception($"Payments manager for provider {provider.Id} is already registered.");

                var currentProviderRawConfig = default(string);
                if (config != null && config.RootElement.TryGetProperty(provider.Id, out var currentProviderElement))
                    currentProviderRawConfig = currentProviderElement.GetRawText();

                providers.Add(provider.Id, paymentProviderFactory.GetProvider(provider, currentProviderRawConfig));
            }

            paymentProviders = providers.ToImmutableDictionary();
        }

        private ImmutableDictionary<string, PaymentProviderBase> paymentProviders;

        public bool TryGetManager(string provider, out PaymentProviderBase paymentProvider)
        {
            return paymentProviders.TryGetValue(provider, out paymentProvider);
        }

        public PaymentProviderBase GetManager(string provider)
        {
            if (TryGetManager(provider, out var paymentProvider))
                return paymentProvider;
            throw new Exception($"Unable to find provider {provider}.");
        }

        public List<PaymentProviderBase> GetAll()
        {
            return paymentProviders.Values.ToList();
        }

        private JsonDocument GetConfig(string configPath)
        {
            if (!File.Exists(configPath))
                return null;
            return JsonDocument.Parse(File.ReadAllText(configPath));
        }

        public void Dispose()
        {
            foreach (var txM in paymentProviders.Values)
                txM.Dispose();
        }
    }
}
