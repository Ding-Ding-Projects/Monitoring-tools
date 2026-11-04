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
