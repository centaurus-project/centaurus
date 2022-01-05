using Centaurus.Models;
using Centaurus.PaymentProvider;
using Centaurus.PaymentProvider.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public static class PaymentProviderExtensions
    {
        public static void RegisterProviders(this PaymentProvidersManager providersManager, ConstellationSettings constellation, Dictionary<string, string> cursors)
        {
            var settings = constellation.Providers.Select(p =>
            {
                var providerId = PaymentProviderBase.GetProviderId(p.Provider, p.Name);
                cursors.TryGetValue(providerId, out var cursor);
                var settings = p.ToProviderModel(cursor);
                return settings;
            }).ToList();

            foreach (var providerSettings in settings)
            {
                if (providersManager.TryGetManager(providerSettings.Id, out _)) //already registered
                    continue;
                providersManager.Register(providerSettings);
            }
        }
    }
}
