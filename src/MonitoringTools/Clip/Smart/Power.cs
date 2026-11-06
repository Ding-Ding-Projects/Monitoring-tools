using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MonitoringTools.Clip.Smart
{
    /// <summary>A snippet: typing its trigger anywhere replaces the trigger with the text.</summary>
    public sealed class Snippet
    {
        public string Name { get; set; } = "";
        public string Trigger { get; set; } = "";
        public string Hotkey { get; set; } = "";
        public string Body { get; set; } = "";
    }

    /// <summary>The result of filling in a snippet: the text and how many characters from the end the cursor goes.</summary>
    public sealed class Expansion
    {
        public string Text;
        public int CursorFromEnd;
        public List<string> Asks = new List<string>();
    }

    /// <summary>Matches typed characters against snippet triggers. Keeps only the last few characters in memory,
    /// clears them at every word break, and never stores what you type.</summary>
    public sealed class SnippetMatcher
    {
        private readonly StringBuilder _buf = new StringBuilder();
        private const int Keep = 40;
        public Func<IReadOnlyList<Snippet>> Snippets = () => Array.Empty<Snippet>();

        /// <summary>Feeds one key. Returns the snippet whose trigger was just completed, or null.</summary>
        public Snippet Feed(char c)
        {
            if (c == '\0') { _buf.Clear(); return null; }
            if (c == '\b') { if (_buf.Length > 0) _buf.Length--; return null; }
            _buf.Append(c);
            if (_buf.Length > Keep) _buf.Remove(0, _buf.Length - Keep);
            var s = _buf.ToString();
            foreach (var sn in Snippets())
            {
                if (string.IsNullOrEmpty(sn.Trigger) || sn.Trigger.Length < 2) continue;
                if (s.EndsWith(sn.Trigger, StringComparison.Ordinal))
                {
                    // the trigger must start a word, so ";sig" inside "abc;sig" still works but "xsig" for "sig" does not
                    int at = s.Length - sn.Trigger.Length;
                    if (at > 0 && char.IsLetterOrDigit(s[at - 1]) && char.IsLetterOrDigit(sn.Trigger[0])) continue;
                    _buf.Clear();
                    return sn;
                }
            }
            return null;
        }

        public void Reset() => _buf.Clear();

        /// <summary>Fills in {date}, {time}, {clipboard}, {clipboard:domain}, {ask:Name} and {cursor}.</summary>
        public static Expansion Fill(string body, DateTime now, string clipboard, IDictionary<string, string> answers = null)
        {
            var e = new Expansion();
            var text = Regex.Replace(body ?? "", @"\{(date|time|clipboard|clipboard:domain|ask:[^}]{1,40}|cursor)\}", m =>
            {
                var k = m.Groups[1].Value;
                if (k == "date") return now.ToString("ddd d MMM yyyy", CultureInfo.CurrentCulture);
                if (k == "time") return now.ToString("HH:mm", CultureInfo.CurrentCulture);
                if (k == "clipboard") return clipboard ?? "";
                if (k == "clipboard:domain") return Uri.TryCreate((clipboard ?? "").Trim(), UriKind.Absolute, out var u) ? u.Host.Replace("www.", "") : "";
                if (k.StartsWith("ask:")) { var name = k.Substring(4); if (!e.Asks.Contains(name)) e.Asks.Add(name); return answers != null && answers.TryGetValue(name, out var a) ? a : ""; }
                return "\u0001";
            });
            int cur = text.IndexOf('\u0001');
            if (cur >= 0) { e.CursorFromEnd = text.Length - cur - 1; text = text.Replace("\u0001", ""); }
            e.Text = text;
            return e;
        }
    }

    /// <summary>A transform recipe: steps applied in order to copied text.</summary>
    public sealed class Recipe
    {
        public string Name { get; set; } = "";
        public List<string> Steps { get; set; } = new List<string>();
        public bool OnToasts { get; set; }
        public string Hotkey { get; set; } = "";
    }

    public static class Transforms
    {
        public static readonly (string Id, string Label, Func<string, string> Run)[] All =
        {
            ("trim", "Trim spaces", s => s.Trim()),
            ("tidy", "Tidy spaces", s => Regex.Replace(s, @"[ \t]{2,}", " ")),
            ("lower", "lower case", s => s.ToLowerInvariant()),
            ("upper", "UPPER CASE", s => s.ToUpperInvariant()),
            ("title", "Title Case", s => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLowerInvariant())),
            ("accents", "Remove accents", RemoveAccents),
            ("symbols", "Drop symbols", s => Regex.Replace(s, @"[^\p{L}\p{N}\s\-]", "")),
            ("dash", "Spaces to -", s => Regex.Replace(s.Trim(), @"\s+", "-")),
            ("underscore", "Spaces to _", s => Regex.Replace(s.Trim(), @"\s+", "_")),
            ("join", "Join lines", s => Regex.Replace(s, @"\s*\r?\n\s*", " ").Trim()),
            ("dedupe", "Remove duplicate lines", s => string.Join("\n", Lines(s).Distinct())),
            ("sort", "Sort lines", s => string.Join("\n", Lines(s).OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase))),
            ("blank", "Remove blank lines", s => string.Join("\n", Lines(s).Where(x => x.Trim().Length > 0))),
            ("bullets", "Remove bullets and numbers", s => string.Join("\n", Lines(s).Select(x => Regex.Replace(x, @"^\s*(?:[-*•·]|\d+[.)])\s+", "")))),
            ("quotes", "Wrap in quotes", s => "\"" + s + "\""),
            ("quotelines", "Quote each line", s => string.Join("\n", Lines(s).Select(x => "'" + x.Replace("'", "''") + "'"))),
            ("comma", "Lines to a comma list", s => string.Join(", ", Lines(s).Select(x => x.Trim()).Where(x => x.Length > 0))),
            ("urlencode", "URL-encode", s => Uri.EscapeDataString(s)),
            ("urldecode", "URL-decode", s => Uri.UnescapeDataString(s)),
            ("tracking", "Strip tracking from links", s => Regex.Replace(s, @"https?://\S+", m => StripTracking(m.Value))),
            ("smartquotes", "Straight quotes", s => s.Replace('“', '"').Replace('”', '"').Replace('‘', '\'').Replace('’', '\'')),
        };

        public static (string Id, string Label, Func<string, string> Run) Find(string id) => All.FirstOrDefault(t => t.Id == id);

        public static string Apply(Recipe r, string text)
        {
            var s = text ?? "";
            foreach (var id in r.Steps) { var t = Find(id); if (t.Run != null) s = t.Run(s); }
            return s;
        }

        private static IEnumerable<string> Lines(string s) => s.Replace("\r\n", "\n").Split('\n');

        public static string RemoveAccents(string s)
        {
            var d = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(d.Length);
            foreach (var c in d) if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        private static readonly Regex TrackingParam = new Regex(@"^(utm_[a-z]+|fbclid|gclid|dclid|msclkid|mc_cid|mc_eid|igshid|si|ref_src|ref_url|_hsenc|_hsmi|yclid|spm|vero_id|oly_[a-z]+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>Removes tracking parameters (utm_*, fbclid, gclid and others) and keeps everything else in order.</summary>
        public static string StripTracking(string url)
        {
            int q = url.IndexOf('?');
            if (q < 0) return url;
            int hash = url.IndexOf('#', q);
            string query = hash < 0 ? url.Substring(q + 1) : url.Substring(q + 1, hash - q - 1), frag = hash < 0 ? "" : url.Substring(hash);
            var kept = query.Split('&').Where(p => p.Length > 0 && !TrackingParam.IsMatch(Uri.UnescapeDataString(p.Split('=')[0]))).ToList();
            return url.Substring(0, q) + (kept.Count > 0 ? "?" + string.Join("&", kept) : "") + frag;
        }
    }

    /// <summary>A per-app rule: what to do with copies made in that app.</summary>
    public sealed class AppRule
    {
        /// <summary>The process name without .exe, for example "msedge".</summary>
        public string App { get; set; } = "";
        public string Label { get; set; } = "";
        /// <summary>show, quiet (history only) or never (not kept at all).</summary>
        public string Toast { get; set; } = "show";
        /// <summary>none, strip (tracking from links), plain (keep as plain text), clear (after 30 s) or table.</summary>
        public string Auto { get; set; } = "none";

        public static AppRule For(IEnumerable<AppRule> rules, string app) =>
            string.IsNullOrEmpty(app) ? null : rules?.FirstOrDefault(r => string.Equals(r.App, app, StringComparison.OrdinalIgnoreCase) || string.Equals(r.App + ".exe", app, StringComparison.OrdinalIgnoreCase));
    }
}
