using Centaurus.Models;
using System;
using System.Linq;

namespace Centaurus
{
    public static class ConstellationSettingsExtensions
    {
        public static string GetBaseAsset(this ConstellationSettings constellationSettings)
        {
            if (constellationSettings == null)
                throw new ArgumentNullException(nameof(constellationSettings));
            return constellationSettings.Assets.First().Code;
        }
    }
}
