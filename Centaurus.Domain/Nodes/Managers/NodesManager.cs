using Centaurus.Domain.StateManagers;
using Centaurus.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    internal class NodesManager : ContextualBase
    {
        static Logger logger = LogManager.GetCurrentClassLogger();
        public NodesManager(ExecutionContext context, State currentNodeInitState)
            : base(context)
        {
            nodes = new RemoteNodesCollection();
            nodes.OnAdd += Nodes_OnAdd;
            nodes.OnRemove += Nodes_OnRemove;
            CurrentNode = new CurrentNode(Context, Context.Settings.KeyPair, currentNodeInitState);
        }

        public RemoteNode SyncSource { get; private set; }

        public CurrentNode CurrentNode { get; }

        public bool IsMajorityReady { get; private set; }

        public List<RemoteNode> GetRemoteNodes()
        {
            return nodes.GetAllNodes();
        }

        //TODO: handle address switch
        public void SetAuditors(List<Auditor> auditors)
        {
            var currentAuditorKey = (RawPubKey)Context.Settings.KeyPair;
            var ueHttps = Context.Settings.UseSecureConnection;
            var newNodes = new Dictionary<RawPubKey, Lazy<RemoteNode>>();
            foreach (var auditor in auditors)
            {
                var pubKey = auditor.PubKey;
                if (pubKey.Equals(currentAuditorKey))
                    continue;

                UriHelper.TryCreateWsConnection(auditor.Address, ueHttps, out var uri);
                newNodes.Add(pubKey, new Lazy<RemoteNode>(() => new RemoteNode(Context, pubKey, uri)));
            }
            nodes.SetNodes(newNodes);
        }

        public void EnqueueResult(AuditorResult result)
        {
            foreach (var node in GetRemoteNodes())
                node.EnqueueResult(result);
        }

        public void EnqueueMessage(MessageEnvelopeBase message)
        {
            foreach (var node in GetRemoteNodes())
            {
                node.EnqueueMessage(message);
            }
        }

        public bool TryGetNode(RawPubKey rawPubKey, out RemoteNode node)
        {
            return nodes.TryGetNode(rawPubKey, out node);
        }

        private void Nodes_OnAdd(RemoteNode node)
        {
            node.OnStateUpdated += Node_OnStateUpdated;
            node.OnLastApexUpdated += Node_OnLastApexUpdated;
            node.Connect();
            CalcMajorityReadiness();
        }

        private void Nodes_OnRemove(RemoteNode node)
        {
            node.OnStateUpdated -= Node_OnStateUpdated;
            node.OnLastApexUpdated -= Node_OnLastApexUpdated;
            node.CloseOugoingConnection();
            CalcMajorityReadiness();
        }

        private void Node_OnLastApexUpdated(RemoteNode node)
        {
            //update current sync node
            if (!Context.IsAlpha && node.LastApex > 0)
            {
                var syncSourceLastApex = SyncSource.LastApex;
                if (SyncSource == null ||
                    syncSourceLastApex < node.LastApex //if the current node ahead
                    && node.LastApex - syncSourceLastApex > 1000 //and the apexes difference greater than 1000
                    && (DateTime.UtcNow - SyncSourceUpdateDate) > TimeSpan.FromSeconds(1)) //and the last sync source update was later than second ago
                {
                    SetSyncSource(node);
                }
            }
        }

        private void Node_OnStateUpdated(RemoteNode node)
        {
            CalcMajorityReadiness();
        }

        private DateTime SyncSourceUpdateDate;
        private object syncSourceSyncRoot = new { };

        private readonly RemoteNodesCollection nodes;

        private void SetSyncSource(RemoteNode auditorState)
        {
            lock (syncSourceSyncRoot)
            {
                ClearCurrentSyncCursor();

                SyncSource = auditorState;
                SyncSourceUpdateDate = DateTime.UtcNow;

                SetCurrentSyncCursor();
            }
        }

        private void ClearCurrentSyncCursor()
        {
            if (SyncSource != null)
                Context.Notify(SyncSource.PubKey, new SyncCursorReset
                {
                    Cursors = new List<SyncCursor> {
                            new SyncCursor { Type = XdrSyncCursorType.Quanta, DisableSync = true },
                            new SyncCursor { Type = XdrSyncCursorType.Signatures, DisableSync = true },
                        }
                }.CreateEnvelope<MessageEnvelopeSignless>());
        }

        private void SetCurrentSyncCursor()
        {
            if (SyncSource != null)
                Context.Notify(SyncSource.PubKey, new SyncCursorReset
                {
                    Cursors = new List<SyncCursor> {
                            new SyncCursor { Type = XdrSyncCursorType.Quanta, Cursor = Context.QuantumHandler.LastAddedQuantumApex },
                            new SyncCursor { Type = XdrSyncCursorType.Signatures, Cursor = Context.PendingUpdatesManager.LastPersistedApex },
                        }
                }.CreateEnvelope<MessageEnvelopeSignless>());
        }

        private void CalcMajorityReadiness()
        {
            var majorityReadiness = false;
            //if Prime node than it must be connected with other nodes
            if (Context.RoleManager.ParticipationLevel == CentaurusNodeParticipationLevel.Prime)
            {
                majorityReadiness = GetReadinessForPrimeNode();
            }
            else
                majorityReadiness = GetReadinessForAuditorNode();

            TrySetReadiness(majorityReadiness);
        }

        private void TrySetReadiness(bool majorityReadiness)
        {
            if (majorityReadiness != IsMajorityReady)
            {
                IsMajorityReady = majorityReadiness;
                //update current node state
                CurrentNode.RefreshState();
            }
        }

        private bool GetReadinessForPrimeNode()
        {
            //if current server is Alpha, than ignore Alpha validation
            var isAlphaReady = Context.IsAlpha;
            var connectedCount = 0;
            foreach (var node in nodes.GetAllNodes())
            {
                if (!(node.State == State.Ready || node.State == State.Running))
                    continue;
                if (Context.Constellation.Alpha.Equals(node.PubKey))
                    isAlphaReady = true;
                connectedCount++;
            }
            return isAlphaReady && Context.HasMajority(connectedCount, false);
        }

        internal void ClearNodes()
        {
            nodes.Clear();
        }

        private bool GetReadinessForAuditorNode()
        {
            foreach (var node in nodes.GetAllNodes())
            {
                if (!node.PubKey.Equals(Context.Constellation.Alpha))
                    continue;
                //if auditor doesn't have connections with another auditors, we only need to verify alpha's state
                return node.State == State.Ready || node.State == State.Running;
            }
            return false;
        }
    }
}