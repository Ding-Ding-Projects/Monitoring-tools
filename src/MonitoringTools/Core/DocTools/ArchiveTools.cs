using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;

namespace MonitoringTools.Core.DocTools
{
    /// <summary>What to do when a file of the same name is already in the destination. Ask lets the person choose for each one.</summary>
    public enum OverwriteRule { KeepBoth, Replace, Skip, Ask }

    public sealed class ArchiveEntry
    {
        /// <summary>The path inside the archive, with forward slashes and no trailing slash.</summary>
        public string Path;
        public bool IsDirectory;
        public long Size;
        public DateTime? Modified;
        /// <summary>The contents are locked with a password (the name is still visible).</summary>
        public bool Encrypted;
        /// <summary>The path would leave the destination folder (absolute, a drive letter or "..").</summary>
        public bool Unsafe;
        internal string Raw;
        public string Name => Path.Contains('/') ? Path[(Path.LastIndexOf('/') + 1)..] : Path;
    }

    public sealed class ArchiveInfo
    {
        public string File;
        public string Format;                 // "zip", "tar", "tar.gz", "gz", "7z", "xz", "bz2", "tar.xz", "tar.bz2"
        public List<ArchiveEntry> Entries = new List<ArchiveEntry>();
        public long PackedBytes;
        /// <summary>When set, the archive cannot be read here and this says why in plain words (never a guess).</summary>
        public string Unsupported;
        public bool NeedsPassword => NamesLocked || Entries.Any(e => e.Encrypted && !e.IsDirectory);
        /// <summary>The file names themselves are encrypted (a 7z made with encrypted headers), so nothing is listed until the password is given.</summary>
        public bool NamesLocked;
        /// <summary>Read and extracted through the bundled 7-Zip command line (7z, xz, bz2 and AES-encrypted zip).</summary>
        public bool UseSevenZip;
        public long UnpackedBytes => Entries.Where(e => !e.IsDirectory).Sum(e => e.Size);
        public int FileCount => Entries.Count(e => !e.IsDirectory);
        internal Dictionary<string, ZipCentral> Central;
    }

    internal sealed class ZipCentral { public ushort Flags, Method, Time, Date; public uint Crc, CSize, USize, LocalOffset; public string Name; }

    public sealed class ExtractResult
    {
        public int Extracted; public long Bytes;
        public int Skipped;
        public List<string> Renamed = new List<string>();
        public List<string> Refused = new List<string>();
        public int NotSelected;
        public bool Canceled;
        public bool WrongPassword;
        public string Error;
        public string Destination;
    }

    public sealed class ExtractProgress { public int Done, Total; public string Current; public long Bytes, TotalBytes; }

    /// <summary>
    /// Reads and extracts archives. Zip (System.IO.Compression, plus the traditional zip password scheme read by hand), tar,
    /// tar.gz, tgz and single-file gz use the .NET libraries. 7z, xz, bz2, tar.xz, tar.bz2 and AES-encrypted zip go through the
    /// bundled 7-Zip command line (7za). RAR is not supported by 7za, so it reports an honest "cannot read" state.
    /// Every path inside an archive is checked against the destination first, so a zip-slip entry is refused.
    /// </summary>
    public static partial class ArchiveTools
    {
        public static readonly string[] Extensions = { ".zip", ".7z", ".rar", ".tar", ".gz", ".tgz", ".xz", ".txz", ".bz2", ".tbz2", ".tbz" };

        public static bool IsArchive(string path)
        {
            var n = System.IO.Path.GetFileName(path ?? "").ToLowerInvariant();
            return Extensions.Any(e => n.EndsWith(e));
        }

        /// <summary>Formats this build can read; the others need 7-Zip.</summary>
        public static bool Readable(string path)
        {
            var n = System.IO.Path.GetFileName(path ?? "").ToLowerInvariant();
            if (n.EndsWith(".zip") || n.EndsWith(".tar") || n.EndsWith(".tar.gz") || n.EndsWith(".tgz") || n.EndsWith(".gz")) return true;
            return !n.EndsWith(".rar") && IsArchive(n) && SevenZipAvailable;
        }

        // ------------------------------------------------------------------ safety

        /// <summary>True when <paramref name="entryPath"/> stays inside <paramref name="dest"/>; gives the full target path.</summary>
        public static bool TrySafePath(string dest, string entryPath, out string full)
        {
            full = null;
            if (string.IsNullOrWhiteSpace(entryPath)) return false;
            var p = entryPath.Replace('\\', '/');
            if (p.StartsWith("/") || (p.Length >= 2 && p[1] == ':') || p.StartsWith("//")) return false;
            if (p.Split('/').Any(s => s == "..")) return false;
            if (p.IndexOfAny(System.IO.Path.GetInvalidPathChars()) >= 0) return false;
            var root = System.IO.Path.GetFullPath(dest).TrimEnd('\\', '/') + System.IO.Path.DirectorySeparatorChar;
            string f;
            try { f = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, p.TrimEnd('/'))); } catch { return false; }
            if (!f.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return false;
            full = f;
            return true;
        }

        // ------------------------------------------------------------------ listing

        public static ArchiveInfo Open(string file)
        {
            var info = new ArchiveInfo { File = file };
            try { info.PackedBytes = new FileInfo(file).Length; } catch { }
            var n = System.IO.Path.GetFileName(file).ToLowerInvariant();
            try
            {
                if (n.EndsWith(".zip"))
                {
                    info.Format = "zip"; ListZip(info);
                    if (info.Central != null && info.Central.Values.Any(c => c.Method == 99))
                    {
                        info.UseSevenZip = true;
                        if (!SevenZipAvailable) info.Unsupported = "This zip uses AES encryption, which is read through 7-Zip, and 7-Zip is missing from this copy of the app.";
                    }
                }
                else if (n.EndsWith(".tar.gz") || n.EndsWith(".tgz")) { info.Format = "tar.gz"; ListTar(info, () => new GZipStream(File.OpenRead(info.File), CompressionMode.Decompress)); }
                else if (n.EndsWith(".tar.xz") || n.EndsWith(".txz")) { info.Format = "tar.xz"; if (NeedSeven(info)) ListTar(info, () => SevenZipStream(info.File, CancellationToken.None)); }
                else if (n.EndsWith(".tar.bz2") || n.EndsWith(".tbz2") || n.EndsWith(".tbz")) { info.Format = "tar.bz2"; if (NeedSeven(info)) ListTar(info, () => SevenZipStream(info.File, CancellationToken.None)); }
                else if (n.EndsWith(".tar")) { info.Format = "tar"; ListTar(info, () => File.OpenRead(info.File)); }
                else if (n.EndsWith(".gz")) { info.Format = "gz"; ListGz(info); }
                else if (n.EndsWith(".xz")) { info.Format = "xz"; if (NeedSeven(info)) ListSingleSeven(info, 3); }
                else if (n.EndsWith(".bz2")) { info.Format = "bz2"; if (NeedSeven(info)) ListSingleSeven(info, 4); }
                else if (n.EndsWith(".7z")) { info.Format = "7z"; if (NeedSeven(info)) ListSeven(info, null); }
                else if (n.EndsWith(".rar"))
                {
                    info.Format = "rar";
                    info.Unsupported = "RAR archives cannot be read here: the 7-Zip command line bundled with this app does not include the RAR decoder. Zip, 7z, tar, gz, xz and bz2 archives open here.";
                }
                else
                {
                    info.Format = System.IO.Path.GetExtension(n).TrimStart('.');
                    info.Unsupported = "This archive type cannot be read here. Zip, 7z, tar, gz, xz and bz2 archives open here.";
                }
            }
            catch (Exception ex) when (ex is InvalidDataException || ex is IOException || ex is UnauthorizedAccessException || ex is FormatException || ex is NotSupportedException || ex is System.ComponentModel.Win32Exception)
            {
                info.Entries.Clear();
                info.Unsupported = "This file could not be read as " + (info.Format ?? "an archive") + ": " + ex.Message;
            }
            foreach (var e in info.Entries) e.Unsafe = !TrySafePath(System.IO.Path.GetTempPath(), e.Path, out _);
            return info;
        }

        private static string Norm(string name) => name.Replace('\\', '/').TrimEnd('/');

        private static void ListZip(ArchiveInfo info)
        {
            info.Central = ReadCentral(info.File);
            using var z = ZipFile.OpenRead(info.File);
            foreach (var e in z.Entries)
            {
                bool dir = e.FullName.EndsWith("/") || e.FullName.EndsWith("\\");
                var key = e.FullName;
                bool enc = info.Central != null && info.Central.TryGetValue(key, out var c) && (c.Flags & 1) != 0;
                info.Entries.Add(new ArchiveEntry { Raw = e.FullName, Path = Norm(e.FullName), IsDirectory = dir, Size = dir ? 0 : e.Length, Modified = e.LastWriteTime.LocalDateTime, Encrypted = enc && !dir });
            }
        }

        private static void ListTar(ArchiveInfo info, Func<Stream> open)
        {
            using Stream s = open();
            using var r = new TarReader(s);
            TarEntry e;
            while ((e = r.GetNextEntry(copyData: false)) != null)
            {
                if (e.EntryType == TarEntryType.GlobalExtendedAttributes) continue;
                bool dir = e.EntryType == TarEntryType.Directory;
                if (!dir && e.EntryType != TarEntryType.RegularFile && e.EntryType != TarEntryType.V7RegularFile) continue;   // links and devices are not extracted
                info.Entries.Add(new ArchiveEntry { Path = Norm(e.Name), IsDirectory = dir, Size = dir ? 0 : e.Length, Modified = e.ModificationTime.LocalDateTime });
            }
        }

        private static void ListGz(ArchiveInfo info)
        {
            var name = System.IO.Path.GetFileName(info.File);
            name = name.Substring(0, name.Length - 3);
            if (name.Length == 0) name = "content";
            long size = 0;
            using (var fs = File.OpenRead(info.File))
            {
                if (fs.Length > 4) { fs.Seek(-4, SeekOrigin.End); var b = new byte[4]; fs.ReadExactly(b, 0, 4); size = BitConverter.ToUInt32(b, 0); }
                // a gz that is not gzip fails here, so the state says so instead of listing a phantom file
                fs.Seek(0, SeekOrigin.Begin);
                var magic = new byte[2]; fs.ReadExactly(magic, 0, 2);
                if (magic[0] != 0x1f || magic[1] != 0x8b) throw new InvalidDataException("the gzip header is missing");
            }
            info.Entries.Add(new ArchiveEntry { Path = name, Size = size, Modified = File.GetLastWriteTime(info.File) });
        }

        // ------------------------------------------------------------------ zip central directory (for the encryption flag)

        private static Dictionary<string, ZipCentral> ReadCentral(string file)
        {
            try
            {
                using var fs = File.OpenRead(file);
                var tailLen = (int)Math.Min(fs.Length, 66000);
                var tail = new byte[tailLen];
                fs.Seek(-tailLen, SeekOrigin.End); fs.ReadExactly(tail, 0, tailLen);
                int eocd = -1;
                for (int i = tailLen - 22; i >= 0; i--) if (tail[i] == 0x50 && tail[i + 1] == 0x4b && tail[i + 2] == 5 && tail[i + 3] == 6) { eocd = i; break; }
                if (eocd < 0) return null;
                int count = BitConverter.ToUInt16(tail, eocd + 10);
                uint cdSize = BitConverter.ToUInt32(tail, eocd + 12), cdOff = BitConverter.ToUInt32(tail, eocd + 16);
                if (cdOff == 0xFFFFFFFF || count == 0xFFFF) return null;   // zip64: the flag is not read, so nothing is claimed
                fs.Seek(cdOff, SeekOrigin.Begin);
                var cd = new byte[cdSize]; fs.ReadExactly(cd, 0, (int)cdSize);
                var map = new Dictionary<string, ZipCentral>(StringComparer.Ordinal);
                int p = 0;
                for (int i = 0; i < count && p + 46 <= cd.Length; i++)
                {
                    if (BitConverter.ToUInt32(cd, p) != 0x02014b50) break;
                    var c = new ZipCentral
                    {
                        Flags = BitConverter.ToUInt16(cd, p + 8), Method = BitConverter.ToUInt16(cd, p + 10), Time = BitConverter.ToUInt16(cd, p + 12), Date = BitConverter.ToUInt16(cd, p + 14),
                        Crc = BitConverter.ToUInt32(cd, p + 16), CSize = BitConverter.ToUInt32(cd, p + 20), USize = BitConverter.ToUInt32(cd, p + 24), LocalOffset = BitConverter.ToUInt32(cd, p + 42)
                    };
                    int nl = BitConverter.ToUInt16(cd, p + 28), el = BitConverter.ToUInt16(cd, p + 30), cl = BitConverter.ToUInt16(cd, p + 32);
                    var enc = (c.Flags & 0x800) != 0 ? Encoding.UTF8 : Encoding.GetEncoding(437);
                    c.Name = enc.GetString(cd, p + 46, nl);
                    map[c.Name] = c;
                    p += 46 + nl + el + cl;
                }
                return map;
            }
            catch { return null; }
        }

        // ------------------------------------------------------------------ extraction

        /// <summary>
        /// Extracts the chosen entries into <paramref name="dest"/>. Runs on the calling thread (call it from a worker).
        /// Each file is written to a .part name and renamed when complete, so Cancel never leaves a half file under a real name.
        /// </summary>
        /// <param name="ask">Asked for each name that already exists when <paramref name="rule"/> is Ask; it returns Skip, Replace or KeepBoth
        /// (it may block until the person answers, and throws OperationCanceledException to stop). Without it, Ask behaves like KeepBoth.</param>
        public static ExtractResult Extract(ArchiveInfo info, IEnumerable<ArchiveEntry> selected, string dest, OverwriteRule rule, string password,
            IProgress<ExtractProgress> progress, CancellationToken ct, Func<ArchiveEntry, string, OverwriteRule> ask = null)
        {
            var rules = new Conflicts { Rule = rule, Ask = ask, ByPath = info.Entries.GroupBy(e => e.Path, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal) };
            var res = new ExtractResult { Destination = dest };
            var picked = selected.Where(e => !e.IsDirectory).ToList();
            res.NotSelected = info.Entries.Count(e => !e.IsDirectory) - picked.Count;
            var set = new HashSet<string>(picked.Select(e => e.Path), StringComparer.Ordinal);
            long total = picked.Sum(e => e.Size);
            var prog = new ExtractProgress { Total = picked.Count, TotalBytes = total };
            try
            {
                Directory.CreateDirectory(dest);
                switch (info.Format)
                {
                    case "zip" when info.UseSevenZip: ExtractSevenStaged(info, picked, dest, rules, password, progress, prog, res, ct); break;
                    case "7z": ExtractSevenStaged(info, picked, dest, rules, password, progress, prog, res, ct); break;
                    case "zip": ExtractZip(info, picked, dest, rules, password, progress, prog, res, ct); break;
                    case "tar": ExtractTar(() => File.OpenRead(info.File), set, dest, rules, progress, prog, res, ct); break;
                    case "tar.gz": ExtractTar(() => new GZipStream(File.OpenRead(info.File), CompressionMode.Decompress), set, dest, rules, progress, prog, res, ct); break;
                    case "tar.xz": case "tar.bz2": ExtractTar(() => SevenZipStream(info.File, ct), set, dest, rules, progress, prog, res, ct); break;
                    case "gz": ExtractSingle(() => new GZipStream(File.OpenRead(info.File), CompressionMode.Decompress), picked, dest, rules, progress, prog, res, ct); break;
                    case "xz": case "bz2": ExtractSingle(() => SevenZipStream(info.File, ct), picked, dest, rules, progress, prog, res, ct); break;
                    default: res.Error = info.Unsupported ?? "This archive type is not supported here."; break;
                }
            }
            catch (OperationCanceledException) { res.Canceled = true; }
            catch (Exception) when (ct.IsCancellationRequested) { res.Canceled = true; }
            catch (WrongPasswordException) { res.WrongPassword = true; }
            catch (Exception ex) { res.Error = ex.Message; }
            return res;
        }

        private sealed class WrongPasswordException : Exception { }

        /// <summary>The rule for names that exist, the person's answer when it is Ask, and the archive entries by path (for the question).</summary>
        private sealed class Conflicts
        {
            public OverwriteRule Rule;
            public Func<ArchiveEntry, string, OverwriteRule> Ask;
            public Dictionary<string, ArchiveEntry> ByPath;
        }

        /// <summary>Tries the password on the first locked file without writing anything. Null means it works, "wrong" means it does not, anything else says why it cannot be tried.</summary>
        public static string CheckPassword(ArchiveInfo info, string password)
        {
            if (info.UseSevenZip || info.Format == "7z") return CheckSevenPassword(info, password);
            var e = info.Entries.FirstOrDefault(x => x.Encrypted && !x.IsDirectory);
            if (e == null) return null;
            try { using var s = OpenEncrypted(info, e.Raw ?? e.Path, password); return null; }
            catch (WrongPasswordException) { return "wrong"; }
            catch (NotSupportedException ex) { return ex.Message; }
            catch (Exception ex) { return ex.Message; }
        }

        /// <summary>Decides what to do with a target that may already exist. Returns the path to write, or null to skip.</summary>
        private static string Target(string full, Conflicts rules, ExtractResult res, string entryPath)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full));
            if (!File.Exists(full)) return full;
            var rule = rules.Rule;
            if (rule == OverwriteRule.Ask)
            {
                rules.ByPath.TryGetValue(entryPath, out var entry);
                rule = rules.Ask?.Invoke(entry ?? new ArchiveEntry { Path = entryPath }, full) ?? OverwriteRule.KeepBoth;
                if (rule == OverwriteRule.Ask) rule = OverwriteRule.KeepBoth;
            }
            switch (rule)
            {
                case OverwriteRule.Skip: res.Skipped++; return null;
                case OverwriteRule.Replace: return full;
                default:
                    var u = FileOps.Unique(full);
                    res.Renamed.Add(System.IO.Path.GetFileName(full) + " → " + System.IO.Path.GetFileName(u));
                    return u;
            }
        }

        private static void WriteOne(Stream source, string target, DateTime? modified, ExtractResult res, IProgress<ExtractProgress> progress, ExtractProgress prog, CancellationToken ct, string label)
        {
            var part = target + ".part";
            try
            {
                using (var o = File.Create(part))
                {
                    var buf = new byte[81920]; int n;
                    while ((n = source.Read(buf, 0, buf.Length)) > 0)
                    {
                        ct.ThrowIfCancellationRequested();
                        o.Write(buf, 0, n); prog.Bytes += n; res.Bytes += n;
                        progress?.Report(Snapshot(prog, label));
                    }
                }
                if (File.Exists(target)) File.Delete(target);
                File.Move(part, target);
                if (modified.HasValue) try { File.SetLastWriteTime(target, modified.Value); } catch { }
                res.Extracted++;
            }
            finally { try { if (File.Exists(part)) File.Delete(part); } catch { } }
        }

        private static ExtractProgress Snapshot(ExtractProgress p, string current) => new ExtractProgress { Done = p.Done, Total = p.Total, Current = current, Bytes = p.Bytes, TotalBytes = p.TotalBytes };

        private static void ExtractZip(ArchiveInfo info, List<ArchiveEntry> picked, string dest, Conflicts rule, string password, IProgress<ExtractProgress> progress, ExtractProgress prog, ExtractResult res, CancellationToken ct)
        {
            using var z = ZipFile.OpenRead(info.File);
            var byPath = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
            foreach (var x in z.Entries) byPath[Norm(x.FullName)] = x;
            foreach (var pe in picked)
            {
                ct.ThrowIfCancellationRequested();
                prog.Done++;
                if (!TrySafePath(dest, pe.Path, out var full)) { res.Refused.Add(pe.Path); continue; }
                if (!byPath.TryGetValue(pe.Path, out var ze)) continue;
                var target = Target(full, rule, res, pe.Path);
                if (target == null) { prog.Bytes += pe.Size; continue; }
                if (pe.Encrypted)
                {
                    using var s = OpenEncrypted(info, ze.FullName, password);
                    WriteOne(s, target, pe.Modified, res, progress, prog, ct, pe.Path);
                }
                else
                {
                    using var s = ze.Open();
                    WriteOne(s, target, pe.Modified, res, progress, prog, ct, pe.Path);
                }
            }
        }

        private static void ExtractTar(Func<Stream> open, HashSet<string> set, string dest, Conflicts rule, IProgress<ExtractProgress> progress, ExtractProgress prog, ExtractResult res, CancellationToken ct)
        {
            using Stream s = open();
            using var r = new TarReader(s);
            TarEntry e;
            while ((e = r.GetNextEntry(copyData: false)) != null)
            {
                ct.ThrowIfCancellationRequested();
                var path = Norm(e.Name);
                if (e.EntryType == TarEntryType.Directory || !set.Contains(path) || e.DataStream == null) continue;
                prog.Done++;
                if (!TrySafePath(dest, path, out var full)) { res.Refused.Add(path); continue; }
                var target = Target(full, rule, res, path);
                if (target == null) { prog.Bytes += e.Length; continue; }
                WriteOne(e.DataStream, target, e.ModificationTime.LocalDateTime, res, progress, prog, ct, path);
            }
            // a selected path that never appeared (an unsafe path the safe check dropped would have been refused above)
        }

        /// <summary>A single compressed file (gz, xz, bz2): the one entry is the decompressed stream.</summary>
        private static void ExtractSingle(Func<Stream> open, List<ArchiveEntry> picked, string dest, Conflicts rule, IProgress<ExtractProgress> progress, ExtractProgress prog, ExtractResult res, CancellationToken ct)
        {
            if (picked.Count == 0) return;
            var pe = picked[0]; prog.Done = 1;
            if (!TrySafePath(dest, pe.Path, out var full)) { res.Refused.Add(pe.Path); return; }
            var target = Target(full, rule, res, pe.Path);
            if (target == null) return;
            using var s = open();
            WriteOne(s, target, pe.Modified, res, progress, prog, ct, pe.Path);
        }

        // ------------------------------------------------------------------ the traditional zip password scheme

        private static readonly uint[] CrcTable = BuildCrc();
        private static uint[] BuildCrc() { var t = new uint[256]; for (uint i = 0; i < 256; i++) { uint c = i; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1; t[i] = c; } return t; }
        private static uint CrcUpdate(uint crc, byte b) => CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);

        private sealed class ZipCryptoKeys
        {
            private uint k0 = 0x12345678, k1 = 0x23456789, k2 = 0x34567890;
            public ZipCryptoKeys(string password) { foreach (var b in Encoding.UTF8.GetBytes(password ?? "")) Update(b); }
            private void Update(byte c) { k0 = CrcUpdate(k0, c); k1 = (k1 + (k0 & 0xFF)) * 134775813 + 1; k2 = CrcUpdate(k2, (byte)(k1 >> 24)); }
            public byte Decrypt(byte c) { ushort t = (ushort)(k2 | 2); byte p = (byte)(c ^ ((t * (t ^ 1)) >> 8)); Update(p); return p; }
            public byte Encrypt(byte p) { ushort t = (ushort)(k2 | 2); byte c = (byte)(p ^ ((t * (t ^ 1)) >> 8)); Update(p); return c; }
        }

        /// <summary>Opens one password-protected zip entry as plain bytes. Wrong password raises <see cref="WrongPasswordException"/>.</summary>
        private static Stream OpenEncrypted(ArchiveInfo info, string name, string password)
        {
            if (info.Central == null || !info.Central.TryGetValue(name, out var c)) throw new InvalidDataException("the entry's header could not be read");
            if (c.Method == 99) throw new NotSupportedException("this archive uses AES encryption, which is read through 7-Zip, and 7-Zip is missing from this copy of the app");
            if (string.IsNullOrEmpty(password)) throw new WrongPasswordException();
            byte[] raw;
            using (var fs = File.OpenRead(info.File))
            {
                fs.Seek(c.LocalOffset, SeekOrigin.Begin);
                var lh = new byte[30]; fs.ReadExactly(lh, 0, 30);
                if (BitConverter.ToUInt32(lh, 0) != 0x04034b50) throw new InvalidDataException("a local header is damaged");
                int nl = BitConverter.ToUInt16(lh, 26), el = BitConverter.ToUInt16(lh, 28);
                fs.Seek(c.LocalOffset + 30 + nl + el, SeekOrigin.Begin);
                raw = new byte[c.CSize]; fs.ReadExactly(raw, 0, raw.Length);
            }
            if (raw.Length < 12) throw new InvalidDataException("the encrypted data is too short");
            var keys = new ZipCryptoKeys(password);
            var plain = new byte[raw.Length - 12];
            byte last = 0;
            for (int i = 0; i < raw.Length; i++) { var d = keys.Decrypt(raw[i]); if (i < 12) last = d; else plain[i - 12] = d; }
            byte expect = (c.Flags & 8) != 0 ? (byte)(c.Time >> 8) : (byte)(c.Crc >> 24);
            if (last != expect) throw new WrongPasswordException();
            byte[] data;
            if (c.Method == 0) data = plain;
            else if (c.Method == 8)
            {
                try
                {
                    using var ms = new MemoryStream(plain); using var ds = new DeflateStream(ms, CompressionMode.Decompress); using var o = new MemoryStream();
                    ds.CopyTo(o); data = o.ToArray();
                }
                catch (InvalidDataException) { throw new WrongPasswordException(); }
            }
            else throw new NotSupportedException("this zip entry uses compression method " + c.Method + ", which is not supported here");
            uint crc = 0xFFFFFFFF; foreach (var b in data) crc = CrcUpdate(crc, b); crc ^= 0xFFFFFFFF;
            if (crc != c.Crc) throw new WrongPasswordException();
            return new MemoryStream(data, false);
        }

        /// <summary>Test helper: makes a zip whose entries use the traditional password scheme (stored, not compressed).</summary>
        internal static void WritePasswordZip(string file, string password, params (string Name, byte[] Data)[] entries)
        {
            using var fs = File.Create(file);
            using var w = new BinaryWriter(fs);
            var central = new List<(string name, ushort time, ushort date, uint crc, uint size, uint off)>();
            ushort time = 0x6000, date = 0x5821;
            foreach (var (name, data) in entries)
            {
                uint crc = 0xFFFFFFFF; foreach (var b in data) crc = CrcUpdate(crc, b); crc ^= 0xFFFFFFFF;
                var keys = new ZipCryptoKeys(password);
                var header = new byte[12]; new Random(7).NextBytes(header); header[11] = (byte)(crc >> 24);
                var enc = new byte[12 + data.Length];
                for (int i = 0; i < 12; i++) enc[i] = keys.Encrypt(header[i]);
                for (int i = 0; i < data.Length; i++) enc[12 + i] = keys.Encrypt(data[i]);
                var nb = Encoding.UTF8.GetBytes(name);
                uint off = (uint)fs.Position;
                w.Write(0x04034b50u); w.Write((ushort)20); w.Write((ushort)(1 | 0x800)); w.Write((ushort)0); w.Write(time); w.Write(date);
                w.Write(crc); w.Write((uint)enc.Length); w.Write((uint)data.Length); w.Write((ushort)nb.Length); w.Write((ushort)0); w.Write(nb); w.Write(enc);
                central.Add((name, time, date, crc, (uint)data.Length, off));
            }
            uint cdStart = (uint)fs.Position;
            foreach (var (name, t, d, crc, size, off) in central)
            {
                var nb = Encoding.UTF8.GetBytes(name);
                w.Write(0x02014b50u); w.Write((ushort)20); w.Write((ushort)20); w.Write((ushort)(1 | 0x800)); w.Write((ushort)0); w.Write(t); w.Write(d);
                w.Write(crc); w.Write(size + 12); w.Write(size); w.Write((ushort)nb.Length); w.Write((ushort)0); w.Write((ushort)0); w.Write((ushort)0); w.Write((ushort)0);
                w.Write(0u); w.Write(off); w.Write(nb);
            }
            uint cdEnd = (uint)fs.Position;
            w.Write(0x06054b50u); w.Write((ushort)0); w.Write((ushort)0); w.Write((ushort)central.Count); w.Write((ushort)central.Count); w.Write(cdEnd - cdStart); w.Write(cdStart); w.Write((ushort)0);
        }
    }
}
