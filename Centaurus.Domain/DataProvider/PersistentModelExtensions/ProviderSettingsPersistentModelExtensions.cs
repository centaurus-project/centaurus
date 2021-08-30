using Centaurus.Models;
using Centaurus.PersistentStorage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public static class ProviderSettingsPersistentModelExtensions
    {
        public static ProviderSettingsPersistentModel ToPersistentModel(this ProviderSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            return new ProviderSettingsPersistentModel
            {
                Name = settings.Name,
                Assets = settings.Assets.Select(a => a.ToPersistentModel()).ToList(),
                InitCursor = settings.InitCursor,
                PaymentSubmitDelay = settings.PaymentSubmitDelay,
                Provider = settings.Provider,
                Vault = settings.Vault
            };
        }
        public static ProviderSettings ToDomainModel(this ProviderSettingsPersistentModel settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            return new ProviderSettings
            {
                Name = settings.Name,
                Assets = settings.Assets.Select(a => a.ToDomainModel()).ToList(),
                InitCursor = settings.InitCursor,
                PaymentSubmitDelay = settings.PaymentSubmitDelay,
                Provider = settings.Provider,
                Vault = settings.Vault
            };
        }
    }
}
