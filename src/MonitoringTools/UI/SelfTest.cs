using System;
using MonitoringTools.Minecraft;
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

            // ---- appearance: presets, import and export, colour spaces, locks
            Check("appearance: a preset applies its look to another element, letter spacing and underline included", () =>
            {
                var src = new ElementStyle { Background = "#FFAA0000", CornerRadius = 10, LetterSpacing = 4, Underline = true, Hover = new StateLook { Background = "#FF00AA00" } };
                var err = ElementStyles.SavePreset("Selftest red", src);
                if (err != null) return "preset not saved: " + err;
                try
                {
                    var p = ElementStyles.Preset("Selftest red");
                    if (p == null || !ElementStyles.Same(p, src)) return "preset did not round-trip";
                    var target = new Border { Child = new TextBlock { Text = "Save now" } };
                    ElementStyles.Apply(target, p);
                    var tb = (TextBlock)target.Child;
                    if (!(target.Background is SolidColorBrush bg) || bg.Color != Color.FromRgb(0xAA, 0, 0)) return "background not applied";
                    if (target.CornerRadius.TopLeft != 10) return "corner radius not applied";
                    if (!tb.Text.Contains(ElementStyles.Hair) || tb.Text.Replace(ElementStyles.Hair.ToString(), "") != "Save now") return "letter spacing not applied: " + tb.Text.Length;
                    if (tb.TextDecorations == null || tb.TextDecorations.Count == 0) return "underline not applied";
                    var hover = ElementStyles.ResolveState(p, "hover");
                    if (hover.Background != "#FF00AA00" || ElementStyles.ResolveState(p, "pressed").Background != "#FFAA0000") return "state looks do not resolve";
                    ElementStyles.Apply(target, new ElementStyle());
                    if (tb.Text != "Save now" || tb.TextDecorations.Count != 0) return "text not restored";
                    return ElementStyles.DeletePreset("Selftest red") ? null : "preset not deleted";
                }
                finally { ElementStyles.DeletePreset("Selftest red"); }
            });
            Check("appearance: export then import round-trips, and a bad file changes nothing", () =>
            {
                const string key = "selftest|Round trip";
                var original = new ElementStyle { Foreground = "#FF112233", FontSize = 17, Italic = true, Pressed = new StateLook { Foreground = "#FF445566" } };
                ElementStyles.Store(key, original.Clone(), false);
                try
                {
                    var json = ElementStyles.ExportJson();
                    ElementStyles.Store(key, null, false);
                    if (ElementStyles.Get(key) != null) return "style not removed before import";
                    var err = ElementStyles.ImportJson(json, out var n, out _);
                    if (err != null) return "import refused: " + err;
                    if (n < 1 || !ElementStyles.Same(ElementStyles.Get(key), original)) return "imported look differs";

                    var bad = json.Replace("#FF112233", "not-a-colour");
                    ElementStyles.Store(key, null, false);
                    var badErr = ElementStyles.ImportJson(bad, out _, out _);
                    if (badErr == null || ElementStyles.Get(key) != null) return "a file with a bad colour was applied";
                    if (ElementStyles.ImportJson(new string(' ', ElementStyles.MaxImportBytes + 1), out _, out _) == null) return "an oversized file was accepted";
                    if (ElementStyles.ImportJson("{\"format\":\"other\"}", out _, out _) == null) return "a wrong format was accepted";
                    return null;
                }
                finally { ElementStyles.Store(key, null, false); }
            });
            Check("colour picker: CIELAB and LCH convert both ways and match known values", () =>
            {
                var (L, a, b) = ColorMath.ToLab(1, 0, 0);
                if (Math.Abs(L - 53.24) > 0.1 || Math.Abs(a - 80.09) > 0.15 || Math.Abs(b - 67.20) > 0.15) return $"red is lab({L:0.00} {a:0.00} {b:0.00})";
                var (wl, wa, wb) = ColorMath.ToLab(1, 1, 1);
                if (Math.Abs(wl - 100) > 0.05 || Math.Abs(wa) > 0.05 || Math.Abs(wb) > 0.05) return $"white is lab({wl:0.00} {wa:0.00} {wb:0.00})";
                foreach (var (r, g, bl) in new[] { (0.2, 0.5, 0.8), (0.9, 0.1, 0.4), (0.0, 0.0, 0.0), (0.33, 0.33, 0.33), (0.1, 0.9, 0.3) })
                {
                    var (l2, a2, b2) = ColorMath.ToLab(r, g, bl);
                    var (r2, g2, b3) = ColorMath.FromLab(l2, a2, b2, out var clip);
                    if (clip || Math.Abs(r2 - r) > 0.002 || Math.Abs(g2 - g) > 0.002 || Math.Abs(b3 - bl) > 0.002) return $"lab round trip off for {r},{g},{bl}";
                    var (lc, c, h) = ColorMath.ToLch(r, g, bl);
                    var (r4, g4, b4) = ColorMath.FromLch(lc, c, h, out _);
                    if (Math.Abs(r4 - r) > 0.002 || Math.Abs(g4 - g) > 0.002 || Math.Abs(b4 - bl) > 0.002) return $"lch round trip off for {r},{g},{bl}";
                }
                ColorMath.FromLab(60, 120, 100, out var outside);
                return outside ? null : "an out-of-gamut value was not reported as clipped";
            });
            Check("locks: an appearance lock freezes a look until it is opened with its PIN", () =>
            {
                const string key = "selftest|Frozen look";
                var look = new ElementStyle { Background = "#FF0000FF" };
                ElementStyles.Store(key, look, false);
                var err = Locks.Create(Locks.AppearancePage, key, "Frozen look", LockPolicy.Pin, 5, "4826", null, null);
                if (err != null) return "lock not created: " + err;
                try
                {
                    var e = Locks.FindAppearance(key);
                    if (e == null || !ElementStyles.IsLocked(key)) return "look not reported as locked";
                    if (ElementStyles.Store(key, new ElementStyle { Background = "#FFFF0000" }, false)) return "a locked look was overwritten";
                    if (ElementStyles.Reset(key, null)) return "a locked look was reset";
                    if (ElementStyles.ImportJson(ElementStyles.ExportJson(), out _, out _) == null) return "an import over a locked look was accepted";
                    if (ElementStyles.Get(key)?.Background != "#FF0000FF") return "the locked look changed";
                    new Locks.Session(e).Submit("4826");
                    if (ElementStyles.IsLocked(key)) return "the PIN did not open the lock";
                    if (!ElementStyles.Store(key, new ElementStyle { Background = "#FFFF0000" }, false)) return "an open lock still refused a change";
                    return Locks.Remove(e.Id) == null ? null : "lock not removed";
                }
                finally
                {
                    var e = Locks.FindAppearance(key);
                    if (e != null) { new Locks.Session(e).Submit("4826"); Locks.Remove(e.Id); }
                    ElementStyles.Store(key, null, false);
                }
            });

            // ---- ollama chat kit (no network)
            Check("ollama: an image attachment is sent as base64 in the message's images array", () =>
            {
                var png = new byte[64]; png[0] = 0x89; png[1] = 0x50; png[2] = 0x4E; png[3] = 0x47; png[10] = 7;
                var att = OllamaAttachments.FromImageBytes("a.png", png);
                if (att.Kind != "image" || Convert.FromBase64String(att.Base64).Length != 64) return "image did not encode";
                var json = OllamaChatOptions.BuildChatBody("m", new[] { new OllamaChatTurn { Role = "user", Content = "hi", Images = new List<string> { att.Base64 } } }, new OllamaChatOptions(), "5m");
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var msg = doc.RootElement.GetProperty("messages")[0];
                if (msg.GetProperty("images")[0].GetString() != att.Base64) return "images array missing or different";
                return null;
            });
            Check("ollama: attachments refuse non-images, binary files and oversized images", () =>
            {
                try { OllamaAttachments.FromImageBytes("x.png", new byte[40]); return "a non-image was accepted"; } catch (InvalidOperationException) { }
                try { OllamaAttachments.FromTextBytes("x.bin", new byte[] { 65, 0, 66 }); return "a binary file was accepted"; } catch (InvalidOperationException) { }
                try { var big = new byte[OllamaAttachments.MaxImageBytes + 1]; big[0] = 0x89; big[1] = 0x50; big[2] = 0x4E; big[3] = 0x47; OllamaAttachments.FromImageBytes("big.png", big); return "an oversized image was accepted"; } catch (InvalidOperationException) { }
                return null;
            });
            Check("ollama: text attachments are inlined in a fence, capped, and survive backticks", () =>
            {
                var t = OllamaAttachments.FromTextBytes("n.txt", System.Text.Encoding.UTF8.GetBytes(new string('x', OllamaAttachments.MaxInlineChars + 50)));
                if (!t.Truncated || t.Text.Length != OllamaAttachments.MaxInlineChars) return "the text was not capped";
                var tick = OllamaAttachments.FromTextBytes("c.md", System.Text.Encoding.UTF8.GetBytes("a ``` b"));
                var content = OllamaAttachments.BuildContent("question", new[] { tick });
                if (!content.StartsWith("question") || !content.Contains("````")) return "fence not widened: " + content;
                return null;
            });
            Check("ollama: chat body carries the system prompt, only the options that are set, and keep_alive 0 as a number", () =>
            {
                var o = new OllamaChatOptions { SystemPrompt = "be brief", Temperature = 0.5, NumCtx = 4096, KeepAlive = "0" };
                using var doc = System.Text.Json.JsonDocument.Parse(OllamaChatOptions.BuildChatBody("m", new[] { new OllamaChatTurn { Role = "user", Content = "q" } }, o, "5m"));
                var r = doc.RootElement;
                if (r.GetProperty("messages")[0].GetProperty("role").GetString() != "system") return "no system message first";
                var opt = r.GetProperty("options");
                if (opt.TryGetProperty("top_p", out _) || opt.GetProperty("num_ctx").GetInt32() != 4096) return "options wrong";
                if (r.GetProperty("keep_alive").ValueKind != System.Text.Json.JsonValueKind.Number) return "keep_alive 0 should be a number";
                return null;
            });
            Check("ollama: parameter validation names the problem", () =>
            {
                var bad = new OllamaChatOptions { Temperature = 3, TopP = 0, NumCtx = 10, KeepAlive = "soon" }.Validate();
                var over = new OllamaChatOptions { NumCtx = 8192 }.Validate(4096);
                var ok = new OllamaChatOptions { Temperature = 0.7, TopP = 0.9, NumCtx = 2048, KeepAlive = "10m" }.Validate();
                return bad.Count == 4 && over.Count == 1 && ok.Count == 0 ? null : $"bad={bad.Count} over={over.Count} ok={ok.Count}";
            });
            Check("ollama: capabilities come only from the server's list", () =>
            {
                var none = OllamaCapabilityInfo.From(new string[0]);
                var text = OllamaCapabilityInfo.From(new[] { "completion", "tools" });
                var vis = OllamaCapabilityInfo.From(new[] { "completion", "vision" });
                var emb = OllamaCapabilityInfo.From(new[] { "embedding" });
                if (none.Reported || none.ImageBlockReason() != null) return "unreported capabilities must not block";
                if (text.ImageBlockReason() == null || vis.ImageBlockReason() != null) return "vision gating wrong";
                if (emb.ChatBlockReason() == null || text.ChatBlockReason() != null) return "embedding gating wrong";
                return null;
            });
            Check("ollama: a saved chat round-trips, exports as Markdown and deletes", () =>
            {
                var s = new OllamaSession { Model = "llama3", SystemPrompt = "be brief", Temperature = 0.4 };
                s.Messages.Add(new OllamaSessionMessage { Role = "user", Content = "Hello there", Attachments = new List<string> { "pic.png" } });
                s.Messages.Add(new OllamaSessionMessage { Role = "assistant", Content = "Hi" });
                OllamaSessionStore.Save(s);
                var back = OllamaSessionStore.List().FirstOrDefault(x => x.Id == s.Id);
                if (back == null || back.Messages.Count != 2 || back.Temperature != 0.4) return "chat did not round-trip";
                var md = OllamaSessionStore.ToMarkdown(back);
                if (!md.Contains("# Hello there") || !md.Contains("## You") || !md.Contains("pic.png") || !md.Contains("> be brief")) return "markdown incomplete:\n" + md;
                if (!OllamaSessionStore.Delete(s.Id) || OllamaSessionStore.List().Any(x => x.Id == s.Id)) return "chat was not deleted";
                return null;
            });
            Check("ollama: profiles upsert by name and refuse bad values", () =>
            {
                var list = OllamaProfiles.Starters();
                var n = list.Count;
                if (OllamaProfiles.Upsert(list, new OllamaProfile { Name = "concise", SystemPrompt = "x", Temperature = 0.1 }) != null || list.Count != n) return "same name should replace";
                if (OllamaProfiles.Upsert(list, new OllamaProfile { Name = "New", SystemPrompt = "y" }) != null || list.Count != n + 1) return "new name should add";
                if (OllamaProfiles.Upsert(list, new OllamaProfile { Name = "", SystemPrompt = "y" }) == null) return "empty name accepted";
                if (OllamaProfiles.Upsert(list, new OllamaProfile { Name = "Hot", Temperature = 9 }) == null) return "bad temperature accepted";
                return null;
            });

            // ---- world generator editors (no jar, no network)
            const string sampleNoise = "{\"sea_level\":63,\"aquifers_enabled\":true,\"default_block\":{\"Name\":\"minecraft:stone\"},\"noise\":{\"min_y\":-64,\"height\":384,\"size_horizontal\":1,\"size_vertical\":2},\"spawn_target\":[{\"temperature\":[-1,1]}],\"surface_rule\":{\"type\":\"minecraft:sequence\",\"sequence\":[]}}";
            Check("worldgen: changing one noise value writes a data pack with only that change", () =>
            {
                var src = WorldgenSource.FromFiles(new Dictionary<string, string> { ["data/minecraft/worldgen/noise_settings/overworld.json"] = sampleNoise }, "test");
                var edits = new List<WorldgenEdit> { new WorldgenEdit { Kind = WorldgenKind.NoiseSettings, Id = "minecraft:overworld", Path = "sea_level", Value = "70" } };
                var files = WorldgenEditor.BuildFiles(src, edits, new HashSet<string> { "minecraft:stone" });
                if (files.Count != 1) return "expected one file";
                var packDir = Path.Combine(dir, "worldgen-pack-test");
                var written = WorldgenEditor.WritePack(packDir, 48, files);
                var path = Path.Combine(packDir, WorldgenEditor.PackFolder, "data", "minecraft", "worldgen", "noise_settings", "overworld.json");
                if (written.Count != 1 || !File.Exists(path) || !File.Exists(Path.Combine(packDir, WorldgenEditor.PackFolder, "pack.mcmeta"))) return "pack files missing";
                var a = System.Text.Json.Nodes.JsonNode.Parse(sampleNoise); var b = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path));
                if ((int)b["sea_level"] != 70) return "sea_level not changed";
                a["sea_level"] = 70;
                return a.ToJsonString() == b.ToJsonString() ? null : "something besides sea_level changed:\n" + b.ToJsonString();
            });
            Check("worldgen: invalid values are refused with a reason", () =>
            {
                var src = WorldgenSource.FromFiles(new Dictionary<string, string> { ["data/minecraft/worldgen/noise_settings/overworld.json"] = sampleNoise }, "test");
                string Try(string path, string value, ISet<string> blocks = null)
                {
                    try { WorldgenEditor.BuildFiles(src, new[] { new WorldgenEdit { Kind = WorldgenKind.NoiseSettings, Id = "minecraft:overworld", Path = path, Value = value } }, blocks); return null; }
                    catch (InvalidOperationException ex) { return ex.Message; }
                }
                if (Try("sea_level", "high") == null) return "a word was accepted for a whole number";
                if (Try("aquifers_enabled", "maybe") == null) return "maybe accepted for a boolean";
                if (Try("noise.height", "385") == null) return "a height that is not a multiple of 16 was accepted";
                if (Try("noise.min_y", "2000") == null) return "min_y plus height above 2032 was accepted";
                if (Try("surface_rule.type", "x") == null) return "an excluded subtree was editable";
                if (Try("default_block.Name", "minecraft:nonsense", new HashSet<string> { "minecraft:stone" }) == null) return "an unknown block was accepted";
                if (Try("noise.height", "320") != null) return "a valid height was refused";
                return null;
            });
            Check("worldgen: the editor lists values from the default file and skips the big subtrees", () =>
            {
                var paths = WorldgenEditor.Leaves(sampleNoise).Select(l => l.Path).ToList();
                var types = WorldgenEditor.Leaves(sampleNoise).ToDictionary(l => l.Path, l => l.Type);
                if (paths.Any(p => p.StartsWith("surface_rule") || p.StartsWith("spawn_target"))) return "excluded subtree listed";
                return types["sea_level"] == "integer" && types["aquifers_enabled"] == "boolean" && types["default_block.Name"] == "string" && paths.Contains("noise.size_vertical") ? null : "types wrong: " + string.Join(",", paths);
            });
            Check("worldgen: a drawn square becomes a centre and radius, capped at the limit", () =>
            {
                var s = ChunkAreaPicker.SquareFromCorners((-10, -10), (9, 9));
                var big = ChunkAreaPicker.SquareFromCorners((0, 0), (999, 5));
                var one = ChunkAreaPicker.SquareFromCorners((4, 4), (4, 4));
                return s == (0, 0, 10) && big.Radius == ChunkAreaPicker.MaxRadius && one.Radius == 1 ? null : $"{s} {big} {one}";
            });

            Check("history: every save keeps a version, duplicates are skipped and the oldest beyond 50 are dropped", () =>
            {
                const string key = "selftest-history";
                try { var d = System.IO.Path.Combine(Settings.DataDir, "history", key); if (System.IO.Directory.Exists(d)) System.IO.Directory.Delete(d, true); } catch { }
                if (!ScriptHistory.Save(key, "one")) return "first save not kept";
                if (ScriptHistory.Save(key, "one")) return "an identical save was stored twice";
                for (int i = 2; i <= 55; i++) ScriptHistory.Save(key, "version " + i);
                var list = ScriptHistory.List(key);
                if (list.Count != ScriptHistory.Cap) return "expected " + ScriptHistory.Cap + " versions, found " + list.Count;
                if (ScriptHistory.Read(key, list[0].Id) != "version 55") return "newest is not first";
                if (ScriptHistory.Read(key, list[^1].Id) != "version 6") return "oldest kept is wrong: " + ScriptHistory.Read(key, list[^1].Id);
                if (ScriptHistory.Read(key, "../../settings") != null) return "an id outside the history folder was readable";
                return null;
            });
            Check("history: a restored version comes back exactly and the replaced text is kept", () =>
            {
                const string key = "selftest-restore";
                try { var d = System.IO.Path.Combine(Settings.DataDir, "history", key); if (System.IO.Directory.Exists(d)) System.IO.Directory.Delete(d, true); } catch { }
                ScriptHistory.Save(key, "alpha\r\nbeta");
                var first = ScriptHistory.List(key)[0];
                ScriptHistory.Save(key, "alpha\r\ngamma");       // the text that will be replaced
                var restored = ScriptHistory.Read(key, first.Id);
                ScriptHistory.Save(key, restored);
                var all = ScriptHistory.List(key);
                if (restored != "alpha\r\nbeta") return "restored text differs";
                if (all.Count != 3) return "expected 3 versions, found " + all.Count;
                return all.Any(v => ScriptHistory.Read(key, v.Id) == "alpha\r\ngamma") ? null : "the replaced text was not kept";
            });
            Check("history: the comparison marks added and removed lines", () =>
            {
                var d = ScriptHistory.Diff("a\nb\nc", "a\nx\nc\nd");
                var text = string.Join("|", d.Select(l => l.Op + l.Text));
                return text == " a|-b|+x| c|+d" ? null : text;
            });
            Check("forges: remote addresses are parsed and the host is recognised", () =>
            {
                var a = ForgeHosts.ParseRemote("https://gitlab.com/group/sub/project.git");
                var b = ForgeHosts.ParseRemote("git@gitea.example.org:me/thing.git");
                var c = ForgeHosts.ParseRemote("https://git.example.org:8443/me/repo");
                var d = ForgeHosts.ParseRemote("ssh://git@codeberg.org/me/repo.git");
                if (a == null || a.Kind != ForgeKind.GitLab || a.Owner != "group/sub" || a.Name != "project" || a.BaseUrl != "https://gitlab.com") return "gitlab url: " + a;
                if (b == null || b.Kind != ForgeKind.Gitea || b.Owner != "me" || b.Name != "thing" || b.Host != "gitea.example.org") return "scp style: " + b;
                if (c == null || c.Kind != null || c.Host != "git.example.org:8443" || c.Name != "repo") return "self-hosted with port: " + c;
                if (d == null || d.Kind != ForgeKind.Gitea || d.Name != "repo") return "ssh url: " + d;
                if (ForgeHosts.ParseRemote("not a url") != null || ForgeHosts.ParseRemote("https://github.com/only-one") != null) return "a malformed address was accepted";
                return ForgeHosts.ParseRemote("https://github.com/o/r.git")?.Kind == ForgeKind.GitHub ? null : "github not recognised";
            });
            Check("forges: server addresses are normalised and unsafe ones refused", () =>
            {
                if (ForgeHosts.NormaliseBase("git.example.org/") != "https://git.example.org") return "no scheme: " + ForgeHosts.NormaliseBase("git.example.org/");
                if (ForgeHosts.NormaliseBase("http://203.0.113.5:3000") != "http://203.0.113.5:3000") return "port kept: " + ForgeHosts.NormaliseBase("http://203.0.113.5:3000");
                if (ForgeHosts.NormaliseBase("https://user:pw@host.example") != null) return "an address carrying a password was accepted";
                if (ForgeHosts.NormaliseBase("ftp://host.example") != null) return "a non-web scheme was accepted";
                return ForgeHosts.NormaliseBase("   ") == null ? null : "blank accepted";
            });
            // ---- bulk selection, scheduled date ranges and guided input
            Check("bulk select: select all shown skips rows hidden by the search filter", () =>
            {
                var hidden = new HashSet<string> { "b", "d" };
                var bulk = new BulkSelect<string>(() => { }, "items");
                foreach (var x in new[] { "a", "b", "c", "d", "e" }) { var item = x; bulk.Track(item, () => !hidden.Contains(item)); }
                bulk.Set("b", true);                       // a hidden row chosen earlier stays chosen until cleared
                bulk.SelectAllShown();
                var got = string.Join(",", bulk.Chosen);
                if (got != "a,b,c,e") return "selected " + got;
                bulk.SelectNone();
                if (bulk.Count != 0) return "select none left " + bulk.Count;
                bulk.SelectAllShown();
                bulk.Prune(new[] { "a", "c" });
                return string.Join(",", bulk.Chosen) == "a,c" ? null : "prune kept " + string.Join(",", bulk.Chosen);
            });
            Check("scheduled settings: a date range limits the rule to whole calendar days, ends included", () =>
            {
                var r = new ScheduleRule { Setting = "sounds", Value = "off", Start = "09:00", End = "17:00", FromDate = "2026-12-01", ToDate = "2026-12-05" };
                bool M(ScheduleRule x, string t) => Modes.Matches(x, DateTime.Parse(t, System.Globalization.CultureInfo.InvariantCulture));
                if (!M(r, "2026-12-01 10:00") || !M(r, "2026-12-05 16:59")) return "range ends should match";
                if (M(r, "2026-11-30 10:00") || M(r, "2026-12-06 10:00")) return "outside the range should not match";
                var open = new ScheduleRule { Setting = "sounds", Value = "off", Start = "09:00", End = "17:00", FromDate = "2026-12-01" };
                if (!M(open, "2027-06-01 10:00") || M(open, "2026-11-30 10:00")) return "open-ended range wrong";
                var night = new ScheduleRule { Setting = "sounds", Value = "off", Start = "22:00", End = "02:00", FromDate = "2026-12-01", ToDate = "2026-12-01" };
                if (!M(night, "2026-12-01 23:00") || !M(night, "2026-12-02 01:00")) return "a window crossing midnight belongs to the day it starts on";
                if (M(night, "2026-12-02 23:00")) return "the next evening is outside the range";
                var bad = new ScheduleRule { Setting = "sounds", Value = "off", Start = "09:00", End = "17:00", FromDate = "2026-12-05", ToDate = "2026-12-01" };
                if (M(bad, "2026-12-03 10:00") || Modes.ValidDateRange(bad.FromDate, bad.ToDate)) return "an end before the start must not match";
                if (Modes.TryParseDate("2026-13-40", out _) || !Modes.TryParseDate("", out var none) || none != null) return "date parsing wrong";
                return Modes.DescribeRule(r).Contains("2026-12-01 to 2026-12-05") ? null : "description missing the range";
            });
            Check("guided input: DNS server format, copy names and shortcut export", () =>
            {
                if (ListActions.ValidateDnsServers("1.1.1.1", "") != null || ListActions.ValidateDnsServers("1.1.1.1", "1.0.0.1") != null) return "valid servers refused";
                if (ListActions.ValidateDnsServers("", "") == null || ListActions.ValidateDnsServers("1.1.1", "") == null || ListActions.ValidateDnsServers("1.1.1.1", "nope") == null
                    || ListActions.ValidateDnsServers("1.1.1.1", "1.1.1.1") == null) return "bad servers accepted";
                if (ListActions.ValidateDnsName(" ") == null || ListActions.ValidateDnsName("Work") != null) return "name check wrong";
                var taken = new HashSet<string> { "tool-copy" };
                if (ListActions.CopyName("tool", n => taken.Contains(n)) != "tool-copy2") return "copy name " + ListActions.CopyName("tool", n => taken.Contains(n));
                var long32 = new string('a', 32);
                var cn = ListActions.CopyName(long32, n => false);
                if (cn == null || cn.Length > 32 || !Shortcuts.IsValidName(cn)) return "long name copy invalid: " + cn;
                var json = ListActions.ShortcutsJson(new[] { new Shortcut { Name = "demo", Description = "d", Shell = ScriptShell.Batch } }, sc => "echo hi");
                using var doc = JsonDocument.Parse(json);
                var first = doc.RootElement.GetProperty("shortcuts")[0];
                return first.GetProperty("Name").GetString() == "demo" && first.GetProperty("Script").GetString() == "echo hi" ? null : "export shape wrong";
            });
            // ---- language headlines, accessible names, School mode and the ADHD aids
            Check("humour: every headline differs by level yet keeps the same facts", () =>
            {
                var facts = new object[] { "FACT-ZERO", "FACT-ONE" };
                var indexes = new Regex(@"\{(\d)\}");
                foreach (var key in Core.Language.HeadlineKeys.ToList())
                    foreach (var lang in new[] { LanguageMode.English, LanguageMode.Cantonese })
                    {
                        var used = Enumerable.Range(1, 5).Select(l => string.Join(",", indexes.Matches(Core.Language.Variant(key, lang, l)).Select(m => m.Groups[1].Value).Distinct().OrderBy(x => x))).Distinct().ToList();
                        if (used.Count != 1) return $"{key} ({lang}): levels use different facts: {string.Join(" | ", used)}";
                        var texts = Enumerable.Range(1, 5).Select(l => Core.Language.Variant(key, lang, l, facts)).ToList();
                        if (texts.Distinct().Count() < 3) return $"{key} ({lang}): fewer than three distinct levels";
                        foreach (var idx in used[0].Split(',', StringSplitOptions.RemoveEmptyEntries))
                            foreach (var t in texts)
                                if (!t.Contains(facts[int.Parse(idx)].ToString())) return $"{key} ({lang}): a level lost fact {idx}: {t}";
                    }
                return null;
            });
            Check("humour: the plain level of each notice headline keeps the original title", () =>
            {
                var pairs = new[]
                {
                    ("Open in editor", "notice.editor"), ("Time check", "notice.timecheck"), ("IP address renewed", "notice.ip.renewed"), ("Cancelled", "notice.cancelled"),
                    ("ipconfig finished", "notice.ipconfig"), ("Administrator rights unchanged", "notice.admin.unchanged"), ("DNS switch failed", "notice.dns.failed"),
                    ("Checking the connection", "notice.checking"), ("Uninstalled", "notice.uninstalled"), ("Wording file refused", "notice.wording.refused"),
                    ("Internet connection lost", "offline.title"), ("No network connection", "offline.noadapter"), ("Router is not responding", "offline.router"),
                    ("Sign-in required", "offline.signin"), ("Conversion cancelled", "convert.cancelled"),
                };
                foreach (var (title, key) in pairs)
                    if (Core.Language.Variant(key, LanguageMode.English, 1) != title) return $"{key} at level 1 is not \"{title}\"";
                var saved = (Settings.Current.EnglishHumour, Settings.Current.LanguageMode);
                try
                {
                    Settings.Current.LanguageMode = LanguageMode.English; Settings.Current.EnglishHumour = 1;
                    var dns = Core.Language.NoticeTitle("DNS is now Cloudflare");
                    return dns == "DNS is now Cloudflare" && Core.Language.NoticeTitle("Cancelled") == "Cancelled" && Core.Language.NetHeadline("Router is not responding") == "Router is not responding"
                        ? null : "the plain level changed a title: " + dns;
                }
                finally { (Settings.Current.EnglishHumour, Settings.Current.LanguageMode) = saved; }
            });
            Check("language: accessible names and help text translate and restore exactly", () =>
            {
                var saved = Settings.Current.LanguageMode;
                try
                {
                    var panel = new StackPanel();
                    var b = new Button(); System.Windows.Automation.AutomationProperties.SetName(b, "Clipboard history"); System.Windows.Automation.AutomationProperties.SetHelpText(b, "Overview");
                    var lit = new Button(); System.Windows.Automation.AutomationProperties.SetName(lit, "Clipboard history"); Core.Language.SetLiteral(lit, true);
                    panel.Children.Add(b); panel.Children.Add(lit);
                    bool Cjk(string x) => x.Any(c => c >= 0x2E80);
                    Settings.Current.LanguageMode = LanguageMode.Cantonese; Core.Language.Refresh(panel);
                    var yue = System.Windows.Automation.AutomationProperties.GetName(b); var help = System.Windows.Automation.AutomationProperties.GetHelpText(b);
                    if (!Cjk(yue)) return "name not translated: " + yue;
                    if (!Cjk(help)) return "help text not translated: " + help;
                    if (System.Windows.Automation.AutomationProperties.GetName(lit) != "Clipboard history") return "an element marked literal was translated";
                    Settings.Current.LanguageMode = LanguageMode.Bilingual; Core.Language.Refresh(panel);
                    var both = System.Windows.Automation.AutomationProperties.GetName(b);
                    if (!both.Contains("Clipboard history") || !Cjk(both)) return "bilingual name wrong: " + both;
                    Settings.Current.LanguageMode = LanguageMode.English; Core.Language.Refresh(panel);
                    var back = System.Windows.Automation.AutomationProperties.GetName(b); var backHelp = System.Windows.Automation.AutomationProperties.GetHelpText(b);
                    return back == "Clipboard history" && backHelp == "Overview" ? null : $"not restored exactly: {back} / {backHelp}";
                }
                finally { Settings.Current.LanguageMode = saved; }
            });
            Check("school: a password needs 8 characters and its hash round-trips", () =>
            {
                if (Modes.PasswordLooksValid("short7!")) return "a 7-character password was accepted";
                if (!Modes.PasswordLooksValid("longer-pass")) return "an 11-character password was refused";
                var c = Modes.MakeCredential("longer-pass");
                if (!Modes.MatchesCredential("longer-pass", c.Salt, c.Hash, c.Iterations)) return "the right password did not match";
                if (Modes.MatchesCredential("longer-pass!", c.Salt, c.Hash, c.Iterations)) return "a wrong password matched";
                return null;
            });
            // The page search skips button labels on purpose, so choices are found by walking the visible tree.
            static bool HasChoice(DependencyObject root, string label)
            {
                for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
                {
                    var c = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
                    if (c is System.Windows.Controls.Primitives.ButtonBase b && b.IsVisible && (b.Content as string ?? (b.Content as TextBlock)?.Text) == label) return true;
                    if (HasChoice(c, label)) return true;
                }
                return false;
            }
            await CheckAsync("school: the language page hides the language choices and humour sliders and English is forced", async () =>
            {
                var saved = Settings.Current.LanguageMode;
                var cc = new ControlCentreWindow { Width = 1180, Height = 820 };
                try
                {
                    cc.Show();
                    await Task.Delay(700);
                    Settings.Current.LanguageMode = LanguageMode.English;
                    cc.Navigate("language", animate: false);
                    await Task.Delay(400);
                    var open = SearchScope.Text(cc);
                    if (!open.Contains("English humour") || !HasChoice(cc, "廣東話")) return "the language choices or humour sliders are missing before School mode";
                    Settings.Current.LanguageMode = LanguageMode.Cantonese;
                    Modes.ForceSchoolForTest(true);
                    if (!Core.Language.IsEnglish) return "language is not forced to English";
                    if (Core.Language.Headline("snooze", "14:30") != Core.Language.Variant("snooze", LanguageMode.English, 1, "14:30")) return "headlines are not plain";
                    cc.Navigate("language", animate: false);
                    await Task.Delay(400);
                    var text = SearchScope.Text(cc);
                    if (HasChoice(cc, "廣東話")) return "a Cantonese choice is still shown";
                    if (text.Contains("English humour") || text.Contains("Cantonese humour")) return "a humour slider is still shown";
                    return text.Contains("English only") ? null : "the page does not name the mode";
                }
                finally { Modes.ForceSchoolForTest(false); Settings.Current.LanguageMode = saved; cc.Close(); }
            });
            Check("palette: every ADHD switch and the Focus spotlight have an entry", () =>
            {
                var titles = CommandPalette.Items().Select(i => i.Title).ToList();
                foreach (var t in new[] { "Focus spotlight", "Focus", "Low stimulation", "Time awareness", "One thing at a time", "Momentum" })
                    if (!titles.Contains(t)) return "missing the entry " + t;
                return null;
            });
            await CheckAsync("spotlight: switching it on adds a dim layer to an open window and off removes it", async () =>
            {
                var cc = new ControlCentreWindow { Width = 1180, Height = 820 };
                try
                {
                    cc.Show();
                    await Task.Delay(700);
                    var root = (UIElement)cc.Content;
                    var layer = System.Windows.Documents.AdornerLayer.GetAdornerLayer(root);
                    if (layer == null) return "the window has no adorner layer";
                    int Count() => layer.GetAdorners(root)?.Length ?? 0;
                    int before = Count();
                    FocusSpotlight.Set(true);
                    await Task.Delay(250);
                    int on = Count();
                    FocusSpotlight.Set(false);
                    await Task.Delay(250);
                    int off = Count();
                    return on == before + 1 && off == before ? null : $"layers before {before}, on {on}, off {off}";
                }
                finally { FocusSpotlight.Set(false); cc.Close(); }
            });

            Check("wording: a copied wording file path is applied and an unrelated JSON file is left alone", () =>
            {
                var dir = System.IO.Path.Combine(Settings.DataDir, "wording-test");
                System.IO.Directory.CreateDirectory(dir);
                var good = System.IO.Path.Combine(dir, "words.json");
                var other = System.IO.Path.Combine(dir, "package.json");
                System.IO.File.WriteAllText(good, "{\"schemaVersion\": 1, \"entries\": {\"Clipboard history\": \"Copy pile\"}}");
                System.IO.File.WriteAllText(other, "{\"name\": \"demo\", \"version\": \"1.0.0\"}");
                var saved = Settings.Current.ApplyCopiedWordingFile;
                try
                {
                    Settings.Current.ApplyCopiedWordingFile = true;
                    if (App.TryApplyCopiedWording(new MonitoringTools.Clip.ClipItem { Kind = MonitoringTools.Clip.ClipKind.Files, Files = new[] { other } })) return "an unrelated JSON file was taken";
                    if (!App.TryApplyCopiedWording(new MonitoringTools.Clip.ClipItem { Kind = MonitoringTools.Clip.ClipKind.Files, Files = new[] { good } })) return "the wording file was not taken";
                    if (PersonalWording.Apply("Clipboard history") != "Copy pile") return "the wording was not applied";
                    PersonalWording.Clear();
                    Settings.Current.ApplyCopiedWordingFile = false;
                    if (App.TryApplyCopiedWording(new MonitoringTools.Clip.ClipItem { Kind = MonitoringTools.Clip.ClipKind.Files, Files = new[] { good } })) return "applied while the switch was off";
                    return PersonalWording.Count == 0 ? null : "clear left phrases behind";
                }
                finally { Settings.Current.ApplyCopiedWordingFile = saved; PersonalWording.Clear(); }
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
