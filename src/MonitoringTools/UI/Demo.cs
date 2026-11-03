using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MonitoringTools.Clip;
using MonitoringTools.Core;
using MonitoringTools.Media;
using MonitoringTools.Net;

namespace MonitoringTools.UI
{
    /// <summary>Sample content and "Try it" previews, used by the welcome tour and the screenshot run.</summary>
    public static class Demo
    {
        public const string SampleMarkdown =
            "# Release checklist\n" +
            "Bump the version, run `dotnet publish`, then **tag** it.\n" +
            "- [x] Build on Windows\n" +
            "- [ ] Upload the zip\n" +
            "- Post notes to the team\n" +
            "> Ship it before Friday\n" +
            "Details: https://github.com/Ding-Ding-Projects/Monitoring-tools";

        public const string SampleUrl = "https://github.com/Ding-Ding-Projects/Monitoring-tools/releases?utm_source=newsletter&utm_medium=email";

        public static string Folder
        {
            get
            {
                var d = Path.Combine(Path.GetTempPath(), "MonitoringTools", "Samples");
                Directory.CreateDirectory(d);
                return d;
            }
        }

        /// <summary>Writes a small set of sample files once: Markdown, CSV, a compose file, images and a folder.</summary>
        public static Dictionary<string, string> EnsureSamples()
        {
            var d = Folder;
            var map = new Dictionary<string, string>
            {
                ["markdown"] = Path.Combine(d, "Release notes.md"),
                ["csv"] = Path.Combine(d, "Sales 2026.csv"),
                ["compose"] = Path.Combine(d, "docker-compose.yml"),
                ["image"] = Path.Combine(d, "Launch poster.png"),
                ["image2"] = Path.Combine(d, "Team photo.png"),
                ["folder"] = Path.Combine(d, "Footage"),
            };
            if (!File.Exists(map["markdown"])) File.WriteAllText(map["markdown"], SampleMarkdown);
            if (!File.Exists(map["csv"])) File.WriteAllText(map["csv"], "Region,Q1,Q2,Q3,Q4\nNorth,1200,1340,1415,1610\nSouth,980,1020,1105,1190\nEast,1430,1390,1520,1688\nWest,860,920,990,1075\n");
            if (!File.Exists(map["compose"])) File.WriteAllText(map["compose"], "services:\n  web:\n    image: nginx:alpine\n    ports:\n      - \"8080:80\"\n  cache:\n    image: redis:7\n");
            if (!File.Exists(map["image"])) SavePng(SampleBitmap(1200, 800, "Launch", Color.FromRgb(0x7C, 0xF2, 0xC2)), map["image"]);
            if (!File.Exists(map["image2"])) SavePng(SampleBitmap(1600, 1000, "Team", Color.FromRgb(0xB9, 0xA2, 0xFF)), map["image2"]);
            Directory.CreateDirectory(map["folder"]);
            for (int i = 1; i <= 4; i++)
            {
                var p = Path.Combine(map["folder"], $"Shot {i:00}.png");
                if (!File.Exists(p)) SavePng(SampleBitmap(800, 450, $"Shot {i}", Color.FromRgb(0xFF, 0xB5, 0x47)), p);
            }
            var notes = Path.Combine(map["folder"], "notes.txt");
            if (!File.Exists(notes)) File.WriteAllText(notes, "Keep takes 2 and 4.\n");
            return map;
        }

        /// <summary>A generated picture: graphite ground, a soft glow and a big label.</summary>
        public static BitmapSource SampleBitmap(int w, int h, string label, Color accent)
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x14, 0x16, 0x1B)), null, new Rect(0, 0, w, h));
                var glow = new RadialGradientBrush(Color.FromArgb(0x90, accent.R, accent.G, accent.B), Colors.Transparent) { Center = new Point(0.3, 0.35), GradientOrigin = new Point(0.3, 0.35), RadiusX = 0.7, RadiusY = 0.9 };
                dc.DrawRectangle(glow, null, new Rect(0, 0, w, h));
                var pen = new Pen(new SolidColorBrush(accent), h / 60.0) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                var g = new StreamGeometry();
                using (var c = g.Open())
                {
                    c.BeginFigure(new Point(w * 0.1, h * 0.62), false, false);
                    c.PolyLineTo(new[] { new Point(w * 0.3, h * 0.62), new Point(w * 0.4, h * 0.38), new Point(w * 0.52, h * 0.8), new Point(w * 0.6, h * 0.62), new Point(w * 0.9, h * 0.62) }, true, true);
                }
                dc.DrawGeometry(null, pen, g);
                var ft = new FormattedText(label, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI Variable Display, Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal), h / 7.0,
                    new SolidColorBrush(Color.FromRgb(0xE8, 0xEA, 0xED)), 1.0);
                dc.DrawText(ft, new Point(w * 0.1, h * 0.12));
            }
            var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(dv);
            bmp.Freeze();
            return bmp;
        }

        public static void SavePng(BitmapSource img, string path)
        {
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(img));
            using var fs = File.Create(path);
            enc.Save(fs);
        }

        public static NetworkState SampleOutage()
        {
            var st = new NetworkState
            {
                Online = false, Cause = Cause.DnsFailing, Headline = "Internet connection lost",
                Explanation = "The line is up, but DNS lookups are failing, so websites by name will not load. Switching DNS usually fixes this.",
                DownSince = DateTime.Now.AddSeconds(-38), GatewayMs = 4
            };
            st.Stages.Add(new Stage { Name = "Adapter", Result = StageResult.Ok, Value = "up" });
            st.Stages.Add(new Stage { Name = "Gateway", Result = StageResult.Ok, Value = "4 ms" });
            st.Stages.Add(new Stage { Name = "DNS", Result = StageResult.Fail, Value = "timeout" });
            st.Stages.Add(new Stage { Name = "Internet", Result = StageResult.Ok, Value = "by IP" });
            st.Details.Add(("Adapter", "Wi-Fi · Intel Wi-Fi 6E AX211"));
            st.Details.Add(("Network", "Home-5G"));
            st.Details.Add(("IPv4", "10.0.0.42"));
            st.Details.Add(("Gateway", "10.0.0.1"));
            st.Details.Add(("DNS servers", "10.0.0.1"));
            return st;
        }

        // -------------------------------------------------------------- Try it

        public static void TryClipboard()
        {
            App.Toasts.ShowClip(ClipItem.FromText(SampleMarkdown));
            After(900, () => App.Toasts.ShowClip(ClipItem.FromText(SampleUrl)));
            After(1800, () => App.Toasts.ShowClip(ClipItem.FromFiles(new[] { EnsureSamples()["image"] })));
        }

        /// <summary>A real conversion: the sample PNG becomes a JPG beside it (works without FFmpeg).</summary>
        public static void TryQuickCommand()
        {
            var s = EnsureSamples();
            Converter.Start(s["image"], "jpg");
        }

        public static void PreviewOffline()
        {
            var w = new OfflineWindow(SampleOutage());
            w.Show();
            After(7000, () => { var ok = new NetworkState { Online = true, DownSince = DateTime.Now.AddMinutes(-2).AddSeconds(-14) }; w.ShowRestored(ok); });
        }

        public static void TryFileTools()
        {
            var s = EnsureSamples();
            FileToolsWindow.Open(new[] { s["image"], s["image2"], s["csv"] });
        }

        public static void After(int ms, Action a)
        {
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
            t.Tick += (o, e) => { t.Stop(); a(); };
            t.Start();
        }

        /// <summary>Fills history, activity, outages and latency so a fresh screenshot run looks lived in.</summary>
        public static void SeedLivedIn()
        {
            var s = EnsureSamples();
            App.History.Add(ClipItem.FromText("Meeting moved to 3 pm, same room."));
            App.History.Add(ClipItem.FromText(SampleUrl));
            App.History.Add(ClipItem.FromFiles(new[] { s["csv"] }));
            App.History.Add(ClipItem.FromText("#7CF2C2"));
            App.History.Add(ClipItem.FromFiles(new[] { s["image"] }));
            App.History.Add(ClipItem.FromText(SampleMarkdown));

            var now = DateTime.Now;
            Activity.Seed(new[]
            {
                new ActivityEntry { At = now.AddMinutes(-2), Kind = ActivityKind.Convert, Title = "launch-teaser.mov → launch-teaser.mp4", Detail = "412 MB → 96 MB" },
                new ActivityEntry { At = now.AddMinutes(-9), Kind = ActivityKind.Push, Title = "Commit and push · Committed 3 files and pushed main", Detail = "Update my-repo on 2026-11-03 at 16:20" },
                new ActivityEntry { At = now.AddMinutes(-14), Kind = ActivityKind.Shortcut, Title = ":backup · :backup finished in 1.8 s", Detail = "Copied 12 files to D:\\Backup" },
                new ActivityEntry { At = now.AddMinutes(-31), Kind = ActivityKind.Network, Title = "Back online", Detail = "down 2 m 14 s · Internet connection lost", Ok = true },
                new ActivityEntry { At = now.AddMinutes(-33), Kind = ActivityKind.Network, Title = "Connection lost", Detail = "DNS lookups failing", Ok = false },
                new ActivityEntry { At = now.AddMinutes(-48), Kind = ActivityKind.Files, Title = "Encrypted 2 items", Detail = "AES-256-GCM" },
                new ActivityEntry { At = now.AddMinutes(-66), Kind = ActivityKind.Docker, Title = "docker compose up · Compose up finished", Detail = "web, cache started" },
            }, new[]
            {
                new Outage { Start = now.AddMinutes(-33), End = now.AddMinutes(-31), Cause = "DNS lookups failing" },
                new Outage { Start = now.AddHours(-5), End = now.AddHours(-5).AddMinutes(7), Cause = "Router is not responding" },
            });

            var rnd = new Random(7);
            App.Net.Seed(Enumerable.Range(0, 90).Select(i =>
            {
                bool down = i >= 40 && i < 46;
                long gw = down ? -1 : 2 + rnd.Next(5) + (i % 17 == 0 ? 14 : 0);
                long dns = down ? -1 : 9 + rnd.Next(12) + (i % 23 == 0 ? 30 : 0);
                return (now.AddSeconds(-3 * (90 - i)), gw, dns, !down);
            }));

            if (Settings.Current.Shortcuts.Count == 0)
            {
                Settings.Current.Shortcuts.Add(new Shortcut { Name = "backup", Shell = ScriptShell.PowerShell, Description = "Copy to D:\\Backup with today's date" });
                Settings.Current.Shortcuts.Add(new Shortcut { Name = "zip", Shell = ScriptShell.Batch, Description = "Zip it next to the original" });
            }
            if (string.IsNullOrWhiteSpace(Settings.Current.CommitTemplate)) Settings.Current.CommitTemplate = GitPush.SuggestedTemplate;
        }
    }
}
