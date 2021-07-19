using Centaurus.Models;
using Centaurus.PaymentProvider.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public static class SettingsModelExtensions
    {
        public static SettingsModel ToProviderModel(this ProviderSettings providerSettings)
        {
            if (providerSettings == null)
                throw new ArgumentNullException(nameof(providerSettings));
            return new SettingsModel
            {
                Assets = providerSettings.Assets.Select(a => a.ToProviderModel()).ToList(),
                InitCursor = providerSettings.InitCursor,
                Name = providerSettings.Name,
                PaymentSubmitDelay = providerSettings.PaymentSubmitDelay,
                Provider = providerSettings.Provider,
                Vault = providerSettings.Vault
            };
        }
    }
}
