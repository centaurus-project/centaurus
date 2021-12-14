using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public static class ContextHelper
    {
        /// <summary>
        /// Depending on constellation state will return the constellation settings auditors or current server config genesis auditors
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static List<KeyPair> GetRelevantAuditors(this ExecutionContext context)
        {
            return (context.ConstellationSettingsManager.Current == null
                ? context.Settings.GenesisAuditors.Select(a => a.PubKey)
                : context.ConstellationSettingsManager.Current.Auditors.Select(a => (KeyPair)a.PubKey))
                .ToList();
        }
    }
}
