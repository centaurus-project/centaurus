using Centaurus.Domain.Nodes;
using Centaurus.Domain.Nodes.Common;
using Centaurus.Domain.RemoteNodes;
using Centaurus.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    //TODO: move SyncSource logic to separate class
    internal class NodesManager : ContextualBase
    {
        static Logger logger = LogManager.GetCurrentClassLogger();
        public NodesManager(ExecutionContext context, State currentNodeInitState)
            : base(context)
        {
            nodes = new RemoteNodesCollection();
            nodes.OnAdd += Nodes_OnAdd;
            nodes.OnRemove += Nodes_OnRemove;
            CurrentNode = new CurrentNode(Context, currentNodeInitState);
            SyncSourceManager = new SyncSourceManager(Context);
        }

        public CurrentNode CurrentNode { get; }

        public SyncSourceManager SyncSourceManager { get; }

        public bool IsMajorityReady { get; private set; }

        public List<NodeBase> AllNodes { get; private set; }

        public NodeBase AlphaNode { get; private set; }

        public Dictionary<byte, RawPubKey> NodeIds { get; private set; }

        public Dictionary<RawPubKey, byte> NodePubKeys { get; private set; }

        /// <summary>
        /// Is current node is Alpha
        /// </summary>
        public bool IsAlpha => CurrentNode.IsAlpha;

        public bool TryGetNode(RawPubKey rawPubKey, out RemoteNode node)
        {
            return nodes.TryGetNode(rawPubKey, out node);
        }

        public List<RemoteNode> GetRemoteNodes()
        {
            return nodes.GetAllNodes();
        }

        public async Task SetNodes(List<Auditor> auditors)
        {
            var invalidatedNodes = await UpdateNodes(auditors);
            RemoveNodes(invalidatedNodes);
            SetAllNodes();
            SetNodeIds();
            SetAlphaNode();
            CalcMajorityReadiness();
        }

        public bool IsNode(RawPubKey pubKey)
        {
            return NodePubKeys.ContainsKey(pubKey);
        }

        private void SetAlphaNode()
        {
            if (Context.ConstellationSettingsManager.Current == null)
                return;
            var alphaNode = AllNodes.FirstOrDefault(n => n.PubKey.Equals(Context.ConstellationSettingsManager.Current.Alpha));
            if (alphaNode == null)
                throw new Exception("Alpha node is not found");
            AlphaNode = alphaNode;
        }

        private void SetNodeIds()
        {
            var nodeIds = new Dictionary<byte, RawPubKey>();
            var nodePubkeys = new Dictionary<RawPubKey, byte>();
            foreach (var node in AllNodes)
            {
                if (node.Id == 0)
                    continue;
                nodeIds.Add(node.Id, node.PubKey);
                nodePubkeys.Add(node.PubKey, node.Id);
            }
            NodeIds = nodeIds;
            NodePubKeys = nodePubkeys;
        }

        private void RemoveNodes(List<RemoteNode> invalidatedNodes)
        {
            foreach (var node in invalidatedNodes)
                nodes.RemoveNode(node);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="auditors"></param>
        /// <returns>Invalidated nodes</returns>
        private async Task<List<RemoteNode>> UpdateNodes(List<Auditor> auditors)
        {
            var currentNodes = GetRemoteNodes().ToDictionary(n => n.PubKey, n => n);
            var currentAuditorKey = (RawPubKey)Context.Settings.KeyPair;
            foreach (var auditor in auditors)
            {
                if (auditor.PubKey.Equals(currentAuditorKey))
                {
                    UpdateCurrentNode(auditor.PubKey);
                    continue;
                }
                await SetRemoteNode(auditor);
                currentNodes.Remove(auditor.PubKey);
            }
            //all nodes that weren't removed are no more relevant
            return currentNodes.Values.ToList();
        }

        private async Task SetRemoteNode(Auditor auditor)
        {
            var pubKey = auditor.PubKey;
            UriHelper.TryCreateWsConnection(auditor.Address, Context.Settings.UseSecureConnection, out var uri);
            var settings = new NodeSettings(GetNodeId(pubKey), uri);
            if (nodes.TryGetNode(pubKey, out var node))
            {
                if (node.Settings == settings)
                    return;
                var shouldReconnect = settings.Address != node.Address;
                node.UpdateSettings(settings);
                if (shouldReconnect)
                {
                    await node.CloseOutConnection();
                    node.ConnectTo();
                }
                return;
            }
            node = new RemoteNode(Context, pubKey);
            node.UpdateSettings(settings);
            nodes.AddNode(node);
        }

        private void UpdateCurrentNode(RawPubKey pubKey)
        {
            var nodeId = GetNodeId(pubKey);
            if (nodeId != CurrentNode.Id)
                CurrentNode.UpdateSettings(new NodeSettings(nodeId, null));
        }

        private int GetNodeId(RawPubKey pubKey)
        {
            if (Context.ConstellationSettingsManager.Current == null)
                return 0;
            return Context.ConstellationSettingsManager.Current.GetNodeId(pubKey);
        }

        private void SetAllNodes()
        {
            var allNodes = GetRemoteNodes().Cast<NodeBase>().ToList();
            allNodes.Insert(0, CurrentNode);
            AllNodes = allNodes;
        }

        private void Nodes_OnAdd(RemoteNode node)
        {
            node.OnStateUpdated += Node_OnStateUpdated;
            node.OnLastApexUpdated += Node_OnLastApexUpdated;
            node.ConnectTo();
            CalcMajorityReadiness();
        }

        private async void Nodes_OnRemove(RemoteNode node)
        {
            node.OnStateUpdated -= Node_OnStateUpdated;
            node.OnLastApexUpdated -= Node_OnLastApexUpdated;
            await node.CloseConnections();
            CalcMajorityReadiness();
        }

        private void Node_OnLastApexUpdated(RemoteNode node)
        {

        }

        private void Node_OnStateUpdated(RemoteNode node)
        {
            CalcMajorityReadiness();
        }

        private readonly RemoteNodesCollection nodes;

        private void CalcMajorityReadiness()
        {
            var majorityReadiness = false;
            //if Prime node than it must be connected with other nodes
            if (Context.NodesManager.CurrentNode.IsPrimeNode)
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
            var isAlphaReady = Context.NodesManager.IsAlpha;
            var connectedCount = 0;
            foreach (var node in nodes.GetAllNodes())
            {
                if (!(node.State == State.Ready || node.State == State.Running))
                    continue;
                if (node == AlphaNode)
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
                if (node != AlphaNode)
                    continue;
                //if auditor doesn't have connections with another auditors, we only need to verify alpha's state
                return node.State == State.Ready || node.State == State.Running;
            }
            return false;
        }
    }
}