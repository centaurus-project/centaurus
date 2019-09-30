using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class FSSnapshotDataProvider : BaseSnapshotDataProvider
    {

        public const string SnaphsotExtension = "snpsht";

        public string SnapshotsDirectory = Global.Settings.SnapshotsDirectory;

        public FSSnapshotDataProvider()
        {
            if (!Directory.Exists(SnapshotsDirectory))
                Directory.CreateDirectory(SnapshotsDirectory);
        }
        public override async Task<byte[]> GetLastSnapshot()
        {
            var lastSnapshotId = await GetLastSnapshotId();
            if (lastSnapshotId == 0)
                return null;
            return await GetSnapshot(lastSnapshotId);
        }

        public override async Task<ulong> GetLastSnapshotId()
        {
            var file = getOrderedSnapshotFiles().FirstOrDefault();
            if (file == null)
                return 0;
            ulong lastId;
            _ = ulong.TryParse(Path.GetFileNameWithoutExtension(file.Name), out lastId);
            return await Task.FromResult(lastId);
        }

        public override async Task<byte[]> GetSnapshot(ulong snapshotId)
        {
            var snapshotFilePath = getSnapshotFilePathById(snapshotId);
            if (!File.Exists(snapshotFilePath))
                return null;
            return await File.ReadAllBytesAsync(snapshotFilePath);
        }

        public override async Task SaveSnapshot(ulong snapshotId, byte[] snapshotData)
        {
            var snapshotFilePath = getSnapshotFilePathById(snapshotId);
            if (File.Exists(snapshotFilePath))
                throw new InvalidOperationException($"Snapshot {snapshotId} is already exists");

            await File.WriteAllBytesAsync(snapshotFilePath, snapshotData);
        }

        private string getSnapshotFilePathById(ulong snapshotId)
        {
            return Path.Combine(SnapshotsDirectory, $"{snapshotId}.{SnaphsotExtension}");
        }

        private IEnumerable<FileInfo> getOrderedSnapshotFiles()
        {
            string pattern = $"*.{SnaphsotExtension}";
            var dirInfo = new DirectoryInfo(SnapshotsDirectory);
            return dirInfo.GetFiles(pattern).OrderByDescending(f => f.LastWriteTimeUtc);
        }

        private string getPendingQuantumsFileName()
        {
            return Path.Combine(SnapshotsDirectory, $"pending.qnts");
        }


        public override async Task SavePendingQuantums(byte[] quantums)
        {
            await File.WriteAllBytesAsync(getPendingQuantumsFileName(), quantums);
        }

        public override async Task<byte[]> GetPendingQuantums()
        {
            var pendingQuantumsFileName = getPendingQuantumsFileName();
            if (!File.Exists(pendingQuantumsFileName))
                return null;
            return await File.ReadAllBytesAsync(pendingQuantumsFileName);
        }
    }
}
