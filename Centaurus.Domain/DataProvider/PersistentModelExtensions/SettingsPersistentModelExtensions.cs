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
                Assets = settings.Assets.Select(a => a.ToPersistentModel()).ToList(),
                Auditors = settings.Auditors.Select(a => a.ToPesrsistentModel()).ToList(),
                MinAccountBalance = settings.MinAccountBalance,
                MinAllowedLotSize = settings.MinAllowedLotSize,
                Providers = settings.Providers.Select(p => p.ToPersistentModel()).ToList(),
                RequestRateLimits = settings.RequestRateLimits.ToPersistentModel(),
                Alpha = settings.Alpha.Data,
                Apex = settings.Apex
            };
        }
        public static ConstellationSettings ToDomainModel(this SettingsPersistentModel settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            return new ConstellationSettings
            {
                Assets = settings.Assets.Select(a => a.ToDomainModel()).ToList(),
                Auditors = settings.Auditors.Select(a => a.ToDomainModel()).ToList(),
                MinAccountBalance = settings.MinAccountBalance,
                MinAllowedLotSize = settings.MinAllowedLotSize,
                Providers = settings.Providers.Select(p => p.ToDomainModel()).ToList(),
                RequestRateLimits = settings.RequestRateLimits.ToDomainModel(),
                Alpha = new RawPubKey(settings.Alpha),
                Apex = settings.Apex
            };
        }

        public static AuditorModel ToPesrsistentModel(this Auditor auditor)
        {
            if (auditor == null)
                throw new ArgumentNullException(nameof(auditor));

            return new AuditorModel { PubKey = auditor.PubKey, Address = auditor.Address };
        }

        public static Auditor ToDomainModel(this AuditorModel auditor)
        {
            if (auditor == null)
                throw new ArgumentNullException(nameof(auditor));

            return new Auditor { PubKey = new RawPubKey(auditor.PubKey), Address = auditor.Address };
        }
    }
}
