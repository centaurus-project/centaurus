using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus
{
    public static class SettingsExtensions
    {
        public static bool IsPrimeNode(this Settings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            return settings.ParticipationLevel == ParticipationLevel.Prime; 
        }
    }
}
