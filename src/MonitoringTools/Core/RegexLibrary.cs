using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MonitoringTools.Core
{
    /// <summary>
    /// Saved patterns with descriptions and parameters, and search over the whole collection.
    /// A parameter is written {{name}} in the pattern; its value is substituted as plain text, never as a
    /// pattern, unless the caller asks for it to be escaped as a literal.
    /// </summary>
    public static class RegexLibrary
    {
        private static readonly Regex ParamToken = new Regex(@"\{\{\s*([A-Za-z_]\w*)\s*\}\}", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

        /// <summary>Names used as {{name}} in a pattern, in order of first appearance.</summary>
        public static List<string> Placeholders(string pattern)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(pattern)) return list;
            foreach (Match m in ParamToken.Matches(pattern)) if (!list.Contains(m.Groups[1].Value)) list.Add(m.Groups[1].Value);
            return list;
        }

        /// <summary>
        /// Fills the placeholders. A value the caller gives wins over the default; a placeholder with neither is left as it is
        /// and reported, so a half-filled pattern never looks finished. Values are inserted as typed, so a value may itself be pattern text.
        /// </summary>
        public static string Expand(SavedPattern p, IDictionary<string, string> values, out List<string> unfilled, bool escapeValues = false)
        {
            unfilled = new List<string>();
            var pattern = p?.Pattern ?? "";
            var defaults = (p?.Parameters ?? new List<RegexParam>()).Where(x => !string.IsNullOrEmpty(x.Name)).GroupBy(x => x.Name).ToDictionary(g => g.Key, g => g.First().Default ?? "");
            var missing = new List<string>();
            var result = ParamToken.Replace(pattern, m =>
            {
                var name = m.Groups[1].Value;
                if (values != null && values.TryGetValue(name, out var v) && v != null) return escapeValues ? Regex.Escape(v) : v;
                if (defaults.TryGetValue(name, out var d)) return escapeValues ? Regex.Escape(d) : d;
                if (!missing.Contains(name)) missing.Add(name);
                return m.Value;
            });
            unfilled = missing;
            return result;
        }

        /// <summary>Parameters declared on the entry, plus any placeholder found in the pattern that was never declared (with an empty default).</summary>
        public static List<RegexParam> EffectiveParameters(SavedPattern p)
        {
            var list = (p?.Parameters ?? new List<RegexParam>()).Where(x => !string.IsNullOrEmpty(x.Name)).Select(x => new RegexParam { Name = x.Name, Default = x.Default ?? "" }).ToList();
            foreach (var n in Placeholders(p?.Pattern)) if (list.All(x => x.Name != n)) list.Add(new RegexParam { Name = n, Default = "" });
            return list;
        }

        /// <summary>Parses "name=default" lines (or semicolon separated) into parameters. A name must be a letter or underscore followed by word characters.</summary>
        public static List<RegexParam> ParseParameters(string text, out List<string> problems)
        {
            problems = new List<string>();
            var list = new List<RegexParam>();
            foreach (var raw in (text ?? "").Split(new[] { '\n', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var line = raw.Trim('\r', ' ', '\t');
                if (line.Length == 0) continue;
                int eq = line.IndexOf('=');
                var name = (eq < 0 ? line : line[..eq]).Trim();
                var def = eq < 0 ? "" : line[(eq + 1)..];
                if (!Regex.IsMatch(name, @"^[A-Za-z_]\w*$")) { problems.Add("\"" + name + "\" is not a valid parameter name."); continue; }
                if (list.Any(x => x.Name == name)) { problems.Add("\"" + name + "\" is listed twice."); continue; }
                list.Add(new RegexParam { Name = name, Default = def });
            }
            return list;
        }

        public static string FormatParameters(IEnumerable<RegexParam> parameters) => string.Join("\n", (parameters ?? Enumerable.Empty<RegexParam>()).Select(p => p.Name + "=" + p.Default));

        /// <summary>
        /// Filters the collection by name, description, pattern and parameter names. Plain text needs every word (any case);
        /// with regex on, the query is a pattern tried against the same text with a short time limit.
        /// </summary>
        public static List<SavedPattern> Search(IEnumerable<SavedPattern> all, string query, bool regex, out string error)
        {
            error = null;
            var list = (all ?? Enumerable.Empty<SavedPattern>()).ToList();
            if (string.IsNullOrWhiteSpace(query)) return list;
            string Haystack(SavedPattern p) => string.Join("\n", p.Name, p.Description, p.Pattern, string.Join(" ", (p.Parameters ?? new List<RegexParam>()).Select(x => x.Name)));
            if (regex)
            {
                var rx = RegexKit.TryCreate(query, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, out error);
                if (rx == null) return list;
                var hits = new List<SavedPattern>();
                foreach (var p in list)
                {
                    try { if (rx.IsMatch(Haystack(p))) hits.Add(p); }
                    catch (RegexMatchTimeoutException) { error = "Stopped at the time limit on \"" + p.Name + "\"."; }
                }
                return hits;
            }
            var words = query.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            return list.Where(p => { var h = Haystack(p); return words.All(w => h.IndexOf(w, StringComparison.CurrentCultureIgnoreCase) >= 0); }).ToList();
        }
    }
}
