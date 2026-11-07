using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace MonitoringTools.Core.Disks
{
    public sealed class PackProgress
    {
        public long Done, Total;
        public int FilesDone, FilesTotal;
        public string Current;
        public double BytesPerSecond;
        public TimeSpan? Left;
        public bool Paused;
        public string Note;
    }

    public sealed class PackResult
    {
        public bool Ok, Cancelled, Partial;
        public string Error;
        public int Files, Verified;
        public long Bytes;
        public List<(string Path, string Reason)> Skipped = new List<(string, string)>();
        public List<(long From, long To)> Growths = new List<(long, long)>();
    }

    /// <summary>
    /// Copies everything dropped into the packed disk with its folder tree, names, long paths, timestamps, attributes and
    /// alternate data streams (the mark of the web included), and permissions when asked. Every file is read back and
    /// checked by SHA-256 against its source. When the disk fills, it asks for the disk to grow and carries on from the file
    /// it stopped at, checking that file again. A file that cannot be read is listed with its reason and the pack is partial.
    /// The sources are only ever read.
    /// </summary>
    public sealed class PackCopy
    {
        /// <summary>Called when the disk is full, with the bytes still to copy. Returns null when it grew, or why it could not.</summary>
        public Func<long, Task<string>> Grow;
        public bool CopyPermissions;
        /// <summary>Reset to pause between blocks; set to carry on.</summary>
        public readonly ManualResetEventSlim Running = new ManualResetEventSlim(true);

        private readonly byte[] _buffer = new byte[1 << 20];

        private sealed class Entry { public string Source; public string Relative; public bool Folder; public long Size; }

        /// <summary>Every folder and file under the dropped items, with sizes, for the plan and the copy.</summary>
        public static List<(string Source, string Relative, bool Folder, long Size)> Walk(IEnumerable<string> items)
        {
            var list = new List<(string, string, bool, long)>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var opts = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint, ReturnSpecialDirectories = false };
            foreach (var raw in items)
            {
                var item = raw.TrimEnd('\\', '/');
                var name = Path.GetFileName(item);
                if (string.IsNullOrEmpty(name)) name = "Drive " + item.TrimEnd(':');
                var top = name;
                for (int n = 2; !names.Add(top); n++) top = Path.GetFileNameWithoutExtension(name) + $" ({n})" + Path.GetExtension(name);
                if (File.Exists(item)) { list.Add((item, top, false, SafeLength(item))); continue; }
                if (!Directory.Exists(item)) continue;
                list.Add((item, top, true, 0));
                foreach (var d in SafeEnum(() => Directory.EnumerateDirectories(item, "*", opts)))
                    list.Add((d, Path.Combine(top, Path.GetRelativePath(item, d)), true, 0));
                foreach (var f in SafeEnum(() => Directory.EnumerateFiles(item, "*", opts)))
                    list.Add((f, Path.Combine(top, Path.GetRelativePath(item, f)), false, SafeLength(f)));
            }
            return list;
        }

        private static long SafeLength(string f) { try { return new FileInfo(f).Length; } catch { return 0; } }

        private static IEnumerable<string> SafeEnum(Func<IEnumerable<string>> make)
        {
            IEnumerator<string> e;
            try { e = make().GetEnumerator(); } catch { yield break; }
            using (e)
                while (true)
                {
                    string cur;
                    try { if (!e.MoveNext()) yield break; cur = e.Current; } catch { yield break; }
                    yield return cur;
                }
        }

        public static bool IsDiskFull(Exception ex) => ex is IOException io && (io.HResult == unchecked((int)0x80070070) || io.HResult == unchecked((int)0x80070027));

        public async Task<PackResult> RunAsync(IReadOnlyList<(string Source, string Relative, bool Folder, long Size)> walk, string destRoot, IProgress<PackProgress> progress, CancellationToken ct)
        {
            var r = new PackResult();
            long total = walk.Where(w => !w.Folder).Sum(w => w.Size), done = 0;
            int filesTotal = walk.Count(w => !w.Folder), filesDone = 0;
            var clock = Stopwatch.StartNew();
            long lastTick = 0; double speed = 0; long lastDone = 0; double lastT = 0;
            string current = null;
            void Report(bool force = false, string note = null)
            {
                long ms = clock.ElapsedMilliseconds;
                if (!force && ms - lastTick < 150) return;
                double t = clock.Elapsed.TotalSeconds;
                if (t - lastT > 0.25) { double inst = (done - lastDone) / (t - lastT); speed = speed == 0 ? inst : speed * 0.8 + inst * 0.2; lastDone = done; lastT = t; }
                lastTick = ms;
                progress?.Report(new PackProgress
                {
                    Done = done, Total = total, FilesDone = filesDone, FilesTotal = filesTotal, Current = current, BytesPerSecond = speed,
                    Left = speed > 1 ? TimeSpan.FromSeconds((total - done) / speed) : null, Paused = !Running.IsSet, Note = note
                });
            }
            try
            {
                await Task.Run(async () =>
                {
                    foreach (var w in walk.Where(w => w.Folder)) Directory.CreateDirectory(Path.Combine(destRoot, w.Relative));
                    foreach (var w in walk.Where(w => !w.Folder))
                    {
                        ct.ThrowIfCancellationRequested();
                        current = w.Relative;
                        var dst = Path.Combine(destRoot, w.Relative);
                        while (true)
                        {
                            long before = done;
                            try
                            {
                                var problem = CopyOne(w.Source, dst, ref done, Report, ct);
                                if (problem != null) { r.Skipped.Add((w.Source, problem)); done = before + w.Size; break; }
                                r.Verified++;
                                break;
                            }
                            catch (Exception ex) when (IsDiskFull(ex))
                            {
                                // full: take out the half-written file, grow the disk, and start this file again
                                done = before;
                                TryDelete(dst);
                                Report(true, "The disk is full. Growing it…");
                                var err = Grow == null ? "The disk is full." : await Grow(total - done);
                                if (err != null) { r.Error = err; return; }
                                Report(true, "Grown. Carrying on from " + Path.GetFileName(w.Source) + ".");
                            }
                        }
                        filesDone++;
                        r.Files++;
                        Report();
                    }
                    // folder times last, after their contents stopped changing them
                    foreach (var w in walk.Where(w => w.Folder).Reverse()) CopyTimes(w.Source, Path.Combine(destRoot, w.Relative), folder: true);
                }, ct);
                r.Bytes = done;
                if (r.Error != null) return r;
                r.Partial = r.Skipped.Count > 0;
                r.Ok = !r.Partial;
            }
            catch (OperationCanceledException) { r.Cancelled = true; r.Error = "Cancelled."; }
            catch (Exception ex) { r.Error = ex.Message; }
            Report(true);
            return r;
        }

        /// <summary>Copies and checks one file. Returns null when it matches its source, or why it was left out.</summary>
        private string CopyOne(string src, string dst, ref long done, Action<bool, string> report, CancellationToken ct)
        {
            FileStream s;
            try { s = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 16, FileOptions.SequentialScan); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { return ReadProblem(ex); }
            byte[] want;
            using (s)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dst));
                using var d = new FileStream(dst, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 16);
                using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                int n;
                while (true)
                {
                    Running.Wait(ct);
                    try { n = s.Read(_buffer, 0, _buffer.Length); }
                    catch (Exception ex) when (ex is IOException && !IsDiskFull(ex)) { d.Dispose(); TryDelete(dst); return ReadProblem(ex); }
                    if (n <= 0) break;
                    ct.ThrowIfCancellationRequested();
                    sha.AppendData(_buffer, 0, n);
                    d.Write(_buffer, 0, n);
                    done += n;
                    report(false, null);
                }
                d.Flush(true);
                want = sha.GetHashAndReset();
            }
            CopyStreams(src, dst);
            if (CopyPermissions) CopyAcl(src, dst);
            CopyTimes(src, dst, folder: false);
            try { File.SetAttributes(dst, File.GetAttributes(src) & ~(FileAttributes.Compressed | FileAttributes.Encrypted | FileAttributes.SparseFile | FileAttributes.ReparsePoint)); } catch { }
            // read it back from the disk and compare
            byte[] got;
            using (var back = new FileStream(dst, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, FileOptions.SequentialScan))
            using (var sha = SHA256.Create()) got = sha.ComputeHash(back);
            return CryptographicOperations.FixedTimeEquals(want, got) ? null : "did not match its original after copying, so it is not counted";
        }

        private static string ReadProblem(Exception ex) => ex switch
        {
            UnauthorizedAccessException => "Windows denied read access",
            IOException io when io.HResult == unchecked((int)0x80070020) => "open in another app, which locks it",
            IOException io when io.HResult == unchecked((int)0x80070021) => "part of it is locked by another app",
            FileNotFoundException or DirectoryNotFoundException => "it was removed while packing",
            _ => "it could not be read: " + ex.Message.TrimEnd('.')
        };

        private static void TryDelete(string p) { try { if (File.Exists(p)) { File.SetAttributes(p, FileAttributes.Normal); File.Delete(p); } } catch { } }

        private static void CopyTimes(string src, string dst, bool folder)
        {
            try
            {
                if (folder)
                {
                    Directory.SetCreationTimeUtc(dst, Directory.GetCreationTimeUtc(src));
                    Directory.SetLastWriteTimeUtc(dst, Directory.GetLastWriteTimeUtc(src));
                    Directory.SetLastAccessTimeUtc(dst, Directory.GetLastAccessTimeUtc(src));
                }
                else
                {
                    File.SetCreationTimeUtc(dst, File.GetCreationTimeUtc(src));
                    File.SetLastWriteTimeUtc(dst, File.GetLastWriteTimeUtc(src));
                    File.SetLastAccessTimeUtc(dst, File.GetLastAccessTimeUtc(src));
                }
            }
            catch { }
        }

        private static void CopyAcl(string src, string dst)
        {
            if (!OperatingSystem.IsWindows()) return;
            try { new FileInfo(dst).SetAccessControl(new FileInfo(src).GetAccessControl()); } catch { }
        }

        // ------------------------------------------------------------------ alternate data streams

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct StreamData { public long Size; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 296)] public string Name; }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr FindFirstStreamW(string path, int level, out StreamData data, int flags);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool FindNextStreamW(IntPtr find, out StreamData data);
        [DllImport("kernel32.dll")]
        private static extern bool FindClose(IntPtr find);

        /// <summary>The named data streams of a file, such as ":Zone.Identifier:$DATA" (the mark of the web).</summary>
        public static List<string> Streams(string path)
        {
            var list = new List<string>();
            if (!OperatingSystem.IsWindows()) return list;
            var h = FindFirstStreamW(path, 0, out var d, 0);
            if (h == new IntPtr(-1)) return list;
            try
            {
                do { if (!string.IsNullOrEmpty(d.Name) && d.Name != "::$DATA") list.Add(d.Name); }
                while (FindNextStreamW(h, out d));
            }
            finally { FindClose(h); }
            return list;
        }

        private void CopyStreams(string src, string dst)
        {
            foreach (var name in Streams(src))
            {
                var stream = name.EndsWith(":$DATA", StringComparison.OrdinalIgnoreCase) ? name.Substring(0, name.Length - 6) : name;
                try
                {
                    using var i = new FileStream(src + stream, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var o = new FileStream(dst + stream, FileMode.Create, FileAccess.Write);
                    i.CopyTo(o);
                }
                catch (Exception ex) when (IsDiskFull(ex)) { throw; }
                catch { /* a stream that cannot be copied does not stop the file; the data and its check still count */ }
            }
        }
    }
}
