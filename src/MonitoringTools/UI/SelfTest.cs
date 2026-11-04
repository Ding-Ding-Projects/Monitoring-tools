using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MonitoringTools.Core;
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI
{
    /// <summary>
    /// "--selftest DIR": drives real features inside the built app, in a throwaway data folder,
    /// and writes selftest.json with one pass or fail per check. Exit code 0 only when every check passes.
    /// It proves behaviour through the app's own code paths; it does not move the mouse.
    /// </summary>
    public static class SelfTest
    {
        private sealed record Result(string Name, bool Pass, string Detail);
        private static readonly List<Result> Results = new List<Result>();

        private static void Check(string name, Func<string> run)
        {
            try
            {
                var failure = run();
                Results.Add(new Result(name, failure == null, failure ?? "ok"));
            }
            catch (Exception ex) { Results.Add(new Result(name, false, ex.GetType().Name + ": " + ex.Message)); }
        }

        private static async Task CheckAsync(string name, Func<Task<string>> run)
        {
            try
            {
                var failure = await run();
                Results.Add(new Result(name, failure == null, failure ?? "ok"));
            }
            catch (Exception ex) { Results.Add(new Result(name, false, ex.GetType().Name + ": " + ex.Message)); }
        }

        public static async Task RunAsync(string dir)
        {
            dir = Path.GetFullPath(dir);
            Directory.CreateDirectory(dir);

            // ---- search
            Check("search: plain text needs every word", () =>
            {
                var s = SearchState.For("selftest:plain");
                s.SetQuery("poster png");
                return s.Matches("Launch poster.png") && !s.Matches("Launch poster.jpg") ? null : "plain word matching wrong";
            });
            Check("search: regex mode uses the .NET engine with captures", () =>
            {
                var s = SearchState.For("selftest:regex");
                s.SetRegex(true); s.SetQuery(@"(?<y>\d{4})-\d{2}-\d{2}");
                if (!s.Matches("Invoice 2026-10-31") || s.Matches("no date")) return "regex match wrong";
                var run = RegexKit.Test(s.Regex, "a 2026-10-31 b 2026-11-02");
                return run.Matches.Count == 2 && run.Matches[0].Groups.Any(g => g.Name == "y" && g.Value == "2026") ? null : "captures wrong";
            });
            Check("search: an invalid pattern filters nothing and says why", () =>
            {
                var s = SearchState.For("selftest:invalid");
                s.SetRegex(true); s.SetQuery("(unclosed");
                return s.Error != null && s.Matches("anything") ? null : "invalid pattern not reported";
            });
            Check("search: catastrophic pattern stops at the limit", () =>
            {
                var s = SearchState.For("selftest:redos");
                s.SetRegex(true); s.SetQuery("^(a+)+$");
                var hit = s.Matches(new string('a', 40) + "!");
                return !hit && s.TimedOut && RegexKit.Risk("^(a+)+$", s.EngineOptions) != null ? null : "timeout or risk warning missing";
            });
            Check("search: each field keeps its own state", () =>
            {
                var a = SearchState.For("selftest:a"); var b = SearchState.For("selftest:b");
                a.SetQuery("one"); b.SetQuery("two");
                return a.Query == "one" && b.Query == "two" ? null : "states leaked";
            });
            Check("regex builder: replace and split previews", () =>
            {
                var rx = new Regex(@"(?<name>\w+)\.png");
                var r = RegexKit.ReplacePreview(rx, "poster.png and logo.png", "${name}.webp", out var e1);
                var parts = RegexKit.SplitPreview(new Regex(","), "a,b,c", out var e2);
                return r == "poster.webp and logo.webp" && parts.Length == 3 && e1 == null && e2 == null ? null : "previews wrong: " + r;
            });
            Check("regex builder: code generation includes the timeout", () =>
                RegexKit.Code("C#", @"\d+", RegexOptions.IgnoreCase).Contains("TimeSpan.FromMilliseconds(200)") ? null : "no timeout in code");

            // ---- exports
            Check("exports: CSV quotes and defuses formulas", () =>
            {
                var t = new ExportTable { Name = "t", Columns = new[] { "a", "b" } };
                t.Rows.Add(new[] { "=1+1", "he said \"hi\", ok" });
                var csv = Exporter.Format(t, ExportFormat.Csv);
                return csv.Contains("'=1+1") && csv.Contains("\"he said \"\"hi\"\", ok\"") ? null : "csv wrong: " + csv;
            });
            Check("exports: HTML is escaped and self-contained", () =>
            {
                var t = new ExportTable { Name = "t", Columns = new[] { "a" } };
                t.Rows.Add(new[] { "<script>x</script>" });
                var html = Exporter.Format(t, ExportFormat.Html);
                return !html.Contains("<script>x") && html.Contains("&lt;script&gt;") && !html.Contains("http") ? null : "html not escaped";
            });

            // ---- settings history
            Check("settings history: a save records a snapshot", () =>
            {
                var before = SettingsHistory.List().Count;
                Settings.Current.ToastSeconds = Settings.Current.ToastSeconds + 1;
                Settings.Save();
                var after = SettingsHistory.List().Count;
                return after > before ? null : $"no snapshot ({before} → {after})";
            });
            Check("isolation: the run writes only to its throwaway folder", () =>
                Settings.DataDir.Contains("MonitoringTools-capture-") ? null : "data folder is the real one: " + Settings.DataDir);

            // ---- language
            Check("language: Cantonese table translates labels", () =>
            {
                var y = Core.Language.Yue("Clipboard history");
                return !string.IsNullOrEmpty(y) && y != "Clipboard history" ? null : "no Cantonese for Clipboard history";
            });
            await CheckAsync("language: switching mode rewrites an open window", async () =>
            {
                var cc = new ControlCentreWindow { Width = 1180, Height = 820 };
                cc.Show();
                await Task.Delay(900);
                Settings.Current.LanguageMode = LanguageMode.Cantonese;
                Core.Language.Refresh();
                await Task.Delay(300);
                var text = SearchScope.Text(cc);
                Settings.Current.LanguageMode = LanguageMode.English;
                Core.Language.Refresh();
                await Task.Delay(300);
                var back = SearchScope.Text(cc);
                cc.Close();
                return text.Contains("斷線次數") && !text.Contains("Outages") && back.Contains("Outages") ? null : "window text did not switch and restore";
            });

            // ---- appearance
            Check("appearance: light theme on Violet repaints the surface role", () =>
            {
                var s = Settings.Current;
                s.ThemeMode = "light"; s.SeedColour = "violet";
                Appearance.Apply();
                var c = ((SolidColorBrush)Application.Current.Resources["B.Window"]).Color;
                s.ThemeMode = "dark"; s.SeedColour = "mint";
                Appearance.Apply();
                var d = ((SolidColorBrush)Application.Current.Resources["B.Window"]).Color;
                return c == Color.FromRgb(0xFE, 0xF7, 0xFF) && d == Color.FromRgb(0x0F, 0x15, 0x12) ? null : $"surfaces {c} / {d}";
            });

            // ---- two-key confirmation
            await CheckAsync("confirmation: needs both keys and a full slide", async () =>
            {
                bool fired = false;
                var w = new ConfirmWindow("Remove?", "Self-test.", "Remove", danger: true);
                w.Confirmed += _ => fired = true;
                w.Show();
                await Task.Delay(400);
                var slider = (Slider)typeof(ConfirmWindow).GetField("_slider", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(w);
                if (slider.IsEnabled) { w.Close(); return "slider enabled with no keys"; }
                slider.Value = 100; // ignored while disabled
                if (fired) { w.Close(); return "confirmed without keys"; }
                w.ArmForPreview(10);
                if (!slider.IsEnabled) { w.Close(); return "slider still disabled after both keys"; }
                slider.Value = 100;
                await Task.Delay(900);
                return fired ? null : "full slide did not confirm";
            });

            // ---- authenticator
            Check("authenticator: RFC 6238 SHA1 vector", () =>
            {
                var key = Encoding.ASCII.GetBytes("12345678901234567890");
                var code = Totp.Code(key, 59, "SHA1", 8, 30);
                return code == "94287082" ? null : "got " + code;
            });

            // ---- QR pairing
            Check("qr: encode then decode an otpauth link in the app", () =>
            {
                const string uri = "otpauth://totp/Example:alice@example.com?secret=JBSWY3DPEHPK3PXP&issuer=Example&digits=6&period=30";
                if (!QrEncoder.TryEncode(uri, out var modules, out var version, out var err)) return "encode failed: " + err;
                var bmp = QrEncoder.ToBitmap(modules, 6, 4);
                if (!QrDecoder.TryDecode(bmp, out var text, out var derr)) return "decode failed: " + derr;
                return text == uri ? null : "decoded a different text (version " + version + ")";
            });

            // ---- regex debugger and compare
            Check("regex debug: the tracer agrees with .NET and records steps", () =>
            {
                var r = RegexTrace.Run(@"(\w+)@(\w+)\.com", RegexOptions.None, "mail alice@example.com now");
                if (r.Unsupported != null || r.Disagreement != null) return r.Unsupported ?? r.Disagreement;
                return r.Steps.Count > 5 ? null : "too few steps: " + r.Steps.Count;
            });
            Check("regex debug: unsupported constructs are named, not faked", () =>
            {
                var r = RegexTrace.Run(@"(?<=a)b", RegexOptions.None, "ab");
                return r.Unsupported != null && r.Steps.Count == 0 ? null : "lookbehind was traced";
            });
            Check("regex compare: non-backtracking refuses lookbehind with the engine's message", () =>
            {
                var modes = RegexBench.Compare(@"(?<=a)b", RegexOptions.None, "ab ab");
                var nb = modes.FirstOrDefault(m => m.Mode.IndexOf("non", StringComparison.OrdinalIgnoreCase) >= 0);
                var bt = modes.FirstOrDefault();
                return nb != null && !nb.Supported && bt != null && bt.Supported && bt.Matches == 2 ? null : "compare results unexpected";
            });

            // ---- locks
            Check("locks: a lock closes an element, refuses removal while locked, and opens with its PIN", () =>
            {
                var err = Locks.Create("selftest", "Selftest button", "Selftest button", LockPolicy.Pin, 5, "4826", null, null);
                if (err != null) return "lock not created: " + err;
                var e = Locks.Find("selftest", "Selftest button");
                var closed = Locks.IsLocked("selftest", "Selftest button");
                var refused = Locks.Remove(e.Id) != null;
                var wrong = new Locks.Session(e).Submit("0000");
                var right = new Locks.Session(e).Submit("4826");
                var open = !Locks.IsLocked("selftest", "Selftest button");
                var removed = Locks.Remove(e.Id) == null;
                return closed && refused && open && removed ? null : $"closed={closed} refused={refused} wrong={wrong} right={right} open={open} removed={removed}";
            });

            // ---- density
            Check("density: compact rows are shorter", () =>
            {
                var s = Settings.Current;
                s.CompactDensity = false; Appearance.Apply();
                var normal = Appearance.Row(40);
                s.CompactDensity = true; Appearance.Apply();
                var compact = Appearance.Row(40);
                s.CompactDensity = false; Appearance.Apply();
                return compact < normal ? null : $"rows {normal} / {compact}";
            });

            // ---- regex syntax tree
            Check("regex tree: ten patterns give the expected node counts and spans", () =>
            {
                var cases = new (string Pattern, int Nodes, string[] Spans)[]
                {
                    ("abc", 1, new[] { "Literal[0,3)" }),
                    (@"\d+", 2, new[] { "Quantifier[0,3)", "Shorthand[0,2)" }),
                    ("a|b", 5, new[] { "Alternation[0,3)", "Branch[2,3)" }),
                    ("(a)(?:b)", 4, new[] { "Group[0,3)", "Group[3,8)", "Literal[6,7)" }),
                    ("[a-z-[aeiou]]+", 3, new[] { "Quantifier[0,14)", "CharClass[0,13)", "Subtraction[5,12)" }),
                    (@"(?<year>\d{4})-(?<m>\d\d)", 7, new[] { "Group[0,14)", "Quantifier[8,13)", "Group[15,25)" }),
                    (@"(?<=\$)\d+(?!\d)", 6, new[] { "Group[0,7)", "Group[10,16)", "Escape[4,6)" }),
                    (@"^(?:(?<open>\()|(?<-open>\))|[^()])*(?(open)(?!))$", 17, new[] { "Group[4,15)", "Group[16,28)", "Conditional[36,49)", "Condition[38,44)", "Anchor[49,50)" }),
                    ("(?x) a+ # note\n b", 5, new[] { "InlineOptions[0,4)", "Comment[8,15)", "Literal[16,17)" }),
                    (@"(?<x>\p{Lu}+?)\k<x>{2}\G", 6, new[] { "Property[5,11)", "Backreference[14,19)", "Anchor[22,24)" }),
                };
                foreach (var (pattern, nodes, spans) in cases)
                {
                    var r = RegexTree.Parse(pattern);
                    if (!r.Valid) return $"{pattern}: not valid: {r.Message}";
                    if (r.Note != null) return $"{pattern}: disagreement with the engine: {r.Note}";
                    if (r.NodeCount != nodes) return $"{pattern}: {r.NodeCount} nodes, expected {nodes}";
                    var have = RegexTree.Walk(r.Root).Skip(1).Select(n => $"{n.Kind}[{n.Start},{n.End})").ToHashSet();
                    var missing = spans.FirstOrDefault(x => !have.Contains(x));
                    if (missing != null) return $"{pattern}: no node {missing}";
                    foreach (var n in RegexTree.Walk(r.Root).Skip(1))
                        if (n.Parent == null || n.Start < n.Parent.Start || n.End > n.Parent.End) return $"{pattern}: node {n.Kind}[{n.Start},{n.End}) is outside its parent";
                }
                return null;
            });
            Check("regex tree: a balancing group is recognised and counted as the engine counts it", () =>
            {
                var r = RegexTree.Parse(@"(?<open>\()(?<a-open>\))(?<-open>x)");
                var groups = RegexTree.Walk(r.Root).Where(n => n.Kind == RegexTree.NodeKind.Group).ToList();
                if (groups.Count != 3 || groups[1].Group != RegexTree.GroupKind.Balancing || groups[1].Name != "a" || groups[1].Other != "open") return "balancing group not parsed";
                if (groups[2].Group != RegexTree.GroupKind.Balancing || groups[2].Name != "" || groups[2].Captures) return "a -open group must not capture";
                return r.Valid && r.Note == null && r.Captures == 2 ? null : $"captures {r.Captures}, valid {r.Valid}, note {r.Note}";
            });
            Check("regex tree: a pattern .NET rejects reports the engine's message and position", () =>
            {
                var r = RegexTree.Parse("(a");
                return !r.Valid && r.EngineError != null && r.EngineOffset >= 0 ? null : "no engine error reported";
            });
            Check("regex tree: guided editing rewrites a quantifier, a set and a group opening", () =>
            {
                var q = RegexTree.Parse(@"a\d+b");
                var qn = RegexTree.Walk(q.Root).First(n => n.Kind == RegexTree.NodeKind.Quantifier);
                if (RegexTree.ApplyQuantifier(q.Pattern, qn, 2, 5, true) != @"a\d{2,5}?b") return "quantifier rewrite";
                if (RegexTree.ApplyQuantifier(q.Pattern, qn, 3, 1, false) != null) return "a reversed range was accepted";
                var c = RegexTree.Parse("x[a-z-[aeiou]]y");
                var cn = RegexTree.Walk(c.Root).First(n => n.Kind == RegexTree.NodeKind.CharClass);
                if (RegexTree.ApplyClass(c.Pattern, cn, "0-9", true) != "x[^0-9-[aeiou]]y") return "set rewrite";
                var g = RegexTree.Parse("(ab)+");
                var gn = RegexTree.Walk(g.Root).First(n => n.Kind == RegexTree.NodeKind.Group);
                return RegexTree.ApplyGroupKind(g.Pattern, gn, RegexTree.GroupKind.Named, "pair") == "(?<pair>ab)+" && RegexTree.ApplyGroupKind(g.Pattern, gn, RegexTree.GroupKind.Atomic, null) == "(?>ab)+" ? null : "group rewrite";
            });
            Check("regex replace: invalid references are flagged and valid ones explained", () =>
            {
                var rx = new Regex("(?<w>a)(b)");
                var t = RegexKit.ExplainReplacement("$1-${w}-$3-${zz}-$$", rx);
                var bad = t.Where(x => x.Problem).Select(x => x.Text).ToList();
                return bad.SequenceEqual(new[] { "$3", "${zz}" }) && t.Any(x => x.Text == "$$" && !x.Problem) ? null : "flagged: " + string.Join(" ", bad);
            });

            // ---- docs and pages
            Check("docs: the offline docs embed every article", () => Docs.All.Count >= 25 ? null : "only " + Docs.All.Count + " articles");
            await CheckAsync("pages: every control-centre page builds and gets a search bar", async () =>
            {
                var cc = new ControlCentreWindow { Width = 1180, Height = 820 };
                cc.Show();
                await Task.Delay(900);
                var bad = new List<string>();
                foreach (var id in cc.SectionIds.ToList())
                {
                    cc.Navigate(id, animate: false);
                    await Task.Delay(120);
                    if (cc.PageSearch == null && id != "help") bad.Add(id);
                }
                cc.Close();
                return bad.Count == 0 ? null : "no search on: " + string.Join(", ", bad);
            });

            var report = new { at = DateTime.Now.ToString("O"), version = typeof(SelfTest).Assembly.GetName().Version?.ToString(), passed = Results.Count(r => r.Pass), failed = Results.Count(r => !r.Pass), results = Results };
            File.WriteAllText(Path.Combine(dir, "selftest.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            App.Current.Shutdown(Results.All(r => r.Pass) ? 0 : 1);
        }
    }
}
