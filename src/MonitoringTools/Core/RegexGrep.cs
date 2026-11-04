using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;

namespace MonitoringTools.Core
{
    /// <summary>
    /// Searches and replaces across files for the regex builder. Searching never writes. Replacing is a separate
    /// step that needs a preview first, copies every file to a timestamped backup folder before changing it,
    /// and can restore the last run.
    /// </summary>
    public static class RegexGrep
    {
        public const int MaxMatchesPerFile = 2000;
        public const int MaxMatchesTotal = 50000;

        public sealed class Options
        {
            public string Folder = "";
            /// <summary>Semicolon- or comma-separated file name masks such as *.txt;*.md. Empty means every file.</summary>
            public string Masks = "*";
            /// <summary>Masks for file or folder names to leave out.</summary>
            public string ExcludeMasks = "";
            public bool Subfolders = true;
            public bool SkipBinary = true;
            public long MaxBytes = 5L * 1024 * 1024;
        }

        public sealed class Hit
        {
            public int Line;
            public int Column;
            public int Length;
            public string LineText;
            public string Value;
        }

        public sealed class FileResult
        {
            public string Path;
            public string Encoding;
            public List<Hit> Hits = new List<Hit>();
            /// <summary>Total matches, which can exceed Hits.Count when the per-file cap was reached.</summary>
            public int Count;
            public long Size;
            public DateTime Modified;
        }

        public sealed class Skip
        {
            public string Path;
            public string Reason;
        }

        public sealed class Report
        {
            public List<FileResult> Files = new List<FileResult>();
            public List<Skip> Skipped = new List<Skip>();
            public int FilesScanned;
            public int TotalMatches;
            public bool Capped;
            public bool Cancelled;
            public int FilesWithHits => Files.Count;
        }

        // ---------------------------------------------------------------- masks

        public static List<Regex> CompileMasks(string masks)
        {
            var list = new List<Regex>();
            foreach (var m in (masks ?? "").Split(new[] { ';', ',', '|' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var t = m.Trim();
                if (t.Length == 0) continue;
                var pat = "^" + Regex.Escape(t).Replace("\\*", ".*").Replace("\\?", ".") + "$";
                list.Add(new Regex(pat, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
            }
            return list;
        }

        private static bool Any(List<Regex> masks, string name) { foreach (var r in masks) if (r.IsMatch(name)) return true; return false; }

        // ---------------------------------------------------------------- reading

        /// <summary>A file's text with the encoding it was read as, so a replace can write it back the same way.</summary>
        public sealed class TextFile
        {
            public string Text;
            public Encoding Encoding;
            public bool HasBom;
            public string EncodingName;
        }

        /// <summary>Whether the first bytes look like a binary file: a null byte, unless a UTF-16 or UTF-32 byte order mark explains it.</summary>
        public static bool LooksBinary(byte[] bytes)
        {
            if (HasWideBom(bytes)) return false;
            int n = Math.Min(bytes.Length, 8000);
            for (int i = 0; i < n; i++) if (bytes[i] == 0) return true;
            return false;
        }

        private static bool HasWideBom(byte[] b) =>
            b.Length >= 2 && ((b[0] == 0xFF && b[1] == 0xFE) || (b[0] == 0xFE && b[1] == 0xFF));

        /// <summary>Decodes with a byte order mark when there is one, otherwise strict UTF-8, otherwise Latin-1 so nothing is lost.</summary>
        public static TextFile Decode(byte[] b)
        {
            if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF)
                return new TextFile { Text = new UTF8Encoding(true).GetString(b, 3, b.Length - 3), Encoding = new UTF8Encoding(true), HasBom = true, EncodingName = "UTF-8 with BOM" };
            if (b.Length >= 4 && b[0] == 0xFF && b[1] == 0xFE && b[2] == 0 && b[3] == 0)
                return new TextFile { Text = new UTF32Encoding(false, true).GetString(b, 4, b.Length - 4), Encoding = new UTF32Encoding(false, true), HasBom = true, EncodingName = "UTF-32 LE" };
            if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE)
                return new TextFile { Text = new UnicodeEncoding(false, true).GetString(b, 2, b.Length - 2), Encoding = new UnicodeEncoding(false, true), HasBom = true, EncodingName = "UTF-16 LE" };
            if (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF)
                return new TextFile { Text = new UnicodeEncoding(true, true).GetString(b, 2, b.Length - 2), Encoding = new UnicodeEncoding(true, true), HasBom = true, EncodingName = "UTF-16 BE" };
            try
            {
                var strict = new UTF8Encoding(false, true);
                return new TextFile { Text = strict.GetString(b), Encoding = new UTF8Encoding(false), HasBom = false, EncodingName = "UTF-8" };
            }
            catch (DecoderFallbackException)
            {
                return new TextFile { Text = Encoding.Latin1.GetString(b), Encoding = Encoding.Latin1, HasBom = false, EncodingName = "Latin-1 (not valid UTF-8)" };
            }
        }

        public static byte[] Encode(TextFile f, string text)
        {
            var body = f.Encoding.GetBytes(text);
            if (!f.HasBom) return body;
            var pre = f.Encoding.GetPreamble();
            var all = new byte[pre.Length + body.Length];
            Buffer.BlockCopy(pre, 0, all, 0, pre.Length);
            Buffer.BlockCopy(body, 0, all, pre.Length, body.Length);
            return all;
        }

        // ---------------------------------------------------------------- enumeration

        private static IEnumerable<string> Enumerate(Options o, List<Regex> include, List<Regex> exclude, List<Skip> skipped, CancellationToken ct)
        {
            var stack = new Stack<string>();
            stack.Push(o.Folder);
            while (stack.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                var dir = stack.Pop();
                string[] files = Array.Empty<string>(), dirs = Array.Empty<string>();
                try { files = Directory.GetFiles(dir); }
                catch (Exception ex) { skipped.Add(new Skip { Path = dir, Reason = "Folder could not be read: " + ex.Message }); }
                foreach (var f in files.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                {
                    var name = System.IO.Path.GetFileName(f);
                    if (exclude.Count > 0 && Any(exclude, name)) continue;
                    if (include.Count > 0 && !Any(include, name)) continue;
                    yield return f;
                }
                if (!o.Subfolders) continue;
                try { dirs = Directory.GetDirectories(dir); } catch { }
                foreach (var d in dirs.OrderByDescending(x => x, StringComparer.OrdinalIgnoreCase))
                {
                    var name = System.IO.Path.GetFileName(d);
                    if (exclude.Count > 0 && Any(exclude, name)) continue;
                    try { if ((File.GetAttributes(d) & FileAttributes.ReparsePoint) != 0) continue; } catch { continue; } // never follow links out of the folder
                    stack.Push(d);
                }
            }
        }

        // ---------------------------------------------------------------- search

        /// <summary>
        /// Searches every selected file. Matches are found on the whole file text (so a pattern may span lines) and
        /// reported with the line and column where each starts. Honours cancellation between files and between matches.
        /// </summary>
        public static Report Search(Regex rx, Options o, CancellationToken ct, Action<string> progress = null)
        {
            var report = new Report();
            if (string.IsNullOrWhiteSpace(o.Folder) || !Directory.Exists(o.Folder))
            {
                report.Skipped.Add(new Skip { Path = o.Folder ?? "", Reason = "Choose a folder that exists." });
                return report;
            }
            var include = CompileMasks(o.Masks);
            var exclude = CompileMasks(o.ExcludeMasks);
            try
            {
                foreach (var path in Enumerate(o, include, exclude, report.Skipped, ct))
                {
                    ct.ThrowIfCancellationRequested();
                    report.FilesScanned++;
                    if (report.FilesScanned % 20 == 0) progress?.Invoke($"Scanned {report.FilesScanned} file(s), {report.FilesWithHits} with matches...");
                    var fr = ScanFile(rx, path, o, report.Skipped, ct, out var capped);
                    if (fr == null) continue;
                    if (fr.Count == 0) continue;
                    report.Files.Add(fr);
                    report.TotalMatches += fr.Count;
                    if (capped) report.Capped = true;
                    if (report.TotalMatches >= MaxMatchesTotal) { report.Capped = true; break; }
                }
            }
            catch (OperationCanceledException) { report.Cancelled = true; }
            progress?.Invoke($"Scanned {report.FilesScanned} file(s), {report.FilesWithHits} with matches.");
            return report;
        }

        private static FileResult ScanFile(Regex rx, string path, Options o, List<Skip> skipped, CancellationToken ct, out bool capped)
        {
            capped = false;
            try
            {
                var info = new FileInfo(path);
                if (info.Length > o.MaxBytes) { skipped.Add(new Skip { Path = path, Reason = $"Larger than the {o.MaxBytes / (1024.0 * 1024):0.#} MB limit." }); return null; }
                var bytes = File.ReadAllBytes(path);
                if (o.SkipBinary && LooksBinary(bytes)) { skipped.Add(new Skip { Path = path, Reason = "Looks like a binary file." }); return null; }
                var tf = Decode(bytes);
                var fr = new FileResult { Path = path, Encoding = tf.EncodingName, Size = info.Length, Modified = info.LastWriteTimeUtc };
                var starts = LineStarts(tf.Text);
                foreach (Match m in rx.Matches(tf.Text))
                {
                    ct.ThrowIfCancellationRequested();
                    fr.Count++;
                    if (fr.Hits.Count >= MaxMatchesPerFile) { capped = true; continue; }
                    int line = LineOf(starts, m.Index);
                    int ls = starts[line], le = line + 1 < starts.Count ? starts[line + 1] : tf.Text.Length;
                    var lineText = tf.Text.Substring(ls, le - ls).TrimEnd('\r', '\n');
                    if (lineText.Length > 300) lineText = lineText[..300] + "...";
                    fr.Hits.Add(new Hit { Line = line + 1, Column = m.Index - ls + 1, Length = m.Length, LineText = lineText, Value = m.Value.Length > 200 ? m.Value[..200] + "..." : m.Value });
                }
                return fr;
            }
            catch (OperationCanceledException) { throw; }
            catch (RegexMatchTimeoutException) { skipped.Add(new Skip { Path = path, Reason = "Stopped at the 200 ms limit: the pattern backtracks too much on this file." }); return null; }
            catch (Exception ex) { skipped.Add(new Skip { Path = path, Reason = ex.Message }); return null; }
        }

        private static List<int> LineStarts(string text)
        {
            var l = new List<int> { 0 };
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n') l.Add(i + 1);
                else if (text[i] == '\r' && (i + 1 >= text.Length || text[i + 1] != '\n')) l.Add(i + 1);
            }
            return l;
        }

        private static int LineOf(List<int> starts, int index)
        {
            int lo = 0, hi = starts.Count - 1;
            while (lo < hi) { int mid = (lo + hi + 1) / 2; if (starts[mid] <= index) lo = mid; else hi = mid - 1; }
            return lo;
        }

        // ---------------------------------------------------------------- CSV

        public static string ToCsv(Report r)
        {
            var sb = new StringBuilder("file,line,column,length,match,line_text\r\n");
            foreach (var f in r.Files)
                foreach (var h in f.Hits)
                    sb.Append(Csv(f.Path)).Append(',').Append(h.Line).Append(',').Append(h.Column).Append(',').Append(h.Length).Append(',').Append(Csv(h.Value)).Append(',').Append(Csv(h.LineText)).Append("\r\n");
            return sb.ToString();
        }

        private static string Csv(string s)
        {
            s ??= "";
            // A leading formula character would run as a formula when the file is opened in a spreadsheet.
            if (s.Length > 0 && "=+-@".IndexOf(s[0]) >= 0) s = "'" + s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }

        // ---------------------------------------------------------------- replace

        public sealed class ReplacePlanItem
        {
            public string Path;
            public int Count;
            public long Size;
            public DateTime Modified;
        }

        /// <summary>The preview: which files would change and how many replacements each would get. Writes nothing.</summary>
        public static List<ReplacePlanItem> Plan(Report r) =>
            r.Files.Select(f => new ReplacePlanItem { Path = f.Path, Count = f.Count, Size = f.Size, Modified = f.Modified }).ToList();

        public sealed class ReplaceOutcome
        {
            public string BackupFolder;
            public int FilesChanged;
            public int Replacements;
            public List<Skip> Failed = new List<Skip>();
        }

        public static string BackupRoot => System.IO.Path.Combine(Settings.DataDir, "regex-backups");

        private sealed class ManifestEntry { public string Original { get; set; } public string Backup { get; set; } }

        /// <summary>
        /// Writes the replacements. Each file is first copied to a new timestamped backup folder; a file that changed
        /// since the preview, is read-only, or cannot be backed up is skipped and reported, never half-written.
        /// </summary>
        public static ReplaceOutcome Apply(Regex rx, string replacement, List<ReplacePlanItem> plan, CancellationToken ct, Action<string> progress = null)
        {
            var outcome = new ReplaceOutcome();
            var folder = System.IO.Path.Combine(BackupRoot, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            int suffix = 1;
            while (Directory.Exists(folder)) folder = System.IO.Path.Combine(BackupRoot, DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + suffix++);
            Directory.CreateDirectory(folder);
            outcome.BackupFolder = folder;
            var manifest = new List<ManifestEntry>();
            int i = 0;
            foreach (var item in plan)
            {
                if (ct.IsCancellationRequested) { outcome.Failed.Add(new Skip { Path = item.Path, Reason = "Not changed: the run was cancelled." }); continue; }
                progress?.Invoke($"Replacing in file {++i} of {plan.Count}...");
                try
                {
                    var info = new FileInfo(item.Path);
                    if (!info.Exists) { outcome.Failed.Add(new Skip { Path = item.Path, Reason = "The file no longer exists." }); continue; }
                    if (info.Length != item.Size || info.LastWriteTimeUtc != item.Modified) { outcome.Failed.Add(new Skip { Path = item.Path, Reason = "Changed since the preview. Search again." }); continue; }
                    if (info.IsReadOnly) { outcome.Failed.Add(new Skip { Path = item.Path, Reason = "The file is read-only." }); continue; }
                    var bytes = File.ReadAllBytes(item.Path);
                    var tf = Decode(bytes);
                    int count = 0;
                    var result = rx.Replace(tf.Text, m => { count++; return m.Result(replacement ?? ""); });
                    if (count == 0 || result == tf.Text) continue;
                    var backupName = (manifest.Count + 1).ToString("D5") + "-" + System.IO.Path.GetFileName(item.Path);
                    var backupPath = System.IO.Path.Combine(folder, backupName);
                    File.WriteAllBytes(backupPath, bytes);
                    if (!File.ReadAllBytes(backupPath).AsSpan().SequenceEqual(bytes)) { outcome.Failed.Add(new Skip { Path = item.Path, Reason = "The backup could not be verified, so the file was not changed." }); continue; }
                    manifest.Add(new ManifestEntry { Original = item.Path, Backup = backupName });
                    File.WriteAllText(System.IO.Path.Combine(folder, "manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
                    var temp = item.Path + ".regex-tmp";
                    File.WriteAllBytes(temp, Encode(tf, result));
                    File.Move(temp, item.Path, true);
                    outcome.FilesChanged++;
                    outcome.Replacements += count;
                }
                catch (RegexMatchTimeoutException) { outcome.Failed.Add(new Skip { Path = item.Path, Reason = "Stopped at the 200 ms limit." }); }
                catch (Exception ex) { outcome.Failed.Add(new Skip { Path = item.Path, Reason = ex.Message }); }
            }
            if (manifest.Count == 0) { try { Directory.Delete(folder, true); outcome.BackupFolder = null; } catch { } }
            return outcome;
        }

        /// <summary>The newest backup folder that has a manifest, or null.</summary>
        public static string LastBackup()
        {
            try
            {
                if (!Directory.Exists(BackupRoot)) return null;
                return Directory.GetDirectories(BackupRoot).OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault(d => File.Exists(System.IO.Path.Combine(d, "manifest.json")));
            }
            catch { return null; }
        }

        /// <summary>Copies every file in the newest backup back over its original. Returns a plain report.</summary>
        public static (int Restored, List<Skip> Failed, string Folder) RestoreLast()
        {
            var failed = new List<Skip>();
            var folder = LastBackup();
            if (folder == null) return (0, failed, null);
            int restored = 0;
            List<ManifestEntry> manifest;
            try { manifest = JsonSerializer.Deserialize<List<ManifestEntry>>(File.ReadAllText(System.IO.Path.Combine(folder, "manifest.json"))) ?? new List<ManifestEntry>(); }
            catch (Exception ex) { failed.Add(new Skip { Path = folder, Reason = "The backup list could not be read: " + ex.Message }); return (0, failed, folder); }
            foreach (var e in manifest)
            {
                try
                {
                    var src = System.IO.Path.Combine(folder, e.Backup);
                    if (!File.Exists(src)) { failed.Add(new Skip { Path = e.Original, Reason = "The backup copy is missing." }); continue; }
                    File.Copy(src, e.Original, true);
                    restored++;
                }
                catch (Exception ex) { failed.Add(new Skip { Path = e.Original, Reason = ex.Message }); }
            }
            return (restored, failed, folder);
        }
    }
}
