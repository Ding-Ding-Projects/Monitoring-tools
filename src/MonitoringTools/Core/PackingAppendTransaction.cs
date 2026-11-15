using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using MonitoringTools.Core.Disks;

namespace MonitoringTools.Core.Packing
{
    /// <summary>A same-volume staged update. The original is never mounted or written by the packing helper.</summary>
    internal sealed class PackingAppendTransaction
    {
        public string Target { get; }
        public string OriginalHash { get; private set; }
        public string Backup { get; private set; }
        public VDiskInfo Info { get; private set; }
        public PackingAppendTransaction(string target) { Target = Path.GetFullPath(target); }
        public static VDiskInfo RequireDetached(string target)
        {
            if (!Path.IsPathFullyQualified(target) || !target.EndsWith(".vhdx", StringComparison.OrdinalIgnoreCase) || !File.Exists(target)) throw new IOException("Choose an existing local VHDX file.");
            if (target.StartsWith(@"\\") || (File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0) throw new IOException("Network paths and links cannot be updated.");
            var info = VirtDisk.Read(target);
            if (info.Error != null) throw new IOException(info.Error);
            if (info.Format != "VHDX" || info.Type != "dynamic") throw new IOException("Adding files requires a dynamic VHDX.");
            var attached = VirtDisk.Attached();
            if (VirtDisk.LastAttachedError != 0) throw new IOException("The attached-disk inventory could not be verified.");
            if (info.Attached || attached.Any(p => string.Equals(Path.GetFullPath(p), target, StringComparison.OrdinalIgnoreCase))) throw new IOException("Detach the target disk before adding files.");
            return info;
        }
        public async Task CopyAsync(string staged, IProgress<PackProgress> progress, CancellationToken ct)
        {
            Info = RequireDetached(Target);
            await Task.Run(() =>
            {
                // Refuse an in-use target, and prevent writes while the snapshot is copied.
                using var input = new FileStream(Target, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var output = new FileStream(staged, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = new byte[1024 * 1024]; long done = 0;
                var clock = Stopwatch.StartNew(); long last = -200;
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) != 0)
                {
                    ct.ThrowIfCancellationRequested(); output.Write(buffer, 0, read); hash.AppendData(buffer, 0, read); done += read;
                    if (clock.ElapsedMilliseconds - last >= 150 || done == input.Length)
                    {
                        last = clock.ElapsedMilliseconds;
                        var rate = done / Math.Max(.001, clock.Elapsed.TotalSeconds);
                        progress?.Report(new PackProgress { Done = done, Total = input.Length, FilesDone = done == input.Length ? 1 : 0, FilesTotal = 1, BytesPerSecond = rate, Left = TimeSpan.FromSeconds(Math.Max(0, input.Length - done) / rate), Current = Path.GetFileName(Target) });
                    }
                }
                output.Flush(true);
                OriginalHash = Convert.ToHexString(hash.GetHashAndReset());
            }, ct);
        }
        public async Task PublishAsync(string staged, CancellationToken ct)
        {
            RequireDetached(Target);
            RequireDetached(staged);
            await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                using (var original = new FileStream(Target, FileMode.Open, FileAccess.Read, FileShare.Read))
                    if (Convert.ToHexString(SHA256.HashData(original)) != OriginalHash) throw new IOException("The original disk changed during packing. It was not replaced.");
                RequireDetached(Target);
                ct.ThrowIfCancellationRequested();
                Backup = Target + ".before-add-" + Guid.NewGuid().ToString("N") + ".bak";
                File.Replace(staged, Target, Backup, ignoreMetadataErrors: false);
                // A name-based atomic replace cannot promise compare-and-swap. Keep both files if another writer raced it.
                using var backup = new FileStream(Backup, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (Convert.ToHexString(SHA256.HashData(backup)) != OriginalHash)
                    throw new IOException("The disk changed at replacement time. Both the updated disk and the displaced version are retained; inspect the rollback backup before continuing.");
            }, ct);
        }
    }
}
