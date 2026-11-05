using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using MonitoringTools.Core.Archives;

namespace MonitoringTools.Core.DocTools
{
    // The archive formats read through the bundled 7-Zip command line (7za): 7z, xz, bz2, tar.xz, tar.bz2 and AES-encrypted zip.
    // 7za is always given a password switch and a closed input, so it never waits for a typed answer. Extraction of a 7z or an
    // AES zip goes into a hidden folder inside the destination first; only entries that pass the zip-slip check are asked for,
    // and each finished file is then moved into place under the rule for names that exist. Cancel removes that folder, so a
    // cancel while 7-Zip is unpacking leaves nothing in the destination.
    public static partial class ArchiveTools
    {
        /// <summary>A placeholder password for archives that may not need one: it keeps 7-Zip from stopping to ask.</summary>
        private const string NoPassword = "mt-no-password";

        public static bool SevenZipAvailable => ArchiveEngine.SevenZip != null;

        private static bool NeedSeven(ArchiveInfo info)
        {
            info.UseSevenZip = true;
            if (SevenZipAvailable) return true;
            info.Unsupported = $"A {info.Format.ToUpperInvariant()} archive is read through 7-Zip, and 7-Zip is missing from this copy of the app.";
            return false;
        }

        private static ProcessStartInfo SevenStart(IEnumerable<string> args, string cwd = null)
        {
            var exe = ArchiveEngine.SevenZip ?? throw new IOException("7-Zip is missing from this copy of the app.");
            var psi = new ProcessStartInfo(exe)
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, WorkingDirectory = cwd ?? Path.GetTempPath()
            };
            foreach (var a in args) psi.ArgumentList.Add(a);
            return psi;
        }

        /// <summary>Runs 7za to the end on the calling thread. Each output line goes to <paramref name="line"/>; Cancel kills it.</summary>
        private static (int Code, string Text) SevenRun(IEnumerable<string> args, CancellationToken ct, Action<string> line = null)
        {
            using var p = Process.Start(SevenStart(args)) ?? throw new IOException("7-Zip did not start.");
            p.StandardInput.Close();
            var all = new StringBuilder();
            using var reg = ct.Register(() => { try { p.Kill(true); } catch { } });
            var err = p.StandardError.ReadToEndAsync();
            var buf = new StringBuilder(); int ch;
            while ((ch = p.StandardOutput.Read()) >= 0)
            {
                if (ch == '\r' || ch == '\n' || ch == '\b')
                {
                    if (buf.Length > 0) { var s = buf.ToString(); buf.Clear(); all.AppendLine(s); line?.Invoke(s); }
                }
                else buf.Append((char)ch);
            }
            if (buf.Length > 0) { all.AppendLine(buf.ToString()); line?.Invoke(buf.ToString()); }
            p.WaitForExit();
            all.Append(err.Result);
            ct.ThrowIfCancellationRequested();
            return (p.ExitCode, all.ToString());
        }

        private static bool SaysWrongPassword(string text) => text.Contains("Wrong password", StringComparison.OrdinalIgnoreCase) || text.Contains("Cannot open encrypted archive", StringComparison.OrdinalIgnoreCase);

        private static string SevenTail(string text)
        {
            var lines = text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && (l.StartsWith("ERROR") || l.Contains("Error", StringComparison.OrdinalIgnoreCase) || l.StartsWith("Can"))).ToList();
            var t = string.Join(" ", lines.Take(2));
            t = Regex.Replace(t, @"[A-Za-z]:\\[^:]*?\s:\s", "");    // 7-Zip repeats the full archive path before the reason
            return t.Length == 0 ? "7-Zip could not read it." : t;
        }

        // ------------------------------------------------------------------ listing

        /// <summary>Lists a 7z or another multi-file archive with "7za l -slt". A 7z with encrypted names is marked NamesLocked unless a password is given.</summary>
        private static void ListSeven(ArchiveInfo info, string password)
        {
            var (code, text) = SevenRun(new[] { "l", "-slt", "-ba", "-sccUTF-8", "-p" + (string.IsNullOrEmpty(password) ? NoPassword : password), "--", info.File }, CancellationToken.None);
            if (code != 0)
            {
                if (SaysWrongPassword(text))
                {
                    if (password != null) throw new WrongPasswordException();
                    info.NamesLocked = true; return;
                }
                throw new InvalidDataException(SevenTail(text));
            }
            info.Entries.Clear();
            foreach (var block in Blocks(text))
            {
                if (!block.TryGetValue("Path", out var raw) || raw.Length == 0) continue;
                bool dir = (block.TryGetValue("Folder", out var f) && f == "+") || (block.TryGetValue("Attributes", out var at) && at.StartsWith("D"));
                long.TryParse(block.GetValueOrDefault("Size"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var size);
                info.Entries.Add(new ArchiveEntry
                {
                    Raw = raw, Path = Norm(raw), IsDirectory = dir, Size = dir ? 0 : size, Modified = When(block.GetValueOrDefault("Modified")),
                    Encrypted = !dir && block.GetValueOrDefault("Encrypted") == "+"
                });
            }
            info.NamesLocked = false;
        }

        /// <summary>A single compressed file (xz or bz2): the entry is the file name without the last extension. bzip2 does not store the size.</summary>
        private static void ListSingleSeven(ArchiveInfo info, int extLength)
        {
            var name = System.IO.Path.GetFileName(info.File);
            name = name.Length > extLength ? name[..^extLength] : "content";
            var (code, text) = SevenRun(new[] { "l", "-slt", "-ba", "-sccUTF-8", "-p" + NoPassword, "--", info.File }, CancellationToken.None);
            if (code != 0) throw new InvalidDataException(SevenTail(text));
            var b = Blocks(text).FirstOrDefault();
            long size = 0;
            if (b != null) long.TryParse(b.GetValueOrDefault("Size"), NumberStyles.Integer, CultureInfo.InvariantCulture, out size);
            info.Entries.Add(new ArchiveEntry { Path = name, Size = size, Modified = File.GetLastWriteTime(info.File) });
        }

        private static List<Dictionary<string, string>> Blocks(string text)
        {
            var list = new List<Dictionary<string, string>>();
            Dictionary<string, string> cur = null;
            foreach (var l in text.Split('\n').Select(x => x.TrimEnd('\r')))
            {
                if (l.Length == 0) { cur = null; continue; }
                int eq = l.IndexOf(" = ", StringComparison.Ordinal);
                string key, val;
                if (eq > 0) { key = l[..eq]; val = l[(eq + 3)..]; }
                else if (l.EndsWith(" =")) { key = l[..^2]; val = ""; }
                else continue;
                if (cur == null || cur.ContainsKey(key)) { cur = new Dictionary<string, string>(StringComparer.Ordinal); list.Add(cur); }
                cur[key] = val;
            }
            return list;
        }

        private static DateTime? When(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (s.Length > 19) s = s[..19];
            return DateTime.TryParseExact(s, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var d) ? d : null;
        }

        // ------------------------------------------------------------------ passwords

        /// <summary>
        /// Checks a password through 7-Zip without writing anything: for encrypted names it lists the archive with the password
        /// (and fills in the entries when it works), otherwise it tests the first locked file. Null means it works, "wrong" means it does not.
        /// </summary>
        private static string CheckSevenPassword(ArchiveInfo info, string password)
        {
            if (string.IsNullOrEmpty(password)) return info.NeedsPassword ? "wrong" : null;
            try
            {
                if (info.NamesLocked)
                {
                    ListSeven(info, password);
                    foreach (var e in info.Entries) e.Unsafe = !TrySafePath(System.IO.Path.GetTempPath(), e.Path, out _);
                    return null;
                }
                // a name 7-Zip would rewrite (an unsafe one) might match nothing, and testing nothing proves nothing
                var first = info.Entries.FirstOrDefault(x => x.Encrypted && !x.IsDirectory && !x.Unsafe);
                if (first == null) return info.Entries.Any(x => x.Encrypted && !x.IsDirectory) ? "Every locked file in this archive has an unsafe path, so nothing would be extracted." : null;
                var (code, text) = SevenRun(new[] { "t", "-spd", "-sccUTF-8", "-p" + password, "--", info.File, SevenName(first) }, CancellationToken.None);
                if (code == 0 && !text.Contains("No files to process", StringComparison.OrdinalIgnoreCase)) return null;
                if (code == 0) return "7-Zip could not find the first locked file to check the password.";
                return SaysWrongPassword(text) ? "wrong" : SevenTail(text);
            }
            catch (WrongPasswordException) { return "wrong"; }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is Win32Exception) { return ex.Message; }
        }

        /// <summary>The name 7-Zip uses for an entry on Windows (backslashes).</summary>
        private static string SevenName(ArchiveEntry e) => (e.Raw ?? e.Path).Replace('/', '\\').TrimEnd('\\');

        // ------------------------------------------------------------------ streaming (xz, bz2, tar.xz, tar.bz2)

        /// <summary>The decompressed content of a single-stream archive (xz or bz2) as a stream read from "7za x -so".</summary>
        internal static Stream SevenZipStream(string file, CancellationToken ct)
        {
            var p = Process.Start(SevenStart(new[] { "x", "-so", "-sccUTF-8", "-p" + NoPassword, "--", file })) ?? throw new IOException("7-Zip did not start.");
            p.StandardInput.Close();
            p.ErrorDataReceived += (s, e) => { };
            p.BeginErrorReadLine();
            return new ProcessStream(p, ct);
        }

        private sealed class ProcessStream : Stream
        {
            private readonly Process _p; private readonly Stream _s; private readonly CancellationTokenRegistration _reg;
            public ProcessStream(Process p, CancellationToken ct) { _p = p; _s = p.StandardOutput.BaseStream; _reg = ct.Register(Kill); }
            private void Kill() { try { if (!_p.HasExited) _p.Kill(true); } catch { } }
            public override int Read(byte[] buffer, int offset, int count) => _s.Read(buffer, offset, count);
            public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            protected override void Dispose(bool disposing)
            {
                if (disposing) { _reg.Dispose(); Kill(); try { _p.WaitForExit(5000); } catch { } _p.Dispose(); }
                base.Dispose(disposing);
            }
        }

        // ------------------------------------------------------------------ 7z and AES zip extraction

        private static readonly Regex SevenPct = new Regex(@"^\s*(\d{1,3})%(?:\s+\d+)?(?:\s+-\s+(.*))?$", RegexOptions.Compiled);

        private static void ExtractSevenStaged(ArchiveInfo info, List<ArchiveEntry> picked, string dest, Conflicts rule, string password, IProgress<ExtractProgress> progress, ExtractProgress prog, ExtractResult res, CancellationToken ct)
        {
            var safe = new List<ArchiveEntry>();
            foreach (var pe in picked)
            {
                if (TrySafePath(dest, pe.Path, out _)) safe.Add(pe);
                else { res.Refused.Add(pe.Path); prog.Done++; }
            }
            if (safe.Count == 0) return;
            if (info.NeedsPassword && string.IsNullOrEmpty(password)) throw new WrongPasswordException();
            var staging = System.IO.Path.Combine(dest, ".extracting-" + Guid.NewGuid().ToString("N")[..10]);
            var list = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mt-extract-" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                Directory.CreateDirectory(staging);
                try { File.SetAttributes(staging, FileAttributes.Directory | FileAttributes.Hidden); } catch { }
                File.WriteAllLines(list, safe.Select(SevenName), new UTF8Encoding(false));
                var args = new List<string> { "x", "-o" + staging, "-y", "-spd", "-scsUTF-8", "-sccUTF-8", "-bsp1", "-bso1", "-bse1", "-i@" + list, "-p" + (string.IsNullOrEmpty(password) ? NoPassword : password), "--", info.File };
                long total = Math.Max(1, safe.Sum(e => e.Size));
                var (code, text) = SevenRun(args, ct, line =>
                {
                    var m = SevenPct.Match(line);
                    if (!m.Success || !int.TryParse(m.Groups[1].Value, out var pc)) return;
                    prog.Bytes = total * Math.Clamp(pc, 0, 100) / 100;
                    var cur = m.Groups[2].Success ? m.Groups[2].Value.Trim().Replace('\\', '/') : "";
                    progress?.Report(Snapshot(prog, cur.Length > 0 ? cur : "unpacking"));
                });
                if (code != 0)
                {
                    if (SaysWrongPassword(text)) throw new WrongPasswordException();
                    throw new IOException("7-Zip stopped: " + SevenTail(text));
                }
                prog.Bytes = 0;
                var missing = new List<string>();
                foreach (var pe in safe)
                {
                    ct.ThrowIfCancellationRequested();
                    prog.Done++;
                    TrySafePath(dest, pe.Path, out var full);
                    if (!TrySafePath(staging, pe.Path, out var staged) || !File.Exists(staged)) { missing.Add(pe.Path); continue; }
                    var target = Target(full, rule, res, pe.Path);
                    long size = new FileInfo(staged).Length;
                    if (target != null)
                    {
                        if (File.Exists(target)) File.Delete(target);
                        File.Move(staged, target);
                        if (pe.Modified.HasValue) try { File.SetLastWriteTime(target, pe.Modified.Value); } catch { }
                        res.Extracted++; res.Bytes += size;
                    }
                    prog.Bytes += size;
                    progress?.Report(Snapshot(prog, pe.Path));
                }
                if (missing.Count > 0) res.Error = (missing.Count == 1 ? "7-Zip did not write 1 file: " : $"7-Zip did not write {missing.Count} files: ") + string.Join(", ", missing.Take(3)) + ".";
            }
            finally
            {
                // a 7-Zip that was just stopped may hold its last file for a moment
                for (int i = 0; i < 20 && Directory.Exists(staging); i++)
                {
                    try { Directory.Delete(staging, true); } catch { Thread.Sleep(100); }
                }
                try { File.Delete(list); } catch { }
            }
        }
    }
}
