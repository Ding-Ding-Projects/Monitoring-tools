using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace MonitoringTools.Core
{
    /// <summary>
    /// The one place search patterns meet the real .NET regex engine: bounded compilation,
    /// token explanations, backtracking-risk hints, test runs, replace/split previews and code
    /// generation. Everything runs locally; nothing here stores or sends a pattern.
    /// </summary>
    public static class RegexKit
    {
        public const int MaxPatternLength = 4096;
        public const int MaxSampleLength = 256 * 1024;
        public static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(200);

        public static string EngineLabel(RegexOptions o) =>
            $"{RuntimeInformation.FrameworkDescription} · System.Text.RegularExpressions · " +
            ((o & RegexOptions.NonBacktracking) != 0 ? "non-backtracking (linear time)" : "backtracking") +
            $" · {Timeout.TotalMilliseconds:0} ms limit";

        /// <summary>Options the builder exposes, in the order they are shown.</summary>
        public static readonly (RegexOptions Flag, string Label, string Help)[] OptionDefs =
        {
            (RegexOptions.Multiline, "Multiline", "^ and $ match at the start and end of every line"),
            (RegexOptions.Singleline, "Singleline", ". also matches line breaks"),
            (RegexOptions.IgnorePatternWhitespace, "Ignore whitespace", "spaces and # comments inside the pattern are ignored"),
            (RegexOptions.ExplicitCapture, "Explicit capture", "only named groups capture"),
            (RegexOptions.RightToLeft, "Right to left", "search from the end of the text"),
            (RegexOptions.NonBacktracking, "Non-backtracking", "linear-time engine; no lookaround, backreferences or atomic groups"),
            (RegexOptions.CultureInvariant, "Culture invariant", "case-insensitive matching ignores the current culture"),
            (RegexOptions.ECMAScript, "ECMAScript", "JavaScript-style behaviour; only works with ignore case and multiline"),
        };

        /// <summary>Compiles a pattern or returns a readable error. Never throws.</summary>
        public static Regex TryCreate(string pattern, RegexOptions options, out string error)
        {
            error = null;
            if (pattern == null) pattern = "";
            if (pattern.Length > MaxPatternLength) { error = $"Pattern is longer than {MaxPatternLength} characters."; return null; }
            try { return new Regex(pattern, options, Timeout); }
            catch (ArgumentException ex) { error = Clean(ex.Message); }
            catch (NotSupportedException ex) { error = Clean(ex.Message); }
            return null;
        }

        private static string Clean(string m)
        {
            m = (m ?? "").Split('\n')[0].Trim();
            const string prefix = "Invalid pattern ";
            return m.StartsWith(prefix, StringComparison.Ordinal) ? m[prefix.Length..] : m;
        }

        /// <summary>True when the pattern matches; a timeout counts as no match and is reported.</summary>
        public static bool SafeIsMatch(Regex rx, string text, ref bool timedOut)
        {
            if (rx == null || text == null) return false;
            if (text.Length > MaxSampleLength) text = text[..MaxSampleLength];
            try { return rx.IsMatch(text); }
            catch (RegexMatchTimeoutException) { timedOut = true; return false; }
        }

        // ---------------------------------------------------------------- risk

        private static readonly Regex NestedQuantifier = new Regex(@"\((?:[^()\\]|\\.|\([^()]*\))*[+*}]\)\??[+*{]", RegexOptions.None, TimeSpan.FromMilliseconds(50));
        private static readonly Regex OverlappingAlternation = new Regex(@"\((?:[^()|]+\|)+[^()|]+\)[+*]", RegexOptions.None, TimeSpan.FromMilliseconds(50));

        /// <summary>Plain-language warning for patterns known to backtrack catastrophically, or null.</summary>
        public static string Risk(string pattern, RegexOptions options)
        {
            if (string.IsNullOrEmpty(pattern) || (options & RegexOptions.NonBacktracking) != 0) return null;
            try
            {
                if (NestedQuantifier.IsMatch(pattern))
                    return "Nested quantifier: input such as \"aaaaaaaaaaaaaaaa!\" can make this backtrack for a very long time. Searches stop after 200 ms. An atomic group (?>…) or the Non-backtracking option avoids it.";
                if (OverlappingAlternation.IsMatch(pattern))
                    return "A repeated group of alternatives can backtrack heavily when the alternatives overlap. Searches stop after 200 ms.";
            }
            catch (RegexMatchTimeoutException) { }
            return null;
        }

        // ---------------------------------------------------------------- explanation

        public sealed record Token(string Text, string Meaning, int Depth);

        private static readonly Dictionary<string, string> Named = new Dictionary<string, string>
        {
            [@"\d"] = "one digit", [@"\D"] = "one character that is not a digit", [@"\w"] = "one word character (letter, digit or _)",
            [@"\W"] = "one character that is not a word character", [@"\s"] = "one whitespace character", [@"\S"] = "one non-whitespace character",
            [@"\b"] = "a word boundary", [@"\B"] = "not a word boundary", [@"\A"] = "the very start of the text", [@"\Z"] = "the end of the text, before a final line break",
            [@"\z"] = "the very end of the text", [@"\G"] = "where the previous match ended", [@"\n"] = "a line feed", [@"\r"] = "a carriage return", [@"\t"] = "a tab",
            ["^"] = "start of the text (of each line with Multiline)", ["$"] = "end of the text (of each line with Multiline)", ["|"] = "or",
            ["."] = "any character except a line break (any character with Singleline)", ["("] = "start of numbered group", [")"] = "end of group",
            ["(?:"] = "start of a group that does not capture", ["(?="] = "start of a lookahead: must be followed by", ["(?!"] = "start of a negative lookahead: must not be followed by",
            ["(?<="] = "start of a lookbehind: must be preceded by", ["(?<!"] = "start of a negative lookbehind: must not be preceded by",
            ["(?>"] = "start of an atomic group: no backtracking into it", ["*"] = "zero or more of the previous item, as many as possible",
            ["+"] = "one or more of the previous item, as many as possible", ["?"] = "the previous item is optional", ["*?"] = "zero or more, as few as possible",
            ["+?"] = "one or more, as few as possible", ["??"] = "optional, preferring to skip it",
        };

        private static readonly Regex Lexer = new Regex(
            @"\\[pP]\{[^}]*\}|\\k<[^>]*>|\\k'[^']*'|\\u[0-9A-Fa-f]{4}|\\x[0-9A-Fa-f]{2}|\\c[A-Za-z]|\\[0-9]+|\\.|\[(?:\\.|[^\]\\])*\]|\(\?<[A-Za-z_]\w*>|\(\?'[A-Za-z_]\w*'|\(\?\(|\(\?[imnsx-]+[:)]|\(\?<=|\(\?<!|\(\?#[^)]*\)|\(\?[:=!>#]|\(|\)|\{\d+(?:,\d*)?\}\??|[*+?]\??|[\^$|.]|[\uD800-\uDBFF][\uDC00-\uDFFF]|[\s\S]",
            RegexOptions.None, TimeSpan.FromMilliseconds(200));

        /// <summary>Token-by-token annotation. Built by a lexer here, not by the engine, so it is labelled as an explanation, not a parse tree.</summary>
        public static List<Token> Explain(string pattern, int max = 120)
        {
            var list = new List<Token>();
            if (string.IsNullOrEmpty(pattern)) return list;
            int depth = 0;
            try
            {
                foreach (Match m in Lexer.Matches(pattern))
                {
                    if (list.Count >= max) { list.Add(new Token("…", $"{pattern.Length - m.Index} more characters not explained", depth)); break; }
                    var t = m.Value;
                    if (t == ")") depth = Math.Max(0, depth - 1);
                    list.Add(new Token(t, Meaning(t), depth));
                    if (t.StartsWith("(", StringComparison.Ordinal) && !t.StartsWith("(?#", StringComparison.Ordinal) && !(t.StartsWith("(?", StringComparison.Ordinal) && t.EndsWith(")", StringComparison.Ordinal) && t.Length > 2)) depth++;
                }
            }
            catch (RegexMatchTimeoutException) { list.Add(new Token("…", "explanation stopped: the pattern is too long to annotate quickly", 0)); }
            return list;
        }

        private static string Meaning(string t)
        {
            if (Named.TryGetValue(t, out var n)) return n;
            if (t.StartsWith(@"\p{", StringComparison.Ordinal)) return $"one character in the Unicode category or block {t[3..^1]}";
            if (t.StartsWith(@"\P{", StringComparison.Ordinal)) return $"one character not in the Unicode category or block {t[3..^1]}";
            if (t.StartsWith(@"\k<", StringComparison.Ordinal) || t.StartsWith(@"\k'", StringComparison.Ordinal)) return $"the same text the group \"{t[3..^1]}\" matched";
            if (t == @"\x" || t == @"\u") return "an incomplete hexadecimal escape (the engine rejects this)";
            if (t == @"\c") return "an incomplete control-character escape (the engine rejects this)";
            if (t == @"\k") return "an incomplete named backreference (the engine rejects this)";
            if (t == @"\p" || t == @"\P") return "an incomplete Unicode property escape (the engine rejects this)";
            if (t.StartsWith(@"\u", StringComparison.Ordinal) || t.StartsWith(@"\x", StringComparison.Ordinal))
            {
                var code = Convert.ToInt32(t[2..], 16);
                return code >= 0xD800 && code <= 0xDFFF ? $"the UTF-16 surrogate half U+{code:X4}" : $"the character U+{code:X4} ({char.ConvertFromUtf32(code)})";
            }
            if (t.StartsWith(@"\c", StringComparison.Ordinal)) return $"the control character Ctrl+{char.ToUpperInvariant(t[2])}";
            if (t.StartsWith(@"\0", StringComparison.Ordinal)) return $"the character with octal code {t[1..]}";
            if (t.StartsWith("(?#", StringComparison.Ordinal)) return "a comment, ignored when matching";
            if (t.Length > 1 && t[0] == '\\' && char.IsDigit(t[1])) return $"the same text group {t[1..]} matched";
            if (t == @"\") return "a backslash with nothing after it (the engine rejects this)";
            if (t.StartsWith(@"\", StringComparison.Ordinal)) return $"the literal character {t[1..]}";
            if (t.StartsWith("[^", StringComparison.Ordinal)) return $"one character not in the set {t}";
            if (t.StartsWith("[", StringComparison.Ordinal)) return t.Contains("-[") ? $"one character from the set {t} (with subtraction)" : $"one character from the set {t}";
            if (t.StartsWith("(?<", StringComparison.Ordinal) || t.StartsWith("(?'", StringComparison.Ordinal)) return $"start of the named group \"{t[3..^1]}\"";
            if (t == "(?(") return "start of a conditional: (?(condition)yes|no)";
            if (t.StartsWith("(?", StringComparison.Ordinal)) return t.EndsWith(":", StringComparison.Ordinal) ? $"start of a group with inline options {t[2..^1]}" : $"turn on inline options {t[2..^1]} from here";
            if (t.StartsWith("{", StringComparison.Ordinal) && t.Length > 2 && (t.EndsWith("}", StringComparison.Ordinal) || t.EndsWith("}?", StringComparison.Ordinal)))
            {
                var lazy = t.EndsWith("?", StringComparison.Ordinal);
                var body = t.Trim('{', '}', '?');
                var parts = body.Split(',');
                var what = parts.Length == 1 ? (parts[0] == "1" ? "exactly once" : $"exactly {parts[0]} times") : parts[1].Length == 0 ? $"{parts[0]} or more times" : $"{parts[0]} to {parts[1]} times";
                return $"repeat the previous item {what}" + (lazy ? ", as few as possible" : "");
            }
            if (t == " ") return "a space";
            if (t == "\n" || t == "\r") return "a line break";
            if (t == "\t") return "a tab";
            if (t.Length > 0 && char.IsWhiteSpace(t[0])) return "a whitespace character";
            return $"the literal character {t}";
        }

        // ---------------------------------------------------------------- test, replace, split

        public sealed record GroupHit(string Name, string Value, int Index, int Length, bool Success);
        public sealed record MatchHit(int Index, int Length, string Value, List<GroupHit> Groups);
        public sealed record TestRun(List<MatchHit> Matches, double Milliseconds, bool TimedOut, bool Truncated);

        public static TestRun Test(Regex rx, string sample, int maxMatches = 500)
        {
            var list = new List<MatchHit>();
            bool timedOut = false, truncated = false;
            sample ??= "";
            if (sample.Length > MaxSampleLength) { sample = sample[..MaxSampleLength]; truncated = true; }
            var sw = Stopwatch.StartNew();
            try
            {
                var names = rx.GetGroupNames();
                for (var m = rx.Match(sample); m.Success; m = m.NextMatch())
                {
                    if (list.Count >= maxMatches) { truncated = true; break; }
                    var groups = new List<GroupHit>();
                    foreach (var name in names)
                    {
                        var g = m.Groups[name];
                        groups.Add(new GroupHit(name, g.Value, g.Index, g.Length, g.Success));
                    }
                    list.Add(new MatchHit(m.Index, m.Length, m.Value, groups));
                }
            }
            catch (RegexMatchTimeoutException) { timedOut = true; }
            sw.Stop();
            return new TestRun(list, sw.Elapsed.TotalMilliseconds, timedOut, truncated);
        }

        public static string ReplacePreview(Regex rx, string sample, string replacement, out string error)
        {
            error = null;
            try { return rx.Replace(sample ?? "", replacement ?? ""); }
            catch (RegexMatchTimeoutException) { error = "Stopped after 200 ms: the pattern backtracks too much on this text."; }
            catch (ArgumentException ex) { error = Clean(ex.Message); }
            return null;
        }

        // ---------------------------------------------------------------- replacement explanation

        /// <summary>One piece of a replacement string: the text, what it does, and whether it is a mistake the engine will not report.</summary>
        public sealed record ReplacementToken(string Text, string Meaning, bool Problem);

        /// <summary>
        /// Explains a .NET replacement string piece by piece. The .NET engine never rejects a bad
        /// reference: a dollar sign that does not form a valid token stays in the output as plain
        /// text, so those pieces are flagged here. Group references are checked against the pattern.
        /// </summary>
        public static List<ReplacementToken> ExplainReplacement(string replacement, Regex rx, int max = 200)
        {
            var list = new List<ReplacementToken>();
            replacement ??= "";
            if (replacement.Length == 0) return list;
            var numbers = rx?.GetGroupNumbers() ?? new[] { 0 };
            var literal = new StringBuilder();
            void flush()
            {
                if (literal.Length == 0) return;
                list.Add(new ReplacementToken(literal.ToString(), "literal text, copied to the result as it is", false));
                literal.Clear();
            }
            string groupLabel(int n)
            {
                if (n == 0) return "the whole match";
                var name = rx?.GroupNameFromNumber(n);
                return name != null && name != n.ToString() ? $"the text captured by group {n} (\"{name}\")" : $"the text captured by group {n}";
            }
            var i = 0;
            while (i < replacement.Length)
            {
                if (list.Count >= max) { flush(); list.Add(new ReplacementToken("…", $"{replacement.Length - i} more characters not explained", false)); return list; }
                var c = replacement[i];
                if (c != '$' || i + 1 >= replacement.Length) { literal.Append(c); i++; continue; }
                var d = replacement[i + 1];
                if (d == '$') { flush(); list.Add(new ReplacementToken("$$", "a single dollar sign", false)); i += 2; continue; }
                if (d == '&') { flush(); list.Add(new ReplacementToken("$&", "the whole match (same as $0)", false)); i += 2; continue; }
                if (d == '`') { flush(); list.Add(new ReplacementToken("$`", "the text before the match", false)); i += 2; continue; }
                if (d == '\'') { flush(); list.Add(new ReplacementToken("$'", "the text after the match", false)); i += 2; continue; }
                if (d == '+') { flush(); list.Add(new ReplacementToken("$+", "the text of the highest-numbered group that captured", false)); i += 2; continue; }
                if (d == '_') { flush(); list.Add(new ReplacementToken("$_", "the whole input text", false)); i += 2; continue; }
                if (d == '{')
                {
                    var close = replacement.IndexOf('}', i + 2);
                    if (close < 0)
                    {
                        flush(); list.Add(new ReplacementToken(replacement[i..], "an unclosed ${ reference: .NET leaves it as plain text", true));
                        i = replacement.Length; continue;
                    }
                    var name = replacement.Substring(i + 2, close - i - 2);
                    var text = replacement.Substring(i, close + 1 - i);
                    flush();
                    int num;
                    if (name.Length > 0 && name.All(char.IsAsciiDigit)) num = int.TryParse(name, out var parsed) && Array.IndexOf(numbers, parsed) >= 0 ? parsed : -1;
                    else num = rx != null && name.Length > 0 ? rx.GroupNumberFromName(name) : -1;
                    list.Add(num >= 0
                        ? new ReplacementToken(text, groupLabel(num), false)
                        : new ReplacementToken(text, $"no group \"{name}\" in this pattern: .NET leaves this as plain text", true));
                    i = close + 1; continue;
                }
                if (char.IsAsciiDigit(d))
                {
                    var j = i + 1;
                    while (j < replacement.Length && char.IsAsciiDigit(replacement[j])) j++;
                    var digits = replacement.Substring(i + 1, j - i - 1);
                    var text = "$" + digits;
                    flush();
                    var ok = int.TryParse(digits, out var n) && Array.IndexOf(numbers, n) >= 0;
                    list.Add(ok ? new ReplacementToken(text, groupLabel(n), false)
                        : new ReplacementToken(text, $"this pattern has no group {digits}: .NET leaves this as plain text", true));
                    i = j; continue;
                }
                // a dollar sign that starts nothing
                flush();
                list.Add(new ReplacementToken("$" + d, "a dollar sign followed by a character that makes no token: kept as plain text", true));
                i += 2;
            }
            flush();
            return list;
        }

        public static string[] SplitPreview(Regex rx, string sample, out string error)
        {
            error = null;
            try { return rx.Split(sample ?? ""); }
            catch (RegexMatchTimeoutException) { error = "Stopped after 200 ms: the pattern backtracks too much on this text."; }
            return null;
        }

        // ---------------------------------------------------------------- code

        public static readonly string[] CodeLanguages = { "C#", "PowerShell", "Python" };

        public static string Code(string language, string pattern, RegexOptions o)
        {
            pattern ??= "";
            switch (language)
            {
                case "PowerShell":
                {
                    var flags = new List<string>();
                    if ((o & RegexOptions.IgnoreCase) != 0) flags.Add("IgnoreCase");
                    foreach (var (f, _, _) in OptionDefs) if ((o & f) != 0) flags.Add(f.ToString());
                    var lit = "'" + pattern.Replace("'", "''") + "'";
                    var opt = flags.Count == 0 ? "'None'" : "'" + string.Join(", ", flags) + "'";
                    return $"$rx = [regex]::new({lit}, {opt}, [timespan]::FromMilliseconds(200))\n$rx.Matches($text) | ForEach-Object {{ $_.Value }}";
                }
                case "Python":
                {
                    var flags = new List<string>();
                    if ((o & RegexOptions.IgnoreCase) != 0) flags.Add("re.IGNORECASE");
                    if ((o & RegexOptions.Multiline) != 0) flags.Add("re.MULTILINE");
                    if ((o & RegexOptions.Singleline) != 0) flags.Add("re.DOTALL");
                    if ((o & RegexOptions.IgnorePatternWhitespace) != 0) flags.Add("re.VERBOSE");
                    var note = Regex.IsMatch(pattern, @"\(\?<[A-Za-z_]") ? "\n# Python names groups with (?P<name>…); this pattern was converted." : "";
                    var py = Regex.Replace(pattern, @"\(\?<([A-Za-z_]\w*)>", "(?P<$1>");
                    var unsupported = (o & (RegexOptions.RightToLeft | RegexOptions.NonBacktracking | RegexOptions.ExplicitCapture)) != 0
                        ? "\n# Right to left, non-backtracking and explicit capture have no Python equivalent and were left out." : "";
                    return $"import re{note}{unsupported}\nrx = re.compile(r\"{py.Replace("\"", "\\\"")}\"{(flags.Count > 0 ? ", " + string.Join(" | ", flags) : "")})\nfor m in rx.finditer(text):\n    print(m.group(0))";
                }
                default:
                {
                    var flags = new List<string>();
                    if ((o & RegexOptions.IgnoreCase) != 0) flags.Add("RegexOptions.IgnoreCase");
                    foreach (var (f, _, _) in OptionDefs) if ((o & f) != 0) flags.Add("RegexOptions." + f);
                    var lit = "@\"" + pattern.Replace("\"", "\"\"") + "\"";
                    return $"var rx = new Regex({lit},\n    {(flags.Count == 0 ? "RegexOptions.None" : string.Join(" | ", flags))},\n    TimeSpan.FromMilliseconds(200));\nforeach (Match m in rx.Matches(text))\n    Console.WriteLine(m.Value);";
                }
            }
        }

        /// <summary>Escapes literal text so it can be pasted into a pattern.</summary>
        public static string Escape(string literal) => Regex.Escape(literal ?? "");
    }
}
