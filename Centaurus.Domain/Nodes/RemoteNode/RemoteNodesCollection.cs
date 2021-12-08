using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain.StateManagers
{
    internal class RemoteNodesCollection
    {
        private object syncRoot = new { };
        private Dictionary<RawPubKey, RemoteNode> nodes = new Dictionary<RawPubKey, RemoteNode>();

        public void SetNodes(Dictionary<RawPubKey, Lazy<RemoteNode>> nodeItems)
        {
            lock (syncRoot)
            {
                //copy current auditors
                var oldNodes = nodes.ToDictionary(a => a.Key, a => a.Value);
                nodes.Clear();
                foreach (var nodeItem in nodeItems)
                {
                    var pubKey = nodeItem.Key;
                    var node = nodeItem.Value;
                    //if the auditor node already presented, re-add it. Otherwise create and add the new instance.
                    if (oldNodes.Remove(pubKey, out var auditorState))
                    {
                        nodes.Add(pubKey, auditorState);
                        continue;
                    }
                    nodes.Add(pubKey, node.Value);
                    OnAdd?.Invoke(node.Value);
                }
                //all nodes that are not presented in new auditors list, should be removed
                NodesRemoved(oldNodes.Values.ToList());
            }
        }

        public event Action<RemoteNode> OnAdd;

        public event Action<RemoteNode> OnRemove;

        public List<RemoteNode> GetAllNodes()
        {
            lock (syncRoot)
            {
                return nodes.Values.ToList();
            }
        }

        public bool TryGetNode(RawPubKey pubKey, out RemoteNode node)
        {
            lock (syncRoot)
                return nodes.TryGetValue(pubKey, out node);
        }

        internal void Clear()
        {
            lock (syncRoot)
            {
                var removedNodes = nodes.Values.ToList();
                nodes.Clear();
                NodesRemoved(removedNodes);
            }
        }

        private void NodesRemoved(List<RemoteNode> removedNodes)
        {
            foreach (var removedNode in removedNodes)
            {
                OnRemove?.Invoke(removedNode);
            }
        }
    }
}