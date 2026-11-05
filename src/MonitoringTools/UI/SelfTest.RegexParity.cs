using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using MonitoringTools.Core;
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI
{
    // Checks for the regex builder parity ledger: the engines, code templates, library, sample tools, debugger, benchmark,
    // guided editing, help, preferences, file search extras and the command line, each driven through the real builder where it has one.
    public static partial class SelfTest
    {
        private static string RxFirstText(DependencyObject root, Func<string, bool> accept) =>
            Descend<TextBlock>(root).Select(t => t.Text).FirstOrDefault(t => t != null && accept(t));

        private static string RxAllText(DependencyObject root) => string.Join(" | ", Descend<TextBlock>(root).Select(t => t.Text).Where(t => !string.IsNullOrEmpty(t)));

        private static string RxRun(string exe, string args, string stdin, out int exit, IDictionary<string, string> env = null)
        {
            var psi = new ProcessStartInfo(exe, args) { RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true, UseShellExecute = false, CreateNoWindow = true, StandardOutputEncoding = new UTF8Encoding(false) };
            if (env != null) foreach (var kv in env) psi.Environment[kv.Key] = kv.Value;
            using var p = Process.Start(psi);
            if (stdin != null) p.StandardInput.Write(stdin);
            p.StandardInput.Close();
            var o = p.StandardOutput.ReadToEndAsync(); var e = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(30000)) { try { p.Kill(true); } catch { } exit = -99; return "timed out"; }
            exit = p.ExitCode;
            return o.Result + e.Result;
        }

        private static async Task RegexParityChecksAsync(string dir)
        {
            var saved = Settings.Current;
            var work = Path.Combine(dir, "regex-parity");
            Directory.CreateDirectory(work);

            // ================================================================ debugger
            Check("regex parity debug: lookahead, lookbehind, atomic groups and Unicode categories trace, and every capture agrees with .NET", () =>
            {
                var cases = new (string Pattern, string Subject)[]
                {
                    (@"foo(?=bar)", "xx foobar yy"), (@"(?<=\$)(\d+)", "cost $15 now"), (@"(?<!a)b", "ab cb"), (@"(?!x)\w(\w)", "xa ba"),
                    (@"(?>a+)b", "aaab"), (@"(?>a+)ab", "aaab"), (@"\p{Lu}(\p{Ll}+)", "see Zurich now"), (@"[\p{Nd}x]+", "ab 123x"), (@"\P{L}+", "ab12cd"),
                    (@"(\w+)@(\w+)\.com", "mail bob@site.com"), (@"(a)|(b)", "b"),
                };
                var bad = new List<string>();
                foreach (var (pattern, subject) in cases)
                {
                    var r = RegexTrace.Run(pattern, RegexOptions.None, subject);
                    if (pattern == @"(?>a+)ab") { if (r.Unsupported != null || r.Disagreement != null || r.Matched) bad.Add(pattern + " should trace a failed atomic match: " + (r.Unsupported ?? r.Disagreement ?? "matched")); continue; }
                    if (r.Unsupported != null || r.Disagreement != null) { bad.Add(pattern + ": " + (r.Unsupported ?? r.Disagreement)); continue; }
                    if (!r.Verified || !r.CapturesVerified || r.Steps.Count < 3) bad.Add(pattern + ": verified=" + r.Verified + " captures=" + r.CapturesVerified + " steps=" + r.Steps.Count);
                }
                // the walk-through names the new constructs in plain words
                var look = RegexTrace.Run(@"foo(?=bar)", RegexOptions.None, "foobar");
                if (!look.Steps.Any(s => s.Text.Contains("look-ahead"))) bad.Add("no step names the look-ahead");
                var atom = RegexTrace.Run(@"(?>a+)b", RegexOptions.None, "aaab");
                if (!atom.Steps.Any(s => s.Text.Contains("atomic"))) bad.Add("no step names the atomic group");
                return bad.Count == 0 ? null : string.Join(" ; ", bad);
            });
            Check("regex parity debug: what is still not traced is named, never faked", () =>
            {
                foreach (var p in new[] { "(?x)a b", "(?(foo)a|b)", @"\p{IsGreek}", "a(?#note)" })
                {
                    var r = RegexTrace.Run(p, RegexOptions.None, "a");
                    if (r.Unsupported == null || r.Steps.Count != 0) return p + " was traced or accepted";
                }
                return null;
            });

            // ================================================================ flavours and conversion
            Check("regex parity flavours: translation to Python, JavaScript and Java names every limit it cannot translate", () =>
            {
                var py = RegexFlavours.Convert("Python", @"(?<y>\d{4})\k<y>\z", RegexOptions.IgnoreCase | RegexOptions.Multiline);
                if (py.Pattern != @"(?P<y>\d{4})(?P=y)\Z" || py.Flags != "im") return "python: " + py.Pattern + " / " + py.Flags;
                var js = RegexFlavours.Convert("JavaScript", @"\Aab(?>c)", RegexOptions.Singleline);
                if (!js.Pattern.StartsWith("(?<![\\s\\S])ab") || js.Flags != "s" || !js.Limits.Any(l => l.Contains("Atomic"))) return "javascript: " + js.Pattern + " / " + string.Join(",", js.Limits);
                var java = RegexFlavours.Convert("Java", @"(?<n>a)\p{L}[a-z-[aeiou]]", RegexOptions.None);
                if (!java.Limits.Any(l => l.Contains("subtraction"))) return "java did not name the class subtraction";
                var bal = RegexFlavours.Convert("Python", @"(?<o>\()(?<-o>\))", RegexOptions.None);
                if (!bal.Limits.Any(l => l.Contains("Balancing"))) return "balancing groups not named";
                var lim = new List<string>();
                var r1 = RegexFlavours.ConvertReplacement("Python", "$1-${name}-$$-$0", lim);
                var r2 = RegexFlavours.ConvertReplacement("JavaScript", "$1-${name}-$$-$&", lim);
                var r3 = RegexFlavours.ConvertReplacement("Java", "$1-${name}-$$", lim);
                return r1 == "\\g<1>-\\g<name>-$-\\g<0>" && r2 == "$1-$<name>-$$-$&" && r3 == "$1-${name}-\\$" ? null : $"replacements: {r1} | {r2} | {r3}";
            });
            Check("regex parity flavours: the pattern really runs in the Windows JScript engine and in Windows PowerShell 5.1, and the answers are compared", () =>
            {
                var sample = "a 12 b 345 c 6";
                var all = RegexFlavours.RunAll(@"\d+", RegexOptions.None, sample, includeExternal: true);
                var bad = new List<string>();
                foreach (var e in all)
                {
                    if (!e.Available) { if (e.Engine.StartsWith(".NET") || e.Engine.Contains("PowerShell") || e.Engine.Contains("JScript")) bad.Add(e.Engine + " should be available on Windows: " + e.Note); continue; }
                    if (e.Error != null) { bad.Add(e.Engine + " rejected a plain pattern: " + e.Error); continue; }
                    if (e.Matches != 3) bad.Add(e.Engine + " found " + e.Matches + " matches, not 3");
                    else if (RegexFlavours.Difference(all[0].Hits, e.Hits) != "Identical matches.") bad.Add(e.Engine + " disagrees: " + RegexFlavours.Difference(all[0].Hits, e.Hits));
                }
                // a construct the JScript engine does not have comes back as that engine's own error
                var js = RegexFlavours.Jscript(@"(?<=a)b", RegexOptions.None, "ab");
                if (js.Available && js.Error == null) bad.Add("JScript accepted a look-behind");
                // an engine that behaves differently is reported as different, not hidden: ignore case differs only by option, so use \A which JScript lacks
                var anchor = RegexFlavours.Jscript(@"\Aab", RegexOptions.None, "ab");
                if (anchor.Available && anchor.Error == null && anchor.Matches == 1) bad.Add("JScript treated \\A as a start anchor, which it is not in that flavour");
                var names = string.Join(", ", all.Select(e => e.Engine + (e.Available ? " ran" : " unavailable")));
                return bad.Count == 0 ? null : string.Join(" ; ", bad) + " [" + names + "]";
            });
            Check("regex parity flavours: a translated pattern gives the same matches in Python and Node.js when they are installed", () =>
            {
                var sample = "2026-11 and 2027-02 and x";
                var o = RegexOptions.None;
                var pattern = @"(?<y>\d{4})-(?<m>\d\d)";
                var net = RegexFlavours.Dotnet(pattern, o, sample);
                var bad = new List<string>();
                int ran = 0;
                foreach (var e in new[] { RegexFlavours.Python(pattern, o, sample), RegexFlavours.Node(pattern, o, sample) })
                {
                    if (!e.Available) continue;
                    ran++;
                    if (e.Error != null) { bad.Add(e.Engine + ": " + e.Error); continue; }
                    if (RegexFlavours.Difference(net.Hits, e.Hits) != "Identical matches.") bad.Add(e.Engine + ": " + RegexFlavours.Difference(net.Hits, e.Hits));
                }
                // an astral character moves Python's code point positions; they are mapped back to .NET positions
                var astral = "😀 12 and 34";
                var py = RegexFlavours.Python(@"\d+", o, astral);
                if (py.Available && py.Error == null)
                {
                    var n2 = RegexFlavours.Dotnet(@"\d+", o, astral);
                    if (RegexFlavours.Difference(n2.Hits, py.Hits) != "Identical matches.") bad.Add("python astral: " + RegexFlavours.Difference(n2.Hits, py.Hits));
                }
                // the engine's own error is reported, not swallowed
                var rejected = RegexFlavours.Node(@"(?>a)", o, "a");
                if (rejected.Available && rejected.Error == null) bad.Add("Node accepted an atomic group");
                return bad.Count == 0 ? null : string.Join(" ; ", bad) + $" (external engines run: {ran})";
            });

            // ================================================================ code templates
            Check("regex parity code: five languages by three kinds, every snippet uses its language's own literal and flags", () =>
            {
                Settings.Current.RegexCodeTemplates = new Dictionary<string, string>();
                var bad = new List<string>();
                foreach (var lang in RegexCode.Languages)
                    foreach (var kind in RegexCode.Kinds)
                    {
                        var code = RegexCode.Render(lang, kind, @"(?<n>\w+)\.png", RegexOptions.IgnoreCase | RegexOptions.Multiline, "${n}.webp");
                        if (code.Contains("{{")) bad.Add(lang + " " + kind + " has an unreplaced placeholder");
                        if (kind == "Replace" && !(code.Contains("webp"))) bad.Add(lang + " replace has no replacement");
                        if (kind == "Split" && !code.ToLowerInvariant().Contains("split")) bad.Add(lang + " split has no split");
                    }
                var cs = RegexCode.Render("C#", "Replace", "a", RegexOptions.None, "$1");
                var py = RegexCode.Render("Python", "Replace", @"(?<n>a)", RegexOptions.None, "${n}-$1");
                var js = RegexCode.Render("JavaScript", "Replace", @"(?<n>a)", RegexOptions.None, "${n}-$1");
                var java = RegexCode.Render("Java", "Match", @"a\d", RegexOptions.IgnoreCase | RegexOptions.Singleline, "");
                if (!cs.Contains("TimeSpan.FromMilliseconds(200)") || !cs.Contains("rx.Replace(text, @\"$1\")")) bad.Add("C# replace: " + cs);
                if (!py.Contains("\\\\g<n>-\\\\g<1>") || !py.Contains("(?P<n>a)")) bad.Add("python replace: " + py);
                if (!js.Contains("$<n>-$1") || !js.Contains("\"g\"")) bad.Add("js replace: " + js);
                if (!java.Contains("Pattern.CASE_INSENSITIVE | Pattern.DOTALL") || !java.Contains("\"a\\\\d\"")) bad.Add("java match: " + java);
                return bad.Count == 0 ? null : string.Join(" ; ", bad);
            });
            Check("regex parity code: the generated Python, JavaScript and PowerShell snippets run and print the matches", () =>
            {
                var bad = new List<string>();
                var o = RegexOptions.IgnoreCase;
                var text = "Alpha 12 beta 345";
                // Python
                var pyExe = FindExe("python.exe");
                if (pyExe != null)
                {
                    var code = "text = \"" + text + "\"\n" + RegexCode.Render("Python", "Match", @"\d+", o, "");
                    var f = Path.Combine(work, "snippet.py"); File.WriteAllText(f, code);
                    var outp = RxRun(pyExe, "-I \"" + f + "\"", "", out var exit);
                    if (exit != 0 || outp.Replace("\r", "").Trim() != "12\n345") bad.Add("python: exit " + exit + " " + outp.Trim());
                    var rep = "text = \"" + text + "\"\n" + RegexCode.Render("Python", "Replace", @"(?<n>\d+)", o, "<${n}>");
                    File.WriteAllText(f, rep);
                    outp = RxRun(pyExe, "-I \"" + f + "\"", "", out exit);
                    if (exit != 0 || outp.Trim() != "Alpha <12> beta <345>") bad.Add("python replace: " + outp.Trim());
                }
                // Node.js
                var nodeExe = FindExe("node.exe");
                if (nodeExe != null)
                {
                    var f = Path.Combine(work, "snippet.js");
                    File.WriteAllText(f, "const text = \"" + text + "\";\n" + RegexCode.Render("JavaScript", "Match", @"\d+", o, ""));
                    var outp = RxRun(nodeExe, "\"" + f + "\"", "", out var exit);
                    if (exit != 0 || outp.Replace("\r", "").Trim() != "12\n345") bad.Add("node: exit " + exit + " " + outp.Trim());
                    File.WriteAllText(f, "const text = \"" + text + "\";\n" + RegexCode.Render("JavaScript", "Replace", @"(?<n>\d+)", o, "<${n}>"));
                    outp = RxRun(nodeExe, "\"" + f + "\"", "", out exit);
                    if (exit != 0 || outp.Trim() != "Alpha <12> beta <345>") bad.Add("node replace: " + outp.Trim());
                }
                // Windows PowerShell 5.1
                var ps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
                if (File.Exists(ps))
                {
                    var f = Path.Combine(work, "snippet.ps1");
                    File.WriteAllText(f, "$text = 'Alpha 12 beta 345'\n" + RegexCode.Render("PowerShell", "Match", @"\d+", o, ""));
                    var outp = RxRun(ps, "-NoProfile -ExecutionPolicy Bypass -File \"" + f + "\"", "", out var exit);
                    if (exit != 0 || outp.Replace("\r", "").Trim() != "12\n345") bad.Add("powershell: exit " + exit + " " + outp.Trim());
                    File.WriteAllText(f, "$text = 'Alpha 12 beta 345'\n" + RegexCode.Render("PowerShell", "Split", @"\s+", o, ""));
                    outp = RxRun(ps, "-NoProfile -ExecutionPolicy Bypass -File \"" + f + "\"", "", out exit);
                    if (exit != 0 || outp.Replace("\r", "").Trim() != "Alpha\n12\nbeta\n345") bad.Add("powershell split: " + outp.Trim());
                }
                return bad.Count == 0 ? null : string.Join(" ; ", bad);
            });
            Check("regex parity code: an edited template is kept per language and kind, validated and restorable", () =>
            {
                Settings.Current.RegexCodeTemplates = new Dictionary<string, string>();
                var edited = "// mine\nRegex.IsMatch(text, {{literal}}); // {{timeout}} ms {{oops}}";
                RegexCode.SaveTemplate("C#", "Match", edited);
                var out1 = RegexCode.Render("C#", "Match", @"\d", RegexOptions.None, "");
                var other = RegexCode.Render("C#", "Split", @"\d", RegexOptions.None, "");
                var unknown = RegexCode.UnknownPlaceholders(edited);
                RegexCode.SaveTemplate("C#", "Match", RegexCode.DefaultTemplate("C#", "Match"));
                var back = RegexCode.IsCustom("C#", "Match");
                Settings.Current.RegexCodeTemplates = new Dictionary<string, string>();
                return out1 == "// mine\nRegex.IsMatch(text, @\"\\d\"); // 200 ms {{oops}}" && other.Contains("Split") && unknown.SequenceEqual(new[] { "oops" }) && !back ? null : "template: " + out1;
            });

            // ================================================================ library
            Check("regex parity library: descriptions, parameters with defaults, and collection search in plain and regex mode", () =>
            {
                var p = new SavedPattern { Name = "Dated file", Pattern = @"^{{prefix}}-\d{{{n}}}\.txt$", Description = "Finds report files by year", Parameters = new List<RegexParam> { new RegexParam { Name = "prefix", Default = "report" } } };
                var eff = RegexLibrary.EffectiveParameters(p);
                if (eff.Count != 2 || eff[0].Name != "prefix" || eff[1].Name != "n") return "effective parameters: " + string.Join(",", eff.Select(x => x.Name));
                // only {{name}} is a placeholder; {4} is a quantifier and stays
                var q = new SavedPattern { Name = "q", Pattern = @"^{{prefix}}-\d{4}$", Parameters = p.Parameters };
                var expandedQ = RegexLibrary.Expand(q, null, out var unfilledQ);
                if (expandedQ != @"^report-\d{4}$" || unfilledQ.Count != 0) return "quantifier was disturbed: " + expandedQ;
                var withValue = RegexLibrary.Expand(q, new Dictionary<string, string> { ["prefix"] = "inv" }, out _);
                if (withValue != @"^inv-\d{4}$") return "given value did not win: " + withValue;
                var escaped = RegexLibrary.Expand(q, new Dictionary<string, string> { ["prefix"] = "a.b" }, out _, escapeValues: true);
                if (escaped != @"^a\.b-\d{4}$") return "escaping: " + escaped;
                var half = RegexLibrary.Expand(p, null, out var unfilled);
                if (unfilled.Count != 1 || unfilled[0] != "n" || !half.Contains("{{n}}")) return "an unfilled placeholder was hidden";
                var parsed = RegexLibrary.ParseParameters("a=1\nb=x y;a=2;1bad=z", out var problems);
                if (parsed.Count != 2 || problems.Count != 2) return "parse: " + parsed.Count + " parameters, " + problems.Count + " problems";
                var all = new List<SavedPattern> { p, new SavedPattern { Name = "ISO date", Pattern = @"\d{4}-\d{2}-\d{2}", Description = "calendar dates" }, new SavedPattern { Name = "IPv4", Pattern = @"\d+\.\d+\.\d+\.\d+" } };
                if (RegexLibrary.Search(all, "report files", false, out _).Count != 1) return "plain search by description";
                if (RegexLibrary.Search(all, "DATES iso", false, out _).Count != 1) return "plain search needs every word, any case";
                if (RegexLibrary.Search(all, "ip.4", true, out _).Count != 1) return "regex search";
                RegexLibrary.Search(all, "(", true, out var err);
                return err != null ? null : "an invalid search pattern was not reported";
            });

            // ================================================================ sample tools
            Check("regex parity sample: scope per line and per page, line break conversion, encodings and the hex dump", () =>
            {
                var rx = new Regex(@"^\d+$");
                var text = "12\nab\r\n34";
                var whole = RegexSample.Run(rx, text, "Whole text");
                var lines = RegexSample.Run(rx, text, "Each line");
                if (whole.Matches.Count != 0 || lines.Matches.Count != 2 || lines.Matches[0].Index != 0 || lines.Matches[1].Index != 7) return $"line scope: whole {whole.Matches.Count}, lines {lines.Matches.Count} at {string.Join(",", lines.Matches.Select(m => m.Index))}";
                var pages = RegexSample.Run(new Regex(@"\Azero"), "zero\fone\fzero", "Each page");
                if (pages.Matches.Count != 2 || pages.Matches[1].Index != 9) return "page scope: " + pages.Matches.Count;
                if (RegexSample.NormalizeLineEndings("a\r\nb\rc\nd", "LF") != "a\nb\nc\nd" || RegexSample.NormalizeLineEndings("a\nb", "CRLF") != "a\r\nb" || RegexSample.NormalizeLineEndings("a\nb", "Keep") != "a\nb") return "line ending conversion";
                if (RegexSample.DescribeLineEndings("a\r\nb\nc\rd") != "1 CRLF, 1 LF, 1 CR") return "describe: " + RegexSample.DescribeLineEndings("a\r\nb\nc\rd");
                var u16 = Encoding.Unicode.GetBytes("h\u00E9");
                if (RegexSample.Decode(u16, "UTF-16", out _) != "h\u00E9" || RegexSample.Decode(new byte[] { 0x68, 0xE9 }, "Latin-1", out _) != "h\u00E9") return "decode";
                RegexSample.Decode(new byte[] { 0xEF, 0xBB, 0xBF, 0x41 }, "Latin-1", out var used);
                if (!used.Contains("byte order mark")) return "a byte order mark did not win over the chosen encoding";
                var hex = RegexSample.HexDump("AB\n", "UTF-8");
                if (!hex.Contains("41 42 0A") || !hex.Contains("|AB.|")) return "hex dump: " + hex;
                if (!RegexSample.HexDump("A", "UTF-16").Contains("41 00")) return "utf-16 hex";
                var lc = RegexSample.LineColumn("ab\ncd", 4);
                return lc == (2, 2) ? null : "line and column " + lc;
            });

            // ================================================================ benchmark
            Check("regex parity benchmark: two patterns side by side, a per-part profile, and a bounded saved history", () =>
            {
                var same = RegexBench.ComparePatterns(@"\d+", "[0-9]+", RegexOptions.None, "a 12 b 345");
                if (same.A.Matches != 2 || same.B.Matches != 2 || !same.Verdict.StartsWith("Both find the same 2")) return "same: " + same.Verdict;
                var diff = RegexBench.ComparePatterns(@"\d+", @"\d", RegexOptions.None, "a 12 b 345");
                if (diff.A.Matches != 2 || diff.B.Matches != 5 || !diff.Verdict.Contains("differ")) return "different: " + diff.Verdict;
                var bad = RegexBench.ComparePatterns(@"\d+", "(", RegexOptions.None, "1");
                if (bad.B.Error == null || !bad.Verdict.Contains("not valid")) return "an invalid pattern was not reported";
                var slow = RegexBench.ComparePatterns("^(a+)+$", "^a+$", RegexOptions.None, new string('a', 40) + "!");
                if (!slow.A.TimedOut || slow.B.TimedOut) return "the catastrophic pattern did not hit the limit while the safe one finished";
                var prof = RegexBench.Profile(@"\w+@\w+\.com", RegexOptions.None, string.Concat(Enumerable.Repeat("some text bob@site.com and more ", 20)));
                var timed = prof.Where(x => !x.Skipped).ToList();
                if (timed.Count < 4 || timed.Any(x => x.DeltaMs < 0) || timed.Last().PrefixMatches != 20) return $"profile: {timed.Count} timed parts, last matches {timed.LastOrDefault()?.PrefixMatches}";
                var alt = RegexBench.Profile("red|blue|green", RegexOptions.None, "blue red");
                if (alt.Count(x => !x.Skipped) != 3) return "alternation branches were not timed one by one";
                Settings.Current.RegexBenchHistory = new List<RegexBenchRecord>();
                var sb = new RegexBench.SubjectBench("x", 5, true, 0.5, false, new List<RegexBench.SizeTiming>(), "Linear", false);
                for (int i = 0; i < 35; i++) RegexBench.Remember("p" + i, RegexOptions.None, sb);
                var h = Settings.Current.RegexBenchHistory;
                return h.Count == RegexBench.MaxHistory && h[0].Pattern == "p34" ? null : "history size " + h.Count;
            });

            // ================================================================ guided editing and restructuring
            Check("regex parity tree: anchors, shorthands, escapes, comments, balancing groups and reordering rewrite only their own text", () =>
            {
                RegexTree.Node Find(string p, RegexTree.NodeKind k, Func<RegexTree.Node, bool> f = null) => RegexTree.Walk(RegexTree.Parse(p).Root).First(n => n.Kind == k && (f == null || f(n)));
                var a = RegexTree.Parse(@"^ab$");
                var anchor = RegexTree.Walk(a.Root).First(n => n.Kind == RegexTree.NodeKind.Anchor);
                if (RegexTree.ApplyAnchor(a.Pattern, anchor, @"\A") != @"\Aab$" || RegexTree.ApplyAnchor(a.Pattern, anchor, "nonsense") != null) return "anchor";
                var sh = Find(@"x\dy", RegexTree.NodeKind.Shorthand);
                if (RegexTree.ApplyShorthand(@"x\dy", sh, @"\W") != @"x\Wy") return "shorthand";
                var esc = Find(@"a\.b", RegexTree.NodeKind.Escape);
                if (RegexTree.ApplyEscape(@"a\.b", esc, @"\-") != @"a\-b" || RegexTree.ApplyEscape(@"a\.b", esc, "q") != "aqb" || RegexTree.ApplyEscape(@"a\.b", esc, "(") != null || RegexTree.ApplyEscape(@"a\.b", esc, @"\") != null) return "escape";
                var cm = Find("a(?#old)b", RegexTree.NodeKind.Comment);
                if (RegexTree.CommentText("a(?#old)b", cm) != "old" || RegexTree.ApplyComment("a(?#old)b", cm, "new note") != "a(?#new note)b" || RegexTree.ApplyComment("a(?#old)b", cm, "a)b") != null) return "comment";
                var bg = Find(@"(?<open>\()(?<a-open>\))", RegexTree.NodeKind.Group, n => n.Group == RegexTree.GroupKind.Balancing);
                var edited = RegexTree.ApplyBalancing(@"(?<open>\()(?<a-open>\))", bg, "b", "open");
                if (edited != @"(?<open>\()(?<b-open>\))" || RegexTree.ApplyBalancing(@"(?<open>\()(?<a-open>\))", bg, "b", "") != null) return "balancing: " + edited;
                // reordering: pieces keep their text, only the order changes; a result .NET rejects is refused
                var t = RegexTree.Parse("a+b*c");
                var kids = t.Root.Children;
                var swapped = RegexTree.Reorder(t.Pattern, kids[0], kids[1], after: true);
                if (swapped != "b*a+c") return "reorder: " + swapped;
                var bw = RegexTree.Parse("red|blue|green");
                var branches = RegexTree.Walk(bw.Root).Where(n => n.Kind == RegexTree.NodeKind.Branch).ToList();
                var moved = RegexTree.Reorder(bw.Pattern, branches[2], branches[0], after: false);
                if (moved != "green|red|blue") return "alternation reorder: " + moved;
                var g = RegexTree.Parse("(a)(?<n>b)");
                var gs = g.Root.Children;
                if (RegexTree.Reorder(g.Pattern, gs[0], gs[0], true) != null) return "a part was moved onto itself";
                return null;
            });

            // ================================================================ analysis export
            Check("regex parity export: the analysis carries the match results and a timing line when a sample is given", () =>
            {
                var res = RegexTree.Parse(@"(?<d>\d+)");
                var md = RegexTree.ToMarkdown(res, RegexOptions.None, false, "a 12 b 345");
                var html = RegexTree.ToHtml(res, RegexOptions.None, true, "a 12 b 345");
                var plain = RegexTree.ToMarkdown(res, RegexOptions.None, false);
                if (!md.Contains("## Match results and timing") || !md.Contains("2 match(es)") || !md.Contains("at 2, length 2") || !md.Contains("Timing:")) return "markdown: " + md[Math.Max(0, md.Length - 400)..];
                if (!html.Contains("Match results and timing") || !html.Contains("at 7, length 3")) return "html";
                return plain.Contains("Match results") ? "results appeared without a sample" : null;
            });

            // ================================================================ help
            Check("regex parity help: every page's example works in the real engine and the right page opens for each kind of part", () =>
            {
                var bad = new List<string>();
                foreach (var t in RegexHelp.Topics)
                {
                    var rx = RegexKit.TryCreate(t.Example, RegexOptions.None, out var err);
                    if (rx == null) { bad.Add(t.Id + ": " + err); continue; }
                    var n = rx.Matches(t.Sample).Count;
                    if (t.Id == "backtracking" || t.Id == "flags") continue;
                    if (n == 0) bad.Add(t.Id + ": the example matches nothing in its sample");
                }
                if (new Regex(@"^b", RegexOptions.Multiline).Matches("a\nb").Count != 1) bad.Add("flags example");
                string Id(string pattern, int at) { var tr = RegexTree.Parse(pattern); var node = RegexTree.Smallest(tr.Root, at, 1); while (node != null && RegexHelp.For(node) == null) node = node.Parent; return RegexHelp.For(node)?.Id; }
                var expect = new (string, int, string)[] { (@"(?<=\$)\d+", 1, "lookaround"), (@"a+", 1, "quantifiers"), (@"^x", 0, "anchors"), (@"(?>a)", 2, "atomic"), (@"[abc]", 1, "sets"), (@"(?<n>a)\k<n>", 8, "backreferences"), (@"\p{L}", 1, "properties"), (@"a|b", 1, "alternation"), (@"\d", 0, "shorthands") };
                foreach (var (p, at, want) in expect) { var got = Id(p, at); if (got != want) bad.Add($"{p} at {at}: {got} not {want}"); }
                if (RegexHelp.Search("lookbehind").Count == 0 || RegexHelp.Search("zzzzqq").Count != 0 || RegexHelp.Search("").Count != RegexHelp.Topics.Length) bad.Add("help search");
                return bad.Count == 0 ? null : string.Join(" ; ", bad);
            });

            // ================================================================ files and editors
            Check("regex parity files: file name pattern, every backup run listed and restorable, editor line arguments", () =>
            {
                var root = Path.Combine(work, "files"); if (Directory.Exists(root)) Directory.Delete(root, true);
                Directory.CreateDirectory(root);
                File.WriteAllText(Path.Combine(root, "a1.txt"), "needle one\n");
                File.WriteAllText(Path.Combine(root, "b.txt"), "needle two\n");
                File.WriteAllText(Path.Combine(root, "c2.log"), "needle three\n");
                var rx = new Regex("needle");
                var all = RegexGrep.Search(rx, new RegexGrep.Options { Folder = root }, default);
                var named = RegexGrep.Search(rx, new RegexGrep.Options { Folder = root, NameRegex = @"\d" }, default);
                var bad = RegexGrep.Search(rx, new RegexGrep.Options { Folder = root, NameRegex = "(" }, default);
                if (all.FilesWithHits != 3 || named.FilesWithHits != 2 || named.Files.Any(f => f.Path.EndsWith("b.txt"))) return $"name filter: all {all.FilesWithHits}, named {named.FilesWithHits}";
                if (bad.Skipped.Count == 0 || !bad.Skipped[0].Reason.Contains("file name pattern")) return "an invalid file name pattern was not reported";
                var oldOverride = Settings.DataDirOverride;
                Settings.DataDirOverride = Path.Combine(work, "data");
                try
                {
                    if (Directory.Exists(RegexGrep.BackupRoot)) Directory.Delete(RegexGrep.BackupRoot, true);
                    var r1 = RegexGrep.Apply(rx, "first", RegexGrep.Plan(named), default);
                    System.Threading.Thread.Sleep(1100);
                    var plan2 = RegexGrep.Plan(RegexGrep.Search(new Regex("first"), new RegexGrep.Options { Folder = root, NameRegex = @"a1" }, default));
                    var r2 = RegexGrep.Apply(new Regex("first"), "second", plan2, default);
                    var backups = RegexGrep.ListBackups();
                    if (r1.FilesChanged != 2 || r2.FilesChanged != 1 || backups.Count != 2) return $"backups: {backups.Count} runs ({r1.FilesChanged}, {r2.FilesChanged})";
                    // restore the OLDER run, which the old button could not reach
                    var older = backups[1];
                    var restored = RegexGrep.RestoreFolder(older.Folder);
                    var text = File.ReadAllText(Path.Combine(root, "a1.txt"));
                    if (restored.Restored != 2 || !text.StartsWith("needle one")) return "restore of an older run: " + restored.Restored + " " + text.Trim();
                }
                finally { Settings.DataDirOverride = oldOverride; }
                var vs = ExternalEditor.LineArguments(new EditorInfo { Id = "vscode", Path = "x" }, @"C:\a b\f.txt", 12, 5);
                var npp = ExternalEditor.LineArguments(new EditorInfo { Id = "notepadpp", Path = "x" }, @"C:\f.txt", 7, 0);
                var dflt = ExternalEditor.LineArguments(new EditorInfo { Id = "default" }, @"C:\f.txt", 7, 1);
                if (vs == null || vs[0] != "-g" || vs[1] != @"C:\a b\f.txt:12:5" || npp == null || npp[0] != "-n7" || npp[1] != "-c1" || dflt != null) return "line arguments";
                return null;
            });

            // ================================================================ command line
            Check("regex parity cli: library by name with parameters, in-place replace with a backup, and exit codes", () =>
            {
                var exe = Environment.ProcessPath;
                if (exe == null || !exe.EndsWith("MonitoringTools.exe", StringComparison.OrdinalIgnoreCase)) return null;   // run only from the real executable
                var data = Path.Combine(work, "cli-data"); if (Directory.Exists(data)) Directory.Delete(data, true);
                Directory.CreateDirectory(data);
                var s = new Settings();
                s.SavedPatterns.Add(new SavedPattern { Name = "Dated", Pattern = @"{{word}}-\d+", Replacement = "[$0]", Options = (int)RegexOptions.IgnoreCase, Parameters = new List<RegexParam> { new RegexParam { Name = "word", Default = "inv" } } });
                File.WriteAllText(Path.Combine(data, "settings.json"), System.Text.Json.JsonSerializer.Serialize(s));
                var env = new Dictionary<string, string> { ["MT_DATA_DIR"] = data };
                var f = Path.Combine(work, "cli.txt");
                File.WriteAllText(f, "INV-12 and rep-7 and inv-9\n");
                var list = RxRun(exe, "--regex --list-library", null, out var e0, env);
                if (e0 != 0 || !list.Contains("Dated")) return "list-library: " + e0 + " " + list.Trim();
                var m1 = RxRun(exe, "--regex --lib Dated \"" + f + "\"", null, out var e1, env);
                if (e1 != 0 || !m1.Contains("INV-12") || !m1.Contains("inv-9") || m1.Contains("rep-7")) return "lib default: " + e1 + " " + m1.Trim();
                var m2 = RxRun(exe, "--regex --lib Dated --param word=rep \"" + f + "\"", null, out var e2, env);
                if (e2 != 0 || !m2.Contains("rep-7") || m2.Contains("INV-12")) return "lib param: " + e2 + " " + m2.Trim();
                var m3 = RxRun(exe, "--regex --lib NoSuchName \"" + f + "\"", null, out var e3, env);
                if (e3 != 2) return "an unknown saved pattern returned " + e3;
                var before = File.ReadAllText(f);
                var dry = RxRun(exe, "--regex \"inv-\\d+\" --options i --replace [x] \"" + f + "\"", null, out var e4, env);
                if (e4 != 0 || File.ReadAllText(f) != before) return "a plain replace changed the file";
                var inplace = RxRun(exe, "--regex --lib Dated --in-place \"" + f + "\"", null, out var e5, env);
                var after = File.ReadAllText(f);
                if (e5 != 0 || after != "[INV-12] and rep-7 and [inv-9]\n" || !inplace.Contains("2 replacement(s) in 1 file(s)")) return "in-place: " + e5 + " " + inplace.Trim() + " / " + after.Trim();
                var backup = Directory.GetDirectories(Path.Combine(data, "regex-backups")).FirstOrDefault();
                if (backup == null || !File.Exists(Path.Combine(backup, "manifest.json")) || !Directory.GetFiles(backup, "*cli.txt").Any(b => File.ReadAllText(b) == before)) return "no backup of the original";
                var noReplace = RxRun(exe, "--regex a --in-place \"" + f + "\"", null, out var e6, env);
                return e6 == 2 ? null : "--in-place without --replace returned " + e6;
            });

            RegexParityGapChecks(work);
            await RegexParityUiChecksAsync(work);
            Settings.Current.RegexCodeTemplates = saved.RegexCodeTemplates ?? new Dictionary<string, string>();
        }

        private static string FindExe(string file)
        {
            foreach (var d in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                try { var f = Path.Combine(d.Trim(), file); if (!string.IsNullOrWhiteSpace(d) && File.Exists(f) && f.IndexOf("WindowsApps", StringComparison.OrdinalIgnoreCase) < 0) return f; } catch { }
            }
            return null;
        }

        // ==================================================================== the real builder window

        private static ButtonBase RxChip(DependencyObject root, string label) => FindButton(root, label);

        private static async Task RegexParityUiChecksAsync(string work)
        {
            var cc = new ControlCentreWindow { Width = 1180, Height = 820 };
            SearchField f = null;
            RegexBuilderPopup b = null;
            var settings = Settings.Current;
            var keepHist = settings.RegexKeepHistory;
            try
            {
                cc.Show(); await Task.Delay(900);
                cc.Navigate("overview", animate: false); await Task.Delay(400);
                f = cc.PageSearch;
                if (f == null) { Results.Add(new Result("regex parity ui: the page search field exists", false, "no PageSearch")); return; }
                settings.RegexHiddenTabs = new List<string>(); settings.RegexDefaultTab = "Build"; settings.RegexCardWidth = 0; settings.RegexCardMaxHeight = 620;
                f.State.SetRegex(true);
                f.State.SetQuery(@"^\d+$");
                f.OpenBuilder("Test"); await Task.Delay(500);
                b = f.Builder;
                var card = f.BuilderCard;

                await CheckAsync("regex parity ui test: scope chips, line break conversion, paste, file read and the hex view drive the real Test view", async () =>
                {
                    var sample = FindBox(card, "Sample text to test the pattern against");
                    if (sample == null) return "no sample box";
                    sample.Text = "12\nab\r\n34";
                    await Task.Delay(250);
                    if (!RxAllText(card).Contains("0 match(es)")) return "whole-text scope should find nothing: " + RxFirstText(card, t => t.Contains("match(es)"));
                    if (!Press(card, "Each line")) return "no Each line chip";
                    await Task.Delay(250);
                    var summary = RxFirstText(card, t => t.Contains("match(es)"));
                    if (summary == null || !summary.StartsWith("2 match(es)")) return "per-line scope shows: " + summary;
                    if (!RxAllText(card).Contains("line 3, at 7")) return "the match list does not name the line";
                    if (!Press(card, "Convert to LF")) return "no LF chip";
                    await Task.Delay(200);
                    if (sample.Text.Contains('\r')) return "the sample still has a carriage return";
                    if (!Press(card, "Convert to CRLF")) return "no CRLF chip";
                    await Task.Delay(200);
                    if (!sample.Text.Contains("\r\n") || sample.Text.Replace("\r\n", "").Contains('\n')) return "CRLF conversion";
                    if (!RxAllText(card).Contains("2 CRLF")) return "the line break description did not update: " + RxFirstText(card, t => t.StartsWith("Sample:") || t.StartsWith("Line breaks"));
                    bool realClipboard = true;
                    try { Clipboard.SetText("x1\ny22\nz"); } catch (Exception) { realClipboard = false; }   // a hidden desktop may have no clipboard to open
                    if (!Press(card, "Paste from clipboard")) return "no paste chip";
                    if (!realClipboard) { await Task.Delay(100); b.PasteText("x1\ny22\nz"); }
                    await Task.Delay(250);
                    if (sample.Text != "x1\ny22\nz") return "paste gave: " + sample.Text;
                    var file = Path.Combine(work, "sample-utf16.txt");
                    File.WriteAllBytes(file, new byte[] { 0xFF, 0xFE }.Concat(Encoding.Unicode.GetBytes("77\r\nq")).ToArray());
                    b.LoadSampleFile(file);
                    await Task.Delay(250);
                    if (sample.Text != "77\r\nq") return "file read gave: " + sample.Text;
                    if (!RxAllText(card).Contains("UTF-16")) return "the encoding that was read is not named";
                    Press(card, "Hex view");
                    await Task.Delay(250);
                    var hex = FindBox(card, "Sample as hexadecimal bytes");
                    if (hex == null || hex.Visibility != Visibility.Visible || !hex.Text.Contains("37 37 0D 0A 71")) return "hex view: " + hex?.Text;
                    Press(card, "UTF-16"); await Task.Delay(200);
                    if (!hex.Text.Contains("37 00 37 00")) return "UTF-16 hex: " + hex.Text;
                    Press(card, "Hex view"); await Task.Delay(150);
                    return hex.Visibility == Visibility.Collapsed ? null : "the hex view did not close";
                });

                f.OpenBuilder("Compare"); await Task.Delay(400);
                await CheckAsync("regex parity ui compare: Run in other engines lists each engine, its matches and how it differs from .NET", async () =>
                {
                    f.State.SetQuery(@"\d+");
                    f.State.Sample = "a 12 b 345";
                    await Task.Delay(250);
                    if (!Press(card, "Run in other engines")) return "no button";
                    for (int i = 0; i < 80 && !RxAllText(card).Contains("Sample of"); i++) await Task.Delay(250);
                    var text = RxAllText(card);
                    foreach (var want in new[] { ".NET", "JScript", "Windows PowerShell 5.1" }) if (!text.Contains(want)) return "missing engine " + want + " in: " + text[^Math.Min(text.Length, 500)..];
                    if (!text.Contains("2 match(es)") && !text.Contains("3 match(es)")) return "no match counts in: " + text[^Math.Min(text.Length, 400)..];
                    // an unavailable engine is named as such rather than skipped
                    return text.Contains("Python") && text.Contains("Node") ? null : "the Python or Node.js row is missing (shown as ran or not available)";
                });

                f.OpenBuilder("Benchmark"); await Task.Delay(400);
                await CheckAsync("regex parity ui benchmark: pattern pair, profile and a saved result", async () =>
                {
                    settings.RegexBenchHistory = new List<RegexBenchRecord>();
                    f.State.SetQuery(@"\d+"); f.State.Sample = "a 12 b 345 c 6";
                    await Task.Delay(250);
                    var second = FindBox(card, "Second pattern to compare with the first");
                    if (second == null) return "no second pattern box";
                    second.Text = "[0-9]+";
                    if (!Press(card, "Compare the two patterns")) return "no compare button";
                    for (int i = 0; i < 40 && !RxAllText(card).Contains("Both find"); i++) await Task.Delay(150);
                    if (!RxAllText(card).Contains("Both find the same 3 match(es)")) return "pair verdict: " + RxFirstText(card, t => t.Contains("Both find") || t.Contains("differ"));
                    if (!Press(card, "Profile the parts")) return "no profile button";
                    for (int i = 0; i < 40 && !RxAllText(card).Contains("Most added time"); i++) await Task.Delay(150);
                    if (!RxAllText(card).Contains("Most added time")) return "no profile summary";
                    if (!Press(card, "Run benchmark")) return "no run button";
                    for (int i = 0; i < 80 && !RxAllText(card).Contains("Done."); i++) await Task.Delay(250);
                    if (!RxAllText(card).Contains("Done.")) return "the benchmark did not finish";
                    if (!Press(card, "Save last result")) return "no save button";
                    await Task.Delay(300);
                    return settings.RegexBenchHistory.Count == 1 && settings.RegexBenchHistory[0].Pattern == @"\d+" && RxAllText(card).Contains("Use pattern") ? null : "history: " + settings.RegexBenchHistory.Count;
                });

                f.OpenBuilder("Code"); await Task.Delay(400);
                await CheckAsync("regex parity ui code: language and kind chips, an edited template saved and restored", async () =>
                {
                    settings.RegexCodeTemplates = new Dictionary<string, string>();
                    f.State.SetQuery(@"(?<n>\d+)"); f.State.Replacement = "<${n}>";
                    await Task.Delay(250);
                    Press(card, "JavaScript"); await Task.Delay(250);
                    Press(card, "Replace snippet"); await Task.Delay(250);
                    var code = FindBox(card, "Generated code");
                    if (code == null || !code.Text.Contains("text.replace(rx, \"<$<n>>\")")) return "javascript replace: " + code?.Text;
                    Press(card, "Split snippet"); await Task.Delay(250);
                    code = FindBox(card, "Generated code");
                    if (!code.Text.Contains("text.split(rx)")) return "javascript split: " + code.Text;
                    Press(card, "Java"); await Task.Delay(250);
                    code = FindBox(card, "Generated code");
                    if (!code.Text.Contains("rx.split(text)") || !code.Text.Contains("Pattern.compile")) return "java split: " + code.Text;
                    Press(card, "C#"); await Task.Delay(200);
                    Press(card, "Match snippet"); await Task.Delay(200);
                    Press(card, "Edit template"); await Task.Delay(300);
                    var tpl = FindBox(card, "Code template");
                    if (tpl == null) return "no template box";
                    tpl.Text = "// my template\nvar r = new Regex({{literal}}); // {{nope}}";
                    await Task.Delay(250);
                    code = FindBox(card, "Generated code");
                    if (!code.Text.StartsWith("// my template") || !RxAllText(card).Contains("Unknown placeholder")) return "live preview: " + code.Text + " / " + RxFirstText(card, t => t.Contains("placeholder"));
                    Press(card, "Save template"); await Task.Delay(250);
                    if (!settings.RegexCodeTemplates.ContainsKey("C#|Match")) return "template was not stored";
                    Press(card, "Restore built-in"); await Task.Delay(250);
                    code = FindBox(card, "Generated code");
                    return settings.RegexCodeTemplates.Count == 0 && code.Text.Contains("TimeSpan.FromMilliseconds(200)") ? null : "restore: " + code.Text;
                });

                f.OpenBuilder("Library"); await Task.Delay(400);
                await CheckAsync("regex parity ui library: description, parameters with defaults, search and Use", async () =>
                {
                    settings.SavedPatterns.Clear();
                    f.State.SetQuery(@"^{{prefix}}-\d+$");
                    await Task.Delay(250);
                    FindBox(card, "Name for the saved pattern").Text = "Dated file";
                    FindBox(card, "Description of the saved pattern").Text = "Finds report numbers";
                    FindBox(card, "Parameters of the saved pattern, one name=default per line").Text = "prefix=report";
                    Press(card, "Save"); await Task.Delay(400);
                    if (settings.SavedPatterns.Count != 1 || settings.SavedPatterns[0].Description != "Finds report numbers" || settings.SavedPatterns[0].Parameters.Count != 1) return "entry: " + settings.SavedPatterns.Count;
                    var pbox = FindBox(card, "Value for parameter prefix of Dated file");
                    if (pbox == null || pbox.Text != "report") return "no parameter box with its default";
                    var search = FindBox(card, "Search the saved patterns");
                    search.Text = "zzz"; await Task.Delay(300);
                    if (!RxAllText(card).Contains("No saved pattern matches the search")) return "search found something for zzz";
                    search = FindBox(card, "Search the saved patterns");
                    search.Text = "report numbers"; await Task.Delay(300);
                    pbox = FindBox(card, "Value for parameter prefix of Dated file");
                    if (pbox == null) return "search by description lost the entry";
                    pbox.Text = "inv"; await Task.Delay(100);
                    Press(card, "Use"); await Task.Delay(300);
                    return f.State.Query == @"^inv-\d+$" ? null : "Use gave: " + f.State.Query;
                });

                f.State.SetQuery(@"(?<=\$)\d+");
                f.OpenBuilder("Help"); await Task.Delay(400);
                await CheckAsync("regex parity ui help: the page for the selected part comes first, pages search, an example loads, preferences change the builder", async () =>
                {
                    b.PatternBox.Focus(); b.PatternBox.Select(1, 3); await Task.Delay(200);
                    f.OpenBuilder("Help"); await Task.Delay(300);
                    var text = RxAllText(card);
                    if (!text.ToLowerInvariant().Contains("for the selected part") || !text.Contains("Lookahead and lookbehind")) return "no contextual page: " + text[..Math.Min(text.Length, 300)];
                    var search = FindBox(card, "Search the help pages");
                    search.Text = "balancing"; await Task.Delay(300);
                    if (FindButton(card, "Balancing groups") == null || FindButton(card, "Atomic groups") != null) return "help search";
                    Press(card, "Balancing groups"); await Task.Delay(250);
                    Press(card, "Use this example"); await Task.Delay(300);
                    if (!f.State.Query.Contains("(?<o>")) return "example not loaded: " + f.State.Query;
                    // preferences
                    var before = card.Width;
                    Press(card, "Extra wide"); await Task.Delay(250);
                    if (settings.RegexCardWidth != 1100 || card.Width != 1100) return "width " + settings.RegexCardWidth + " / " + card.Width;
                    Press(card, "Short"); await Task.Delay(200);
                    if (settings.RegexCardMaxHeight != 420) return "height pref " + settings.RegexCardMaxHeight;
                    Press(card, "Show Files"); await Task.Delay(250);   // hides the Files tab
                    if (b.VisibleTabs.Contains("Files") || !b.VisibleTabs.Contains("Build") || !b.VisibleTabs.Contains("Help")) return "hidden tab: " + string.Join(",", b.VisibleTabs);
                    Press(card, "Start on Test"); await Task.Delay(150);
                    if (settings.RegexDefaultTab != "Test") return "the start view was not stored";
                    // wait for the close to finish, then past the guard that stops the closing click reopening it
                    b.Close();
                    for (var i = 0; i < 30 && b.IsOpen; i++) await Task.Delay(100);
                    if (b.IsOpen) return "the builder did not close";
                    await Task.Delay(400);
                    f.OpenBuilder();
                    for (var i = 0; i < 30 && !(b.IsOpen && b.Tab == "Test"); i++) await Task.Delay(100);
                    if (!b.IsOpen) return "the builder did not reopen";
                    if (b.Tab != "Test") return "the builder opened on " + b.Tab + " instead of Test";
                    f.OpenBuilder("Help"); await Task.Delay(300);
                    Press(card, "Reset preferences"); await Task.Delay(300);
                    return settings.RegexCardWidth == 0 && settings.RegexCardMaxHeight == 620 && settings.RegexHiddenTabs.Count == 0 && settings.RegexDefaultTab == "Build" && b.VisibleTabs.Contains("Files") ? null : "reset";
                });

                f.OpenBuilder("Build"); await Task.Delay(400);
                await CheckAsync("regex parity ui build: selecting an anchor, a comment or a balancing group opens its guided editor and applying it rewrites the pattern", async () =>
                {
                    f.State.SetQuery(@"^ab(?#old)$");
                    await Task.Delay(350);
                    b.PatternBox.Focus(); b.PatternBox.Select(0, 1); await Task.Delay(300);
                    if (!Press(b.EditorPanel, @"\A")) return "no anchor chip: " + RxFirstText(card, t => t.StartsWith("Selected"));
                    await Task.Delay(300);
                    if (f.State.Query != @"\Aab(?#old)$") return "anchor edit gave " + f.State.Query;
                    b.PatternBox.Focus(); b.PatternBox.Select(4, 7); await Task.Delay(300);
                    var cbox = FindBox(card, "Comment text");
                    if (cbox == null) return "no comment editor: " + RxFirstText(card, t => t.StartsWith("Selected"));
                    cbox.Text = "new note";
                    Press(b.EditorPanel, "Apply to the pattern"); await Task.Delay(300);
                    if (f.State.Query != @"\Aab(?#new note)$") return "comment edit gave " + f.State.Query;
                    f.State.SetQuery(@"(?<o>\()(?<a-o>\))"); await Task.Delay(350);
                    b.PatternBox.Focus(); b.PatternBox.Select(8, 10); await Task.Delay(300);
                    var gname = FindBox(b.EditorPanel, "Name the group captures into, empty for none");
                    if (gname == null) return "no balancing editor";
                    gname.Text = "b"; Press(b.EditorPanel, "Apply to the pattern"); await Task.Delay(300);
                    if (f.State.Query != @"(?<o>\()(?<b-o>\))") return "balancing edit gave " + f.State.Query;
                    // reordering: buttons and the drop that a drag would perform
                    f.State.SetQuery("a+b*c"); await Task.Delay(350);
                    var kids = b.CurrentTree.Root.Children;
                    b.PatternBox.Focus(); b.PatternBox.Select(kids[0].Start, kids[0].End - kids[0].Start); await Task.Delay(300);
                    if (!Press(b.EditorPanel, "Later")) return "no Later button";
                    await Task.Delay(300);
                    if (f.State.Query != "b*a+c") return "Later gave " + f.State.Query;
                    var tree = b.CurrentTree.Root.Children;
                    if (!b.DropNode(tree[2], tree[0])) return "drop refused";
                    await Task.Delay(300);
                    if (f.State.Query != "cb*a+") return "drop gave " + f.State.Query;
                    return null;
                });

                f.OpenBuilder("Files"); await Task.Delay(400);
                await CheckAsync("regex parity ui files: a file name pattern, saved searches, history and the backup list work in the Files view", async () =>
                {
                    var root = Path.Combine(work, "ui-files"); if (Directory.Exists(root)) Directory.Delete(root, true);
                    Directory.CreateDirectory(root);
                    File.WriteAllText(Path.Combine(root, "a1.txt"), "needle\n"); File.WriteAllText(Path.Combine(root, "b.txt"), "needle\n"); File.WriteAllText(Path.Combine(root, "c2.txt"), "needle\n");
                    settings.RegexGrepPresets = new List<RegexGrepPreset>(); settings.RegexGrepHistory = new List<RegexGrepRecord>();
                    f.State.SetQuery("needle");
                    f.OpenBuilder("Files"); await Task.Delay(400);
                    FindBox(card, "Folder to search").Text = root;
                    FindBox(card, "Regular expression for file names").Text = @"\d";
                    Press(card, "Search files");
                    for (int i = 0; i < 60 && !RxAllText(card).Contains(" in 2 of "); i++) await Task.Delay(200);
                    if (!RxAllText(card).Contains("2 match(es) in 2 of 2")) return "file name filter: " + RxFirstText(card, t => t.Contains("match(es) in"));
                    if (settings.RegexGrepHistory.Count != 1 || settings.RegexGrepHistory[0].FilesWithMatches != 2) return "history record";
                    FindBox(card, "Name for the saved file search").Text = "digits only";
                    Press(card, "Save these settings"); await Task.Delay(300);
                    if (settings.RegexGrepPresets.Count != 1 || settings.RegexGrepPresets[0].NameRegex != @"\d") return "preset";
                    FindBox(card, "Regular expression for file names").Text = "";
                    Press(card, "Load"); await Task.Delay(500);
                    if (FindBox(card, "Regular expression for file names")?.Text != @"\d" || FindBox(card, "Folder to search")?.Text != root) return "the preset did not come back";
                    Press(card, "Show history"); await Task.Delay(250);
                    if (!RxAllText(card).Contains("needle · 2 match(es) in 2 of 2")) return "history list: " + RxAllText(card)[^Math.Min(RxAllText(card).Length, 300)..];
                    Press(card, "Choose a backup..."); await Task.Delay(250);
                    return RxAllText(card).Contains("No backups yet") || RxAllText(card).Contains("Restore ") ? null : "no backup list";
                });

                await RegexParityGapUiChecksAsync(f, b, card, work);
            }
            finally
            {
                settings.RegexFileTypes = new List<RegexFileType>();
                settings.RegexKeepHistory = keepHist;
                settings.RegexHiddenTabs = new List<string>(); settings.RegexDefaultTab = "Build"; settings.RegexCardWidth = 0; settings.RegexCardMaxHeight = 620;
                try { f?.State.SetQuery(""); f?.State.SetRegex(false); } catch { }
                try { b?.Close(); } catch { }
                cc.Close();
            }
        }
    }
}
