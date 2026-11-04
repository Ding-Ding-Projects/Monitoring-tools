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
using MonitoringTools.Minecraft;
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
