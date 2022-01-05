using Centaurus.PaymentProvider;
using Centaurus.PaymentProvider.Models;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Centaurus.Domain
{
    public class PaymentProvidersManager : IDisposable
    {
        public PaymentProvidersManager(PaymentProvidersFactoryBase paymentProviderFactory, string configPath)
        {
            this.configPath = configPath;
            this.paymentProviderFactory = paymentProviderFactory;
        }

        public event Action<PaymentProviderBase> OnRegistered;

        public event Action<PaymentProviderBase> OnRemoved;

        public void Register(SettingsModel settings)
        {
            var config = GetConfig(configPath);
            RegisterProvider(settings, config);
        }

        public bool TryGetManager(string provider, out PaymentProviderBase paymentProvider)
        {
            lock (syncRoot)
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
            lock (syncRoot)
                return paymentProviders.Values.ToList();
        }

        public void Dispose()
        {
            RemoveAndDisposeAllProviders();
        }

        private Dictionary<string, PaymentProviderBase> paymentProviders = new Dictionary<string, PaymentProviderBase>();
        private string configPath;
        private PaymentProvidersFactoryBase paymentProviderFactory;

        private void RegisterProvider(SettingsModel settings, JsonDocument config)
        {
            if (TryGetManager(settings.Id, out _))
                throw new Exception($"Provider {settings.Id} already registered.");
            var provider = GetProvider(config, settings);
            AddProvider(provider);
        }

        private PaymentProviderBase GetProvider(JsonDocument config, SettingsModel settings)
        {
            var currentProviderRawConfig = default(string);
            var assemblyPath = default(string);
            if (config != null && config.RootElement.TryGetProperty(settings.Id, out var currentProviderElement))
            {
                if (!currentProviderElement.TryGetProperty("assemblyPath", out var assemblyPathProperty))
                    throw new ArgumentNullException("Path property is missing.");
                assemblyPath = assemblyPathProperty.GetString();

                if (currentProviderElement.TryGetProperty("config", out var providerConfig))
                    currentProviderRawConfig = providerConfig.GetRawText();
            }

            return paymentProviderFactory.GetProvider(settings, assemblyPath, currentProviderRawConfig);
        }

        private JsonDocument GetConfig(string configPath)
        {
            if (!File.Exists(configPath))
                return null;
            return JsonDocument.Parse(File.ReadAllText(configPath));
        }

        private void RemoveAndDisposeAllProviders()
        {
            if (paymentProviders == null)
                return;
            lock (syncRoot)
            {
                var providerIds = paymentProviders.Keys.ToList();
                foreach (var providerId in providerIds)
                    RemoveAndDisposeProvider(providerId);
            }
        }

        private void RemoveAndDisposeProvider(string providerId)
        {
            lock (syncRoot)
            {
                if (!paymentProviders.Remove(providerId, out var provider))
                    return;
                OnRemoved?.Invoke(provider);
                provider.Dispose();
            }
        }

        private void AddProvider(PaymentProviderBase provider)
        {
            lock (syncRoot)
            {
                if (paymentProviders.ContainsKey(provider.Id))
                    throw new Exception($"Payments manager for provider {provider.Id} is already registered.");
                paymentProviders.Add(provider.Id, provider);
                OnRegistered?.Invoke(provider);
            }
        }

        private object syncRoot = new { };
    }
}
