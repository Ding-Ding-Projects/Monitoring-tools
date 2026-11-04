using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MonitoringTools.Minecraft
{
    /// <summary>
    /// The most players each server had online on each day, kept for 30 days in player-peaks.json beside servers.json. Every
    /// "list" answer the app reads feeds it, so the Overview can show the peak today and over the last 7 days. A day with no
    /// reading is a day nothing was known, not a day with nobody online, and is shown that way.
    /// </summary>
    public static class McPeaks
    {
        private static readonly object Gate = new object();
        private static Dictionary<string, Dictionary<string, int>> _data;
        private static string _loadedFrom;

        public static string FilePath => Path.Combine(ServerManager.Root, "player-peaks.json");

        private static string Key(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        private static Dictionary<string, Dictionary<string, int>> Data()
        {
            if (_data != null && _loadedFrom == FilePath) return _data;
            _loadedFrom = FilePath;
            try { _data = File.Exists(FilePath) ? JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>(File.ReadAllText(FilePath)) ?? new() : new(); }
            catch { _data = new Dictionary<string, Dictionary<string, int>>(); }
            return _data;
        }

        /// <summary>Raises the day's peak when this reading is higher, and saves when it changed.</summary>
        public static void Record(string serverId, int online, DateTime? when = null)
        {
            if (string.IsNullOrEmpty(serverId) || online < 0) return;
            var day = Key(when ?? DateTime.Now);
            lock (Gate)
            {
                var d = Data();
                if (!d.TryGetValue(serverId, out var days)) d[serverId] = days = new Dictionary<string, int>();
                bool changed = !days.TryGetValue(day, out var old) || online > old;
                if (!changed) return;
                days[day] = online;
                var cutoff = Key((when ?? DateTime.Now).AddDays(-30));
                foreach (var stale in days.Keys.Where(k => string.CompareOrdinal(k, cutoff) < 0).ToList()) days.Remove(stale);
                try { Directory.CreateDirectory(Path.GetDirectoryName(FilePath)); File.WriteAllText(FilePath, JsonSerializer.Serialize(d)); } catch { /* the history is a convenience; a read-only folder loses it, not the app */ }
            }
        }

        /// <summary>The last <paramref name="days"/> days ending today, oldest first, with null where nothing was read.</summary>
        public static List<(DateTime Day, int? Peak)> Last(string serverId, int days = 7, DateTime? today = null)
        {
            var end = (today ?? DateTime.Now).Date;
            lock (Gate)
            {
                Data().TryGetValue(serverId ?? "", out var map);
                return Enumerable.Range(0, days).Select(i => end.AddDays(i - days + 1)).Select(d => (d, map != null && map.TryGetValue(Key(d), out var v) ? (int?)v : null)).ToList();
            }
        }

        public static int? PeakToday(string serverId, DateTime? today = null) => Last(serverId, 1, today)[0].Peak;

        public static int? Peak7Days(string serverId, DateTime? today = null)
        {
            var seen = Last(serverId, 7, today).Where(x => x.Peak != null).Select(x => x.Peak.Value).ToList();
            return seen.Count == 0 ? null : seen.Max();
        }

        /// <summary>Sample servers show fixed sample history; nothing was read from a real server for them.</summary>
        public static List<(DateTime Day, int? Peak)> Sample(DateTime? today = null)
        {
            var end = (today ?? DateTime.Now).Date;
            var v = new int?[] { 4, 6, 3, null, 7, 5, 8 };
            return v.Select((p, i) => (end.AddDays(i - 6), p)).ToList();
        }

        /// <summary>Forgets what was loaded, so the next read comes from the file (used when the data folder changes).</summary>
        public static void Reload() { lock (Gate) { _data = null; } }
    }
}
