using System.Threading.Tasks;
using Centaurus.Models;
using Centaurus.Xdr;

namespace Centaurus.Domain
{
    //TODO: add stream support
    public abstract class BaseSnapshotDataProvider
    {
        /// <summary>
        /// Fetches last snapshot id
        /// </summary>
        /// <returns></returns>
        public abstract Task<ulong> GetLastSnapshotId();

        /// <summary>
        /// Fetches last saved snapshot
        /// </summary>
        /// <returns>Snapshot as byte array</returns>
        public abstract Task<byte[]> LoadLastSnapshot();

        /// <summary>
        /// Fetches snapshot by id
        /// </summary>
        /// <param name="snapshotId"></param>
        /// <returns>Snapshot as byte array</returns>
        public abstract Task<byte[]> LoadSnapshot(ulong snapshotId);

        /// <summary>
        /// Saves snapshot
        /// </summary>
        /// <param name="snapshotId"></param>
        /// <param name="snapshotData"></param>
        /// <returns>Save task</returns>
        public abstract Task SaveSnapshot(ulong snapshotId, byte[] snapshotData);

        /// <summary>
        /// Saves pending quanta
        /// </summary>
        /// <param name="quanta"></param>
        /// <returns>Save task</returns>
        public abstract Task SavePendingQuanta(byte[] quanta);

        /// <summary>
        /// Fetches pending quanta
        /// </summary>
        /// <returns>Pending quanta as byte array</returns>
        public abstract Task<byte[]> LoadPendingQuanta();



        public virtual async Task<PendingQuanta> GetPendingQuanta()
        {
            var rawPendingQuanta = await LoadPendingQuanta();
            if (rawPendingQuanta == null)
                return null;

            return XdrConverter.Deserialize<PendingQuanta>(rawPendingQuanta);
        }

        public async Task<Snapshot> GetLastSnapshot()
        {
            var lastSnapshotData = await LoadLastSnapshot();
            if (lastSnapshotData == null)
                return null;
            return XdrConverter.Deserialize<Snapshot>(lastSnapshotData);
        }
    }
}
