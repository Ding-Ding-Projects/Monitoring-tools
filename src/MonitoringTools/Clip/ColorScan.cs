using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace MonitoringTools.Clip
{
    /// <summary>
    /// Finds colours written in copied text and code so the toast can show a preview of each one.
    /// Understood: CSS hex (#rgb, #rgba, #rrggbb, #rrggbbaa), rgb()/rgba() and hsl()/hsla() with commas or spaces,
    /// percentages and a slash alpha, 0xRRGGBB constants and 0xAARRGGBB on a line that mentions colour, Color.FromRgb / FromArgb calls,
    /// WPF and .NET named colours (Colors.Tomato, Color.Red), CSS named colours after a colour property
    /// (color: tomato) and a copy that is only a colour name.
    /// </summary>
    public static class ColorScan
    {
        public sealed class Found
        {
            /// <summary>The text exactly as it was written.</summary>
            public string Text;
            public Color Color;
            public int Index;
            public string Hex => Color.A == 255 ? $"#{Color.R:X2}{Color.G:X2}{Color.B:X2}" : $"#{Color.R:X2}{Color.G:X2}{Color.B:X2}{Color.A:X2}";
            public string Rgb => Color.A == 255 ? $"rgb({Color.R}, {Color.G}, {Color.B})" : $"rgba({Color.R}, {Color.G}, {Color.B}, {Math.Round(Color.A / 255.0, 2).ToString(CultureInfo.InvariantCulture)})";
        }

        private const string Num = @"[+-]?(?:\d+\.?\d*|\.\d+)";
        private static readonly Regex HexRx = new Regex(@"(?<![\w&#/])#(?<h>[0-9a-fA-F]{8}|[0-9a-fA-F]{6}|[0-9a-fA-F]{3,4})(?![\w-])", RegexOptions.Compiled);
        private static readonly Regex FuncRx = new Regex(
            @"(?<![\w-])(?<f>rgba?|hsla?)\(\s*(?<a>" + Num + @")(?<au>%|deg)?\s*[, ]\s*(?<b>" + Num + @")(?<bu>%)?\s*[, ]\s*(?<c>" + Num + @")(?<cu>%)?\s*(?:[,/]\s*(?<d>" + Num + @")(?<du>%)?\s*)?\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex OxRx = new Regex(@"(?<![\w])0x(?<h>[0-9a-fA-F]{8}|[0-9a-fA-F]{6})(?![\w])", RegexOptions.Compiled);
        private static readonly Regex CallRx = new Regex(@"\bFrom(?<k>A?rgb)\(\s*(?<p>\d{1,3}(?:\s*,\s*\d{1,3}){2,3})\s*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex DotNameRx = new Regex(@"\bColors?\.(?<n>[A-Z][A-Za-z]+)\b", RegexOptions.Compiled);
        private static readonly Regex PropNameRx = new Regex(@"(?:^|[\s;{""'])(?:color|background(?:-color)?|fill|stroke|border(?:-(?:top|right|bottom|left))?(?:-color)?|outline(?:-color)?|accent-color|caret-color|text-decoration-color|Foreground|Background|Fill|Stroke|Color)\s*[:=]\s*[""']?(?<n>[A-Za-z]+)\b", RegexOptions.Compiled);
        private static readonly Regex ColorWordRx = new Regex(@"colou?r|argb|rgba?|tint|brush|fill|stroke|foreground|background|accent|palette", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex WordRx = new Regex(@"^[A-Za-z]{3,24}$", RegexOptions.Compiled);

        private static readonly Lazy<Dictionary<string, Color>> Names = new Lazy<Dictionary<string, Color>>(() =>
        {
            var d = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in typeof(Colors).GetProperties(BindingFlags.Public | BindingFlags.Static))
                if (p.GetValue(null) is Color c) d[p.Name] = c;
            // CSS spellings WPF does not list
            foreach (var g in d.Keys.Where(k => k.IndexOf("Gray", StringComparison.OrdinalIgnoreCase) >= 0).ToList())
                d[g.Replace("Gray", "Grey").Replace("gray", "grey")] = d[g];
            d["RebeccaPurple"] = Color.FromRgb(0x66, 0x33, 0x99);
            return d;
        });

        /// <summary>Every colour in the text, in reading order, each written form once. Never throws.</summary>
        public static List<Found> Find(string text, int max = 24)
        {
            var list = new List<Found>();
            if (string.IsNullOrWhiteSpace(text)) return list;
            if (text.Length > 200_000) text = text.Substring(0, 200_000);
            var taken = new List<(int, int)>();
            void Add(int i, int len, string written, Color? c)
            {
                if (c == null || taken.Any(t => i < t.Item1 + t.Item2 && t.Item1 < i + len)) return;
                taken.Add((i, len));
                list.Add(new Found { Text = written, Color = c.Value, Index = i });
            }
            try
            {
                var whole = text.Trim();
                bool single = !whole.Contains('\n') && !whole.Contains(' ');
                foreach (Match m in FuncRx.Matches(text)) Add(m.Index, m.Length, m.Value, FromFunc(m));
                foreach (Match m in CallRx.Matches(text)) Add(m.Index, m.Length, m.Value, FromCall(m));
                foreach (Match m in HexRx.Matches(text))
                {
                    var h = m.Groups["h"].Value;
                    // "#123" in prose is far more often an issue number than a colour
                    if (h.Length <= 4 && h.All(char.IsDigit) && !single) continue;
                    // Eight digits inside quotes ("#FF2B4A3F" in XAML or a .NET string) are alpha first; in CSS alpha comes last.
                    bool quoted = m.Index > 0 && (text[m.Index - 1] == '"' || text[m.Index - 1] == '\'');
                    Add(m.Index, m.Length, m.Value, h.Length == 8 && quoted ? FromOx(h) : FromCssHex(h));
                }
                foreach (Match m in OxRx.Matches(text))
                {
                    // 0xAARRGGBB counts only on a line that talks about colour: "0x80070005" alone is a Windows error code.
                    if (m.Groups["h"].Value.Length == 8)
                    {
                        int ls = text.LastIndexOf('\n', m.Index) + 1, le = text.IndexOf('\n', m.Index); if (le < 0) le = text.Length;
                        if (!ColorWordRx.IsMatch(text.Substring(ls, le - ls))) continue;
                    }
                    Add(m.Index, m.Length, m.Value, FromOx(m.Groups["h"].Value));
                }
                foreach (Match m in DotNameRx.Matches(text)) Add(m.Index, m.Length, m.Value, Named(m.Groups["n"].Value));
                foreach (Match m in PropNameRx.Matches(text)) { var g = m.Groups["n"]; Add(g.Index, g.Length, g.Value, Named(g.Value)); }
                if (list.Count == 0 && WordRx.IsMatch(whole)) Add(text.IndexOf(whole, StringComparison.Ordinal), whole.Length, whole, Named(whole));
            }
            catch { }
            return list.OrderBy(f => f.Index)
                       .GroupBy(f => f.Text, StringComparer.OrdinalIgnoreCase).Select(g => g.First())
                       .Take(max).ToList();
        }

        /// <summary>
        /// The colour when the whole copy is one colour written in a code form (hex, rgb, hsl, a call or Colors.Name).
        /// A bare colour name is not counted here, so copying the word "orange" stays a text copy (with a preview).
        /// </summary>
        public static Color? Whole(string text)
        {
            var t = (text ?? "").Trim().TrimEnd(';');
            if (t.Length == 0 || t.Length > 80 || t.Contains('\n')) return null;
            var f = Find(t, 2);
            if (f.Count != 1 || f[0].Text.Length != t.Length) return null;
            // a bare name stays a text copy, and so does a 0x number (it may be a code or an address; the preview still shows)
            return WordRx.IsMatch(t) || t.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? (Color?)null : f[0].Color;
        }

        /// <summary>Parses one colour in any form <see cref="Find"/> understands, names included.</summary>
        public static Color? Parse(string text)
        {
            var t = (text ?? "").Trim().TrimEnd(';');
            if (t.Length == 0) return null;
            if (Regex.IsMatch(t, @"^[0-9a-fA-F]{6}$")) return FromCssHex(t);
            var f = Find(t, 1);
            return f.Count == 1 ? f[0].Color : (Color?)null;
        }

        /// <summary>A colour name, or null when the word is not one.</summary>
        public static Color? Named(string name) => name != null && Names.Value.TryGetValue(name, out var c) ? c : (Color?)null;

        /// <summary>Hue in degrees, saturation and lightness in percent.</summary>
        public static (int H, int S, int L) Hsl(Color c)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            double h = 0, s, lum = (max + min) / 2;
            if (max == min) s = 0;
            else
            {
                double d = max - min;
                s = lum > 0.5 ? d / (2 - max - min) : d / (max + min);
                if (max == r) h = (g - b) / d + (g < b ? 6 : 0);
                else if (max == g) h = (b - r) / d + 2;
                else h = (r - g) / d + 4;
                h *= 60;
            }
            return ((int)Math.Round(h) % 360, (int)Math.Round(s * 100), (int)Math.Round(lum * 100));
        }

        /// <summary>True when dark text reads better than light text on this colour.</summary>
        public static bool IsLight(Color c) => (0.2126 * Lin(c.R) + 0.7152 * Lin(c.G) + 0.0722 * Lin(c.B)) > 0.179;
        private static double Lin(byte v) { var x = v / 255.0; return x <= 0.03928 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4); }

        // ------------------------------------------------------------------ forms

        private static Color? FromCssHex(string h)
        {
            if (h.Length == 3 || h.Length == 4) h = string.Concat(h.Select(ch => $"{ch}{ch}"));
            byte P(int i) => byte.Parse(h.Substring(i, 2), NumberStyles.HexNumber);
            return h.Length == 6 ? Color.FromRgb(P(0), P(2), P(4)) : h.Length == 8 ? Color.FromArgb(P(6), P(0), P(2), P(4)) : (Color?)null;
        }

        private static Color? FromOx(string h)
        {
            byte P(int i) => byte.Parse(h.Substring(i, 2), NumberStyles.HexNumber);
            return h.Length == 6 ? Color.FromRgb(P(0), P(2), P(4)) : Color.FromArgb(P(0), P(2), P(4), P(6));
        }

        private static Color? FromCall(Match m)
        {
            var p = m.Groups["p"].Value.Split(',').Select(s => int.Parse(s.Trim(), CultureInfo.InvariantCulture)).ToArray();
            if (p.Any(v => v > 255)) return null;
            bool argb = m.Groups["k"].Value.StartsWith("A", StringComparison.OrdinalIgnoreCase);
            if (p.Length == 3) return Color.FromRgb((byte)p[0], (byte)p[1], (byte)p[2]);
            if (p.Length == 4 && argb) return Color.FromArgb((byte)p[0], (byte)p[1], (byte)p[2], (byte)p[3]);
            return null;
        }

        private static Color? FromFunc(Match m)
        {
            double D(string g) => double.Parse(m.Groups[g].Value, CultureInfo.InvariantCulture);
            bool Pct(string g) => m.Groups[g].Value == "%";
            byte Clamp(double v) => (byte)Math.Round(Math.Max(0, Math.Min(255, v)));
            double alpha = 1;
            if (m.Groups["d"].Success) alpha = Pct("du") ? D("d") / 100 : D("d");
            var a = Clamp(alpha * 255);
            if (m.Groups["f"].Value.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
            {
                double Ch(string g, string u) => Pct(u) ? D(g) * 2.55 : D(g);
                return Color.FromArgb(a, Clamp(Ch("a", "au")), Clamp(Ch("b", "bu")), Clamp(Ch("c", "cu")));
            }
            double hue = ((D("a") % 360) + 360) % 360, sat = Math.Max(0, Math.Min(100, D("b"))) / 100, lit = Math.Max(0, Math.Min(100, D("c"))) / 100;
            double q = lit < 0.5 ? lit * (1 + sat) : lit + sat - lit * sat, pp = 2 * lit - q;
            double T(double t) { t = (t + 1) % 1; return t < 1 / 6.0 ? pp + (q - pp) * 6 * t : t < 0.5 ? q : t < 2 / 3.0 ? pp + (q - pp) * (2 / 3.0 - t) * 6 : pp; }
            double hh = hue / 360;
            return Color.FromArgb(a, Clamp(T(hh + 1 / 3.0) * 255), Clamp(T(hh) * 255), Clamp(T(hh - 1 / 3.0) * 255));
        }
    }
}
