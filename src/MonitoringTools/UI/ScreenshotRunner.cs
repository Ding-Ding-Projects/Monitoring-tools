using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MonitoringTools.Clip;
using MonitoringTools.Core;
using MonitoringTools.Media;

namespace MonitoringTools.UI
{
    /// <summary>
    /// "--screenshots DIR": opens every surface with sample data, renders each to a PNG at 2x,
    /// writes an index.json, then exits. CI runs this on a Windows runner after the build.
    /// Nothing is installed and settings are never saved.
    /// </summary>
    public static partial class ScreenshotRunner
    {
        private static string _dir;
        private static readonly List<object> Index = new List<object>();

        public static async Task RunAsync(string dir)
        {
            _dir = Path.GetFullPath(dir);
            Directory.CreateDirectory(_dir);
            int code = 0;
            try
            {
                Settings.Current.ToastSeconds = 600; // keep toasts on screen while we capture
                SetCursorPos(0, 0);
                Demo.SeedLivedIn();
                var s = Demo.EnsureSamples();
                if (Environment.GetEnvironmentVariable("MT_WAVE6_ONLY") == "1")   // quick loop while developing these scenes
                {
                    await Wave6ScenesAsync();
                    File.WriteAllText(Path.Combine(_dir, "index.json"), JsonSerializer.Serialize(Index, new JsonSerializerOptions { WriteIndented = true }));
                    App.Current.Shutdown(0);
                    return;
                }

                // clipboard toasts, hovered so the actions show
                await Toast("toast-markdown", "Markdown copied, hovered", App.Toasts.ShowClip(ClipItem.FromText(Demo.SampleMarkdown)));
                await Toast("toast-link", "Link with tracking parameters", App.Toasts.ShowClip(ClipItem.FromText(Demo.SampleUrl)));
                await Toast("toast-image-file", "Image file with a rich preview", App.Toasts.ShowClip(ClipItem.FromFiles(new[] { s["image"] })), 2200);
                await Toast("toast-csv", "CSV file preview", App.Toasts.ShowClip(ClipItem.FromFiles(new[] { s["csv"] })), 1800);
                var quoted = ClipItem.FromText($"\"{s["markdown"]}\"");
                await Toast("toast-quoted-path", "Path copied with quotes", App.Toasts.ShowClip(quoted), 1800);
                await Toast("toast-compose", "Docker Compose file", App.Toasts.ShowClip(ClipItem.FromFiles(new[] { s["compose"] })), 1500);
                await Toast("toast-folder", "Folder with batch conversion chips", App.Toasts.ShowClip(ClipItem.FromFiles(new[] { s["folder"] })), 1800);
                await Toast("toast-multi", "Several files at once", App.Toasts.ShowClip(ClipItem.FromFiles(new[] { s["image"], s["image2"], s["csv"] })));
                await Toast("toast-bitmap", "Screenshot on the clipboard", App.Toasts.ShowClip(ClipItem.FromImage(Demo.SampleBitmap(1920, 1080, "Capture", Color.FromRgb(0x8A, 0xB4, 0xFF)))));
                await Toast("toast-color", "Color value", App.Toasts.ShowClip(ClipItem.FromText("#7CF2C2")));
                await Toast("toast-base64", "Base64 text", App.Toasts.ShowClip(ClipItem.FromText(FileOps.EncodeText("Monitoring Tools decodes this for you."))));

                // a conversion in progress
                var job = new ConversionJob
                {
                    Source = Path.Combine(s["folder"], "launch-teaser.mov"), Format = Formats.Find("mp4"),
                    Output = Path.Combine(s["folder"], "launch-teaser.mp4"), SourceBytes = 412L * 1024 * 1024,
                    Duration = TimeSpan.FromSeconds(286), Position = TimeSpan.FromSeconds(134), Speed = 3.1, Progress = 0.47
                };
                var jw = App.Toasts.ShowJob(job);
                await Task.Delay(700);
                job.Raise();
                await Shot(jw, "toast-converting", "Converting with :mp4", 1100);
                jw.CloseAnimated();

                // offline dialog
                var off = new OfflineWindow(Demo.SampleOutage());
                off.Show();
                await Shot(off, "offline-dialog", "Internet connection lost", 1800);
                off.Close();
                App.Toasts.ReservedBottom = 0;

                // tray menu
                var menu = new TrayMenuWindow();
                menu.ShowAtCursor();
                await Shot(menu, "tray-menu", "Tray right-click menu", 1400);
                menu.Close();

                // setup wizard: first, a middle and the last step
                var wiz = new SetupWizardWindow();
                wiz.Show();
                await Shot(wiz, "setup-start", "Setup wizard: start-up", 1500);
                wiz.GoTo(2);
                await Shot(wiz, "setup-connection", "Setup wizard: internet watch", 1100);
                wiz.GoTo(6);
                await Shot(wiz, "setup-done", "Setup wizard: summary", 1100);
                wiz.Close();
                Settings.Current.OnboardingStep = 0;

                // command palette
                CommandPalette.Open();
                await Shot(CommandPalette.Current, "command-palette", "Command palette (Ctrl+Shift+F)", 900);
                CommandPalette.Current?.Close();

                // public IP change dialog
                var ipw = App.Toasts.ShowIpChanged(
                    new Net.IpRecord { Ip = "203.0.113.24", FirstSeen = DateTime.Now.AddHours(-6), LastSeen = DateTime.Now.AddMinutes(-1), Org = "AS64500 Example Fibre", City = "Toronto", Country = "CA" },
                    new Net.IpRecord { Ip = "198.51.100.77", FirstSeen = DateTime.Now, LastSeen = DateTime.Now, Org = "AS64500 Example Fibre", City = "Toronto", Country = "CA", Times = 2 });
                await Toast("toast-public-ip", "Public IP changed", ipw);

                // welcome tour
                var welcome = new WelcomeWindow();
                welcome.Show();
                await Shot(welcome, "welcome", "Welcome tour", 1800);
                welcome.Close();

                // control centre, every page
                var cc = new ControlCentreWindow { Width = 1180, Height = 820 };
                cc.Show();
                await Task.Delay(1200);
                foreach (var page in cc.SectionIds.ToList())
                {
                    cc.Navigate(page);
                    await Shot(cc, "control-" + page, "Control centre: " + page, 1300);
                }

                // search and the regex builder, anchored to the page's own search field
                await SearchScenes(cc);
                await LookScenes(cc);
                await ScaleScenes(cc);
                await ConvertScenes();
                await Wave6Scenes(cc);
                cc.Close();

                var gate = new ConfirmWindow("Uninstall Monitoring Tools?", "This stops the background service, removes it from start-up and deletes the program.", "Uninstall", danger: true);
                gate.Show();
                await Shot(gate, "super-confirm-start", "Destructive actions need two keys and a full slide", 1100);
                gate.ArmForPreview(60);
                await Shot(gate, "super-confirm-armed", "Both keys on, slider part way", 700);
                gate.Close();

                // tray menu filtered, and the palette in regex mode
                var fmenu = new TrayMenuWindow();
                fmenu.ShowAtCursor();
                fmenu.FindField.State.SetQuery("dns");
                await Shot(fmenu, "search-tray-filter", "Tray menu filtered with its own search", 1200);
                fmenu.FindField.State.SetQuery("");
                fmenu.Close();
                CommandPalette.Open();
                var ps = CommandPalette.CurrentSearch;
                if (ps != null) { ps.State.SetRegex(true); ps.State.SetQuery("^(open|show)"); }
                await Shot(CommandPalette.Current, "search-palette-regex", "Command palette searching with a regular expression", 900);
                if (ps != null) { ps.State.SetQuery(""); ps.State.SetRegex(false); }
                CommandPalette.Current?.Close();

                // file tools, every tab
                var ft = new FileToolsWindow(new[] { s["image"], s["image2"], s["csv"] });
                ft.Show();
                for (int tab = 0; tab < 3; tab++)
                {
                    ft.Select(tab);
                    await Shot(ft, "filetools-" + new[] { "move", "base64", "encrypt" }[tab], "File tools: " + new[] { "move and copy", "Base64", "encrypt" }[tab], 1100);
                }
                ft.Close();

                var commit = new CommitTemplateWindow(continueAfter: false);
                commit.Show();
                await Shot(commit, "commit-template", "First :push asks for a commit message", 1300);
                commit.Close();

                var confirm = new ConfirmWindow("Uninstall Monitoring Tools?", "This stops the background service, removes it from start-up and deletes the program.", "Uninstall", "Also remove settings, logs and scripts", danger: true);
                confirm.Show();
                await Shot(confirm, "uninstall-confirm", "Uninstall confirmation", 1100);
                confirm.Close();

                await Wave6ScenesAsync();

                File.WriteAllText(Path.Combine(_dir, "index.json"), JsonSerializer.Serialize(Index, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                Log.Error("Screenshot run failed", ex);
                File.WriteAllText(Path.Combine(_dir, "error.txt"), ex.ToString());
                code = 1;
            }
            App.Current.Shutdown(code);
        }

        private static async Task SearchScenes(ControlCentreWindow cc)
        {
            Settings.Current.SavedPatterns.Clear(); // in memory only: the run never saves settings
            Settings.Current.SavedPatterns.Add(new SavedPattern { Name = "ISO date", Pattern = @"\d{4}-\d{2}-\d{2}" });
            Settings.Current.SavedPatterns.Add(new SavedPattern { Name = "IPv4 address", Pattern = @"\b(?:\d{1,3}\.){3}\d{1,3}\b" });
            Settings.Current.SavedPatterns.Add(new SavedPattern { Name = "Windows path", Pattern = @"^[A-Za-z]:\\" });

            cc.Navigate("history");
            await Task.Delay(900);
            var f = cc.PageSearch;
            f.State.SetQuery("png");
            await Shot(cc, "search-history-plain", "Clipboard history searched as plain text", 900);

            f.State.SetQuery(@"(?<name>\w+)\.(png|md|csv)");
            f.OpenBuilder("Build");
            await ShotWithBuilder(cc, f, "search-builder-build", "Regex builder anchored to the history search: tokens, options and explanation", 1100);
            f.OpenBuilder("Test");
            await ShotWithBuilder(cc, f, "search-builder-test", "Regex builder: matches and capture groups against the history", 1100);
            f.State.Replacement = "${name}.webp";
            f.OpenBuilder("Replace");
            await ShotWithBuilder(cc, f, "search-builder-replace", "Regex builder: replacement preview", 1000);
            f.OpenBuilder("Debug");
            await ShotWithBuilder(cc, f, "search-builder-debug", "Regex builder: step-by-step trace checked against the .NET engine", 1300);
            f.OpenBuilder("Compare");
            await ShotWithBuilder(cc, f, "search-builder-compare", "Regex builder: the pattern in each .NET engine mode", 1500);
            f.OpenBuilder("Files");
            await ShotWithBuilder(cc, f, "search-builder-files", "Regex builder: search and replace in files with backups", 1000);
            f.OpenBuilder("Code");
            await ShotWithBuilder(cc, f, "search-builder-code", "Regex builder: generated code", 900);
            f.OpenBuilder("Library");
            await ShotWithBuilder(cc, f, "search-builder-library", "Regex builder: saved patterns", 900);
            f.State.SetQuery("(a+)+$");
            f.OpenBuilder("Build");
            await ShotWithBuilder(cc, f, "search-builder-risk", "Regex builder warns about catastrophic backtracking", 1000);
            f.State.SetQuery("(unclosed");
            await ShotWithBuilder(cc, f, "search-builder-invalid", "An invalid pattern is explained and filters nothing", 900);
            f.State.SetQuery("");
            f.State.SetRegex(false);
            await Task.Delay(400);

            cc.Navigate("service");
            await Task.Delay(900);
            cc.PageSearch.State.SetQuery("sound");
            await Shot(cc, "search-settings", "Settings page searched for one option", 1000);
            cc.PageSearch.State.SetQuery("");
        }

        /// <summary>
        /// 200 % display scale on a 1920 x 1080 screen leaves 960 x 540 of layout space, the tightest
        /// size the control centre must still fit. Captured in English and in bilingual mode, where labels are longest.
        /// </summary>
        private static async Task ScaleScenes(ControlCentreWindow cc)
        {
            double w = cc.Width, h = cc.Height, mw = cc.MinWidth, mh = cc.MinHeight;
            cc.MinWidth = 0; cc.MinHeight = 0; cc.Width = 960; cc.Height = 540;
            foreach (var (mode, name) in new[] { (LanguageMode.English, "en"), (LanguageMode.Bilingual, "bilingual") })
            {
                Settings.Current.LanguageMode = mode;
                foreach (var page in new[] { "overview", "history", "appearance", "safety", "ollama" })
                {
                    cc.Navigate(page);
                    await Task.Delay(500);
                    Language.Refresh();
                    await Shot(cc, $"scale200-{name}-{page}", $"{page} at 960 x 540 (200 % on 1080p), {name}", 700);
                }
            }
            Settings.Current.LanguageMode = LanguageMode.English;
            Language.Refresh();
            cc.Width = w; cc.Height = h; cc.MinWidth = mw; cc.MinHeight = mh;
            await Task.Delay(400);
        }

        /// <summary>Language modes and a light theme on another seed, captured from the same window.</summary>
        private static async Task LookScenes(ControlCentreWindow cc)
        {
            var s = Settings.Current;
            foreach (var (mode, name) in new[] { (LanguageMode.Cantonese, "cantonese"), (LanguageMode.Bilingual, "bilingual") })
            {
                s.LanguageMode = mode;
                cc.Navigate("overview");
                await Task.Delay(700);
                Language.Refresh();
                await Shot(cc, "lang-" + name + "-overview", "Overview in " + name + " mode", 900);
                cc.Navigate("language");
                await Task.Delay(700);
                Language.Refresh();
                await Shot(cc, "lang-" + name + "-language", "Language page in " + name + " mode", 900);
                // the pages whose labels were the longest to translate, checked for clipping in each language mode
                foreach (var page in new[] { "ollama", "safety", "modes", "tools", "records", "appearance", "activity" })
                {
                    cc.Navigate(page);
                    await Task.Delay(700);
                    Language.Refresh();
                    await Shot(cc, "lang-" + name + "-" + page, page + " page in " + name + " mode", 900);
                }
            }
            s.LanguageMode = LanguageMode.English;
            Language.Refresh();

            s.ThemeMode = "light"; s.SeedColour = "violet";
            Appearance.Apply();
            cc.Navigate("appearance");
            await Shot(cc, "theme-light-violet", "Appearance in the light theme on the Violet seed", 1200);
            cc.Navigate("history");
            await Shot(cc, "theme-light-violet-history", "Clipboard history in the light theme", 1000);
            s.ThemeMode = "dark"; s.SeedColour = "mint";
            Appearance.Apply();
            await Task.Delay(400);
        }

        /// <summary>Renders the window and composites the builder popup at its anchored position.</summary>
        private static async Task ShotWithBuilder(Window w, Search.SearchField field, string name, string caption, int waitMs)
        {
            await Task.Delay(waitMs);
            w.UpdateLayout();
            var card = field.BuilderCard;
            if (card == null || !field.BuilderOpen) { await Shot(w, name, caption, 0); return; }
            card.UpdateLayout();
            var at = field.Bar.TranslatePoint(new Point(0, field.Bar.ActualHeight + 8), w);
            double width = Math.Max(w.ActualWidth, at.X + card.ActualWidth + 24), height = Math.Max(w.ActualHeight, at.Y + card.ActualHeight + 24);
            const double scale = 2;
            int pw = (int)Math.Ceiling(width * scale), ph = (int)Math.Ceiling(height * scale);
            var win = new RenderTargetBitmap((int)Math.Ceiling(w.ActualWidth * scale), (int)Math.Ceiling(w.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            win.Render(w);
            var pop = new RenderTargetBitmap((int)Math.Ceiling((card.ActualWidth + 24) * scale), (int)Math.Ceiling((card.ActualHeight + 24) * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            pop.Render(card);
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x08, 0x09, 0x0C)), null, new Rect(0, 0, width, height));
                dc.DrawImage(win, new Rect(0, 0, w.ActualWidth, w.ActualHeight));
                dc.DrawImage(pop, new Rect(at.X, at.Y, card.ActualWidth + 24, card.ActualHeight + 24));
            }
            var bmp = new RenderTargetBitmap(pw, ph, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            bmp.Render(dv);
            var file = name + ".png";
            Demo.SavePng(bmp, Path.Combine(_dir, file));
            Index.Add(new { file, caption, width = bmp.PixelWidth, height = bmp.PixelHeight });
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        private static async Task Toast(string name, string caption, ToastWindow w, int wait = 1300)
        {
            w.ExpandNow();
            await Shot(w, name, caption, wait);
            w.CloseAnimated();
            await Task.Delay(350);
        }

        /// <summary>Renders a window's content over the graphite ground at 2x, shadows included.</summary>
        // The convert dialog for one video and for a mixed folder. The files are empty placeholders in the throwaway
        // folder: the dialog only reads their names and kinds.
        private static async Task ConvertScenes()
        {
            var tmp = System.IO.Path.Combine(Settings.DataDir, "convert-capture");
            System.IO.Directory.CreateDirectory(tmp);
            string Make(string n) { var p = System.IO.Path.Combine(tmp, n); System.IO.File.WriteAllBytes(p, new byte[2048]); return p; }
            var clip = Make("Holiday clip.mp4");
            ConvertDialog.Open(new[] { clip });
            await Task.Delay(500);
            if (ConvertDialog.Current != null)
            {
                await Shot(ConvertDialog.Current, "convert-dialog", "Convert to: every format a video can become, grouped, with what each is for", 400);
                ConvertDialog.Current.PickForTest("mp4-h265");
                await Shot(ConvertDialog.Current, "convert-dialog-picked", "Convert to: a format picked, with where the file will be saved", 300);
                ConvertDialog.Current.Close();
            }
            var files = new[] { clip, Make("Voice memo.m4a"), Make("Cover.png"), Make("Notes.txt") };
            ConvertDialog.Open(files, tmp);
            await Task.Delay(500);
            if (ConvertDialog.Current != null)
            {
                await Shot(ConvertDialog.Current, "convert-dialog-batch", "Convert to for a folder: each format says how many of the files it can take", 400);
                ConvertDialog.Current.Close();
            }
        }

        private static async Task Shot(Window w, string name, string caption, int waitMs)
        {
            await Task.Delay(waitMs);
            w.UpdateLayout();
            if (w == null || w.ActualWidth < 1) return;
            // Render the window itself: its own (0,0) is the frame, so drop shadows inside the
            // window's margins land where they belong and nothing is cropped or shifted.
            double width = w.ActualWidth, height = w.ActualHeight;
            const double scale = 2;
            int pw = (int)Math.Ceiling(width * scale), ph = (int)Math.Ceiling(height * scale);
            var raw = new RenderTargetBitmap(pw, ph, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            raw.Render(w);
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x08, 0x09, 0x0C)), null, new Rect(0, 0, width, height));
                dc.DrawImage(raw, new Rect(0, 0, width, height));
            }
            var bmp = new RenderTargetBitmap(pw, ph, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            bmp.Render(dv);
            var file = name + ".png";
            Demo.SavePng(bmp, Path.Combine(_dir, file));
            Index.Add(new { file, caption, width = bmp.PixelWidth, height = bmp.PixelHeight });
        }
    }
}
