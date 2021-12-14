using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain.RemoteNodes
{
    internal class RemoteNodesCollection
    {
        private object syncRoot = new { };
        private Dictionary<RawPubKey, RemoteNode> nodes = new Dictionary<RawPubKey, RemoteNode>();

        public void AddNode(RemoteNode remoteNode)
        {
            lock (syncRoot)
            {
                nodes.Add(remoteNode.PubKey, remoteNode);
                OnAdd?.Invoke(remoteNode);
            }
        }

        public void RemoveNode(RemoteNode remoteNode)
        {
            lock (syncRoot)
            {
                if (nodes.Remove(remoteNode.PubKey))
                    OnRemove?.Invoke(remoteNode);
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
                foreach (var node in removedNodes)
                {
                    OnRemove?.Invoke(node);
                }
            }
        }
    }
}