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
    public static class Huishots
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

                // welcome tour
                var welcome = new WelcomeWindow();
                welcome.Show();
                await Shot(welcome, "welcome", "Welcome tour", 1800);
                welcome.Close();

                // control centre, every page
                var cc = new ControlCentreWindow { Width = 1180, Height = 820 };
                cc.Show();
                await Task.Delay(1200);
                foreach (var page in new[] { "overview", "activity", "history", "clipboard", "shortcuts", "network", "game", "conversion", "service", "about" })
                {
                    cc.Navigate(page);
                    await Shot(cc, "control-" + page, "Control centre: " + page, 1300);
                }
                cc.Close();

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
        private static async Task Shot(Window w, string name, string caption, int waitMs)
        {
            await Task.Delay(waitMs);
            w.UpdateLayout();
            if (!(w.Content is FrameworkElement el) || el.ActualWidth < 1) return;
            var margin = el.Margin;
            double width = el.ActualWidth + margin.Left + margin.Right, height = el.ActualHeight + margin.Top + margin.Bottom;
            const double scale = 2;
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x08, 0x09, 0x0C)), null, new Rect(0, 0, width, height));
                var brush = new VisualBrush(el)
                {
                    Stretch = Stretch.Fill,
                    ViewboxUnits = BrushMappingMode.Absolute,
                    Viewbox = new Rect(-margin.Left, -margin.Top, width, height)
                };
                dc.DrawRectangle(brush, null, new Rect(0, 0, width, height));
            }
            var bmp = new RenderTargetBitmap((int)Math.Ceiling(width * scale), (int)Math.Ceiling(height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            bmp.Render(dv);
            var file = name + ".png";
            Demo.SavePng(bmp, Path.Combine(_dir, file));
            Index.Add(new { file, caption, width = bmp.PixelWidth, height = bmp.PixelHeight });
        }
    }
}
