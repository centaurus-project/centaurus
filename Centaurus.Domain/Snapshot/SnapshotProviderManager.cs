using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public static class SnapshotProviderManager
    {
        public static BaseSnapshotDataProvider SnapshotDataProvider { get; } = GetSnapshotDataProvider();

        private static BaseSnapshotDataProvider GetSnapshotDataProvider()
        {
            //TODO: read from settings
            return new FSSnapshotDataProvider();
        }

        public static async Task<PendingQuanta> GetPendingQuantums()
        {
            var rawPendingQuantums = await SnapshotDataProvider.GetPendingQuantums();
            if (rawPendingQuantums == null)
                return null;

            return PendingQuanta.FromByteArray(rawPendingQuantums);
        }

        public static async Task<Snapshot> GetLastSnapshot()
        {
            var lastSnapshotData = await SnapshotDataProvider.GetLastSnapshot();
            if (lastSnapshotData == null)
                return null;
            return XdrConverter.Deserialize<Snapshot>(lastSnapshotData);
        }
    }
}
