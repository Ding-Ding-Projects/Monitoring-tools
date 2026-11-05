using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace MonitoringTools.Core.Archives
{
    public enum ArchiveFormat { Zip, SevenZip, TarGz }

    /// <summary>One exclude pattern: a glob (like node_modules or *.tmp) or a regular expression tested against the relative path.</summary>
    public sealed class ExcludeRule
    {
        public string Pattern;
        public bool Regex;
        private Regex _rx;
        private string[] _segments;

        public ExcludeRule(string pattern, bool regex) { Pattern = pattern; Regex = regex; }

        /// <summary>Null when the pattern is usable, otherwise the reason it is not.</summary>
        public string Validate()
        {
            try { Build(); return null; }
            catch (ArgumentException ex) { return ex.Message; }
        }

        private void Build()
        {
            if (_rx != null) return;
            if (Regex)
                _rx = new Regex(Pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));
            else
            {
                // A glob with no path separator is tested against every folder and file name on the way down, so
                // node_modules drops that folder wherever it sits. A glob with a separator is tested against the whole path.
                var p = Pattern.Trim().Replace('\\', '/').Trim('/');
                var rx = "^" + System.Text.RegularExpressions.Regex.Escape(p).Replace(@"\*\*", "\u0001").Replace(@"\*", "[^/]*").Replace("\u0001", ".*").Replace(@"\?", "[^/]") + "$";
                _rx = new Regex(rx, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));
                _segments = p.Contains('/') ? null : new[] { p };
            }
        }

        /// <summary>True when the relative path (forward slashes) is excluded by this rule.</summary>
        public bool Matches(string relative)
        {
            Build();
            try
            {
                if (Regex) return _rx.IsMatch(relative);
                if (_segments != null) return relative.Split('/').Any(seg => _rx.IsMatch(seg));
                return _rx.IsMatch(relative) || relative.Split('/').Select((_, i) => string.Join("/", relative.Split('/').Take(i + 1))).Any(prefix => _rx.IsMatch(prefix));
            }
            catch (RegexMatchTimeoutException) { return false; }
        }
    }

    public sealed class ArchiveOptions
    {
        public string[] Items = Array.Empty<string>();
        public string Name = "Archive";
        public string OutFolder;
        public ArchiveFormat Format = ArchiveFormat.Zip;
        /// <summary>0 Store, 1 Fastest, 2 Fast, 3 Normal, 4 Maximum, 5 Ultra.</summary>
        public int Level = 3;
        public string Method;                      // null: the format's default
        public string Dictionary = "auto";         // 7z with LZMA or LZMA2: "auto", "1m", "64m" ...
        public string Solid = "auto";              // 7z with LZMA or LZMA2: "auto", "off", "on", "64m", "1g" ...
        public int Threads;                        // 0: automatic
        public string Password;
        public bool EncryptNames;                  // 7z only
        public bool Split;
        public long VolumeBytes;
        public bool IncludeParent = true;
        public List<ExcludeRule> Excludes = new List<ExcludeRule>();
        public bool TestAfter = true;

        public static readonly string[] LevelNames = { "Store", "Fastest", "Fast", "Normal", "Maximum", "Ultra" };
        public static readonly string[] ZipMethods = { "Deflate", "Deflate64", "BZip2", "LZMA", "Store" };
        public static readonly string[] SevenZipMethods = { "LZMA2", "LZMA", "PPMd", "BZip2", "Deflate" };
        public static readonly string[] Dictionaries = { "auto", "1m", "4m", "16m", "64m", "256m" };
        public static readonly string[] SolidBlocks = { "auto", "off", "64m", "256m", "1g", "2g", "on" };

        public string EffectiveMethod => Method ?? (Format == ArchiveFormat.Zip ? "Deflate" : Format == ArchiveFormat.SevenZip ? "LZMA2" : "Deflate");
        public bool HasPassword => !string.IsNullOrEmpty(Password);

        /// <summary>True when the options cannot be written by System.IO.Compression and need the bundled 7-Zip.</summary>
        public bool NeedsSevenZip =>
            Format == ArchiveFormat.SevenZip
            || (Format == ArchiveFormat.Zip && (HasPassword || Split || !(EffectiveMethod == "Deflate" || EffectiveMethod == "Store")));

        public string Suffix => Format switch { ArchiveFormat.Zip => ".zip", ArchiveFormat.SevenZip => ".7z", _ => ".tar.gz" };
    }

    public sealed class ScanEntry
    {
        public string Full;
        public string Relative;       // forward slashes, from the scan root
        public long Size;
        public bool IsEmptyDirectory;
        public DateTime Modified;
    }

    /// <summary>Everything the chosen items hold, with sizes, relative to one root folder. Excludes and the parent-folder choice come later.</summary>
    public sealed class ArchiveScan
    {
        public string Root;
        public string RootName;
        public List<ScanEntry> Entries = new List<ScanEntry>();
        public int Unreadable;
        public bool Truncated;
        public const int MaxEntries = 250000;

        public static ArchiveScan Run(string[] items, CancellationToken ct = default)
        {
            var scan = new ArchiveScan();
            var clean = items.Select(p => p.TrimEnd('\\', '/')).Where(p => File.Exists(p) || Directory.Exists(p)).ToList();
            if (clean.Count == 0) return scan;
            // One folder alone is the root itself; anything else is rooted at the closest common parent folder.
            if (clean.Count == 1 && Directory.Exists(clean[0])) scan.Root = clean[0];
            else scan.Root = CommonParent(clean);
            scan.RootName = Path.GetFileName(scan.Root.TrimEnd('\\')) ?? "";
            foreach (var item in clean)
            {
                ct.ThrowIfCancellationRequested();
                if (File.Exists(item)) scan.Add(item, false);
                else if (Directory.Exists(item))
                {
                    if (!string.Equals(item.TrimEnd('\\'), scan.Root.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) scan.AddDirectoryItself(item);
                    scan.Walk(item, ct);
                }
            }
            return scan;
        }

        public static string CommonParent(List<string> paths)
        {
            var parts = paths.Select(p => (Path.GetDirectoryName(p) ?? p).Split('\\')).ToList();
            var first = parts[0];
            int n = first.Length;
            foreach (var p in parts) { int i = 0; while (i < n && i < p.Length && string.Equals(first[i], p[i], StringComparison.OrdinalIgnoreCase)) i++; n = i; }
            if (n == 0) return Path.GetPathRoot(paths[0]);
            var common = string.Join("\\", first.Take(n));
            return common.EndsWith(":") ? common + "\\" : common;
        }

        private void AddDirectoryItself(string dir)
        {
            try { if (!Directory.EnumerateFileSystemEntries(dir).Any()) Add(dir, true); } catch { Unreadable++; }
        }

        private void Add(string full, bool dir)
        {
            if (Entries.Count >= MaxEntries) { Truncated = true; return; }
            try
            {
                var rel = Path.GetRelativePath(Root, full).Replace('\\', '/');
                long size = 0; DateTime mod = DateTime.Now;
                if (!dir) { var fi = new FileInfo(full); size = fi.Length; mod = fi.LastWriteTime; }
                Entries.Add(new ScanEntry { Full = full, Relative = rel, Size = size, IsEmptyDirectory = dir, Modified = mod });
            }
            catch { Unreadable++; }
        }

        private void Walk(string dir, CancellationToken ct)
        {
            IEnumerable<string> files = Array.Empty<string>(), dirs = Array.Empty<string>();
            try { files = Directory.EnumerateFiles(dir); dirs = Directory.EnumerateDirectories(dir); }
            catch { Unreadable++; return; }
            foreach (var f in files) { ct.ThrowIfCancellationRequested(); Add(f, false); }
            foreach (var d in dirs)
            {
                ct.ThrowIfCancellationRequested();
                try { if ((File.GetAttributes(d) & FileAttributes.ReparsePoint) != 0) continue; } catch { Unreadable++; continue; }   // never follow a junction or link out of the chosen folder
                bool empty = false;
                try { empty = !Directory.EnumerateFileSystemEntries(d).Any(); } catch { Unreadable++; continue; }
                if (empty) Add(d, true); else Walk(d, ct);
            }
        }

        /// <summary>The entries that survive the exclude rules, each with the name it will have inside the archive.</summary>
        public List<(ScanEntry Entry, string Name)> Select(ArchiveOptions o)
        {
            var prefix = o.IncludeParent && RootName.Length > 0 ? RootName + "/" : "";
            var rules = o.Excludes.Where(r => r.Validate() == null).ToList();
            return Entries
                .Where(e => !rules.Any(r => r.Matches(e.Relative)))
                .Select(e => (e, prefix + e.Relative)).ToList();
        }
    }

    public sealed class ArchivePlan
    {
        public int Files, Folders;
        public long SourceBytes, EstimatedBytes;
        public int Volumes = 1;
        public TimeSpan EstimatedTime;
        public long FreeNeeded;
        public long? FreeOnTarget;
        public string Backend;                 // "System.IO.Compression" or "7-Zip"
        public int Excluded;
    }

    public sealed class ArchiveProgress
    {
        public string Step;                    // Scan, Compress, Test, Finish
        public long Done, Total;
        public double BytesPerSecond;
        public TimeSpan? Remaining;
        public string CurrentFile;
    }

    public sealed class ArchiveResult
    {
        public bool Ok, Cancelled;
        public string Error;
        public List<string> Outputs = new List<string>();
        public string Primary => Outputs.FirstOrDefault();
        public long ArchiveBytes, SourceBytes;
        public int Files;
        public bool Tested, TestOk;
        public string TestDetail;
        public string Backend;
    }
}
