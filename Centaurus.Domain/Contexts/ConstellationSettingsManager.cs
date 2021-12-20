using Centaurus.Models;
using Microsoft.Extensions.Caching.Memory;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;

namespace Centaurus.Domain
{
    /// <summary>
    /// This class holds recent constellation settings, to be able to obtain relevant node ids
    /// </summary>
    public class ConstellationSettingsManager : ContextualBase, IDisposable
    {
        private ConstellationSettingsCollection settingsCache;

        public ConstellationSettingsManager(ExecutionContext context)
            : base(context)
        {
            settingsCache = new ConstellationSettingsCollection(Context.DataProvider.GetConstellationSettings);
        }

        public ConstellationSettings Current { get; private set; }

        public void Update(ConstellationSettings newSettings)
        {
            if (newSettings == null)
                throw new ArgumentNullException(nameof(newSettings));

            if (Current != null && Current.Apex >= newSettings.Apex)
                throw new ArgumentException("New constellation settings apex is lower than the current one.", nameof(newSettings));

            settingsCache.Add(newSettings);
            Current = newSettings;
        }

        public bool TryGetForApex(ulong apex, out ConstellationSettings apexSettings)
        {
            var current = Current;
            if (apex > current.Apex)
                apexSettings = current;
            else
                settingsCache.TryGetSettings(apex, out apexSettings);
            return apexSettings != null;
        }

        public void Dispose()
        {
            settingsCache.Dispose();
        }
    }

    class ConstellationSettingsCollection : IDisposable
    {
        public ConstellationSettingsCollection(Func<ulong, ConstellationSettings> constellationSettingsDataProvider)
        {
            getConstellationSettings = constellationSettingsDataProvider ?? throw new ArgumentNullException(nameof(constellationSettingsDataProvider));
            InitCleanupTimer();
        }

        public void Add(ConstellationSettings settings)
        {
            lock (syncRoot)
            {
                var settingsWrapper = new ConstellationSettingsWrapper(settings);
                settingsCache.Add(settingsWrapper);
            }
        }

        public bool TryGetSettings(ulong apex, out ConstellationSettings settings)
        {
            settings = null;
            lock (syncRoot)
            {
                //try to find the settings in cache
                for (int i = settingsCache.Count; i-- > 0;)
                {
                    var currentSettings = settingsCache[i];
                    if (apex >= currentSettings.Apex)
                    {
                        currentSettings.AccessDate = DateTime.UtcNow;
                        settings = currentSettings.Value;
                        return true;
                    }
                }

                //try to load from db
                var lastLoadedSettingsApex = settingsCache.First().Apex;
                while (true)
                {
                    var prevSettingsLastApex = lastLoadedSettingsApex - 1;
                    var loadedItem = getConstellationSettings(apex);
                    //db doesn't contains settings for the apex
                    if (loadedItem == null)
                        break;
                    var item = InsertFirst(loadedItem, prevSettingsLastApex);
                    if (apex >= item.Apex)
                    {
                        settings = item.Value;
                        return true;
                    }
                    //set last loaded apex
                    lastLoadedSettingsApex = item.Apex;
                }
                //no settings found
                return false;
            }
        }

        public void Dispose()
        {
            cleanupTimer.Dispose();
        }

        private ConstellationSettingsWrapper InsertFirst(ConstellationSettings settings, ulong validToApex)
        {
            lock (syncRoot)
            {
                var settingsWrapper = new ConstellationSettingsWrapper(settings);
                settingsCache.Insert(0, settingsWrapper);
                return settingsWrapper;
            }
        }

        private static Logger logger = LogManager.GetCurrentClassLogger();

        private List<ConstellationSettingsWrapper> settingsCache = new List<ConstellationSettingsWrapper>();

        private void InitCleanupTimer()
        {
            cleanupTimer = new Timer();
            cleanupTimer.AutoReset = false;
            cleanupTimer.Interval = TimeSpan.FromSeconds(1).TotalMilliseconds;
            cleanupTimer.Elapsed += CleanupTimer_Elapsed;
            cleanupTimer.Start();
        }

        private void CleanupTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                lock (syncRoot)
                {
                    //cache 1000 items
                    if (settingsCache.Count < 1000)
                        return;
                    var currentDate = DateTime.UtcNow;
                    foreach (var settings in settingsCache)
                    {
                        if (currentDate - settings.AccessDate > TimeSpan.FromSeconds(15))
                        {
                            settingsCache.RemoveAt(0);
                            continue;
                        }
                        //break cleanup to keep settings chain
                        break;
                    }
                }
            }
            catch (Exception exc)
            {
                logger.Error(exc, "Error during settings cache cleanup.");
            }
            finally
            {
                cleanupTimer.Start();
            }
        }

        private object syncRoot = new { };
        private Timer cleanupTimer;
        private Func<ulong, ConstellationSettings> getConstellationSettings;

        class ConstellationSettingsWrapper
        {
            public ConstellationSettingsWrapper(ConstellationSettings settings)
            {
                Value = settings ?? throw new ArgumentNullException(nameof(settings));
                AccessDate = DateTime.UtcNow;
            }

            public ulong Apex => Value.Apex;

            public ConstellationSettings Value { get; }

            public DateTime AccessDate { get; set; }
        }
    }
}