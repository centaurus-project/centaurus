using Centaurus.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace Centaurus.Domain
{
    internal class PerformanceStatisticsManager : ContextualBase, IDisposable
    {
        const int updateInterval = 1000;

        public PerformanceStatisticsManager(ExecutionContext context)
            : base(context)
        {
            updateTimer = new Timer();
            updateTimer.Interval = updateInterval;
            updateTimer.AutoReset = false;
            updateTimer.Elapsed += UpdateTimer_Elapsed;
            updateTimer.Start();
        }

        public void Dispose()
        {
            lock (syncRoot)
            {
                updateTimer?.Stop();
                updateTimer?.Dispose();
            }
        }

        private object syncRoot = new { };
        private readonly Timer updateTimer;

        private void UpdateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (Context.NodesManager.CurrentNode.State != State.Undefined)
            {
                lock (syncRoot)
                    SendToSubscribers();
            }
            updateTimer?.Start();
        }

        private List<PerformanceStatistics> GetStatistics()
        {
            var statistics = GetRemoteNodesStatistics();
            statistics.Insert(0, GetNodeStatistics(Context.NodesManager.CurrentNode));
            return statistics;
        }

        private List<PerformanceStatistics> GetRemoteNodesStatistics()
        {
            var nodes = Context.NodesManager.GetRemoteNodes();
            var statistics = new List<PerformanceStatistics>();
            foreach (var node in nodes)
            {
                var nodeStatistics = GetNodeStatistics(node);
                statistics.Add(nodeStatistics);
            }
            return statistics;
        }

        private PerformanceStatistics GetNodeStatistics(NodeBase node)
        {
            var accountId = node.PubKey.GetAccountId();
            var lastApex = node.LastApex;
            var nodeStatistics = new PerformanceStatistics
            {
                PublicKey = accountId,
                Apex = lastApex,
                PersistedApex = node.LastPersistedApex,
                QuantaPerSecond = node.QuantaPerSecond,
                QuantaQueueLength = node.QuantaQueueLength,
                //Throttling = GetThrottling(),
                UpdateDate = DateTime.UtcNow,
                State = (int)node.State,
                ApexDiff = (long)(Context.QuantumHandler.CurrentApex - node.LastApex)
            };
            return nodeStatistics;
        }

        void SendToSubscribers()
        {
            if (!Context.SubscriptionsManager.TryGetSubscription(PerformanceStatisticsSubscription.SubscriptionName, out var subscription))
                return;

            Context.InfoConnectionManager.SendSubscriptionUpdate(subscription, PerformanceStatisticsUpdate.Generate(GetStatistics(), PerformanceStatisticsSubscription.SubscriptionName));
        }
    }
}
