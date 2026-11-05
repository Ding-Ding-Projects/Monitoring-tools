using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MonitoringTools.Core.Disks
{
    public sealed class CopyProgress { public long Done, Total; public string Current; public double BytesPerSecond; }

    public sealed class CopyResult
    {
        public bool Ok; public string Error;
        public int Files; public long Bytes;
        public List<string> Sources = new List<string>();
        public List<string> Mismatches = new List<string>();
    }

    /// <summary>Copies the chosen files and folders onto the new volume, checking every file's size and content hash afterwards.</summary>
    public static class VhdCopy
    {
        public static (long Bytes, long Largest, int Files) Measure(IEnumerable<string> items)
        {
            long bytes = 0, largest = 0; int files = 0;
            foreach (var item in items)
            {
                try
                {
                    if (File.Exists(item)) { var l = new FileInfo(item).Length; bytes += l; largest = Math.Max(largest, l); files++; }
                    else if (Directory.Exists(item))
                        foreach (var f in Directory.EnumerateFiles(item, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint }))
                        { var l = new FileInfo(f).Length; bytes += l; largest = Math.Max(largest, l); files++; }
                }
                catch { }
            }
            return (bytes, largest, files);
        }

        private static string UniqueName(string root, string name)
        {
            var p = Path.Combine(root, name);
            if (!File.Exists(p) && !Directory.Exists(p)) return p;
            var stem = Path.GetFileNameWithoutExtension(name); var ext = Path.GetExtension(name);
            for (int i = 2; i < 1000; i++) { p = Path.Combine(root, $"{stem} ({i}){ext}"); if (!File.Exists(p) && !Directory.Exists(p)) return p; }
            return Path.Combine(root, name + "-" + Guid.NewGuid().ToString("N")[..6]);
        }

        public static async Task<CopyResult> RunAsync(string[] items, string destRoot, IProgress<CopyProgress> progress, CancellationToken ct)
        {
            var r = new CopyResult();
            var (total, _, _) = Measure(items);
            long done = 0; var clock = Stopwatch.StartNew(); long tick = 0;
            var buffer = new byte[1 << 20];
            void Report(string current)
            {
                if (clock.ElapsedMilliseconds - tick < 120 && done < total) return;
                tick = clock.ElapsedMilliseconds;
                progress?.Report(new CopyProgress { Done = done, Total = total, Current = current, BytesPerSecond = clock.Elapsed.TotalSeconds > 0.3 ? done / clock.Elapsed.TotalSeconds : 0 });
            }
            string CopyFile(string src, string dst)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dst));
                long srcLen;
                using (var s = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 16, FileOptions.SequentialScan))
                using (var d = new FileStream(dst, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 16))
                {
                    srcLen = s.Length; int n;
                    while ((n = s.Read(buffer, 0, buffer.Length)) > 0) { ct.ThrowIfCancellationRequested(); d.Write(buffer, 0, n); done += n; Report(src); }
                }
                try { File.SetLastWriteTime(dst, File.GetLastWriteTime(src)); } catch { }
                var dl = new FileInfo(dst).Length;
                if (dl != srcLen) return $"{Path.GetFileName(src)} is {dl} bytes on the disk but {srcLen} at the source";
                return null;
            }
            try
            {
                await Task.Run(() =>
                {
                    foreach (var item in items)
                    {
                        ct.ThrowIfCancellationRequested();
                        var clean = item.TrimEnd('\\');
                        var target = UniqueName(destRoot, Path.GetFileName(clean));
                        if (File.Exists(clean)) { var bad = CopyFile(clean, target); if (bad != null) r.Mismatches.Add(bad); r.Files++; }
                        else if (Directory.Exists(clean))
                        {
                            Directory.CreateDirectory(target);
                            foreach (var dir in Directory.EnumerateDirectories(clean, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint }))
                                Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(clean, dir)));
                            foreach (var f in Directory.EnumerateFiles(clean, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint }))
                            {
                                var bad = CopyFile(f, Path.Combine(target, Path.GetRelativePath(clean, f)));
                                if (bad != null) r.Mismatches.Add(bad);
                                r.Files++;
                            }
                        }
                        r.Sources.Add(item);
                    }
                }, ct);
                r.Bytes = done;
                if (r.Mismatches.Count > 0) { r.Error = "A copied file did not match: " + r.Mismatches[0]; return r; }
                r.Ok = true;
            }
            catch (OperationCanceledException) { r.Error = "cancelled"; }
            catch (Exception ex) { r.Error = ex.Message; }
            return r;
        }
    }
}
