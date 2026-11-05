using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Diagnostics;
using System.IO.Compression;
using MonitoringTools.Core.Archives;

namespace MonitoringTools.Core
{
    /// <summary>
    /// Searches and replaces across files for the regex builder. Searching never writes. Replacing is a separate
    /// step that needs a preview first, copies every file to a timestamped backup folder before changing it,
    /// and can restore the last run. A search can also look inside archives (zip natively, 7z, rar, tar and the
    /// single-file compressors through the bundled 7-Zip) and inside binary files byte by byte, and per-extension
    /// settings choose the encoding, skipping and binary handling. Archive entries and binary matches are never
    /// replaced: a replace writes only ordinary text files.
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
            /// <summary>An optional regular expression tested against each file name (ignore case). Empty means no name filter beyond the masks.</summary>
            public string NameRegex = "";
            public bool Subfolders = true;
            public bool SkipBinary = true;
            /// <summary>How a binary file is searched when it is not skipped: "Bytes" (one character per byte, matches shown with their offset and hex) or "Text" (decoded like any file).</summary>
            public string BinaryMode = "Bytes";
            /// <summary>Look inside archives. Masks and the name pattern then apply to the entries; the archive itself is always opened.</summary>
            public bool SearchArchives = false;
            /// <summary>Per-extension rules: encoding, skip, binary handling.</summary>
            public List<RegexFileType> FileTypes = new List<RegexFileType>();
            public long MaxBytes = 5L * 1024 * 1024;
            /// <summary>The most an archive may unpack to before it is skipped, so a small archive cannot fill memory or disk.</summary>
            public long MaxArchiveBytes = 200L * 1024 * 1024;
        }

        public static readonly string[] ZipExtensions = { ".zip", ".jar", ".nupkg" };
        public static readonly string[] SevenZipExtensions = { ".7z", ".rar", ".tar", ".gz", ".tgz", ".bz2", ".xz", ".cab" };
        public static readonly string[] Encodings = { "Auto", "UTF-8", "UTF-16 LE", "UTF-16 BE", "Latin-1" };
        public static readonly string[] BinaryChoices = { "Default", "Skip", "Bytes", "Text" };

        public static bool IsArchive(string path)
        {
            var ext = System.IO.Path.GetExtension(path ?? "").ToLowerInvariant();
            return ZipExtensions.Contains(ext) || SevenZipExtensions.Contains(ext);
        }

        /// <summary>The rule for a file name's extension, or null.</summary>
        public static RegexFileType RuleFor(List<RegexFileType> rules, string name)
        {
            if (rules == null || rules.Count == 0) return null;
            var ext = System.IO.Path.GetExtension(name ?? "");
            if (string.IsNullOrEmpty(ext)) return null;
            return rules.FirstOrDefault(r => string.Equals(NormalizeExtension(r.Extension), ext, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>".log" from "log", "*.log" or ".LOG"; null when it is not a plain extension.</summary>
        public static string NormalizeExtension(string e)
        {
            e = (e ?? "").Trim().TrimStart('*');
            if (e.Length == 0) return null;
            if (!e.StartsWith(".")) e = "." + e;
            return Regex.IsMatch(e, @"^\.[A-Za-z0-9_\-]{1,16}$") ? e.ToLowerInvariant() : null;
        }

        public sealed class Hit
        {
            public int Line;
            public int Column;
            public int Length;
            public string LineText;
            public string Value;
            /// <summary>Byte offset of the match in a file searched as bytes, otherwise -1. LineText is then the printable view of the bytes around it.</summary>
            public long Offset = -1;
            /// <summary>The bytes around a binary match in hexadecimal, with the match itself from HexStart for HexLength characters.</summary>
            public string Hex;
            public int HexStart, HexLength;
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
            /// <summary>Searched as bytes; matches carry offsets and are never replaced.</summary>
            public bool Binary;
            /// <summary>The archive this entry was read from, or null for an ordinary file. Path is then "archive!/entry".</summary>
            public string Archive;
            /// <summary>The encoding forced by a file type rule, or null when it was detected.</summary>
            public string ForcedEncoding;
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
            public int ArchivesOpened;
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

        /// <summary>Decodes with the named encoding (Auto detects). A byte order mark of that encoding is honoured and kept for writing back.</summary>
        public static TextFile DecodeAs(byte[] b, string encoding)
        {
            switch (encoding)
            {
                case null: case "": case "Auto": return Decode(b);
                case "UTF-8":
                {
                    bool bom = b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF;
                    return new TextFile { Text = new UTF8Encoding(false).GetString(b, bom ? 3 : 0, b.Length - (bom ? 3 : 0)), Encoding = new UTF8Encoding(bom), HasBom = bom, EncodingName = "UTF-8 (file type setting)" };
                }
                case "UTF-16 LE":
                {
                    bool bom = b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE;
                    return new TextFile { Text = new UnicodeEncoding(false, false).GetString(b, bom ? 2 : 0, b.Length - (bom ? 2 : 0)), Encoding = new UnicodeEncoding(false, bom), HasBom = bom, EncodingName = "UTF-16 LE (file type setting)" };
                }
                case "UTF-16 BE":
                {
                    bool bom = b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF;
                    return new TextFile { Text = new UnicodeEncoding(true, false).GetString(b, bom ? 2 : 0, b.Length - (bom ? 2 : 0)), Encoding = new UnicodeEncoding(true, bom), HasBom = bom, EncodingName = "UTF-16 BE (file type setting)" };
                }
                case "Latin-1": return new TextFile { Text = Encoding.Latin1.GetString(b), Encoding = Encoding.Latin1, HasBom = false, EncodingName = "Latin-1 (file type setting)" };
                default: return Decode(b);
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

        private static IEnumerable<string> Enumerate(Options o, List<Regex> include, List<Regex> exclude, Regex nameRx, List<Skip> skipped, CancellationToken ct)
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
                    // with archive search on, an archive is always opened and the masks apply to its entries
                    if (o.SearchArchives && IsArchive(name)) { yield return f; continue; }
                    if (include.Count > 0 && !Any(include, name)) continue;
                    if (nameRx != null && !SafeNameMatch(nameRx, name)) continue;
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
            Regex nameRx = null;
            if (!string.IsNullOrWhiteSpace(o.NameRegex))
            {
                nameRx = RegexKit.TryCreate(o.NameRegex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, out var nameError);
                if (nameRx == null) { report.Skipped.Add(new Skip { Path = "", Reason = "The file name pattern is not valid: " + nameError }); return report; }
            }
            try
            {
                foreach (var path in Enumerate(o, include, exclude, nameRx, report.Skipped, ct))
                {
                    ct.ThrowIfCancellationRequested();
                    if (o.SearchArchives && IsArchive(path))
                    {
                        report.ArchivesOpened++;
                        foreach (var (entry, bytes, modified) in ArchiveEntries(path, o, include, nameRx, report.Skipped, ct))
                        {
                            report.FilesScanned++;
                            var efr = ScanBytes(rx, path + "!/" + entry, entry, bytes, bytes.LongLength, modified, o, report.Skipped, ct, out var ecap);
                            if (efr == null || efr.Count == 0) continue;
                            efr.Archive = path;
                            report.Files.Add(efr);
                            report.TotalMatches += efr.Count;
                            if (ecap) report.Capped = true;
                            if (report.TotalMatches >= MaxMatchesTotal) { report.Capped = true; break; }
                        }
                        if (report.TotalMatches >= MaxMatchesTotal) break;
                        continue;
                    }
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
                var rule = RuleFor(o.FileTypes, path);
                if (rule != null && rule.Skip) { skipped.Add(new Skip { Path = path, Reason = "Skipped by the file type setting for " + NormalizeExtension(rule.Extension) + "." }); return null; }
                if (info.Length > o.MaxBytes) { skipped.Add(new Skip { Path = path, Reason = $"Larger than the {o.MaxBytes / (1024.0 * 1024):0.#} MB limit." }); return null; }
                var bytes = File.ReadAllBytes(path);
                return ScanBytes(rx, path, path, bytes, info.Length, info.LastWriteTimeUtc, o, skipped, ct, out capped);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { skipped.Add(new Skip { Path = path, Reason = ex.Message }); return null; }
        }

        /// <summary>Searches the bytes of one file or archive entry. displayPath is what the results show; name decides the file type rule.</summary>
        private static FileResult ScanBytes(Regex rx, string displayPath, string name, byte[] bytes, long size, DateTime modified, Options o, List<Skip> skipped, CancellationToken ct, out bool capped)
        {
            capped = false;
            try
            {
                var rule = RuleFor(o.FileTypes, name);
                if (rule != null && rule.Skip) { skipped.Add(new Skip { Path = displayPath, Reason = "Skipped by the file type setting for " + NormalizeExtension(rule.Extension) + "." }); return null; }
                var forced = rule != null && rule.Encoding != null && rule.Encoding != "Auto" ? rule.Encoding : null;
                var binaryMode = rule != null && rule.Binary != null && rule.Binary != "Default" ? rule.Binary : (o.SkipBinary ? "Skip" : o.BinaryMode ?? "Bytes");
                // a forced text encoding means the file is text (UTF-16 has null bytes), so it is never treated as binary
                if (forced == null && LooksBinary(bytes))
                {
                    if (binaryMode == "Skip") { skipped.Add(new Skip { Path = displayPath, Reason = "Looks like a binary file." }); return null; }
                    if (binaryMode == "Bytes") return ScanBinary(rx, displayPath, bytes, size, modified, ct, out capped);
                }
                var tf = forced != null ? DecodeAs(bytes, forced) : Decode(bytes);
                var fr = new FileResult { Path = displayPath, Encoding = tf.EncodingName, Size = size, Modified = modified, ForcedEncoding = forced };
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
            catch (RegexMatchTimeoutException) { skipped.Add(new Skip { Path = displayPath, Reason = "Stopped at the 200 ms limit: the pattern backtracks too much on this file." }); return null; }
            catch (Exception ex) { skipped.Add(new Skip { Path = displayPath, Reason = ex.Message }); return null; }
        }

        /// <summary>
        /// Searches a binary file one character per byte (Latin-1), so \x00 to \xFF in the pattern are byte values and text inside the file
        /// can still be found. Each match records its byte offset, a printable view of the bytes around it and their hexadecimal.
        /// </summary>
        private static FileResult ScanBinary(Regex rx, string displayPath, byte[] bytes, long size, DateTime modified, CancellationToken ct, out bool capped)
        {
            capped = false;
            var text = Encoding.Latin1.GetString(bytes);
            var fr = new FileResult { Path = displayPath, Encoding = "binary, searched as bytes", Size = size, Modified = modified, Binary = true };
            foreach (Match m in rx.Matches(text))
            {
                ct.ThrowIfCancellationRequested();
                fr.Count++;
                if (fr.Hits.Count >= MaxMatchesPerFile) { capped = true; continue; }
                int len = Math.Min(m.Length, 32);
                int from = Math.Max(0, m.Index - 8), to = Math.Min(bytes.Length, m.Index + len + 8);
                var view = new StringBuilder();
                for (int k = from; k < to; k++) view.Append(bytes[k] >= 0x20 && bytes[k] < 0x7F ? (char)bytes[k] : '.');
                var hex = new StringBuilder();
                int hexStart = 0, hexLen = 0;
                for (int k = from; k < to; k++)
                {
                    if (hex.Length > 0) hex.Append(' ');
                    if (k == m.Index) hexStart = hex.Length;
                    hex.Append(bytes[k].ToString("X2"));
                    if (k == m.Index + len - 1) hexLen = hex.Length - hexStart;
                }
                var value = new StringBuilder();
                foreach (var c in m.Value.Length > 64 ? m.Value[..64] : m.Value) value.Append(c >= ' ' && c < '\u007F' ? c : '.');
                fr.Hits.Add(new Hit { Line = 0, Column = m.Index - from + 1, Length = len, LineText = view.ToString(), Value = value.ToString(), Offset = m.Index, Hex = hex.ToString(), HexStart = hexStart, HexLength = hexLen });
            }
            return fr;
        }

        // ---------------------------------------------------------------- archives

        /// <summary>
        /// The entries of an archive that pass the masks and name pattern, read into memory. Zip archives are read natively; the other formats
        /// are unpacked by the bundled 7-Zip into a temporary folder that is deleted afterwards. Nested archives are not opened. An archive that
        /// would unpack to more than MaxArchiveBytes, is encrypted, or cannot be read is reported as skipped.
        /// </summary>
        private static IEnumerable<(string Entry, byte[] Bytes, DateTime Modified)> ArchiveEntries(string path, Options o, List<Regex> include, Regex nameRx, List<Skip> skipped, CancellationToken ct)
        {
            var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            var list = new List<(string, byte[], DateTime)>();
            bool Wanted(string entry)
            {
                var n = System.IO.Path.GetFileName(entry);
                if (include.Count > 0 && !Any(include, n)) return false;
                return nameRx == null || SafeNameMatch(nameRx, n);
            }
            if (ZipExtensions.Contains(ext))
            {
                try
                {
                    using var zip = ZipFile.OpenRead(path);
                    long total = 0;
                    foreach (var e in zip.Entries)
                    {
                        ct.ThrowIfCancellationRequested();
                        if (e.FullName.EndsWith("/") || !Wanted(e.FullName)) continue;
                        if (e.Length > o.MaxBytes) { skipped.Add(new Skip { Path = path + "!/" + e.FullName, Reason = $"Larger than the {o.MaxBytes / (1024.0 * 1024):0.#} MB limit." }); continue; }
                        total += e.Length;
                        if (total > o.MaxArchiveBytes) { skipped.Add(new Skip { Path = path, Reason = $"The archive unpacks to more than {o.MaxArchiveBytes / (1024 * 1024)} MB, so the rest of it was not searched." }); break; }
                        using var s = e.Open();
                        using var ms = new MemoryStream();
                        s.CopyTo(ms);
                        list.Add((e.FullName, ms.ToArray(), e.LastWriteTime.UtcDateTime));
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { skipped.Add(new Skip { Path = path, Reason = "The archive could not be read: " + ex.Message }); }
                return list;
            }
            // RAR needs the full 7-Zip (7z.exe with 7z.dll); 7za reads the other formats
            var exe = path.EndsWith(".rar", StringComparison.OrdinalIgnoreCase) ? ArchiveEngine.SevenZipFull : ArchiveEngine.SevenZip;
            if (exe == null) { skipped.Add(new Skip { Path = path, Reason = "This archive type needs 7-Zip, which is not available in this build." }); return list; }
            // list first, so an archive that would unpack to too much is never unpacked
            var (code, listing) = Run7z(exe, new[] { "l", "-slt", "-ba", "-p", "--", path }, null, ct);
            if (code != 0) { skipped.Add(new Skip { Path = path, Reason = "7-Zip could not list the archive (exit code " + code + ")" + (listing.Contains("password", StringComparison.OrdinalIgnoreCase) || listing.Contains("encrypted", StringComparison.OrdinalIgnoreCase) ? ": it is encrypted." : ".") }); return list; }
            long sum = 0;
            foreach (Match m in Regex.Matches(listing, @"^Size = (\d+)", RegexOptions.Multiline)) sum += long.Parse(m.Groups[1].Value);
            if (sum > o.MaxArchiveBytes) { skipped.Add(new Skip { Path = path, Reason = $"The archive unpacks to more than {o.MaxArchiveBytes / (1024 * 1024)} MB, so it was not searched." }); return list; }
            var temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mt-regex-archive-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(temp);
                var (xc, xout) = Run7z(exe, new[] { "x", "-y", "-p", "-o" + temp, "--", path }, null, ct);
                if (xc != 0) { skipped.Add(new Skip { Path = path, Reason = "7-Zip could not unpack the archive (exit code " + xc + ")" + (xout.Contains("password", StringComparison.OrdinalIgnoreCase) || xout.Contains("encrypted", StringComparison.OrdinalIgnoreCase) ? ": it is encrypted." : ".") }); return list; }
                foreach (var f in Directory.EnumerateFiles(temp, "*", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                {
                    ct.ThrowIfCancellationRequested();
                    var rel = System.IO.Path.GetRelativePath(temp, f).Replace('\\', '/');
                    if (!Wanted(rel)) continue;
                    var info = new FileInfo(f);
                    if (info.Length > o.MaxBytes) { skipped.Add(new Skip { Path = path + "!/" + rel, Reason = $"Larger than the {o.MaxBytes / (1024.0 * 1024):0.#} MB limit." }); continue; }
                    list.Add((rel, File.ReadAllBytes(f), info.LastWriteTimeUtc));
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { skipped.Add(new Skip { Path = path, Reason = "The archive could not be read: " + ex.Message }); }
            finally { try { if (Directory.Exists(temp)) Directory.Delete(temp, true); } catch { } }
            return list;
        }

        /// <summary>Runs 7-Zip with no standard input (so an encrypted archive fails instead of waiting for a password), stopped after 60 seconds or on cancellation.</summary>
        private static (int Code, string Output) Run7z(string exe, string[] args, string cwd, CancellationToken ct)
        {
            var psi = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
            foreach (var a in args) psi.ArgumentList.Add(a);
            if (cwd != null) psi.WorkingDirectory = cwd;
            using var p = Process.Start(psi);
            if (p == null) return (-1, "7-Zip did not start.");
            p.StandardInput.Close();
            var o = p.StandardOutput.ReadToEndAsync();
            var e = p.StandardError.ReadToEndAsync();
            var sw = Stopwatch.StartNew();
            while (!p.WaitForExit(100))
            {
                if (ct.IsCancellationRequested || sw.Elapsed.TotalSeconds > 60) { try { p.Kill(true); } catch { } ct.ThrowIfCancellationRequested(); return (-1, "7-Zip took longer than 60 seconds."); }
            }
            return (p.ExitCode, o.Result + e.Result);
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
            var sb = new StringBuilder("file,line,column,length,match,line_text,byte_offset\r\n");
            foreach (var f in r.Files)
                foreach (var h in f.Hits)
                    sb.Append(Csv(f.Path)).Append(',').Append(h.Line).Append(',').Append(h.Column).Append(',').Append(h.Length).Append(',').Append(Csv(h.Value)).Append(',').Append(Csv(h.LineText)).Append(',').Append(h.Offset >= 0 ? h.Offset.ToString() : "").Append("\r\n");
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
            /// <summary>The encoding a file type rule forced when the file was searched; the replace reads and writes it the same way.</summary>
            public string Encoding;
        }

        /// <summary>
        /// The preview: which files would change and how many replacements each would get. Writes nothing. Matches inside archives and in files
        /// searched as bytes are left out, because a replace only writes ordinary text files; NotReplaceable says how many files that was.
        /// </summary>
        public static List<ReplacePlanItem> Plan(Report r) =>
            r.Files.Where(f => f.Archive == null && !f.Binary).Select(f => new ReplacePlanItem { Path = f.Path, Count = f.Count, Size = f.Size, Modified = f.Modified, Encoding = f.ForcedEncoding }).ToList();

        /// <summary>Files with matches that a replace leaves alone: entries inside archives and files searched as bytes.</summary>
        public static int NotReplaceable(Report r) => r.Files.Count(f => f.Archive != null || f.Binary);

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
                    var tf = item.Encoding != null ? DecodeAs(bytes, item.Encoding) : Decode(bytes);
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

        private static bool SafeNameMatch(Regex rx, string name)
        {
            try { return rx.IsMatch(name); }
            catch (RegexMatchTimeoutException) { return false; }
        }

        public sealed record BackupInfo(string Folder, string Name, int Files, DateTime Created);

        /// <summary>Every backup run that has a manifest, newest first, so any run can be restored and not only the last one.</summary>
        public static List<BackupInfo> ListBackups()
        {
            var list = new List<BackupInfo>();
            try
            {
                if (!Directory.Exists(BackupRoot)) return list;
                foreach (var d in Directory.GetDirectories(BackupRoot).OrderByDescending(x => x, StringComparer.OrdinalIgnoreCase))
                {
                    var mf = System.IO.Path.Combine(d, "manifest.json");
                    if (!File.Exists(mf)) continue;
                    int n = 0;
                    try { n = JsonSerializer.Deserialize<List<ManifestEntry>>(File.ReadAllText(mf))?.Count ?? 0; } catch { }
                    list.Add(new BackupInfo(d, System.IO.Path.GetFileName(d), n, Directory.GetCreationTime(d)));
                }
            }
            catch { }
            return list;
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
        public static (int Restored, List<Skip> Failed, string Folder) RestoreLast() => RestoreFolder(LastBackup());

        /// <summary>Copies every file in the given backup run back over its original.</summary>
        public static (int Restored, List<Skip> Failed, string Folder) RestoreFolder(string folder)
        {
            var failed = new List<Skip>();
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
