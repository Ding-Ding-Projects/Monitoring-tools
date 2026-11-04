using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

namespace MonitoringTools.Core
{
    /// <summary>
    /// Scheduled settings and the external settings file. Both produce overlays that sit on top of the
    /// saved settings and are never written back into them, so the base choice returns when a rule ends.
    /// Precedence, lowest to highest: saved setting, external file, scheduled rule (later rule wins), School mode.
    /// </summary>
    public static partial class Modes
    {
        public static readonly string[] SettingKeys = { "language", "theme", "sounds", "toasts", "gamemode" };
        public static readonly string[] LanguageValues = { "en", "yue", "bilingual" };
        public static readonly string[] ThemeValues = { "system", "light", "dark" };
        public const long ExternalMaxBytes = 64 * 1024;

        private static readonly Dictionary<string, string> _sched = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> _ext = new Dictionary<string, string>();
        private static DispatcherTimer _tick;

        private static string Overlay(string key)
            => _sched.TryGetValue(key, out var v) ? v : _ext.TryGetValue(key, out var e) ? e : null;
        private static bool? OverlayBool(string key)
        {
            var v = Overlay(key);
            return v == null ? (bool?)null : v == "on";
        }

        // ------------------------------------------------------------ effective values the app should consult

        /// <summary>Sound effects on, after the schedule, the external file and School mode.</summary>
        public static bool SoundsOn => !SchoolActive && (OverlayBool("sounds") ?? Settings.Current.SoundsEnabled);
        /// <summary>Clipboard toasts on, after the schedule and the external file.</summary>
        public static bool ClipboardToastsOn => OverlayBool("toasts") ?? Settings.Current.ClipboardEnabled;
        /// <summary>Game mode on. Forced on while School mode is active.</summary>
        public static bool GameModeOn => SchoolActive || (OverlayBool("gamemode") ?? Settings.Current.GameModeEnabled);
        /// <summary>en, yue or bilingual when a rule or the file sets it, otherwise null (use the saved choice). English while School mode is on.</summary>
        public static string ScheduledLanguage => SchoolActive ? "en" : Overlay("language");
        /// <summary>system, light or dark when a rule or the file sets it, otherwise null (use the saved choice).</summary>
        public static string ScheduledTheme => Overlay("theme");

        // ------------------------------------------------------------ scheduler

        private static void StartScheduler()
        {
            Evaluate();
            _tick = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            _tick.Tick += (a, b) => Evaluate();
            _tick.Start();
        }

        /// <summary>Re-evaluate every rule now. Called each minute and after any rule edit.</summary>
        public static void Evaluate()
        {
            var next = new Dictionary<string, string>();
            var now = DateTime.Now;
            foreach (var r in Settings.Current.ScheduleRules)           // later rules win
                if (Matches(r, now)) next[r.Setting] = r.Value;
            bool same = next.Count == _sched.Count && next.All(kv => _sched.TryGetValue(kv.Key, out var v) && v == kv.Value);
            if (same) return;
            _sched.Clear();
            foreach (var kv in next) _sched[kv.Key] = kv.Value;
            NotifyChanged();
        }

        public static bool TryParseTime(string text, out int minutes)
        {
            minutes = 0;
            if (text == null) return false;
            var p = text.Trim().Split(':');
            if (p.Length != 2 || !int.TryParse(p[0], out var h) || !int.TryParse(p[1], out var m)) return false;
            if (h < 0 || h > 23 || m < 0 || m > 59 || p[1].Length != 2) return false;
            minutes = h * 60 + m;
            return true;
        }

        public static bool ValidValue(string setting, string value)
        {
            switch (setting)
            {
                case "language": return LanguageValues.Contains(value);
                case "theme": return ThemeValues.Contains(value);
                case "sounds": case "toasts": case "gamemode": return value == "on" || value == "off";
                default: return false;
            }
        }

        /// <summary>
        /// Local wall-clock semantics: the window is [start, end) on the machine's clock. Equal start and end
        /// mean the whole day. A window that crosses midnight belongs to the day it starts on. Daylight-saving
        /// changes follow the wall clock, so a window inside a skipped hour simply does not occur that day.
        /// </summary>
        public static bool Matches(ScheduleRule r, DateTime now)
        {
            if (r == null || !r.Enabled || !SettingKeys.Contains(r.Setting) || !ValidValue(r.Setting, r.Value)) return false;
            if (!TryParseTime(r.Start, out var s) || !TryParseTime(r.End, out var e)) return false;
            if (!ValidDateRange(r.FromDate, r.ToDate)) return false;
            bool Day(DateTime d) => (r.Days == null || r.Days.Count == 0 || r.Days.Contains((int)d.DayOfWeek)) && InDateRange(r, d);
            int m = now.Hour * 60 + now.Minute;
            if (s == e) return Day(now);
            if (s < e) return m >= s && m < e && Day(now);
            if (m >= s) return Day(now);
            return m < e && Day(now.AddDays(-1));
        }

        /// <summary>Parses an optional yyyy-MM-dd date. Empty text is valid and gives null.</summary>
        public static bool TryParseDate(string text, out DateTime? date)
        {
            date = null;
            if (string.IsNullOrWhiteSpace(text)) return true;
            if (!DateTime.TryParseExact(text.Trim(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d)) return false;
            date = d.Date;
            return true;
        }

        /// <summary>True when both optional dates parse and the end is not before the start.</summary>
        public static bool ValidDateRange(string from, string to)
        {
            if (!TryParseDate(from, out var f) || !TryParseDate(to, out var t)) return false;
            return f == null || t == null || f <= t;
        }

        /// <summary>The calendar day (not the clock) must fall in the inclusive range; a missing limit is open-ended.</summary>
        public static bool InDateRange(ScheduleRule r, DateTime day)
        {
            TryParseDate(r.FromDate, out var f); TryParseDate(r.ToDate, out var t);
            var d = day.Date;
            return (f == null || d >= f) && (t == null || d <= t);
        }

        public static string DescribeRule(ScheduleRule r)
        {
            string days = r.Days == null || r.Days.Count == 0 ? "every day"
                : string.Join(", ", r.Days.OrderBy(d => (d + 6) % 7).Select(d => ((DayOfWeek)d).ToString().Substring(0, 3)));
            string when = r.Start == r.End ? "all day" : $"{r.Start} to {r.End}";
            string range = !string.IsNullOrWhiteSpace(r.FromDate) && !string.IsNullOrWhiteSpace(r.ToDate) ? $", {r.FromDate} to {r.ToDate}"
                : !string.IsNullOrWhiteSpace(r.FromDate) ? $", from {r.FromDate}" : !string.IsNullOrWhiteSpace(r.ToDate) ? $", until {r.ToDate}" : "";
            return $"{when}, {days}{range}: {r.Setting} = {r.Value}";
        }

        /// <summary>True when this rule is the one currently deciding its setting.</summary>
        public static bool IsWinning(ScheduleRule r) => _sched.TryGetValue(r.Setting, out var v) && Matches(r, DateTime.Now) && v == r.Value
            && Settings.Current.ScheduleRules.LastOrDefault(x => Matches(x, DateTime.Now) && x.Setting == r.Setting) == r;

        // ------------------------------------------------------------ external settings file

        private static FileSystemWatcher _extWatch;
        private static DispatcherTimer _extDebounce;

        /// <summary>One line describing the last read of the external file.</summary>
        public static string ExternalStatus { get; private set; } = "Not following a file.";
        public static bool ExternalOk { get; private set; } = true;

        public static void StartFollowing(string path, bool save = true)
        {
            StopWatcher();
            var s = Settings.Current;
            s.ExternalSettingsPath = path; s.ExternalFollowing = true;
            if (save) Settings.Save();
            try
            {
                var dir = Path.GetDirectoryName(Path.GetFullPath(path));
                _extWatch = new FileSystemWatcher(dir, Path.GetFileName(path)) { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName };
                FileSystemEventHandler h = (a, b) => Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _extDebounce?.Stop();
                    _extDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                    _extDebounce.Tick += (x, y) => { _extDebounce.Stop(); ApplyExternalNow(); };
                    _extDebounce.Start();
                }));
                _extWatch.Changed += h; _extWatch.Created += h; _extWatch.Renamed += (a, b) => h(a, null);
                _extWatch.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                ExternalOk = false;
                ExternalStatus = "Cannot watch that file for changes (" + ex.Message + "). Use Apply now after editing it.";
            }
            ApplyExternalNow();
        }

        public static void StopFollowing()
        {
            StopWatcher();
            var s = Settings.Current;
            s.ExternalFollowing = false;
            Settings.Save();
            ExternalOk = true;
            ExternalStatus = "Not following a file. Your saved settings are in effect.";
            if (_ext.Count > 0) { _ext.Clear(); NotifyChanged(); }
        }

        private static void StopWatcher()
        {
            if (_extWatch != null) { _extWatch.EnableRaisingEvents = false; _extWatch.Dispose(); _extWatch = null; }
            _extDebounce?.Stop();
        }

        /// <summary>
        /// Read, validate and apply the file now. Any problem leaves the previous valid values in place
        /// and says so; nothing from the file is written into the saved settings.
        /// </summary>
        public static void ApplyExternalNow()
        {
            var path = Settings.Current.ExternalSettingsPath;
            try
            {
                if (string.IsNullOrWhiteSpace(path)) throw new InvalidDataException("no file chosen");
                var info = new FileInfo(path);
                if (!info.Exists) throw new FileNotFoundException("the file does not exist");
                if (info.Length > ExternalMaxBytes) throw new InvalidDataException($"the file is larger than {ExternalMaxBytes / 1024} KB");
                var parsed = ParseExternal(File.ReadAllText(path), out var ignored);
                _ext.Clear();
                foreach (var kv in parsed) _ext[kv.Key] = kv.Value;
                ExternalOk = true;
                ExternalStatus = $"Applied {parsed.Count} setting{(parsed.Count == 1 ? "" : "s")} from the file at {DateTime.Now:HH:mm:ss}"
                    + (ignored > 0 ? $"; ignored {ignored} unknown key{(ignored == 1 ? "" : "s")}." : ".");
            }
            catch (Exception ex)
            {
                ExternalOk = false;
                ExternalStatus = "Not applied: " + ex.Message + ". The last valid values stay in effect.";
                Log.Warn("External settings: " + ExternalStatus);
            }
            NotifyChanged();
        }

        /// <summary>Schema: {"version":1,"settings":{"language":"en|yue|bilingual","theme":"system|light|dark","sounds":bool,"toasts":bool,"gameMode":bool}}.</summary>
        public static Dictionary<string, string> ParseExternal(string json, out int ignored)
        {
            ignored = 0;
            var result = new Dictionary<string, string>();
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 4, AllowTrailingCommas = false });
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("the top level must be an object");
            if (!root.TryGetProperty("version", out var ver) || ver.ValueKind != JsonValueKind.Number || ver.GetInt32() != 1)
                throw new InvalidDataException("\"version\" must be 1");
            if (!root.TryGetProperty("settings", out var set) || set.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("\"settings\" must be an object");
            foreach (var p in set.EnumerateObject())
            {
                var key = p.Name.ToLowerInvariant();
                if (!SettingKeys.Contains(key)) { ignored++; continue; }
                string value;
                if (key == "language" || key == "theme")
                {
                    if (p.Value.ValueKind != JsonValueKind.String) throw new InvalidDataException($"\"{p.Name}\" must be a string");
                    value = p.Value.GetString();
                }
                else
                {
                    if (p.Value.ValueKind != JsonValueKind.True && p.Value.ValueKind != JsonValueKind.False) throw new InvalidDataException($"\"{p.Name}\" must be true or false");
                    value = p.Value.GetBoolean() ? "on" : "off";
                }
                if (!ValidValue(key, value)) throw new InvalidDataException($"\"{p.Name}\" has an unsupported value");
                result[key] = value;
            }
            return result;
        }
    }
}
