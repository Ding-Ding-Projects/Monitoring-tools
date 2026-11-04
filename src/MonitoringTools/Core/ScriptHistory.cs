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
        public const int Cap = 50;

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
                var newest = Files(dir).FirstOrDefault();
                if (newest != null && File.ReadAllText(newest) == text) return false;
                var stamp = DateTime.UtcNow;
                // Versions sort by their name, so a new one must never be named earlier than the newest.
                if (newest != null && DateTime.TryParseExact(Path.GetFileNameWithoutExtension(newest), "yyyyMMddHHmmssfff", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var last) && last >= stamp)
                    stamp = last.AddMilliseconds(1);
                string path = Path.Combine(dir, stamp.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture) + ".txt");
                File.WriteAllText(path, text, new UTF8Encoding(false));
                foreach (var old in Files(dir).Skip(Cap)) { try { File.Delete(old); } catch { } }
                return true;
            }
            catch (Exception ex) { Log.Warn("Could not keep a history copy: " + ex.Message); return false; }
        }

        private static IEnumerable<string> Files(string dir) =>
            Directory.Exists(dir) ? Directory.GetFiles(dir, "*.txt").OrderByDescending(f => Path.GetFileName(f), StringComparer.Ordinal) : Enumerable.Empty<string>();

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
            var p = Path.Combine(Dir(key), id + ".txt");
            return File.Exists(p) ? File.ReadAllText(p) : null;
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
