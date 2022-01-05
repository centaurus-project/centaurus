using Centaurus.Domain.Models;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public static class ConstellationSettingsExtensions
    {
        public static byte GetNodeId(this ConstellationSettings constellation, RawPubKey rawPubKey)
        {
            if (constellation == null)
                throw new ArgumentNullException(nameof(constellation));

            var auditorIndex = constellation.Auditors.FindIndex(a => a.PubKey.Equals(rawPubKey));
            if (auditorIndex == -1)
                throw new ArgumentNullException($"{rawPubKey.GetAccountId()} is not an auditor.");
            //all auditor must have id that is greater than zero
            return (byte)(auditorIndex + 1);
        }

        public static bool TryGetNodePubKey(this ConstellationSettings constellation, byte nodeId, out RawPubKey pubKey)
        {
            pubKey = null;
            if (constellation == null)
                return false;
            if (constellation.Auditors.Count < nodeId)
                return false;
            var nodeIndex = nodeId - 1;
            pubKey = constellation.Auditors[nodeIndex].PubKey;
            return true;
        }

        public static Snapshot ToSnapshot(this ConstellationSettings settings, ulong apex, List<Account> accounts, List<OrderWrapper> orders, Dictionary<string, string> cursors, byte[] quantumHash)
        {
            if (apex < 1)
                throw new ArgumentException("Apex must be greater than zero.");

            var snapshot = new Snapshot
            {
                Apex = apex,
                Accounts = accounts ?? throw new ArgumentNullException(nameof(accounts)),
                Orders = orders ?? throw new ArgumentNullException(nameof(orders)),
                ConstellationSettings = settings ?? throw new ArgumentNullException(nameof(settings)),
                Cursors = cursors ?? throw new ArgumentNullException(nameof(cursors)),
                LastHash = quantumHash ?? throw new ArgumentNullException(nameof(quantumHash))
            };
            return snapshot;
        }
    }
}
