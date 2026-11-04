using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MonitoringTools.Core
{
    public sealed class SettingsSnapshot
    {
        public DateTime At { get; set; }
        public string Summary { get; set; } = "";
        public JsonElement Settings { get; set; }
    }

    public sealed class SettingsChange
    {
        public string Label, Old, New;
    }

    /// <summary>Keeps a snapshot of the settings each time a save changes them, with a one-line summary of what changed.</summary>
    public static class SettingsHistory
    {
        public const int Cap = 200;
        private static readonly object Gate = new object();
        private static readonly JsonSerializerOptions Opts = new JsonSerializerOptions { WriteIndented = true };
        private static string _last;
        private static bool _primed;
        public static event Action Changed;

        public static string Dir
        {
            get
            {
                var d = Path.Combine(Settings.DataDir, "settings-history");
                Directory.CreateDirectory(d);
                return d;
            }
        }

        /// <summary>Called after every save. Stores a snapshot only when the serialized settings differ from the last one.</summary>
        public static void Record(Settings s)
        {
            try
            {
                var now = Normalize(JsonSerializer.Serialize(s));
                lock (Gate)
                {
                    if (!_primed)
                    {
                        _primed = true;
                        var newest = Directory.GetFiles(Dir, "*.json").OrderByDescending(f => f).FirstOrDefault();
                        if (newest != null) { var snap0 = Load(newest); if (snap0 != null) _last = Normalize(snap0.Settings.GetRawText()); }
                    }
                    if (_last == now) return;
                    string summary;
                    if (_last == null) summary = "First saved settings";
                    else
                    {
                        using var a = JsonDocument.Parse(_last);
                        using var b = JsonDocument.Parse(now);
                        summary = Summarize(Diff(a.RootElement, b.RootElement));
                    }
                    using var cur = JsonDocument.Parse(now);
                    var snap = new Dictionary<string, object>
                    {
                        ["At"] = DateTime.Now,
                        ["Summary"] = summary,
                        ["Settings"] = cur.RootElement.Clone()
                    };
                    var name = DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + ".json";
                    File.WriteAllText(Path.Combine(Dir, name), JsonSerializer.Serialize(snap, Opts));
                    _last = now;
                    var files = Directory.GetFiles(Dir, "*.json").OrderBy(f => f).ToList();
                    for (int i = 0; i < files.Count - Cap; i++) { try { File.Delete(files[i]); } catch { } }
                }
                Changed?.Invoke();
            }
            catch (Exception ex) { Log.Warn("Settings history failed: " + ex.Message); }
        }

        private static string Normalize(string json)
        {
            using var d = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(d.RootElement);
        }

        /// <summary>Newest first.</summary>
        public static List<(string File, SettingsSnapshot Snap)> List()
        {
            var list = new List<(string, SettingsSnapshot)>();
            foreach (var f in Directory.GetFiles(Dir, "*.json").OrderByDescending(x => x))
            {
                var s = Load(f);
                if (s != null) list.Add((f, s));
            }
            return list;
        }

        /// <summary>
        /// Deletes the chosen snapshot files. Only files inside the history folder are touched, and the newest snapshot (the
        /// current state) is always kept. Returns how many were deleted.
        /// </summary>
        public static int Delete(IEnumerable<string> files)
        {
            int n = 0;
            lock (Gate)
            {
                var dir = Path.GetFullPath(Dir);
                var newest = Directory.GetFiles(Dir, "*.json").OrderByDescending(f => f).FirstOrDefault();
                foreach (var f in files.ToList())
                {
                    var full = Path.GetFullPath(f);
                    if (!string.Equals(Path.GetDirectoryName(full), dir, StringComparison.OrdinalIgnoreCase)) continue;
                    if (newest != null && string.Equals(full, Path.GetFullPath(newest), StringComparison.OrdinalIgnoreCase)) continue;
                    try { if (File.Exists(full)) { File.Delete(full); n++; } } catch (Exception ex) { Log.Warn("Snapshot not deleted: " + ex.Message); }
                }
            }
            if (n > 0) Changed?.Invoke();
            return n;
        }

        /// <summary>The chosen snapshots (time, summary and full settings) as one JSON array, oldest first.</summary>
        public static string ExportJson(IEnumerable<string> files)
        {
            var rows = files.Select(Load).Where(s => s != null).OrderBy(s => s.At).Select(s => new { s.At, s.Summary, s.Settings }).ToList();
            return JsonSerializer.Serialize(rows, Opts);
        }

        public static SettingsSnapshot Load(string file)
        {
            try { return JsonSerializer.Deserialize<SettingsSnapshot>(File.ReadAllText(file)); }
            catch { return null; }
        }

        /// <summary>Property-by-property differences between two serialized settings objects.</summary>
        public static List<SettingsChange> Diff(JsonElement before, JsonElement after)
        {
            var list = new List<SettingsChange>();
            var names = new List<string>();
            if (after.ValueKind == JsonValueKind.Object) names.AddRange(after.EnumerateObject().Select(p => p.Name));
            if (before.ValueKind == JsonValueKind.Object) names.AddRange(before.EnumerateObject().Select(p => p.Name).Where(n => !names.Contains(n)));
            foreach (var n in names)
            {
                string a = Show(before, n), b = Show(after, n);
                if (a != b) list.Add(new SettingsChange { Label = Label(n), Old = a, New = b });
            }
            return list;
        }

        private static string Show(JsonElement obj, string name)
        {
            if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var v)) return "(not set)";
            var raw = v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText();
            if (raw.Length == 0) return "(empty)";
            return raw.Length > 80 ? raw.Substring(0, 80) + "…" : raw;
        }

        /// <summary>"ProbeIntervalSeconds" becomes "Probe interval seconds".</summary>
        public static string Label(string property)
        {
            var spaced = Regex.Replace(property, "(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", " ");
            return spaced.Length == 0 ? property : char.ToUpperInvariant(spaced[0]) + spaced.Substring(1).ToLowerInvariant();
        }

        public static string Summarize(List<SettingsChange> changes)
        {
            if (changes.Count == 0) return "No visible changes";
            var sb = new StringBuilder();
            sb.Append(string.Join("; ", changes.Take(3).Select(c => $"{c.Label}: {c.Old} → {c.New}")));
            if (changes.Count > 3) sb.Append($"; +{changes.Count - 3} more");
            return sb.ToString();
        }

        /// <summary>Settings parsed from a snapshot, or null when it cannot be read.</summary>
        public static Settings Restore(SettingsSnapshot snap)
        {
            try { return JsonSerializer.Deserialize<Settings>(snap.Settings.GetRawText()); }
            catch (Exception ex) { Log.Warn("Snapshot unreadable: " + ex.Message); return null; }
        }
    }
}
