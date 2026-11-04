using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MonitoringTools.Core
{
    /// <summary>One saved version of a script or template.</summary>
    public sealed record HistoryVersion(string Id, DateTime Time, long Bytes);

    /// <summary>One line of a comparison. Op is ' ' (same), '+' (only in the newer text) or '-' (only in the older text).</summary>
    public readonly record struct DiffLine(char Op, string Text);

    /// <summary>
    /// Local history for texts the person edits in this app: every save keeps a versioned copy in the app data folder, capped
    /// at 50 per item, oldest dropped first. A copy identical to the newest one is not stored twice. Nothing leaves this computer.
    /// </summary>
    public static class ScriptHistory
    {
        /// <summary>The default number of versions kept per item. The person's own limit is <see cref="KeepCount"/>.</summary>
        public const int Cap = 50;

        public static int KeepCount => Math.Clamp(Settings.Current?.HistoryKeepCount ?? Cap, 5, 500);
        public static int KeepDays => Math.Clamp(Settings.Current?.HistoryKeepDays ?? 0, 0, 3650);

        /// <summary>True when versions are encrypted and the password has not been typed.</summary>
        public static bool IsLocked => HistoryVault.IsLocked;

        private static string Root => Path.Combine(Settings.DataDir, "history");

        private static string SafeKey(string key)
        {
            var k = Regex.Replace(key ?? "", @"[^A-Za-z0-9._-]", "_");
            return k.Length == 0 ? "_" : k.Length > 80 ? k.Substring(0, 80) : k;
        }

        private static string Dir(string key) => Path.Combine(Root, SafeKey(key));

        /// <summary>Key for a shortcut script.</summary>
        public static string ScriptKey(string name) => "script-" + (name ?? "").ToLowerInvariant();

        public const string CommitTemplateKey = "commit-template";

        /// <summary>Stores the text as a new version. Returns false when it matches the newest version or could not be written.</summary>
        public static bool Save(string key, string text)
        {
            try
            {
                text ??= "";
                var dir = Dir(key);
                Directory.CreateDirectory(dir);
                if (HistoryVault.IsLocked) { Log.Warn("The history is locked, so this version was not kept."); return false; }
                var newest = Files(dir).FirstOrDefault();
                if (newest != null && ReadFile(newest) == text) return false;
                var stamp = DateTime.UtcNow;
                // Versions sort by their name, so a new one must never be named earlier than the newest.
                if (newest != null && DateTime.TryParseExact(Path.GetFileNameWithoutExtension(newest), "yyyyMMddHHmmssfff", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var last) && last >= stamp)
                    stamp = last.AddMilliseconds(1);
                string stampName = stamp.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
                if (HistoryVault.Encrypts)
                {
                    var sealedBytes = HistoryVault.Encrypt(text);
                    if (sealedBytes == null) { Log.Warn("The history key is unavailable, so this version was not kept."); return false; }
                    File.WriteAllBytes(Path.Combine(dir, stampName + ".enc"), sealedBytes);
                }
                else File.WriteAllText(Path.Combine(dir, stampName + ".txt"), text, new UTF8Encoding(false));
                Prune(key);
                return true;
            }
            catch (Exception ex) { Log.Warn("Could not keep a history copy: " + ex.Message); return false; }
        }

        private static IEnumerable<string> Files(string dir) =>
            Directory.Exists(dir) ? Directory.GetFiles(dir).Where(f => f.EndsWith(".txt") || f.EndsWith(".enc")).OrderByDescending(f => Path.GetFileName(f), StringComparer.Ordinal) : Enumerable.Empty<string>();

        /// <summary>Reads one stored version, decrypting it when it is encrypted. Null when it is encrypted and cannot be opened.</summary>
        internal static string ReadFile(string path)
        {
            try { return path.EndsWith(".enc") ? HistoryVault.Decrypt(File.ReadAllBytes(path)) : File.ReadAllText(path); }
            catch { return null; }
        }

        /// <summary>Applies the person's retention to one item: the newest versions up to the count, and nothing older than the age limit. The newest version always stays.</summary>
        public static int Prune(string key)
        {
            int removed = 0;
            var files = Files(Dir(key)).ToList();
            var cutoff = KeepDays > 0 ? DateTime.UtcNow.AddDays(-KeepDays) : DateTime.MinValue;
            for (int i = 1; i < files.Count; i++)
            {
                bool tooMany = i >= KeepCount;
                bool tooOld = false;
                if (KeepDays > 0 && DateTime.TryParseExact(Path.GetFileNameWithoutExtension(files[i]), "yyyyMMddHHmmssfff", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var t)) tooOld = t < cutoff;
                if (tooMany || tooOld) { try { File.Delete(files[i]); removed++; } catch { } }
            }
            return removed;
        }

        /// <summary>Applies retention to every item.</summary>
        public static int PruneAll() => Items().Sum(i => Prune(i.Key));

        /// <summary>Every item that has a history: its folder name and how many versions it keeps.</summary>
        public static List<(string Key, int Versions)> Items() =>
            Directory.Exists(Root) ? Directory.GetDirectories(Root).Select(d => (Path.GetFileName(d), Files(d).Count())).Where(x => x.Item2 > 0).OrderBy(x => x.Item1, StringComparer.Ordinal).ToList() : new List<(string, int)>();

        /// <summary>Removes every saved version of one item. The caller confirms first.</summary>
        public static bool Delete(string key)
        {
            try { var d = Dir(key); if (Directory.Exists(d)) Directory.Delete(d, true); return true; } catch { return false; }
        }

        /// <summary>The saved versions, newest first.</summary>
        public static List<HistoryVersion> List(string key)
        {
            var list = new List<HistoryVersion>();
            foreach (var f in Files(Dir(key)))
            {
                var id = Path.GetFileNameWithoutExtension(f);
                if (!DateTime.TryParseExact(id, "yyyyMMddHHmmssfff", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var t)) continue;
                try { list.Add(new HistoryVersion(id, t.ToLocalTime(), new FileInfo(f).Length)); } catch { }
            }
            return list;
        }

        public static string Read(string key, string id)
        {
            if (!Regex.IsMatch(id ?? "", @"^\d{17}$")) return null;
            var dir = Dir(key);
            var p = Path.Combine(dir, id + ".enc");
            if (File.Exists(p)) return ReadFile(p);
            p = Path.Combine(dir, id + ".txt");
            return File.Exists(p) ? ReadFile(p) : null;
        }

        /// <summary>
        /// A line comparison from <paramref name="from"/> to <paramref name="to"/>: '+' lines would appear and '-' lines would disappear.
        /// Very large inputs are compared by whole-line prefix and suffix only, and the middle is shown as one replaced block.
        /// </summary>
        public static List<DiffLine> Diff(string from, string to)
        {
            var a = Split(from); var b = Split(to);
            var result = new List<DiffLine>();
            int start = 0;
            while (start < a.Length && start < b.Length && a[start] == b[start]) start++;
            int endA = a.Length, endB = b.Length;
            while (endA > start && endB > start && a[endA - 1] == b[endB - 1]) { endA--; endB--; }
            for (int i = 0; i < start; i++) result.Add(new DiffLine(' ', a[i]));
            int n = endA - start, m = endB - start;
            if ((long)n * m > 4_000_000)
            {
                for (int i = start; i < endA; i++) result.Add(new DiffLine('-', a[i]));
                for (int j = start; j < endB; j++) result.Add(new DiffLine('+', b[j]));
            }
            else
            {
                var lcs = new int[n + 1, m + 1];
                for (int i = n - 1; i >= 0; i--)
                    for (int j = m - 1; j >= 0; j--)
                        lcs[i, j] = a[start + i] == b[start + j] ? lcs[i + 1, j + 1] + 1 : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
                int x = 0, y = 0;
                while (x < n && y < m)
                {
                    if (a[start + x] == b[start + y]) { result.Add(new DiffLine(' ', a[start + x])); x++; y++; }
                    else if (lcs[x + 1, y] >= lcs[x, y + 1]) result.Add(new DiffLine('-', a[start + x++]));
                    else result.Add(new DiffLine('+', b[start + y++]));
                }
                while (x < n) result.Add(new DiffLine('-', a[start + x++]));
                while (y < m) result.Add(new DiffLine('+', b[start + y++]));
            }
            for (int i = endA; i < a.Length; i++) result.Add(new DiffLine(' ', a[i]));
            return result;
        }

        private static string[] Split(string s) => (s ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
    }
}
