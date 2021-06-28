using Centaurus.Models;
using NLog;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Centaurus.PaymentProvider
{
    public class PaymentProvidersManager: IDisposable
    {
        public PaymentProvidersManager(PaymentParsersManager paymentParsersManager, PaymentProviderFactoryBase paymentProviderFactory, List<ProviderSettings> settings, Dictionary<string, WithdrawalStorage> withdrawals)
        {
            var providers = new Dictionary<string, PaymentProviderBase>();
            foreach (var provider in settings)
            {
                if (paymentParsersManager.TryGetParser(provider.Provider, out var parser))
                    throw new Exception($"Payments parser for provider {provider.Provider} not found.");

                var providerId = PaymentProviderBase.GetProviderId(provider.Provider, provider.Provider);
                if (providers.ContainsKey(providerId))
                    throw new Exception($"Payments manager for provider {providerId} is already registered.");

                if (!withdrawals.TryGetValue(providerId, out var providerWithdrawals))
                    providerWithdrawals = new WithdrawalStorage();

                providers.Add(providerId, paymentProviderFactory.GetProvider(parser, provider, null, providerWithdrawals));
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
