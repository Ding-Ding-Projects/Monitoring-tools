using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MonitoringTools.Core.Archives
{
    /// <summary>
    /// Writes and tests archives. Zip and tar.gz use System.IO.Compression and System.Formats.Tar; everything those cannot do
    /// (7z, encrypted zip, split volumes, the extra zip methods) goes through the bundled 7-Zip command line.
    /// </summary>
    public static class ArchiveEngine
    {
        // ------------------------------------------------------------ the live estimate

        private static readonly HashSet<string> Squeezed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".zip", ".7z", ".rar", ".gz", ".xz", ".bz2", ".zst", ".jpg", ".jpeg", ".png", ".webp", ".avif", ".heic", ".gif", ".mp3", ".aac", ".m4a", ".ogg", ".opus", ".flac",
            ".mp4", ".mkv", ".mov", ".webm", ".avi", ".m4v", ".docx", ".xlsx", ".pptx", ".odt", ".pdf", ".jar", ".apk", ".msi", ".cab"
        };
        private static readonly HashSet<string> Texty = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".md", ".json", ".xml", ".csv", ".log", ".html", ".htm", ".css", ".js", ".ts", ".cs", ".cpp", ".h", ".py", ".java", ".yml", ".yaml", ".svg", ".ini", ".sql", ".bat", ".ps1", ".sh", ".tsv", ".rtf"
        };

        /// <summary>A rough size and time from the file types and the settings. It is always shown as an estimate.</summary>
        public static ArchivePlan Estimate(ArchiveScan scan, ArchiveOptions o)
        {
            var chosen = scan.Select(o);
            var plan = new ArchivePlan { Excluded = scan.Entries.Count - chosen.Count };
            double est = 0;
            foreach (var (e, _) in chosen)
            {
                if (e.IsEmptyDirectory) { plan.Folders++; continue; }
                plan.Files++; plan.SourceBytes += e.Size;
                var ext = Path.GetExtension(e.Full);
                double ratio = Squeezed.Contains(ext) ? 0.97 : Texty.Contains(ext) ? 0.24 : ext.Equals(".exe", StringComparison.OrdinalIgnoreCase) || ext.Equals(".dll", StringComparison.OrdinalIgnoreCase) ? 0.45 : 0.62;
                est += e.Size * ratio;
            }
            double level = o.Level switch { 0 => 0, 1 => 1.18, 2 => 1.08, 3 => 1.0, 4 => 0.93, _ => 0.89 };
            plan.EstimatedBytes = o.Level == 0 ? plan.SourceBytes : (long)Math.Min(plan.SourceBytes, est * level);
            plan.EstimatedBytes += 200L * (plan.Files + plan.Folders) + 1024;
            if (o.Split && o.VolumeBytes > 0) plan.Volumes = (int)Math.Max(1, (plan.EstimatedBytes + o.VolumeBytes - 1) / o.VolumeBytes);
            double mbPerSecond = o.Level switch { 0 => 400, 1 => 140, 2 => 90, 3 => 45, 4 => 16, _ => 8 };
            if (o.Format == ArchiveFormat.SevenZip && o.Threads > 1) mbPerSecond *= Math.Min(o.Threads, Environment.ProcessorCount) * 0.7;
            plan.EstimatedTime = TimeSpan.FromSeconds(plan.SourceBytes / 1048576.0 / Math.Max(1, mbPerSecond) * (o.TestAfter ? 1.3 : 1.0));
            plan.FreeNeeded = plan.EstimatedBytes;
            plan.Backend = o.NeedsSevenZip ? "7-Zip" : "System.IO.Compression";
            try { if (!string.IsNullOrEmpty(o.OutFolder)) { var root = Path.GetPathRoot(Path.GetFullPath(o.OutFolder)); if (root != null && !root.StartsWith(@"\\")) plan.FreeOnTarget = new DriveInfo(root).AvailableFreeSpace; } } catch { }
            return plan;
        }

        // ------------------------------------------------------------ checks before anything is written

        /// <summary>Null when the options can be run, otherwise the reason, in a sentence for the person.</summary>
        public static string Validate(ArchiveScan scan, ArchiveOptions o, bool sevenZipAvailable)
        {
            if (scan.Entries.Count == 0) return "There is nothing to archive.";
            if (string.IsNullOrWhiteSpace(o.Name)) return "Give the archive a name.";
            if (o.Name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return "The name cannot contain \\ / : * ? \" < > or |.";
            if (string.IsNullOrWhiteSpace(o.OutFolder)) return "Choose a folder to save in.";
            if (o.NeedsSevenZip && !sevenZipAvailable) return "This needs 7-Zip, which this build does not carry. Zip and tar.gz work without it.";
            if (o.Format == ArchiveFormat.TarGz && (o.HasPassword || o.Split)) return "tar.gz cannot be encrypted or split.";
            if (o.EncryptNames && o.Format != ArchiveFormat.SevenZip) return "Encrypting file names needs the 7z format.";
            if (o.EncryptNames && !o.HasPassword) return "Encrypting file names needs a password.";
            if (o.Split && o.VolumeBytes < 64 * 1024) return "Volumes must be at least 64 KB.";
            var bad = o.Excludes.Select(r => (r, r.Validate())).FirstOrDefault(x => x.Item2 != null);
            if (bad.r != null) return $"The exclude pattern \"{bad.r.Pattern}\" is not valid: {bad.Item2}";
            if (scan.Select(o).Count(x => !x.Entry.IsEmptyDirectory) == 0 && scan.Select(o).Count == 0) return "Every item is excluded by the patterns.";
            return null;
        }

        // ------------------------------------------------------------ output names

        /// <summary>The first output name that does not collide with an existing archive or volume, so nothing is ever overwritten.</summary>
        public static string UniqueOutput(string folder, string name, string suffix)
        {
            string Candidate(int n) => Path.Combine(folder, n == 0 ? name + suffix : $"{name} ({n}){suffix}");
            for (int n = 0; n < 1000; n++)
            {
                var c = Candidate(n);
                if (!File.Exists(c) && !File.Exists(c + ".001")) return c;
            }
            return Candidate(DateTime.Now.Millisecond + 1000);
        }

        /// <summary>Every file an output name can have: the plain file and numbered volumes.</summary>
        public static List<string> OutputsFor(string primary)
        {
            var list = new List<string>();
            if (File.Exists(primary)) list.Add(primary);
            var dir = Path.GetDirectoryName(primary); var file = Path.GetFileName(primary);
            if (dir != null && Directory.Exists(dir))
                list.AddRange(Directory.EnumerateFiles(dir, file + ".0*").Where(p => Regex.IsMatch(p, @"\.\d{3}$")).OrderBy(p => p, StringComparer.OrdinalIgnoreCase));
            return list.Distinct().ToList();
        }

        public static void DeleteOutputs(string primary)
        {
            foreach (var p in OutputsFor(primary).Append(primary + ".part")) { try { if (File.Exists(p)) File.Delete(p); } catch { } }
        }

        // ------------------------------------------------------------ run

        public static async Task<ArchiveResult> RunAsync(ArchiveScan scan, ArchiveOptions o, IProgress<ArchiveProgress> progress, CancellationToken ct)
        {
            var result = new ArchiveResult { Backend = o.NeedsSevenZip ? "7-Zip" : "System.IO.Compression" };
            var chosen = scan.Select(o);
            result.Files = chosen.Count(c => !c.Entry.IsEmptyDirectory);
            result.SourceBytes = chosen.Sum(c => c.Entry.Size);
            Directory.CreateDirectory(o.OutFolder);
            var output = UniqueOutput(o.OutFolder, o.Name.Trim(), o.Suffix);
            var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                progress?.Report(new ArchiveProgress { Step = "Scan", Done = 0, Total = result.SourceBytes });
                if (o.NeedsSevenZip) await RunSevenZipAsync(scan, o, chosen, output, progress, ct);
                else if (o.Format == ArchiveFormat.Zip) await Task.Run(() => WriteZip(o, chosen, output, hashes, result.SourceBytes, progress, ct), ct);
                else await Task.Run(() => WriteTarGz(o, chosen, output, hashes, result.SourceBytes, progress, ct), ct);

                result.Outputs = OutputsFor(output);
                if (result.Outputs.Count == 0) throw new IOException("The archive was not written.");
                result.ArchiveBytes = result.Outputs.Sum(p => new FileInfo(p).Length);
                if (o.TestAfter)
                {
                    result.Tested = true;
                    progress?.Report(new ArchiveProgress { Step = "Test", Done = 0, Total = result.SourceBytes });
                    var (ok, detail) = o.NeedsSevenZip
                        ? await TestSevenZipAsync(result.Outputs[0], o, progress, ct, result.SourceBytes)
                        : await Task.Run(() => TestManaged(o, output, hashes, result.Files, result.SourceBytes, progress, ct), ct);
                    result.TestOk = ok; result.TestDetail = detail;
                    if (!ok) throw new InvalidDataException("The test failed: " + detail);
                }
                progress?.Report(new ArchiveProgress { Step = "Finish", Done = result.SourceBytes, Total = result.SourceBytes });
                result.Ok = true;
            }
            catch (OperationCanceledException) { result.Cancelled = true; DeleteOutputs(output); }
            catch (Exception ex) { result.Error = ex.Message; DeleteOutputs(output); }
            return result;
        }

        private static string Clamp(string name) => name.Replace('\\', '/');

        private sealed class Meter
        {
            private readonly Stopwatch _clock = Stopwatch.StartNew();
            private readonly IProgress<ArchiveProgress> _p; private readonly string _step; private readonly long _total;
            private long _done; private long _lastTick;
            public Meter(IProgress<ArchiveProgress> p, string step, long total) { _p = p; _step = step; _total = total; }
            public void Add(long n, string file)
            {
                _done += n;
                if (_clock.ElapsedMilliseconds - _lastTick < 120 && _done < _total) return;
                _lastTick = _clock.ElapsedMilliseconds;
                var s = _clock.Elapsed.TotalSeconds;
                double rate = s > 0.2 ? _done / s : 0;
                _p?.Report(new ArchiveProgress { Step = _step, Done = _done, Total = _total, BytesPerSecond = rate, Remaining = rate > 0 ? TimeSpan.FromSeconds(Math.Max(0, (_total - _done) / rate)) : null, CurrentFile = file });
            }
        }

        private static DateTimeOffset Stamp(DateTime t) => t.Year < 1980 ? new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero) : t.Year > 2107 ? new DateTimeOffset(2107, 12, 31, 0, 0, 0, TimeSpan.Zero) : new DateTimeOffset(t);

        private static CompressionLevel Level(int level) => level switch { 0 => CompressionLevel.NoCompression, 1 or 2 => CompressionLevel.Fastest, 3 => CompressionLevel.Optimal, _ => CompressionLevel.SmallestSize };

        // ---- zip through System.IO.Compression. Levels collapse to four here: Store, Fastest (Fastest and Fast), Optimal (Normal), Smallest (Maximum and Ultra).
        private static void WriteZip(ArchiveOptions o, List<(ScanEntry Entry, string Name)> chosen, string output, Dictionary<string, string> hashes, long total, IProgress<ArchiveProgress> progress, CancellationToken ct)
        {
            var meter = new Meter(progress, "Compress", total);
            var buffer = new byte[256 * 1024];
            var level = o.EffectiveMethod == "Store" ? CompressionLevel.NoCompression : Level(o.Level);
            using (var fs = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                foreach (var (e, name) in chosen)
                {
                    ct.ThrowIfCancellationRequested();
                    if (e.IsEmptyDirectory) { zip.CreateEntry(Clamp(name) + "/").LastWriteTime = Stamp(e.Modified); continue; }
                    var entry = zip.CreateEntry(Clamp(name), level);
                    entry.LastWriteTime = Stamp(e.Modified);
                    using var src = Open(e.Full);
                    using var dst = entry.Open();
                    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    int n;
                    while ((n = src.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        ct.ThrowIfCancellationRequested();
                        dst.Write(buffer, 0, n); hash.AppendData(buffer, 0, n);
                        meter.Add(n, name);
                    }
                    hashes[Clamp(name)] = Convert.ToHexString(hash.GetHashAndReset());
                }
            }
        }

        private static FileStream Open(string path) => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 16, FileOptions.SequentialScan);

        /// <summary>Reads through a file while counting what was read, hashing it and honouring cancel.</summary>
        private sealed class CountingStream : Stream
        {
            private readonly Stream _inner; private readonly Meter _m; private readonly string _name; private readonly CancellationToken _ct; private readonly IncrementalHash _hash;
            public CountingStream(Stream inner, Meter m, string name, IncrementalHash hash, CancellationToken ct) { _inner = inner; _m = m; _name = name; _hash = hash; _ct = ct; }
            public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
            public override long Length => _inner.Length;
            public override long Position { get => _inner.Position; set => throw new NotSupportedException(); }
            public override int Read(byte[] buffer, int offset, int count)
            {
                _ct.ThrowIfCancellationRequested();
                var n = _inner.Read(buffer, offset, count);
                if (n > 0) { _hash.AppendData(buffer, offset, n); _m.Add(n, _name); }
                return n;
            }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            protected override void Dispose(bool disposing) { if (disposing) _inner.Dispose(); base.Dispose(disposing); }
        }

        private static void WriteTarGz(ArchiveOptions o, List<(ScanEntry Entry, string Name)> chosen, string output, Dictionary<string, string> hashes, long total, IProgress<ArchiveProgress> progress, CancellationToken ct)
        {
            var meter = new Meter(progress, "Compress", total);
            using var fs = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var gz = new GZipStream(fs, Level(o.Level == 0 ? 1 : o.Level));   // gzip has no store mode; Store becomes the fastest level
            using var tar = new TarWriter(gz, TarEntryFormat.Pax, leaveOpen: false);
            foreach (var (e, name) in chosen)
            {
                ct.ThrowIfCancellationRequested();
                var n = Clamp(name);
                if (e.IsEmptyDirectory) { tar.WriteEntry(new PaxTarEntry(TarEntryType.Directory, n.TrimEnd('/') + "/") { ModificationTime = Stamp(e.Modified) }); continue; }
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                using var src = new CountingStream(Open(e.Full), meter, name, hash, ct);
                tar.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, n) { DataStream = src, ModificationTime = Stamp(e.Modified) });
                hashes[n] = Convert.ToHexString(hash.GetHashAndReset());
            }
        }

        // ---- the test step for archives this class wrote itself: read every entry back and compare its hash with the source it came from
        internal static (bool Ok, string Detail) TestManaged(ArchiveOptions o, string archive, Dictionary<string, string> hashes, int files, long total, IProgress<ArchiveProgress> progress, CancellationToken ct)
        {
            var meter = new Meter(progress, "Test", total);
            var seen = 0; var buffer = new byte[256 * 1024];
            string Check(string name, Stream s)
            {
                using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                int n; while ((n = s.Read(buffer, 0, buffer.Length)) > 0) { ct.ThrowIfCancellationRequested(); h.AppendData(buffer, 0, n); meter.Add(n, name); }
                if (!hashes.TryGetValue(name, out var want)) return $"{name} is in the archive but was not written from a source file";
                return Convert.ToHexString(h.GetHashAndReset()) == want ? null : $"{name} reads back different from the original";
            }
            try
            {
                if (o.Format == ArchiveFormat.Zip)
                {
                    using var zip = ZipFile.OpenRead(archive);
                    foreach (var e in zip.Entries)
                    {
                        if (e.FullName.EndsWith("/")) continue;
                        using var s = e.Open();
                        var bad = Check(e.FullName, s);
                        if (bad != null) return (false, bad);
                        seen++;
                    }
                }
                else
                {
                    using var fs = File.OpenRead(archive); using var gz = new GZipStream(fs, CompressionMode.Decompress); using var tar = new TarReader(gz);
                    TarEntry te;
                    while ((te = tar.GetNextEntry(copyData: false)) != null)
                    {
                        if (te.EntryType == TarEntryType.Directory) continue;
                        var bad = Check(te.Name, te.DataStream);
                        if (bad != null) return (false, bad);
                        seen++;
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { return (false, ex.Message); }
            return seen == files ? (true, $"{seen} file{(seen == 1 ? "" : "s")} read back and matched") : (false, $"{seen} of {files} files are in the archive");
        }

        // ------------------------------------------------------------ 7-Zip

        internal static string SevenZipPath;   // set by tests; otherwise the bundled copy
        public static string SevenZip => SevenZipPath ?? BundledTools.SevenZipExe() ?? (File.Exists(Environment.GetEnvironmentVariable("MT_7ZA") ?? "") ? Environment.GetEnvironmentVariable("MT_7ZA") : null);

        /// <summary>The 7za command line for these options, minus the password (which is added last and never logged).</summary>
        public static List<string> SevenZipArguments(ArchiveOptions o, string archive, string listFile)
        {
            var a = new List<string> { "a", "-t" + (o.Format == ArchiveFormat.Zip ? "zip" : "7z"), archive, "@" + listFile };
            var mx = new[] { 0, 1, 3, 5, 7, 9 }[Math.Clamp(o.Level, 0, 5)];
            a.Add("-mx=" + mx);
            var method = o.EffectiveMethod;
            if (o.Level > 0 && !(o.Format == ArchiveFormat.Zip && method == "Store"))
                a.Add(o.Format == ArchiveFormat.Zip ? "-mm=" + method : "-m0=" + method);
            else if (o.Format == ArchiveFormat.Zip && method == "Store") a.Add("-mm=Copy");
            if (o.Format == ArchiveFormat.SevenZip && (method == "LZMA2" || method == "LZMA"))
            {
                if (o.Dictionary != "auto") a.Add("-md=" + o.Dictionary);
                if (o.Solid == "off") a.Add("-ms=off"); else if (o.Solid == "on") a.Add("-ms=on"); else if (o.Solid != "auto") a.Add("-ms=" + o.Solid);
            }
            if (o.Threads > 0) a.Add("-mmt=" + Math.Clamp(o.Threads, 1, 64));
            if (o.Format == ArchiveFormat.SevenZip && o.EncryptNames && o.HasPassword) a.Add("-mhe=on");
            if (o.Format == ArchiveFormat.Zip && o.HasPassword) a.Add("-mem=AES256");
            if (o.Split && o.VolumeBytes > 0) a.Add("-v" + o.VolumeBytes + "b");
            a.AddRange(new[] { "-bsp1", "-bso1", "-bse1", "-y", "-spd", "-scsUTF-8", "-sccUTF-8" });
            return a;
        }

        private static string Quote(string s) => s.Contains(' ') || s.Contains('"') ? "\"" + s.Replace("\"", "\\\"") + "\"" : s;

        private static async Task RunSevenZipAsync(ArchiveScan scan, ArchiveOptions o, List<(ScanEntry Entry, string Name)> chosen, string output, IProgress<ArchiveProgress> progress, CancellationToken ct)
        {
            var exe = SevenZip ?? throw new FileNotFoundException("7-Zip is not available in this build.");
            // Names inside the archive are relative to the working folder: the root, or the folder above it when the parent folder is included.
            bool withParent = o.IncludeParent && scan.RootName.Length > 0 && Path.GetDirectoryName(scan.Root.TrimEnd('\\')) != null;
            var cwd = withParent ? Path.GetDirectoryName(scan.Root.TrimEnd('\\')) : scan.Root;
            var list = Path.Combine(Path.GetTempPath(), "mt-archive-" + Guid.NewGuid().ToString("N") + ".lst");
            try
            {
                File.WriteAllLines(list, chosen.Where(c => !(c.Entry.IsEmptyDirectory && false)).Select(c => withParent ? c.Name.Replace('/', '\\') : c.Entry.Relative.Replace('/', '\\')), new UTF8Encoding(true));
                var args = SevenZipArguments(o, output, list);
                if (o.HasPassword) args.Add("-p" + o.Password);
                long total = Math.Max(1, chosen.Sum(c => c.Entry.Size));
                var meter = new Meter(progress, "Compress", total);
                var (code, text) = await RunProcess(exe, args, cwd, ct, (pct, file) => meter.Add(0, file), pct => { }, total, progress, "Compress");
                if (code != 0) throw new IOException(code == 1 ? "7-Zip finished with warnings, so the archive may be incomplete: " + Tail(text, 2) : "7-Zip stopped: " + Tail(text, 3));
            }
            finally { try { File.Delete(list); } catch { } }
        }

        private static string Tail(string text, int lines)
        {
            var parts = text.Split('\n').Select(l => l.Trim('\r', ' ')).Where(l => l.Length > 0 && !l.Contains('%')).ToList();
            var t = string.Join(" | ", parts.Skip(Math.Max(0, parts.Count - lines)));
            return t.Replace("\r", "");
        }

        private static readonly Regex Pct = new Regex(@"(\d{1,3})%\s*(?:\d+\s*)?(?:[+UR.=-]\s*)?(.*)$", RegexOptions.Compiled);

        private static async Task<(int Code, string Text)> RunProcess(string exe, List<string> args, string cwd, CancellationToken ct, Action<int, string> onProgress, Action<int> unused, long total, IProgress<ArchiveProgress> progress, string step)
        {
            var psi = new ProcessStartInfo(exe) { WorkingDirectory = cwd, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi) ?? throw new IOException("7-Zip did not start.");
            var all = new StringBuilder();
            var clock = Stopwatch.StartNew();
            using var reg = ct.Register(() => { try { p.Kill(true); } catch { } });
            var errTask = p.StandardError.ReadToEndAsync();
            var outTask = Task.Run(() =>
            {
                var line = new StringBuilder(); int ch; var r = p.StandardOutput;
                while ((ch = r.Read()) >= 0)
                {
                    if (ch == '\r' || ch == '\n')
                    {
                        if (line.Length > 0)
                        {
                            var s = line.ToString(); all.AppendLine(s); line.Clear();
                            var m = Pct.Match(s);
                            if (m.Success && int.TryParse(m.Groups[1].Value, out var pc))
                            {
                                long done = total * Math.Clamp(pc, 0, 100) / 100;
                                double rate = clock.Elapsed.TotalSeconds > 0.3 ? done / clock.Elapsed.TotalSeconds : 0;
                                progress?.Report(new ArchiveProgress { Step = step, Done = done, Total = total, BytesPerSecond = rate, Remaining = rate > 0 ? TimeSpan.FromSeconds(Math.Max(0, (total - done) / rate)) : null, CurrentFile = m.Groups[2].Value.Trim() });
                            }
                        }
                    }
                    else line.Append((char)ch);
                }
            });
            await p.WaitForExitAsync(CancellationToken.None);
            await outTask;
            all.Append(await errTask);
            ct.ThrowIfCancellationRequested();
            return (p.ExitCode, all.ToString());
        }

        internal static async Task<(bool Ok, string Detail)> TestSevenZipAsync(string firstVolume, ArchiveOptions o, IProgress<ArchiveProgress> progress, CancellationToken ct, long total)
        {
            var exe = SevenZip;
            var args = new List<string> { "t", firstVolume, "-bsp1", "-bso1", "-bse1", "-y" };
            if (o.HasPassword) args.Add("-p" + o.Password);
            var (code, text) = await RunProcess(exe, args, Path.GetDirectoryName(firstVolume), ct, null, null, Math.Max(1, total), progress, "Test");
            if (code == 0 && text.Contains("Everything is Ok")) return (true, "7-Zip tested every file: everything is Ok");
            return (false, Tail(text, 3));
        }
    }
}
