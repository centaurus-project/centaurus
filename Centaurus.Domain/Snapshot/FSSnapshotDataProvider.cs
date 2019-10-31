using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class FSSnapshotDataProvider : BaseSnapshotDataProvider
    {
        public const string SnaphsotExtension = "snpsht";

        public string SnapshotsDirectory = Path.Combine(Global.Settings.CWD, "Snapshots");

        IFileSystem fileSystem;

        public FSSnapshotDataProvider(IFileSystem fileSystem)
        {
            if (fileSystem == null)
                throw new ArgumentNullException(nameof(fileSystem));
            this.fileSystem = fileSystem;
            if (!this.fileSystem.Directory.Exists(SnapshotsDirectory))
                this.fileSystem.Directory.CreateDirectory(SnapshotsDirectory);
        }
        public override async Task<byte[]> LoadLastSnapshot()
        {
            var lastSnapshotId = await GetLastSnapshotId();
            if (lastSnapshotId == 0)
                return null;
            return await LoadSnapshot(lastSnapshotId);
        }

        public override async Task<ulong> GetLastSnapshotId()
        {
            var file = getOrderedSnapshotFiles().FirstOrDefault();
            if (file == null)
                return 0;
            ulong lastId;
            _ = ulong.TryParse(fileSystem.Path.GetFileNameWithoutExtension(file.Name), out lastId);
            return await Task.FromResult(lastId);
        }

        public override async Task<byte[]> LoadSnapshot(ulong snapshotId)
        {
            var snapshotFilePath = getSnapshotFilePathById(snapshotId);
            if (!fileSystem.File.Exists(snapshotFilePath))
                return null;
            return await fileSystem.File.ReadAllBytesAsync(snapshotFilePath);
        }

        public override async Task SaveSnapshot(ulong snapshotId, byte[] snapshotData)
        {
            var snapshotFilePath = getSnapshotFilePathById(snapshotId);
            if (fileSystem.File.Exists(snapshotFilePath))
                throw new InvalidOperationException($"Snapshot {snapshotId} is already exists");

            await fileSystem.File.WriteAllBytesAsync(snapshotFilePath, snapshotData);
        }

        private string getSnapshotFilePathById(ulong snapshotId)
        {
            return fileSystem.Path.Combine(SnapshotsDirectory, $"{snapshotId}.{SnaphsotExtension}");
        }

        private IEnumerable<FileInfo> getOrderedSnapshotFiles()
        {
            string pattern = $"*.{SnaphsotExtension}";
            var dirInfo = new DirectoryInfo(SnapshotsDirectory);
            return dirInfo.GetFiles(pattern).OrderByDescending(f => f.LastWriteTimeUtc);
        }

        private string getPendingQuantumsFileName()
        {
            return fileSystem.Path.Combine(SnapshotsDirectory, $"pending.qnts");
        }


        public override async Task SavePendingQuanta(byte[] quantums)
        {
            await fileSystem.File.WriteAllBytesAsync(getPendingQuantumsFileName(), quantums);
        }

        public override async Task<byte[]> LoadPendingQuanta()
        {
            var pendingQuantumsFileName = getPendingQuantumsFileName();
            if (!fileSystem.File.Exists(pendingQuantumsFileName))
                return null;
            return await fileSystem.File.ReadAllBytesAsync(pendingQuantumsFileName);
        }
    }
}
