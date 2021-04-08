using Centaurus.DAL;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class AuditorContext : CentaurusContext
    {
        public AuditorContext(BaseSettings settings, IStorage storage, bool useLegacyOrderbook = false) 
            : base(settings, storage, useLegacyOrderbook)
        {
            AppState = new AuditorStateManager(this);
            AppState.StateChanged += AppState_StateChanged;

            QuantumStorage = new AuditorQuantumStorage();
            QuantumHandler = new AuditorQuantumHandler(this);

            OutgoingMessageStorage = new OutgoingMessageStorage();
            OutgoingResultsStorage = new OutgoingResultsStorage(this);
        }

        public override StateManager AppState { get; }

        public override QuantumStorageBase QuantumStorage { get; }

        public override QuantumHandler QuantumHandler { get; }

        public OutgoingMessageStorage OutgoingMessageStorage { get; }

        public OutgoingResultsStorage OutgoingResultsStorage { get; }

        public override async Task Setup(Snapshot snapshot)
        {
            await base.Setup(snapshot);

            TxListener?.Dispose(); TxListener = new AuditorTxListener(this, snapshot.TxCursor);

            PerformanceStatisticsManager = new AuditorPerformanceStatisticsManager(this);
            PerformanceStatisticsManager.OnUpdates += PerformanceStatisticsManager_OnUpdates;
        }

        private void PerformanceStatisticsManager_OnUpdates(PerformanceStatistics statistics)
        {
            OutgoingMessageStorage.EnqueueMessage(statistics.ToModel());
        }

        private void DisposePerformanceStatisticsManager()
        {
            if (PerformanceStatisticsManager != null)
            {
                PerformanceStatisticsManager.OnUpdates -= PerformanceStatisticsManager_OnUpdates;
                PerformanceStatisticsManager.Dispose();
            }
        }
    }
}
