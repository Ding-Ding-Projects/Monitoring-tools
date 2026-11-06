using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using MonitoringTools.Clip;
using MonitoringTools.Core;
using MonitoringTools.Media;

using Activity = MonitoringTools.Core.Activity;
using ActivityKind = MonitoringTools.Core.ActivityKind;

namespace MonitoringTools.UI
{
    public sealed class ToastManager
    {
        private readonly List<ToastWindow> _live = new List<ToastWindow>();
        private const int MaxLive = 4;

        public Color Mint => Ui.C("C.Mint");
        public Color Amber => Ui.C("C.Amber");
        public Color Violet => Ui.C("C.Violet");

        /// <summary>Height taken by the offline dialog at the bottom, so toasts stack above it.</summary>
        public double ReservedBottom { get; set; }

        // -------------------------------------------------------------- placement

        private readonly Queue<ToastWindow> _waiting = new Queue<ToastWindow>();

        /// <summary>How many toasts are waiting their turn under One thing at a time. For the self-test.</summary>
        internal int WaitingCount => _waiting.Count;
        /// <summary>The toasts on screen. For the self-test.</summary>
        internal IReadOnlyList<ToastWindow> LiveToasts => _live;

        private void Present(ToastWindow w)
        {
            if (ToastWindow.IsPaused) { NotificationLog.AddToast(w.TitleText.Text, w.MetaText.Text); w.Close(); return; }   // paused from a toast's menu: still recorded in the log, not shown
            // Focus: a toast that is not urgent goes quietly to the notification history instead of showing.
            if (Modes.ShouldHoldToast(w.Urgent)) { if (!w.Logged) NotificationLog.AddToast(w.TitleText.Text, w.MetaText.Text); w.Logged = true; w.Close(); return; }
            // One thing at a time: only one toast on screen; the others wait their turn, in order.
            if (Modes.OneThing && _live.Any(t => !t.IsClosing))
            {
                if (!w.Logged) NotificationLog.AddToast(w.TitleText.Text, w.MetaText.Text);
                w.Logged = true;
                _waiting.Enqueue(w);
                return;
            }
            if (_live.Count >= MaxLive) _live.First(t => !t.IsClosing).CloseAnimated();
            _live.Add(w); if (!w.Logged) NotificationLog.AddToast(w.TitleText.Text, w.MetaText.Text);
            w.Logged = true;
            w.Gone += () => { _live.Remove(w); Restack(); ShowNextWaiting(); };
            w.SizeChanged += (s, e) => Restack();
            var area = SystemParameters.WorkArea;
            w.Left = area.Right - w.Width;
            w.Top = area.Bottom - 200 - ReservedBottom;
            w.Show();
            Restack();
        }

        private void ShowNextWaiting()
        {
            if (!Modes.OneThing && _waiting.Count == 0) return;
            while (_waiting.Count > 0 && !_live.Any(t => !t.IsClosing))
            {
                var next = _waiting.Dequeue();
                if (next.IsClosing) continue;   // its time ran out while it waited
                Present(next);
            }
        }

        public void Restack()
        {
            var area = SystemParameters.WorkArea;
            double bottom = area.Bottom - ReservedBottom;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var w = _live[i];
                if (w.IsClosing) continue;
                var h = w.ActualHeight > 0 ? w.ActualHeight : 160;
                w.Left = area.Right - w.Width;
                w.SlideTo(bottom - h);
                bottom -= h - 14; // windows overlap by their shadow margins
            }
        }

        // -------------------------------------------------------------- clipboard toasts

        public ToastWindow ShowClip(ClipItem item)
        {
            var w = new ToastWindow();
            w.SetAccent(Mint, Ui.C("C.MintWash"));
            TextToastEditor editor = null;
            if (item.Kind == ClipKind.Install)
            {
                // A copied install command: its own card, with its own states (offer, refused, installing, installed, declined).
                var card = new InstallToast(w, item);
                card.Offer();
                w.StartTimer(card.OfferSeconds);
                Present(w);
                return w;
            }
            switch (item.Kind)
            {
                case ClipKind.Url: Url(w, item); break;
                case ClipKind.Email: w.SetHeader(Icons.Mail, "Email", ""); w.SetBody(Ui.Mono(item.Text, Ui.Res("B.Mint"), 12)); break;
                case ClipKind.Color: ColorBody(w, item); break;
                case ClipKind.Json: Json(w, item); break;
                case ClipKind.Markdown:
                    w.SetHeader(Icons.Text, "Markdown", Counts(item.Text));
                    w.SetBody(Settings.Current.RenderMarkdown ? (UIElement)Literal(Markdown.Render(item.Text)) : PlainText(item.Text));
                    break;
                case ClipKind.Files: Files(w, item); break;
                case ClipKind.Image: Bitmap(w, item); break;
                case ClipKind.Audio: AudioBody(w, item); break;
                default:
                    // Plain text is edited in place: the toast shows the text in a field that stays quiet until it is clicked.
                    if (item.Kind == ClipKind.Text && item.BestFact != null)
                    {
                        var fact = item.BestFact;
                        var smart = new SmartToast(w, fact);
                        w.SetHeader(SmartToast.Icon(fact), fact.Title, fact.FromExtension != null ? "from an extension" : "");
                        w.SetBody(smart.Build());
                        if (item.Sensitive) w.SetAccent(Amber, Ui.C("C.AmberWash"));
                        else SmartLookups.Enrich(item, fact, smart);
                    }
                    else if (item.Kind == ClipKind.Text)
                    {
                        editor = new TextToastEditor(w, item);
                        w.SetHeader(Icons.Text, "Copied text", TextToastEditor.Counts(item.Text));
                        w.SetBody(editor.Build());
                    }
                    else { w.SetHeader(Icons.Text, "Text", Counts(item.Text)); w.SetBody(PlainText(item.Text)); }
                    break;
            }
            var offered = ActionProvider.For(item);
            if (editor != null) { editor.Decorate(offered); w.SetActions(offered); editor.ApplyIdleHeader(); }
            else w.SetActions(offered);
            w.StartTimer(Settings.Current.ToastSeconds);
            Present(w);
            return w;
        }

        /// <summary>Automatic mode: the command with a visible countdown and a Cancel button. Returns null (and runs nothing) when the toast could not be shown.</summary>
        internal InstallToast ShowAutoInstall(ClipItem item) => InstallToast.ShowAuto(this, item, w => { Present(w); return w.IsVisible && !w.IsClosing; });

        private static string Counts(string t)
        {
            t ??= "";
            var lines = t.Split('\n').Length;
            var words = t.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Length;
            return $"{t.Length:N0} chars · {words:N0} words{(lines > 1 ? $" · {lines} lines" : "")}";
        }

        // What was copied is the person's own text, so the language layer leaves it as it is.
        private static UIElement Literal(UIElement e) { Core.Language.SetLiteral(e, true); return e; }

        private static UIElement PlainText(string t)
        {
            var tb = new TextBlock
            {
                FontFamily = Ui.Font("F.Body"), FontSize = 12, Foreground = Ui.Res("B.TextSoft"),
                TextWrapping = TextWrapping.Wrap, MaxHeight = 120, TextTrimming = TextTrimming.CharacterEllipsis, LineHeight = 19
            };
            Core.Language.SetLiteral(tb, true);
            var text = t.Length > 1200 ? t[..1200] + "…" : t;
            // bare links stay clickable in plain text
            int last = 0;
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, @"https?://[^\s)]+"))
            {
                if (m.Index > last) tb.Inlines.Add(new Run(text.Substring(last, m.Index - last)));
                tb.Inlines.Add(Markdown.MakeLink(m.Value, m.Value));
                last = m.Index + m.Length;
            }
            if (last < text.Length) tb.Inlines.Add(new Run(text.Substring(last)));
            return tb;
        }

        private static void Url(ToastWindow w, ClipItem item)
        {
            Uri.TryCreate(item.Text.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? "https://" + item.Text : item.Text, UriKind.Absolute, out var u);
            w.SetHeader(Icons.Link, "Link", u?.Host ?? "");
            var tb = new TextBlock { FontFamily = Ui.Font("F.Mono"), FontSize = 12, TextWrapping = TextWrapping.Wrap, MaxHeight = 80 };
            tb.Inlines.Add(Markdown.MakeLink(item.Text, item.Text));
            w.SetBody(tb);
        }

        private static void ColorBody(ToastWindow w, ClipItem item)
        {
            w.SetHeader(Icons.Palette, "Color", item.Text);
            var c = ActionProvider.ParseColor(item.Text) ?? Colors.Transparent;
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new Border { Width = 56, Height = 56, CornerRadius = new CornerRadius(12), Background = new SolidColorBrush(c), BorderBrush = Ui.Res("B.LineStrong"), BorderThickness = new Thickness(1) });
            var sp = new StackPanel { Margin = new Thickness(14, 4, 0, 0) };
            sp.Children.Add(Ui.Mono($"#{c.R:X2}{c.G:X2}{c.B:X2}", Ui.Res("B.Text"), 14));
            sp.Children.Add(Ui.Mono($"rgb({c.R}, {c.G}, {c.B})", Ui.Res("B.TextMute")));
            row.Children.Add(sp);
            w.SetBody(row);
        }

        private static void Json(ToastWindow w, ClipItem item)
        {
            int keys = 0;
            try
            {
                using var d = System.Text.Json.JsonDocument.Parse(item.Text);
                keys = d.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object ? d.RootElement.EnumerateObject().Count() :
                       d.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array ? d.RootElement.GetArrayLength() : 0;
            }
            catch { }
            w.SetHeader(Icons.Braces, "JSON", $"valid · {keys} top-level {(item.Text.TrimStart().StartsWith("[") ? "items" : "keys")}");
            var lines = item.Text.Split('\n').Take(8);
            w.SetBody(new Border
            {
                Background = Ui.Res("B.Sunken"), CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 8, 10, 8),
                Child = Ui.Mono(string.Join("\n", lines).TrimEnd(), Ui.Res("B.TextSoft"))
            });
        }

        private static string KindLabel(FileKind k) => k switch
        {
            FileKind.Folder => "Folder", FileKind.Video => "Video file", FileKind.Audio => "Audio file",
            FileKind.Image => "Image file", FileKind.Document => "Document", FileKind.Archive => "Archive",
            FileKind.Code => "Code file", _ => "File"
        };

        private static string KindIcon(FileKind k) => k switch
        {
            FileKind.Folder => Icons.Folder, FileKind.Video => Icons.Video, FileKind.Audio => Icons.Audio,
            FileKind.Image => Icons.Image, FileKind.Code => Icons.Code, _ => Icons.File
        };

        private void Files(ToastWindow w, ClipItem item)
        {
            var f = item.FirstFile;
            var kind = item.FirstFileKind;
            if (item.Files.Length > 1)
            {
                w.SetHeader(Icons.File, $"{item.Files.Length} files", Ui.Bytes(item.Files.Where(File.Exists).Sum(p => new FileInfo(p).Length)));
                var list = Ui.Mono(item.Preview, Ui.Res("B.TextSoft")); Core.Language.SetLiteral(list, true);   // a list of file names
                list.MaxHeight = 110;
                w.SetBody(list);
                ToastWindow.EnableDrag(list, () => FileDrop(item.Files));
                w.SetHint("Drag the list out to drop all of them. Hover to move, copy, delete, encode or encrypt.");
                return;
            }

            string meta = "";
            try
            {
                if (kind == FileKind.Folder) meta = $"{Directory.EnumerateFileSystemEntries(f).Take(1000).Count()} items";
                else { var fi = new FileInfo(f); meta = $"{fi.Extension.ToLowerInvariant()} · {Ui.Bytes(fi.Length)}"; }
            }
            catch { }
            var docker = DockerActions.Detect(f);
            var title = docker == DockerFileKind.Compose ? "Compose file" : docker == DockerFileKind.Dockerfile ? "Dockerfile" : KindLabel(kind);
            w.SetHeader(docker != DockerFileKind.None ? Icons.Box : KindIcon(kind), title, meta);

            // Rich preview first (thumbnail, text head, folder or zip listing), filled in asynchronously
            var body = new StackPanel();
            body.Children.Add(FilePreview.Build(f, null));
            var dir = Path.GetDirectoryName(f.TrimEnd('\\'));
            var path = new TextBlock { FontFamily = Ui.Font("F.Mono"), FontSize = 12, Foreground = Ui.Res("B.TextSoft"), TextWrapping = TextWrapping.Wrap, LineHeight = 19, VerticalAlignment = VerticalAlignment.Center };
            if (item.HadQuotes) path.Inlines.Add(new Run("\"") { Foreground = Ui.Res("B.Amber") });
            if (!string.IsNullOrEmpty(dir)) path.Inlines.Add(new Run(dir.TrimEnd('\\') + "\\"));
            path.Inlines.Add(new Run(Path.GetFileName(f.TrimEnd('\\'))) { Foreground = Ui.Res("B.Text"), FontWeight = FontWeights.Medium });
            if (item.HadQuotes) path.Inlines.Add(new Run("\"") { Foreground = Ui.Res("B.Amber") });
            body.Children.Add(path);
            w.SetBody(body);
            ToastWindow.EnableDrag(body, () => FileDrop(item.Files));

            if (item.HadQuotes) w.SetHint("Copied with quotes. Hover for Remove quotes.");
            else if (docker == DockerFileKind.Compose) w.SetHint("Docker Compose file. Hover to see what you can run and what each one does.");
            else if (docker == DockerFileKind.Dockerfile) w.SetHint("Dockerfile. Hover to build an image from it.");
            else if (kind == FileKind.Video || kind == FileKind.Audio || kind == FileKind.Image)
                w.SetHint($"Drag it anywhere. Or copy it again with :{Formats.SuggestedFor(kind, Path.GetExtension(f)).FirstOrDefault() ?? "mp4"} on the end to convert.");
            else if (GitPush.FindRepoRoot(f) != null) w.SetHint("Inside a git repository. Copy it with :push on the end to commit and push.");
        }

        private static FrameworkElement Thumb(string file)
        {
            try
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(file);
                bi.DecodePixelWidth = 224;
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.EndInit();
                bi.Freeze();
                return ThumbFrame(bi);
            }
            catch { return null; }
        }

        private static FrameworkElement ThumbFrame(ImageSource src)
        {
            return new Border
            {
                Width = 112, Height = 78, CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 0, 14, 0),
                Background = new ImageBrush(src) { Stretch = Stretch.UniformToFill },
                BorderBrush = Ui.Res("B.LineStrong"), BorderThickness = new Thickness(1)
            };
        }

        private static DataObject FileDrop(string[] files)
        {
            var d = new DataObject();
            d.SetFileDropList(new System.Collections.Specialized.StringCollection().With(files));
            d.SetText(string.Join(Environment.NewLine, files));
            return d;
        }

        private void Bitmap(ToastWindow w, ClipItem item)
        {
            var img = item.Image;
            w.SetHeader(Icons.Image, "Image", img == null ? "" : $"{img.PixelWidth} × {img.PixelHeight} px");
            if (img == null) return;
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            var thumb = ThumbFrame(img);
            row.Children.Add(thumb);
            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            info.Children.Add(Ui.Mono($"{img.PixelWidth} × {img.PixelHeight} px", Ui.Res("B.Text"), 12));
            info.Children.Add(Ui.Text("Bitmap on the clipboard. Drag the preview out to drop it as a PNG file."));
            foreach (var code in CodeScanner.Scan(img).Take(3))
            {
                info.Children.Add(new TextBlock { Text = code.Kind, FontSize = 12, Foreground = Ui.Res("B.Mint"), Margin = new Thickness(0, 6, 0, 0), FontFamily = Ui.Font("F.Body") });
                var body = Ui.Mono(ActionProvider.Preview(code.Text), Ui.Res("B.Text"), 12);
                body.TextWrapping = TextWrapping.Wrap; body.MaxHeight = 60;
                Core.Language.SetLiteral(body, true);   // the code's own text is data, never translated
                info.Children.Add(body);
            }
            Grid.SetColumn(info, 1);
            row.Children.Add(info);
            w.SetBody(row);
            ToastWindow.EnableDrag(thumb, () =>
            {
                var tmp = Path.Combine(Path.GetTempPath(), "MonitoringTools");
                Directory.CreateDirectory(tmp);
                var p = Path.Combine(tmp, "Clipboard " + DateTime.Now.ToString("yyyy-MM-dd HHmmss") + ".png");
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(img));
                using (var fs = File.Create(p)) enc.Save(fs);
                var d = FileDrop(new[] { p });
                d.SetImage(img);
                return d;
            });
            w.SetHint("Hover to save or convert. Quick commands work here too.");
        }

        private void AudioBody(ToastWindow w, ClipItem item)
        {
            w.SetHeader(Icons.Audio, "Audio clip", item.Audio == null ? "" : Ui.Bytes(item.Audio.Length));
            var body = Ui.Text("Wave audio on the clipboard. Drag this line out to drop it as a .wav file.");
            w.SetBody(body);
            if (item.Audio != null)
            {
                var bytes = item.Audio;
                ToastWindow.EnableDrag(body, () =>
                {
                    var tmp = Path.Combine(Path.GetTempPath(), "MonitoringTools");
                    Directory.CreateDirectory(tmp);
                    var p = Path.Combine(tmp, "Clipboard audio " + DateTime.Now.ToString("yyyy-MM-dd HHmmss") + ".wav");
                    File.WriteAllBytes(p, bytes);
                    return FileDrop(new[] { p });
                });
            }
        }

        // -------------------------------------------------------------- notices

        public ToastWindow ShowNotice(string title, string body, Color accent, double seconds = 5, IList<ToastAction> actions = null, string icon = Icons.Pulse, bool urgent = false)
        {
            var w = new ToastWindow { Urgent = urgent || accent == Amber };
            w.SetAccent(accent, accent);
            w.SetHeader(icon, Core.Language.NoticeTitle(title), "");
            w.SetBody(Ui.Text(body));
            if (actions != null) { w.SetActions(actions); w.ExpandNow(); }
            w.StartTimer(seconds);
            Present(w);
            return w;
        }

        // -------------------------------------------------------------- public IP change

        public ToastWindow ShowIpChanged(Net.IpRecord previous, Net.IpRecord current)
        {
            var w = new ToastWindow();
            w.SetAccent(Ui.C("C.Violet"), Ui.C("C.VioletWash"));
            w.SetHeader(Icons.Globe, Core.Language.Headline("ip.changed", current.Ip), current.Org ?? "");
            var body = new StackPanel();
            var row = new WrapPanel();
            row.Children.Add(new TextBlock { Text = previous.Ip, FontFamily = Ui.Font("F.Mono"), FontSize = 14, Foreground = Ui.Res("B.TextMute"), TextDecorations = TextDecorations.Strikethrough, VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(new ContentControl { Content = Ui.Icon("M5 12h14M13 6l6 6-6 6", Ui.Res("B.Violet"), 16), Margin = new Thickness(10, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(new TextBlock { Text = current.Ip, FontFamily = Ui.Font("F.Mono"), FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = Ui.Res("B.Text"), VerticalAlignment = VerticalAlignment.Center });
            body.Children.Add(row);
            var since = previous.LastSeen - previous.FirstSeen;
            var detail = $"{(string.IsNullOrEmpty(current.Place) ? "" : current.Place + " · ")}{(current.Times > 1 ? $"seen {current.Times} times before" : "new address")}";
            body.Children.Add(Ui.Mono(detail, Ui.Res("B.TextSoft")));
            body.Children.Add(Ui.Mono($"previous held {(since.TotalHours >= 1 ? $"{(int)since.TotalHours} h {since.Minutes} m" : $"{(int)since.TotalMinutes} m")} · last seen {previous.LastSeen:HH:mm}", Ui.Res("B.TextMute"), 11));
            w.SetBody(body);
            w.SetHint("Saved to the IP history repository with first and last seen times.");
            w.SetActions(new List<ToastAction>
            {
                new ToastAction { Label = "Copy new IP", Icon = Icons.Clipboard, Primary = true, Run = () => ClipboardWatcher.SetText(current.Ip), Done = "Copied" },
                new ToastAction { Label = "History", Icon = Icons.Lines, HandsOff = true, Run = () => App.Current.OpenControlCentre("network") },
                new ToastAction { Label = "Open repository", Icon = Icons.Git, Run = () => ActionProvider.Shell(Net.PublicIpMonitor.RepoDir), Done = "Opened" },
            });
            w.StartTimer(15);
            Present(w);
            Sounds.Play(Cue.Success);
            return w;
        }

        // -------------------------------------------------------------- conversion jobs

        public ToastWindow ShowJob(ConversionJob job)
        {
            if (App.ShouldSuppress) { Log.Info("Game mode: conversion runs without a toast"); return null; }

            var w = new ToastWindow();
            w.SetAccent(Mint, Ui.C("C.MintWash"));
            w.SetHeader(Icons.Bolt, Core.Language.Headline("convert.start", job.Format.Label), "starting");
            w.SetWhen("");

            var body = new StackPanel();
            body.Children.Add(Ui.Mono($"{Path.GetFileName(job.Source)} → {Path.GetFileName(job.Output)}", Ui.Res("B.TextSoft")));
            var track = new Border { Height = 8, CornerRadius = new CornerRadius(4), Background = Ui.Res("B.Raised"), Margin = new Thickness(0, 10, 0, 6) };
            var fill = new Border { CornerRadius = new CornerRadius(4), Background = Ui.Res("B.Mint"), HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };
            track.Child = fill;
            body.Children.Add(track);
            var detail = Ui.Mono("reading source…", Ui.Res("B.TextMute"), 11);
            body.Children.Add(detail);
            w.SetBody(body);
            w.SetActions(new List<ToastAction> { new ToastAction { Label = "Cancel", Run = job.Cancel, HandsOff = false, Done = "Cancelling" } });
            w.ExpandNow();
            w.StartTimer(0);
            Present(w);

            job.Changed += () =>
            {
                if (!job.Done)
                {
                    var pct = (int)Math.Round(job.Progress * 100);
                    w.MetaText.Text = job.Duration > TimeSpan.Zero ? $"{pct}%" : "working";
                    var width = Math.Max(0, track.ActualWidth * job.Progress);
                    fill.BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation(width, TimeSpan.FromMilliseconds(300)) { EasingFunction = Ui.Out });
                    var eta = job.Speed > 0 && job.Duration > TimeSpan.Zero ? TimeSpan.FromSeconds((job.Duration - job.Position).TotalSeconds / job.Speed) : (TimeSpan?)null;
                    detail.Text = $"{job.Position:mm\\:ss} / {job.Duration:mm\\:ss}{(job.Speed > 0 ? $" · {job.Speed:0.0}×" : "")}{(eta.HasValue ? $" · ~{(int)eta.Value.TotalSeconds} s left" : "")}";
                    w.SetProgress(job.Progress);
                    return;
                }
                if (job.Failed && job.Error == "ffmpeg-missing") { FfmpegMissing(w, job); return; }
                if (job.Failed)
                {
                    w.SetAccent(Amber, Ui.C("C.AmberWash"));
                    w.SetHeader(Icons.Bolt, job.Error == "Cancelled" ? Core.Language.Headline("convert.cancelled") : Core.Language.Headline("convert.failed", job.Format.Label), Path.GetFileName(job.Source));
                    w.SetBody(Ui.Text(job.Error == "Cancelled" ? "Nothing was written." : ActionProvider.Tail(job.Error, 3)));
                    w.SetActions(new List<ToastAction>
                    {
                        new ToastAction { Label = "Copy error", Run = () => ClipboardWatcher.SetText(job.Error ?? ""), Done = "Copied" },
                        new ToastAction { Label = "Open logs", Run = () => ActionProvider.Shell(Log.Folder), Done = "Opened" },
                    });
                    w.StartTimer(job.Error == "Cancelled" ? 3 : 0);
                    return;
                }
                fill.BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation(track.ActualWidth, TimeSpan.FromMilliseconds(260)));
                w.SetHeader(Icons.Check, Core.Language.Headline("convert.done", job.Format.Label), $"{Ui.Bytes(job.SourceBytes)} → {Ui.Bytes(job.OutputBytes)}");
                var done = Ui.Mono(job.Output, Ui.Res("B.TextSoft"));
                w.SetBody(done);
                ToastWindow.EnableDrag(done, () => FileDrop(new[] { job.Output }));
                w.SetHint("Drag the path out to drop the new file anywhere.");
                w.SetActions(new List<ToastAction>
                {
                    new ToastAction { Label = "Open", Icon = Icons.Play, Primary = true, Run = () => ActionProvider.Shell(job.Output), Done = "Opened" },
                    new ToastAction { Label = "Show in folder", Icon = Icons.Folder, Run = () => ActionProvider.Reveal(job.Output), Done = "Shown" },
                    new ToastAction { Label = "Copy path", Run = () => ClipboardWatcher.SetText(job.Output), Done = "Copied" },
                    new ToastAction { Label = "Copy as file", Run = () => ClipboardWatcher.SetFiles(job.Output), Done = "Copied" },
                });
                w.StartTimer(8);
            };
            return w;
        }

        private void FfmpegMissing(ToastWindow w, ConversionJob job)
        {
            w.SetAccent(Amber, Ui.C("C.AmberWash"));
            // Release builds carry FFmpeg inside; this only shows for a build without the bundle,
            // and it says exactly where it looked so a dead bundle is obvious.
            w.SetHeader(Icons.Bolt, Core.Language.Headline("ffmpeg.missing"), "");
            var searched = string.Join("\n", Converter.FfmpegSearched.Take(6)) + (Converter.FfmpegSearched.Count > 6 ? $"\n+{Converter.FfmpegSearched.Count - 6} more places" : "");
            var body = new StackPanel();
            body.Children.Add(Ui.Text("This copy does not carry its bundled FFmpeg. It can fetch it now and then run this job."));
            var where = Ui.Mono("Searched:\n" + searched, Ui.Res("B.TextMute"), 11);
            where.Margin = new Thickness(0, 8, 0, 0);
            where.MaxHeight = 120;
            body.Children.Add(where);
            w.SetBody(body);
            w.SetActions(new List<ToastAction>
            {
                new ToastAction { Label = "Fetch FFmpeg", Primary = true, HandsOff = true, Run = () => ShowTask("Fetching FFmpeg", "winget · Gyan.FFmpeg", async () =>
                    {
                        var path = await Converter.InstallFfmpegAsync();
                        if (path != null) { Converter.Start(job.Source, job.Format.Id); return (true, "FFmpeg ready", path); }
                        return (false, "Fetching did not finish", "Get the release build, which carries FFmpeg inside, or pick ffmpeg.exe below.");
                    }) },
                new ToastAction { Label = "Pick ffmpeg.exe", HandsOff = true, Run = () =>
                    {
                        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "ffmpeg.exe|ffmpeg.exe", Title = "Find ffmpeg.exe" };
                        if (dlg.ShowDialog() == true) { Settings.Current.FfmpegPath = dlg.FileName; Settings.Save(); Converter.Start(job.Source, job.Format.Id); }
                    } },
            });
            w.ExpandNow();
            w.StartTimer(0);
        }

        // -------------------------------------------------------------- generic background task toast

        public void ShowTask(string title, string subtitle, Func<Task<(bool Ok, string Summary, string Detail)>> work, Func<bool, string, string> headline = null)
            => ShowTask(title, subtitle, _ => work(), headline);

        /// <summary>
        /// Runs a background task under one toast. <paramref name="headline"/> turns the outcome (success, summary) into the
        /// finished toast's headline; without it the generic task headline is used. Either way the summary's facts stay in the text.
        /// </summary>
        public void ShowTask(string title, string subtitle, Func<Action<string>, Task<(bool Ok, string Summary, string Detail)>> work, Func<bool, string, string> headline = null)
        {
            if (App.ShouldSuppress)
            {
                _ = work(_ => { }).ContinueWith(t => Log.Info($"{title} (game mode, no toast): {(t.IsFaulted ? t.Exception?.GetBaseException().Message : t.Result.Summary)}"));
                return;
            }
            var w = new ToastWindow();
            w.SetAccent(Mint, Ui.C("C.MintWash"));
            w.SetHeader(Icons.Bolt, title, subtitle);
            w.SetWhen("");
            var detail = Ui.Mono("working…", Ui.Res("B.TextMute"));
            w.SetBody(detail);
            w.StartTimer(0);
            w.TimerScale.BeginAnimation(ScaleTransform.ScaleXProperty,
                new DoubleAnimation(0.1, 1, TimeSpan.FromMilliseconds(900)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() });
            Present(w);
            // Time awareness: a running task shows how long it has been going.
            var began = DateTime.Now;
            System.Windows.Threading.DispatcherTimer clock = null;
            if (Modes.TimeAwareness)
            {
                clock = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                clock.Tick += (a, b) => w.SetWhen(((int)(DateTime.Now - began).TotalSeconds) + " s elapsed");
                clock.Start();
                w.SetWhen("0 s elapsed");
            }
            _ = Finish();

            async Task Finish()
            {
                (bool Ok, string Summary, string Detail) r;
                Action<string> report = text => w.Dispatcher.BeginInvoke(new Action(() => detail.Text = text));
                try { r = await work(report); }
                catch (Exception ex) { r = (false, "Failed", ex.Message); }
                w.TimerScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                if (clock != null) { clock.Stop(); w.SetWhen("Took " + Math.Max(0, (int)Math.Round((DateTime.Now - began).TotalSeconds)) + " s"); }
                Sounds.Play(r.Ok ? Cue.Complete : Cue.Warning);
                Activity.Add(title.StartsWith(":") ? ActivityKind.Shortcut : title.StartsWith("Commit") ? ActivityKind.Push : title.StartsWith("docker") ? ActivityKind.Docker : ActivityKind.Service,
                    $"{title} · {r.Summary}", ActionProvider.Tail(r.Detail, 1), r.Ok);
                if (!r.Ok) w.SetAccent(Amber, Ui.C("C.AmberWash"));
                var head = headline != null ? headline(r.Ok, r.Summary) : Core.Language.Headline(r.Ok ? "task.done" : "task.failed", r.Summary);
                w.SetHeader(r.Ok ? Icons.Check : Icons.Bolt, head, headline != null ? r.Summary : subtitle);
                var body = Ui.Mono(string.IsNullOrWhiteSpace(r.Detail) ? (r.Ok ? "Finished." : "No output.") : ActionProvider.Tail(r.Detail, 6), Ui.Res("B.TextSoft"));
                body.MaxHeight = 120;
                w.SetBody(body);
                var detailText = r.Detail ?? "";
                var chips = new List<ToastAction> { new ToastAction { Label = "Copy output", Run = () => ClipboardWatcher.SetText(detailText), Done = "Copied" } };
                // Momentum: name the next step, so a finished task does not leave a gap
                if (Modes.Momentum) chips.Add(new ToastAction { Label = "Next: open Activity", Run = () => App.Current.OpenControlCentre("activity"), Done = "Opened" });
                w.SetActions(chips);
                w.StartTimer(r.Ok ? 5 : 0);
            }
        }

        // -------------------------------------------------------------- :push and custom shortcuts

        public void StartPush(string path)
        {
            if (string.IsNullOrWhiteSpace(Settings.Current.CommitTemplate))
            {
                var dlg = new CommitTemplateWindow();
                dlg.Saved += () => StartPush(path);
                dlg.Show();
                return;
            }
            var root = GitPush.FindRepoRoot(path);
            var repoName = root == null ? path : Path.GetFileName(root);
            ShowTask("Commit and push", repoName, async () =>
            {
                var r = await GitPush.RunAsync(path, null);
                var detail = r.Message != null ? $"\"{r.Message}\"\n{r.Detail}" : r.Detail;
                return (r.Ok, r.Summary, detail);
            }, (ok, summary) => Core.Language.Headline(ok ? "push.done" : "push.failed", repoName));
        }

        public void RunShortcut(Shortcut s, string input)
        {
            string took = "0.0"; int exitCode = 0;
            ShowTask(":" + s.Name, string.IsNullOrEmpty(input) ? s.Shell.ToString() : Path.GetFileName(input.TrimEnd('\\')), async () =>
            {
                var r = await Shortcuts.RunAsync(s, input);
                took = r.Took.TotalSeconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture); exitCode = r.ExitCode;
                return (r.ExitCode == 0, r.ExitCode == 0 ? $":{s.Name} finished in {took} s" : $":{s.Name} exited with {r.ExitCode}", r.Output);
            }, (ok, summary) => ok ? Core.Language.Headline("shortcut.done", ":" + s.Name, took) : Core.Language.Headline("shortcut.failed", ":" + s.Name, exitCode.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
    }

    internal static class CollectionExt
    {
        public static System.Collections.Specialized.StringCollection With(this System.Collections.Specialized.StringCollection c, IEnumerable<string> items)
        {
            foreach (var i in items) c.Add(i);
            return c;
        }
    }
}
