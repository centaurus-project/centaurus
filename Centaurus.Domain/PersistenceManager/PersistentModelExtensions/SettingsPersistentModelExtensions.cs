using Centaurus.Models;
using Centaurus.PersistentStorage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public static class SettingsPersistentModelExtensions
    {
        public static SettingsPersistentModel ToPesrsistentModel(this ConstellationSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            return new SettingsPersistentModel
            {
                Apex = settings.Apex,
                Assets = settings.Assets.Select(a => a.ToPersistentModel()).ToList(),
                Auditors = settings.Auditors.Cast<byte[]>().ToList(),
                MinAccountBalance = settings.MinAccountBalance,
                MinAllowedLotSize = settings.MinAllowedLotSize,
                Providers = settings.Providers.Select(p => p.ToPersistentModel()).ToList(),
                RequestRateLimits = settings.RequestRateLimits.ToPersistentModel()
            };
        }
        public static ConstellationSettings ToDomainModel(this SettingsPersistentModel settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            return new ConstellationSettings
            {
                Apex = settings.Apex,
                Assets = settings.Assets.Select(a => a.ToDomainModel()).ToList(),
                Auditors = settings.Auditors.Cast<RawPubKey>().ToList(),
                MinAccountBalance = settings.MinAccountBalance,
                MinAllowedLotSize = settings.MinAllowedLotSize,
                Providers = settings.Providers.Select(p => p.ToDomainModel()).ToList(),
                RequestRateLimits = settings.RequestRateLimits.ToDomainModel()
            };
        }
    }
}
