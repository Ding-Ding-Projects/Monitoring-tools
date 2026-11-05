using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MonitoringTools.Core.DocTools
{
    public enum SubtitleFormat { Srt, Vtt }

    public sealed class Cue
    {
        public int Number;
        /// <summary>Milliseconds from the start; -1 when the time line could not be read.</summary>
        public long Start = -1, End = -1;
        public string Text = "";
        /// <summary>The time line as written, kept so a broken cue can be shown as it was.</summary>
        public string RawTimes = "";
        public string Settings = "";     // WebVTT cue settings (position, align...), dropped when converting to SRT
        public bool Broken => Start < 0 || End < 0;
    }

    public sealed class SubtitleFile
    {
        public SubtitleFormat Format;
        public List<Cue> Cues = new List<Cue>();
        public List<string> Dropped = new List<string>();   // things that cannot be kept in the other format
        public bool HadBom;
    }

    /// <summary>
    /// SRT and WebVTT reading, shifting, stretching, converting and checking, with no external tool. Times are integer
    /// milliseconds: shift adds, stretch is a linear map from two anchor cues, convert changes the time separator and header.
    /// </summary>
    public static class SubtitleTools
    {
        private static readonly Regex TimeRx = new Regex(@"^\s*(?:(\d{1,3}):)?(\d{1,2}):(\d{2})[.,](\d{1,3})\s*$", RegexOptions.Compiled);
        private static readonly Regex ArrowRx = new Regex(@"^(?<a>\S+)\s*-->\s*(?<b>\S+)(?<rest>.*)$", RegexOptions.Compiled);

        public static bool IsSubtitle(string path)
        {
            var e = Path.GetExtension(path ?? "").ToLowerInvariant();
            return e == ".srt" || e == ".vtt";
        }

        public static long? ParseTime(string s)
        {
            if (s == null) return null;
            var m = TimeRx.Match(s);
            if (!m.Success) return null;
            long h = m.Groups[1].Success ? long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
            long mi = long.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture), se = long.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
            if (mi > 59 || se > 59) return null;
            var frac = m.Groups[4].Value.PadRight(3, '0');
            return ((h * 60 + mi) * 60 + se) * 1000 + long.Parse(frac, CultureInfo.InvariantCulture);
        }

        public static string FormatTime(long ms, SubtitleFormat f)
        {
            if (ms < 0) return f == SubtitleFormat.Srt ? "00:00:xx,xxx" : "00:00:xx.xxx";
            long h = ms / 3600000, m = ms / 60000 % 60, s = ms / 1000 % 60, x = ms % 1000;
            return string.Create(CultureInfo.InvariantCulture, $"{h:00}:{m:00}:{s:00}{(f == SubtitleFormat.Srt ? ',' : '.')}{x:000}");
        }

        public static string FormatTimeShort(long ms) => FormatTime(ms, SubtitleFormat.Srt);

        // ------------------------------------------------------------------ reading

        public static SubtitleFile Read(string path)
        {
            var bytes = File.ReadAllBytes(path);
            bool bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            string text;
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) text = Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) text = Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
            else text = new UTF8Encoding(false).GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0));
            var f = Parse(text, Path.GetExtension(path).Equals(".vtt", StringComparison.OrdinalIgnoreCase) ? SubtitleFormat.Vtt : SubtitleFormat.Srt);
            f.HadBom = bom;
            return f;
        }

        public static SubtitleFile Parse(string text, SubtitleFormat declared)
        {
            var f = new SubtitleFile { Format = declared };
            text = text.Replace("\r\n", "\n").Replace('\r', '\n');
            if (text.StartsWith("WEBVTT")) f.Format = SubtitleFormat.Vtt;
            var blocks = Regex.Split(text.Trim('\n'), @"\n{2,}");
            int n = 0;
            foreach (var block0 in blocks)
            {
                var block = block0.Trim('\n');
                if (block.Length == 0) continue;
                var lines = block.Split('\n').ToList();
                if (lines[0].StartsWith("WEBVTT")) continue;
                if (lines[0].StartsWith("NOTE") || lines[0].StartsWith("STYLE") || lines[0].StartsWith("REGION"))
                {
                    f.Dropped.Add(lines[0].Split(' ')[0] + " block"); continue;
                }
                int ti = lines.FindIndex(l => l.Contains("-->"));
                if (ti < 0 || ti > 1) continue;                                   // not a cue block
                var cue = new Cue { Number = ++n, RawTimes = lines[ti].Trim() };
                var m = ArrowRx.Match(lines[ti].Trim());
                if (m.Success)
                {
                    var a = ParseTime(m.Groups["a"].Value); var b = ParseTime(m.Groups["b"].Value);
                    if (a.HasValue && b.HasValue) { cue.Start = a.Value; cue.End = b.Value; }
                    cue.Settings = m.Groups["rest"].Value.Trim();
                }
                cue.Text = string.Join("\n", lines.Skip(ti + 1));
                f.Cues.Add(cue);
            }
            return f;
        }

        // ------------------------------------------------------------------ checks

        public sealed class Problem { public Cue Cue; public string Kind; public string Reason; }

        /// <summary>Overlapping cues (a cue starting before the one before it ends) and cues whose time cannot be read.</summary>
        public static List<Problem> Problems(SubtitleFile f)
        {
            var list = new List<Problem>();
            Cue prev = null;
            foreach (var c in f.Cues)
            {
                if (c.Broken) { list.Add(new Problem { Cue = c, Kind = "broken", Reason = "Time cannot be read" }); continue; }
                if (c.End < c.Start) list.Add(new Problem { Cue = c, Kind = "backwards", Reason = "Ends before it starts" });
                if (prev != null && c.Start < prev.End) list.Add(new Problem { Cue = c, Kind = "overlap", Reason = "Starts before the last one ends" });
                prev = c;
            }
            return list;
        }

        /// <summary>Trims each overlapping cue's predecessor so it ends one millisecond before the next starts. Returns how many changed.</summary>
        public static int FixOverlaps(SubtitleFile f)
        {
            int fixedCount = 0; Cue prev = null;
            foreach (var c in f.Cues.Where(x => !x.Broken))
            {
                if (prev != null && c.Start < prev.End)
                {
                    prev.End = Math.Max(prev.Start + 1, c.Start - 1); fixedCount++;
                }
                prev = c;
            }
            return fixedCount;
        }

        public static int SkipBroken(SubtitleFile f) => f.Cues.RemoveAll(c => c.Broken);

        // ------------------------------------------------------------------ shift, stretch, convert

        /// <summary>Adds <paramref name="deltaMs"/> to every readable cue. Returns the first cue that would start before zero, or null when it fits.</summary>
        public static Cue WouldGoNegative(SubtitleFile f, long deltaMs) => f.Cues.FirstOrDefault(c => !c.Broken && c.Start + deltaMs < 0);

        public static void Shift(SubtitleFile f, long deltaMs)
        {
            foreach (var c in f.Cues.Where(x => !x.Broken)) { c.Start += deltaMs; c.End += deltaMs; }
        }

        /// <summary>new = a + (t - t1) * (b - a) / (t2 - t1), where t1 and t2 are the first and last cue starts and a and b are where they should land.</summary>
        public static void Stretch(SubtitleFile f, long a, long b)
        {
            var ok = f.Cues.Where(c => !c.Broken).ToList();
            if (ok.Count < 2) return;
            long t1 = ok.First().Start, t2 = ok.Last().Start;
            if (t2 == t1) return;
            long Map(long t) => a + (long)Math.Round((t - t1) * (double)(b - a) / (t2 - t1));
            foreach (var c in ok) { c.Start = Math.Max(0, Map(c.Start)); c.End = Math.Max(c.Start, Map(c.End)); }
        }

        /// <summary>The speed factor a stretch applies, for the on-screen note.</summary>
        public static double Factor(SubtitleFile f, long a, long b)
        {
            var ok = f.Cues.Where(c => !c.Broken).ToList();
            if (ok.Count < 2) return 1;
            long t1 = ok.First().Start, t2 = ok.Last().Start;
            return t2 == t1 ? 1 : (double)(b - a) / (t2 - t1);
        }

        /// <summary>Common frame rate pairs: the subtitles were timed for the first rate and the video plays at the second.</summary>
        public static readonly (string Label, double From, double To)[] FrameRatePresets =
        {
            ("23.976 → 25", 23.976, 25.0), ("25 → 23.976", 25.0, 23.976), ("24 → 25", 24.0, 25.0),
            ("25 → 24", 25.0, 24.0), ("29.97 → 25", 29.97, 25.0), ("25 → 29.97", 25.0, 29.97)
        };

        /// <summary>Where the first and last cue land for a frame rate pair: every time is multiplied by from / to.</summary>
        public static (long A, long B) PresetAnchors(SubtitleFile f, double from, double to)
        {
            var ok = f.Cues.Where(c => !c.Broken).ToList();
            if (ok.Count == 0) return (0, 0);
            double k = from / to;
            return ((long)Math.Round(ok.First().Start * k), (long)Math.Round(ok.Last().Start * k));
        }

        /// <summary>Notes on what the other format cannot keep, for the Convert card.</summary>
        public static string ConvertNote(SubtitleFormat from, SubtitleFormat to)
        {
            if (from == to) return "Both are the same format, so nothing changes.";
            return to == SubtitleFormat.Vtt
                ? "Commas in times become dots, a WEBVTT header is added, and basic styling tags are kept."
                : "Dots in times become commas, the WEBVTT header and cue settings (position, alignment) are dropped, and basic styling tags are kept.";
        }

        // ------------------------------------------------------------------ writing

        public static string Write(SubtitleFile f, SubtitleFormat to)
        {
            var sb = new StringBuilder();
            if (to == SubtitleFormat.Vtt) sb.Append("WEBVTT\r\n\r\n");
            int n = 0;
            foreach (var c in f.Cues)
            {
                if (to == SubtitleFormat.Srt) sb.Append(++n).Append("\r\n");
                if (c.Broken) sb.Append(c.RawTimes);
                else
                {
                    sb.Append(FormatTime(c.Start, to)).Append(" --> ").Append(FormatTime(c.End, to));
                    if (to == SubtitleFormat.Vtt && f.Format == SubtitleFormat.Vtt && c.Settings.Length > 0) sb.Append(' ').Append(c.Settings);
                }
                sb.Append("\r\n").Append(c.Text.Replace("\n", "\r\n")).Append("\r\n\r\n");
            }
            return sb.ToString();
        }

        /// <summary>What was dropped converting to SRT: cue settings and non-cue blocks. Empty when nothing is lost.</summary>
        public static List<string> DroppedOnConvert(SubtitleFile f, SubtitleFormat to)
        {
            var list = new List<string>();
            if (to == SubtitleFormat.Srt && f.Format == SubtitleFormat.Vtt)
            {
                int settings = f.Cues.Count(c => c.Settings.Length > 0);
                if (settings > 0) list.Add(settings + " cue settings");
                list.AddRange(f.Dropped.GroupBy(x => x).Select(g => g.Count() + " " + g.Key));
            }
            return list;
        }

        public static void Save(SubtitleFile f, string path, SubtitleFormat to)
        {
            var enc = new UTF8Encoding(f.HadBom);
            File.WriteAllText(path, Write(f, to), enc);
        }

        /// <summary>The default result name: the same name with ".shifted", ".stretched" or the new extension, never an existing file.</summary>
        public static string DefaultOutput(string source, string tag, SubtitleFormat to)
        {
            var dir = Path.GetDirectoryName(source); var stem = Path.GetFileNameWithoutExtension(source);
            var ext = to == SubtitleFormat.Vtt ? ".vtt" : ".srt";
            var name = tag == null ? stem + ext : stem + "." + tag + ext;
            return FileOps.Unique(Path.Combine(dir, name));
        }
    }
}
