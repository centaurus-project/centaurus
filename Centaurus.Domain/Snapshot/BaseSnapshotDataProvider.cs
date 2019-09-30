using System.Threading.Tasks;

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
        public abstract Task<byte[]> GetLastSnapshot();

        /// <summary>
        /// Fetches snapshot by id
        /// </summary>
        /// <param name="snapshotId"></param>
        /// <returns>Snapshot as byte array</returns>
        public abstract Task<byte[]> GetSnapshot(ulong snapshotId);

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
        /// <param name="quantums"></param>
        /// <returns>Save task</returns>
        public abstract Task SavePendingQuantums(byte[] quantums);

        /// <summary>
        /// Fetches pending quanta
        /// </summary>
        /// <returns>Pending quanta as byte array</returns>
        public abstract Task<byte[]> GetPendingQuantums();
    }
}
