using Centaurus.DAL;
using Centaurus.Models;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace Centaurus.Domain
{
    public class PendingUpdatesManager : ContextualBase
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public PendingUpdatesManager(ExecutionContext context)
            : base(context)
        {
        }

        public DiffObject Current { get; private set; } = new DiffObject();

        public event Action<BatchSavedInfo> OnBatchSaved;

        public void ApplyUpdates(DiffObject updates)
        {
            try
            {
                var sw = new Stopwatch();
                sw.Start();
                Context.PersistenceManager.ApplyUpdates(updates);
                sw.Stop();

                var batchInfo = new BatchSavedInfo
                {
                    SavedAt = DateTime.UtcNow,
                    QuantaCount = updates.QuantaCount,
                    EffectsCount = updates.EffectsCount,
                    ElapsedMilliseconds = sw.ElapsedMilliseconds
                };
                _ = Task.Factory.StartNew(() => OnBatchSaved?.Invoke(batchInfo));
            }
            catch (Exception exc)
            {
                if (Context.AppState.State != ApplicationState.Failed)
                {
                    logger.Error(exc, $"Snapshot failed.");
                    Context.AppState.State = ApplicationState.Failed;
                }
            }
        }
    }
}
