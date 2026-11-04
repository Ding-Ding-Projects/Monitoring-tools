using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    public static partial class SelfTest
    {
        /// <summary>All text a tree shows: the window title and every text block.</summary>
        private static string TextOf(DependencyObject root)
        {
            var sb = new StringBuilder();
            void Visit(DependencyObject d, int depth)
            {
                if (d == null || depth > 80) return;
                if (d is Window w) sb.AppendLine(w.Title);
                if (d is TextBlock tb) sb.AppendLine(tb.Text);
                int n = d is Visual || d is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetChildrenCount(d) : 0;
                for (int i = 0; i < n; i++) Visit(VisualTreeHelper.GetChild(d, i), depth + 1);
            }
            Visit(root, 0);
            return sb.ToString();
        }

        private static string PlainEmoji(string text) => Settings.Current.EmojiInDialogs ? text : Core.Language.StripEmoji(text);

        private static async Task LanguageChecksAsync()
        {
            // ---- stored notification titles follow the language and the humour level in force now
            await CheckAsync("language: notification titles are written again when the language or humour level changes", async () =>
            {
                var s = Settings.Current;
                var saved = (s.LanguageMode, s.EnglishHumour, s.CantoneseHumour);
                ToastWindow toast = null;
                try
                {
                    s.LanguageMode = LanguageMode.English; s.EnglishHumour = 3; s.CantoneseHumour = 3;
                    Core.Language.Refresh();
                    NotificationLog.Clear();
                    NotificationLog.Add("Toast", Core.Language.Headline("convert.done", "MP4"), "1 MB to 2 MB");
                    NotificationLog.Add("Offline", Core.Language.NetHeadline("Router is not responding"), "router");
                    NotificationLog.Add("Notice", "Plain title, never a headline", "detail");
                    toast = App.Toasts.ShowNotice("Time check", "It is a notice.", App.Toasts.Mint, 600);
                    await Task.Delay(300);
                    var done = NotificationLog.All().First(n => n.Title.Contains("MP4"));
                    var off = NotificationLog.All().First(n => n.Kind == "Offline");
                    var plain = NotificationLog.All().First(n => n.Title.StartsWith("Plain title"));
                    var en3 = PlainEmoji(Core.Language.Variant("convert.done", LanguageMode.English, 3, "MP4"));
                    if (done.DisplayTitle != en3) return "English level 3: " + done.DisplayTitle;
                    if (Core.Language.HasCjk(toast.TitleText.Text)) return "the open toast is not English to begin with: " + toast.TitleText.Text;

                    s.LanguageMode = LanguageMode.Cantonese; Core.Language.Refresh(); await Task.Delay(300);
                    var yue3 = PlainEmoji(Core.Language.Variant("convert.done", LanguageMode.Cantonese, 3, "MP4"));
                    if (done.DisplayTitle != yue3) return $"Cantonese: expected \"{yue3}\" but the history shows \"{done.DisplayTitle}\"";
                    if (!Core.Language.HasCjk(off.DisplayTitle)) return "the offline entry stayed English: " + off.DisplayTitle;
                    if (plain.DisplayTitle != "Plain title, never a headline") return "a plain title was changed: " + plain.DisplayTitle;
                    if (!Core.Language.HasCjk(toast.TitleText.Text)) return "the open toast kept its English title: " + toast.TitleText.Text;

                    s.LanguageMode = LanguageMode.Bilingual; Core.Language.Refresh(); await Task.Delay(300);
                    if (!done.DisplayTitle.Contains(en3) || !done.DisplayTitle.Contains(yue3)) return "bilingual title is missing one language: " + done.DisplayTitle;

                    s.LanguageMode = LanguageMode.English; s.EnglishHumour = 1; Core.Language.Refresh(); await Task.Delay(300);
                    var en1 = PlainEmoji(Core.Language.Variant("convert.done", LanguageMode.English, 1, "MP4"));
                    if (done.DisplayTitle != en1) return "after the level change the history still shows: " + done.DisplayTitle;
                    if (en1 == en3) return "levels 1 and 3 are identical, so this check proves nothing";
                    if (Core.Language.HasCjk(toast.TitleText.Text)) return "the open toast did not return to English: " + toast.TitleText.Text;

                    // the export writes the same re-rendered title
                    s.LanguageMode = LanguageMode.Cantonese; Core.Language.Refresh();
                    var shown = NotificationLog.All().Select(n => n.DisplayTitle).ToList();
                    return shown.Contains(yue3) ? null : "the log does not show the Cantonese title: " + string.Join(" | ", shown);
                }
                finally
                {
                    (s.LanguageMode, s.EnglishHumour, s.CantoneseHumour) = saved;
                    Core.Language.Refresh();
                    try { toast?.Close(); } catch { }
                    NotificationLog.Clear();
                }
            });

            // ---- text written after a window loaded is translated by the window sweep alone
            foreach (var mode in new[] { LanguageMode.Cantonese, LanguageMode.Bilingual })
            {
                var m = mode;
                await CheckAsync($"language: late text on every page is translated by the window sweep alone ({m})", async () =>
                {
                    var (found, examined, pages) = await LanguageAudit.PagesAsync(m, refresh: false);
                    if (pages < 8) return "only " + pages + " pages were opened";
                    if (found.Count == 0) return null;
                    var shown = found.Take(40).Select(x => "\"" + x.Text.Replace("\n", " / ") + "\" (" + x.Kind + ", " + x.Where + ")");
                    return found.Count + " problems of " + examined + " examined on " + pages + " pages: " + string.Join(" | ", shown);
                });
            }

            // ---- the same bilingual audit with the explicit refresh the Cantonese audit uses, so a gap cannot hide behind either route
            await CheckAsync("language: every page shows Cantonese beside the English in bilingual mode", async () =>
            {
                var (found, examined, pages) = await LanguageAudit.PagesAsync(LanguageMode.Bilingual, refresh: true);
                if (found.Count == 0) return null;
                var shown = found.Take(40).Select(x => "\"" + x.Text.Replace("\n", " / ") + "\" (" + x.Kind + ", " + x.Where + ")");
                return found.Count + " problems of " + examined + " examined on " + pages + " pages: " + string.Join(" | ", shown);
            });

            // ---- every new headline call site, driven at all five levels in both languages and read back from the built surface
            await CheckAsync("humour: each new headline call site shows its own text at all five levels in both languages", async () =>
            {
                var s = Settings.Current;
                var saved = (s.LanguageMode, s.EnglishHumour, s.CantoneseHumour, App.SnoozeUntil);
                var history = App.History.Items.ToList();
                var cc = new ControlCentreWindow { Width = 1180, Height = 820 };
                var problems = new List<string>();
                var distinct = new Dictionary<string, HashSet<string>>();
                int reads = 0;
                try
                {
                    cc.Show(); await Task.Delay(800);
                    foreach (var lang in new[] { LanguageMode.English, LanguageMode.Cantonese })
                        for (int level = 1; level <= 5; level++)
                        {
                            s.LanguageMode = lang; s.EnglishHumour = level; s.CantoneseHumour = level;
                            Core.Language.Refresh();
                            var tag = $"{lang} level {level}";
                            var cur = lang; var lvl = level;

                            void Expect(string key, string surface, string haystack, params object[] args)
                            {
                                var want = PlainEmoji(Core.Language.Variant(key, cur, lvl, args));
                                reads++;
                                if (!haystack.Contains(want)) problems.Add($"{key} on {surface} at {tag}: expected \"{want}\"");
                                if (!distinct.TryGetValue(key + "/" + cur, out var set)) distinct[key + "/" + cur] = set = new HashSet<string>();
                                set.Add(want);
                            }

                            var welcome = new WelcomeWindow();
                            Expect("welcome", "the welcome window title", welcome.Title);
                            welcome.Close();

                            App.SnoozeUntil = DateTime.Now.AddMinutes(30);
                            cc.Navigate("overview", animate: false); await Task.Delay(450);
                            Expect("snooze", "the Overview heading", TextOf(cc), App.SnoozeUntil.ToString("HH:mm"));
                            App.SnoozeUntil = DateTime.MinValue;

                            App.Games.SimulateForTest("Test Game");
                            var gameModeBefore = s.GameModeEnabled; s.GameModeEnabled = true;
                            cc.Navigate("overview", animate: false); await Task.Delay(450);
                            Expect("game.mode", "the Overview heading", TextOf(cc), "Test Game");
                            App.Games.SimulateForTest(null); s.GameModeEnabled = gameModeBefore;

                            ControlCentreWindow.SimulatedLatestTagForTest = "99.0.0";
                            cc.Navigate("about", animate: false); await Task.Delay(300);
                            await cc.CheckUpdatesForTest(); await Task.Delay(450);
                            Expect("update.available", "the About page update line", TextOf(cc), "99.0.0");
                            ControlCentreWindow.SimulatedLatestTagForTest = null;

                            App.History.Items.Clear();
                            cc.Navigate("history", animate: false); await Task.Delay(450);
                            Expect("history.empty", "the History page", TextOf(cc));

                            cc.Navigate("overview", animate: false); await Task.Delay(350);
                            var f = cc.PageSearch;
                            if (f == null) { problems.Add("no page search on Overview at " + tag); continue; }
                            f.State.SetRegex(false); f.State.SetQuery("zzzz-no-match-zzzz"); await Task.Delay(450);
                            Expect("search.none", "the Overview search message", TextOf(cc), "zzzz-no-match-zzzz");
                            f.State.SetQuery("");
                        }
                }
                finally
                {
                    App.Games.SimulateForTest(null); ControlCentreWindow.SimulatedLatestTagForTest = null;
                    (s.LanguageMode, s.EnglishHumour, s.CantoneseHumour, App.SnoozeUntil) = saved;
                    foreach (var h in history) if (!App.History.Items.Contains(h)) App.History.Items.Add(h);
                    cc.Close();
                    Core.Language.Refresh();
                }
                foreach (var kv in distinct)
                    if (kv.Value.Count < 3) problems.Add($"{kv.Key}: only {kv.Value.Count} distinct texts across five levels");
                if (reads < 60) problems.Add("only " + reads + " rendered reads were made");
                return problems.Count == 0 ? null : string.Join(" | ", problems.Take(30));
            });
        }
    }
}
