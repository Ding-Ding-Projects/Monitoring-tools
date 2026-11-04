using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    public static partial class SelfTest
    {
        // Patterns that between them make the regex builder explain every kind of token it knows, valid and invalid.
        private static readonly (string Pattern, RegexOptions Options)[] ExplainPatterns =
        {
            (@"^$.|\d\D\w\W\s\S\b\B\A\Z\z\G\n\r\t\f\v\a\e\x41A\cA\0\012\1\p{L}\P{Lu}\k<n>\k'n'\.\-\@", RegexOptions.None),
            (@"(a)(?:b)(?<n>c)(?'q'd)(?=e)(?!f)(?<=g)(?<!h)(?>i)(?i:j)(?-i:j)(?i)(?#comment)(?(1)a|b)(?(n)a|b)(?(?=x)a|b)(?<a-b>x)(?<-b>x)(?<n2>y)\2\k<n2>", RegexOptions.None),
            (@"a*a+a?a*?a+?a??a{3}a{3,}a{3,5}a{3}?a{3,}?a{3,5}?a{0}a{1}a{0,1}a{1,}a{0,}", RegexOptions.None),
            (@"[abc][^abc][a-z][a-z-[aeiou]][\d\s][^\d][.][\]\-]", RegexOptions.None),
            ("a b # note\n(c)", RegexOptions.IgnorePatternWhitespace),
            (@"(a)(?<x>b)", RegexOptions.ExplicitCapture),
            (@"é€ 😀 ab c", RegexOptions.None), (@"\ud83d\x00\u0000\cZ", RegexOptions.None),
            (@")", RegexOptions.None), (@"*a", RegexOptions.None), (@"a{", RegexOptions.None), (@"\", RegexOptions.None), (@"\p{", RegexOptions.None),
            (@"\p", RegexOptions.None), (@"\k", RegexOptions.None), (@"\k<x", RegexOptions.None), (@"\x4", RegexOptions.None), (@"\u12", RegexOptions.None),
            (@"\c", RegexOptions.None), (@"(?", RegexOptions.None), (@"[a", RegexOptions.None), (@"a{5,3}", RegexOptions.None), (@"(a", RegexOptions.None),
            (@"(?(1)a|b|c)", RegexOptions.None), (@"[z-a]", RegexOptions.None), (@"\p{Bogus}", RegexOptions.None), (@"(?<=a)+", RegexOptions.None),
            (@"(?<n>a)(?<n>b)", RegexOptions.None), (@"a**", RegexOptions.None), (@"(?<1a>x)", RegexOptions.None), (@"[a-\d]", RegexOptions.None),
            (@"(?<a-zz>x)", RegexOptions.None), (@"\8", RegexOptions.None), (@"(?z)", RegexOptions.None), (@"a|*", RegexOptions.None),
            (@"(a)(?=b)\1", RegexOptions.NonBacktracking), (@"a", RegexOptions.ECMAScript | RegexOptions.IgnoreCase),
        };

        private static readonly string[] ExplainReplacements = { "plain", "$$", "$&", "$`", "$'", "$+", "$_", "$1", "${1}", "${n}", "$0", "$9", "${nope}", "${open", "$x", "$", "a$", "$12" };

        /// <summary>Every explanation line the builder can write for the sample patterns: tree rows (brief and detailed), flat token meanings and replacement meanings.</summary>
        private static List<(string Source, string Text)> ExplanationLines()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var all = new List<(string, string)>();
            void Add(string source, string text)
            {
                if (string.IsNullOrWhiteSpace(text) || !seen.Add(text)) return;
                all.Add((source, text));
            }
            void Walk(RegexTree.Node n, string src)
            {
                Add(src + " brief", n.Brief); Add(src + " detailed", n.Detailed);
                foreach (var c in n.Children) Walk(c, src);
            }
            int pi = 0;
            foreach (var (pattern, options) in ExplainPatterns)
            {
                pi++;
                var tree = RegexTree.Parse(pattern, options);
                Walk(tree.Root, "tree " + pi);
                Add("tree note " + pi, tree.Note);
                foreach (var t in RegexKit.Explain(pattern)) Add("flat " + pi + " :: " + t.Text, t.Meaning);
            }
            all.RemoveAll(l => l.Item2.Contains("RegexOptions.NonBacktracking is not supported"));   // the rest of that note is the .NET engine's own English message
            var rx = new Regex(@"(?<n>a)(b)");
            foreach (var r in ExplainReplacements)
                foreach (var t in RegexKit.ExplainReplacement(r, rx)) Add("replacement " + r, t.Meaning);
            return all;
        }

        private static Task RegexExplainChecksAsync(string dir)
        {
            Check("language: every regex explanation line is translated, in Cantonese and in bilingual mode", () =>
            {
                var s = Settings.Current;
                var saved = s.LanguageMode;
                var lines = ExplanationLines();
                var missing = new List<string>();
                try
                {
                    s.LanguageMode = LanguageMode.Cantonese;
                    foreach (var (source, text) in lines)
                    {
                        if (!LanguageAudit.HasUntranslated(text)) continue;   // a name, a code or a number that stays as it is
                        var yue = Core.Language.T(text);
                        if (!Core.Language.HasCjk(yue) || LanguageAudit.HasUntranslated(yue)) missing.Add($"\"{text}\" ({source})");
                    }
                    s.LanguageMode = LanguageMode.Bilingual;
                    foreach (var (source, text) in lines)
                    {
                        if (!LanguageAudit.HasUntranslated(text)) continue;
                        var both = Core.Language.T(text);
                        if (!Core.Language.HasCjk(both) || !both.Contains(text.Trim()) && !text.Contains('\n')) missing.Add($"bilingual \"{text}\" gives \"{both}\" ({source})");
                    }
                }
                finally { s.LanguageMode = saved; }
                try { System.IO.File.WriteAllLines(System.IO.Path.Combine(dir, "regex-explain-missing.txt"), missing); } catch { }
                if (lines.Count < 150) return "only " + lines.Count + " distinct explanation lines were produced, so the sample patterns are too thin";
                return missing.Count == 0 ? null : missing.Count + " of " + lines.Count + " lines: " + string.Join(" | ", missing.Take(60));
            });
            return Task.CompletedTask;
        }
    }
}
