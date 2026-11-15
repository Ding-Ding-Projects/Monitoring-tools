using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace MonitoringTools.Core.Packing
{
    internal static class PackingDiskContents
    {
        // Filesystem-managed metadata is not user content and changes when Windows mounts a volume.
        public static async Task<Dictionary<string, string>> HashAsync(string root, CancellationToken ct)
        {
            var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var directories = new Stack<string>(); directories.Push(root);
            int visited = 0;
            while (directories.Count != 0)
            {
                foreach (var path in Directory.EnumerateFileSystemEntries(directories.Pop()))
                {
                    ct.ThrowIfCancellationRequested();
                    if (++visited > PackingSources.Limit) throw new IOException("The existing disk exceeds the supported content inventory limit.");
                    var relative = Path.GetRelativePath(root, path);
                    if (relative.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase) || relative.Equals("$RECYCLE.BIN", StringComparison.OrdinalIgnoreCase)) continue;
                    var attributes = File.GetAttributes(path);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Existing disk content contains a link; transactional additions require ordinary files and folders.");
                    if ((attributes & FileAttributes.Directory) != 0) { directories.Push(path); continue; }
                    using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
                    hashes.Add(relative, Convert.ToHexString(await SHA256.HashDataAsync(file, ct)));
                }
            }
            return hashes;
        }
        public static async Task<bool> VerifyAsync(string root, Dictionary<string, string> expected, CancellationToken ct)
        {
            foreach (var pair in expected)
            {
                ct.ThrowIfCancellationRequested();
                using var file = new FileStream(Path.Combine(root, pair.Key), FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
                if (Convert.ToHexString(await SHA256.HashDataAsync(file, ct)) != pair.Value) return false;
            }
            return true;
        }
    }
}
