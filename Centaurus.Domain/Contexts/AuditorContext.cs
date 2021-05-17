using Centaurus.DAL;
using Centaurus.Stellar;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class AuditorContext : ExecutionContext<AuditorContext, AuditorSettings>
    {
        public AuditorContext(AuditorSettings settings, IStorage storage, StellarDataProviderBase stellarDataProvider, bool useLegacyOrderbook = false) 
            : base(settings, storage, stellarDataProvider, useLegacyOrderbook)
        {
            AppState = new AuditorStateManager(this);
            AppState.StateChanged += AppState_StateChanged;

            QuantumHandler = new AuditorQuantumHandler(this);

            OutgoingMessageStorage = new OutgoingMessageStorage();
            OutgoingResultsStorage = new OutgoingResultsStorage(this);

            MessageHandlers = new MessageHandlers<AuditorWebSocketConnection, AuditorContext>(this);
        }

        public override StateManager AppState { get; }

        public override QuantumHandler QuantumHandler { get; }

        public override MessageHandlers MessageHandlers { get; }

        public OutgoingMessageStorage OutgoingMessageStorage { get; }

        public OutgoingResultsStorage OutgoingResultsStorage { get; }

        public override async Task Setup(Snapshot snapshot)
        {
            await base.Setup(snapshot);

            TxListener?.Dispose(); TxListener = new AuditorTxListener(this, snapshot.TxCursor);

            DisposePerformanceStatisticsManager(); PerformanceStatisticsManager = new AuditorPerformanceStatisticsManager(this);
            PerformanceStatisticsManager.OnUpdates += PerformanceStatisticsManager_OnUpdates;
        }

        private void PerformanceStatisticsManager_OnUpdates(PerformanceStatistics statistics)
        {
            OutgoingMessageStorage.EnqueueMessage(statistics.ToModel());
        }

        public override void Dispose()
        {
            base.Dispose();
            QuantumHandler?.Dispose();
            DisposePerformanceStatisticsManager();
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
