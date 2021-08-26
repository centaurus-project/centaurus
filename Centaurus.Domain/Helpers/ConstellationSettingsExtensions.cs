using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public static class ConstellationSettingsExtensions
    {
        public static byte GetAuditorId(this ConstellationSettings constellation, RawPubKey rawPubKey)
        {
            if (constellation == null)
                throw new ArgumentNullException(nameof(constellation));
            return (byte)constellation.Auditors.FindIndex(a => a.PubKey.Equals(rawPubKey));
        }

        public static RawPubKey GetAuditorPubKey(this ConstellationSettings constellation, byte auditorId)
        {
            if (constellation == null)
                throw new ArgumentNullException(nameof(constellation));
            return constellation.Auditors.ElementAtOrDefault(auditorId)?.PubKey;
        }
    }
}
