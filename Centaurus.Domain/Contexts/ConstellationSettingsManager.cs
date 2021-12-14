using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    /// <summary>
    /// This class holds recent constellation settings, to be able to obtain relevant node ids
    /// </summary>
    public class ConstellationSettingsManager : ContextualBase
    {
        public ConstellationSettingsManager(ExecutionContext context)
            : base(context)
        {
        }

        public ConstellationSettings Current { get; private set; }

        public void Add(ConstellationSettings newSettings)
        {
            if (newSettings == null)
                throw new ArgumentNullException(nameof(newSettings));

            if (Current != null && Current.Apex >= newSettings.Apex)
                throw new ArgumentException("New constellation settings apex is lower than the current one.", nameof(newSettings));

            lock (syncRoot)
            {
                settings.Add(newSettings.Apex, newSettings);
                Current = newSettings;
                Cleanup();
            }
        }

        public bool TryGetForApex(ulong apex, out ConstellationSettings apexSettings)
        {
            apexSettings = null;
            if (Current == null) //if current is null, than there is no constellation settings yet
                return false;

            if (apex >= Current.Apex)
            {
                apexSettings = Current;
                return true;
            }

            lock (syncRoot)
            {
                //looking for the first settings where apex is lower or equal to the request apex
                foreach (var settingApex in settings.Keys)
                {
                    //the setting is newer than apex
                    if (settingApex > apex)
                        continue;

                    apexSettings = settings[apex];
                    return true;
                }
            }
            return false;
        }

        private object syncRoot = new { };

        private SortedDictionary<ulong, ConstellationSettings> settings = new SortedDictionary<ulong, ConstellationSettings>();

        private void Cleanup()
        {
            //remove old data
            if (settings.Count > 1000)
            {
                var firstApex = settings.Keys.First();
                settings.Remove(firstApex);
            }
        }
    }
}
