using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MonitoringTools.Clip;
using MonitoringTools.Core;
using MonitoringTools.Media;

namespace MonitoringTools.UI
{
    public partial class ControlCentreWindow : Window
    {
        private readonly (string Id, string Label, string Icon)[] _sections =
        {
            ("overview", "Overview", Icons.Pulse),
            ("history", "Clipboard history", Icons.Clipboard),
            ("clipboard", "Clipboard", Icons.Text),
            ("shortcuts", "Shortcuts and scripts", Icons.Code),
            ("network", "Network and DNS", Icons.Wifi),
            ("game", "Game mode", Icons.Game),
            ("conversion", "Conversion", Icons.Bolt),
            ("service", "Service", Icons.Shield),
        };
        private string _current;
        private Shortcut _editing;

        public ControlCentreWindow()
        {
            InitializeComponent();
            SourceInitialized += (s, e) => Native.ApplyDarkChrome(this);
            StateChanged += (s, e) => Shell.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
            FooterVersion.Text = "v" + (typeof(App).Assembly.GetName().Version?.ToString(3) ?? "1.0.0") + (ServiceHost.IsElevated ? " · admin" : "");
            int i = 0;
            foreach (var (id, label, icon) in _sections)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                row.Children.Add(new ContentControl { Content = Ui.Icon(icon, Ui.Res("B.TextSoft"), 15), Margin = new Thickness(4, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center });
                row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
                var b = new Button { Style = Ui.Style("Btn.Row"), Content = row, Tag = id, HorizontalContentAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 4) };
                var sid = id;
                b.Click += (s, e) => Navigate(sid);
                Nav.Children.Add(b);
                Ui.Rise(b, 60 + i++ * 35, 8);
            }
            App.History.Updated += () => { if (_current == "history" || _current == "overview") Navigate(_current, animate: false); };
            Loaded += (s, e) => Navigate("overview");
            RefreshStatus();
        }

        public void RefreshStatus()
        {
            var (_, status) = App.Tray.State();
            FooterState.Text = status;
            if (_current == "overview") Navigate("overview", animate: false);
        }

        // -------------------------------------------------------------- navigation

        public void Navigate(string id, bool animate = true)
        {
            var idx = Array.FindIndex(_sections, s => s.Id == id);
            if (idx < 0) idx = 0;
            id = _sections[idx].Id;
            _current = id;
            NavShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(idx * 42, TimeSpan.FromMilliseconds(animate ? 360 : 0)) { EasingFunction = Ui.Spring });

            FrameworkElement page = id switch
            {
                "history" => HistoryPage(),
                "clipboard" => ClipboardPage(),
                "shortcuts" => ShortcutsPage(),
                "network" => NetworkPage(),
                "game" => GamePage(),
                "conversion" => ConversionPage(),
                "service" => ServicePage(),
                _ => OverviewPage(),
            };
            Page.Content = page;
            if (!animate) return;
            Scroller.ScrollToTop();
            if (page is Panel p)
            {
                int i = 0;
                foreach (FrameworkElement c in p.Children) Ui.Rise(c, i++ * 45, 14, 420);
            }
        }

        // -------------------------------------------------------------- building blocks

        private static StackPanel PageRoot(string eyebrow, string title, string sub = null)
        {
            var root = new StackPanel { MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 20) };
            var head = new StackPanel { Margin = new Thickness(0, 4, 0, 22) };
            head.Children.Add(new TextBlock { Text = eyebrow.ToUpperInvariant(), Style = Ui.Style("T.Eyebrow"), FontSize = 12 });
            head.Children.Add(new TextBlock { Text = title, Style = Ui.Style("T.Title"), FontSize = 30, Margin = new Thickness(0, 4, 0, 0) });
            if (sub != null) head.Children.Add(new TextBlock { Text = sub, Style = Ui.Style("T.Body"), Foreground = Ui.Res("B.TextMute"), Margin = new Thickness(0, 6, 0, 0), MaxWidth = 640, HorizontalAlignment = HorizontalAlignment.Left });
            root.Children.Add(head);
            return root;
        }

        private static Border Card(UIElement child, string edge = null, double pad = 18)
        {
            var b = new Border
            {
                Background = Ui.Res("B.Surface"), CornerRadius = new CornerRadius(16), Padding = new Thickness(pad),
                BorderBrush = edge == null ? Ui.Res("B.Line") : new SolidColorBrush((Color)ColorConverter.ConvertFromString(edge)),
                BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 0, 14), Child = child
            };
            b.MouseEnter += (s, e) => b.BorderBrush = Ui.Res("B.LineStrong");
            b.MouseLeave += (s, e) => b.BorderBrush = edge == null ? Ui.Res("B.Line") : new SolidColorBrush((Color)ColorConverter.ConvertFromString(edge));
            return b;
        }

        private static StackPanel CardTitle(string title, string sub = null)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            sp.Children.Add(new TextBlock { Text = title, Style = Ui.Style("T.Title"), FontSize = 16 });
            if (sub != null) sp.Children.Add(new TextBlock { Text = sub, Style = Ui.Style("T.Body"), Foreground = Ui.Res("B.TextMute"), Margin = new Thickness(0, 3, 0, 0) });
            return sp;
        }

        private static FrameworkElement Switch(string label, string sub, bool value, Action<bool> set, string accent = "B.Mint")
        {
            var cb = new CheckBox { Style = Ui.Style("Switch"), IsChecked = value, Tag = Ui.Res(accent), Margin = new Thickness(0, 4, 0, 10) };
            var text = new StackPanel();
            text.Children.Add(new TextBlock { Text = label, Foreground = Ui.Res("B.Text"), FontSize = 14, FontFamily = Ui.Font("F.Body") });
            if (sub != null) text.Children.Add(new TextBlock { Text = sub, Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute"), Margin = new Thickness(0, 2, 0, 0) });
            cb.Content = text;
            cb.Checked += (s, e) => { set(true); Settings.Save(); App.Tray.Refresh(); };
            cb.Unchecked += (s, e) => { set(false); Settings.Save(); App.Tray.Refresh(); };
            return cb;
        }

        private static FrameworkElement Choice<T>(string label, IEnumerable<(string Label, T Value)> options, T current, Action<T> set)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 4, 0, 10) };
            sp.Children.Add(new TextBlock { Text = label, Foreground = Ui.Res("B.Text"), FontSize = 14, FontFamily = Ui.Font("F.Body"), Margin = new Thickness(0, 0, 0, 8) });
            var wrap = new WrapPanel();
            var buttons = new List<(Button B, T V)>();
            void Paint()
            {
                foreach (var (b, v) in buttons)
                {
                    bool on = EqualityComparer<T>.Default.Equals(v, current);
                    b.Background = on ? Ui.Res("B.Mint") : Ui.Res("B.Raised");
                    b.Foreground = on ? Ui.Res("B.MintInk") : Ui.Res("B.TextSoft");
                    b.BorderBrush = on ? Ui.Res("B.Mint") : Ui.Res("B.LineStrong");
                }
            }
            foreach (var (l, v) in options)
            {
                var val = v;
                var b = Ui.Button(l, null, "Btn.Chip");
                b.Click += (s, e) => { current = val; set(val); Settings.Save(); Paint(); };
                buttons.Add((b, v));
                wrap.Children.Add(b);
            }
            Paint();
            sp.Children.Add(wrap);
            return sp;
        }

        private static FrameworkElement Tile(string label, string value, string sub, Brush accent, string edge = null)
        {
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = label, Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute") });
            sp.Children.Add(new TextBlock { Text = value, Style = Ui.Style("T.Title"), FontSize = 22, Foreground = accent, Margin = new Thickness(0, 6, 0, 4) });
            sp.Children.Add(Ui.Mono(sub, Ui.Res("B.TextMute")));
            var c = Card(sp, edge, 16);
            c.Margin = new Thickness(0, 0, 14, 14);
            return c;
        }

        private static TextBox Input(string text, double width = double.NaN, string hint = null)
        {
            var t = new TextBox { Style = Ui.Style("Input"), Text = text ?? "", Margin = new Thickness(0, 0, 8, 8), ToolTip = hint };
            if (!double.IsNaN(width)) t.Width = width;
            return t;
        }

        // -------------------------------------------------------------- overview

        private FrameworkElement OverviewPage()
        {
            var net = App.Net.Current;
            bool online = net == null || net.Online;
            var root = PageRoot("Overview", online ? (App.ShouldSuppress ? "Quiet while you play" : "Everything is quiet") : (net.Headline ?? "Offline"));

            var tiles = new UniformGrid { Columns = 3, Margin = new Thickness(0, 0, -14, 0) };
            tiles.Children.Add(Tile("Internet", online ? "Online" : "Offline",
                online && net != null ? $"gateway {(net.GatewayMs >= 0 ? net.GatewayMs + " ms" : "silent")} · dns {(net.DnsMs >= 0 ? net.DnsMs + " ms" : "?")}" : DnsSwitcher.ActiveProfileName() ?? "",
                online ? Ui.Res("B.Mint") : Ui.Res("B.Amber"), online ? null : "#FF4A3A22"));
            var h = App.History;
            tiles.Children.Add(Tile("Clipboard today", $"{h.Today} copies", $"{h.TextCount} text · {h.LinkCount} links · {h.FileCount} files · {h.MediaCount} media", Ui.Res("B.Text")));
            var s = Settings.Current;
            tiles.Children.Add(Tile("Game mode", !s.GameModeEnabled ? "Off" : App.Games.IsGaming ? App.Games.ActiveGame : "Standing by",
                $"{s.GameProcesses.Count} games watched · fullscreen {(s.GameModeFullscreen ? "on" : "off")}", Ui.Res("B.Violet"), "#FF3A2F5A"));
            root.Children.Add(tiles);

            // quick command cheat sheet
            var cheat = new StackPanel();
            cheat.Children.Add(CardTitle("Quick commands", "Copy a path with a command on the end. The command runs the moment it lands on the clipboard."));
            var g = new WrapPanel();
            foreach (var (cmd, what) in new[]
            {
                (":mp4", "video or audio to MP4"), (":gif", "video to animated GIF"), (":mp3", "extract or convert audio"),
                (":webp", "image to WebP"), (":png", "image or first frame to PNG"), (":push", "pre-dew and push the repo"),
            }.Concat(s.Shortcuts.Select(x => (":" + x.Name, string.IsNullOrWhiteSpace(x.Description) ? "your shortcut" : x.Description))))
            {
                var item = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 18, 10) };
                item.Children.Add(new Border { Background = Ui.Res("B.MintWash"), CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 3, 8, 4), Child = Ui.Mono(cmd, Ui.Res("B.Mint")) });
                item.Children.Add(new TextBlock { Text = what, Style = Ui.Style("T.Body"), Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
                g.Children.Add(item);
            }
            cheat.Children.Add(g);
            cheat.Children.Add(Ui.Mono(@"Example: D:\Footage\clip.mov:mp4   or   ""C:\Code\my-repo"":push", Ui.Res("B.TextMute")));
            root.Children.Add(Card(cheat));

            if (App.History.Items.Count > 0)
            {
                var recent = new StackPanel();
                recent.Children.Add(CardTitle("Recently copied"));
                foreach (var it in App.History.Items.Take(5)) recent.Children.Add(HistoryRow(it));
                root.Children.Add(Card(recent));
            }
            return root;
        }

        // -------------------------------------------------------------- history

        private FrameworkElement HistoryPage()
        {
            var root = PageRoot("Clipboard", "History", $"The last {Settings.Current.HistorySize} copies, kept in memory only. Quitting the app forgets them.");
            if (App.History.Items.Count == 0)
            {
                root.Children.Add(Card(Ui.Text("Nothing yet. Copy something and it shows up here.")));
                return root;
            }
            var list = new StackPanel();
            foreach (var it in App.History.Items) list.Children.Add(HistoryRow(it));
            root.Children.Add(Card(list, pad: 10));
            return root;
        }

        private FrameworkElement HistoryRow(ClipItem it)
        {
            var g = new Grid { Margin = new Thickness(6, 4, 6, 4) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var icon = it.Kind switch
            {
                ClipKind.Url => Icons.Link, ClipKind.Files => Icons.File, ClipKind.Image => Icons.Image, ClipKind.Audio => Icons.Audio,
                ClipKind.Json => Icons.Braces, ClipKind.Color => Icons.Palette, ClipKind.Email => Icons.Mail, ClipKind.Command => Icons.Bolt, _ => Icons.Text
            };
            g.Children.Add(Ui.IconChip(icon, Ui.Res("B.Mint"), Ui.Res("B.MintWash")));
            var mid = new StackPanel { Margin = new Thickness(12, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
            var prev = (it.Preview ?? "").Replace("\r", " ").Replace("\n", " ");
            mid.Children.Add(new TextBlock { Text = prev.Length > 140 ? prev[..140] + "…" : prev, Foreground = Ui.Res("B.Text"), FontSize = 13, FontFamily = it.Kind == ClipKind.Files ? Ui.Font("F.Mono") : Ui.Font("F.Body"), TextTrimming = TextTrimming.CharacterEllipsis });
            mid.Children.Add(Ui.Mono($"{it.Kind.ToString().ToLowerInvariant()} · {it.At:HH:mm:ss}", Ui.Res("B.TextMute"), 11));
            Grid.SetColumn(mid, 1); g.Children.Add(mid);
            var btns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var again = Ui.Button("Copy again", () => CopyAgain(it), "Btn.Ghost");
            again.Margin = new Thickness(0, 0, 8, 0);
            var acts = Ui.Button("Actions", () => App.Toasts.ShowClip(it), "Btn.Ghost");
            acts.Margin = new Thickness(0);
            btns.Children.Add(again); btns.Children.Add(acts);
            Grid.SetColumn(btns, 2); g.Children.Add(btns);
            var row = new Border { CornerRadius = new CornerRadius(12), Padding = new Thickness(6), Child = g, Background = Brushes.Transparent };
            row.MouseEnter += (s, e) => row.Background = Ui.Res("B.Raised");
            row.MouseLeave += (s, e) => row.Background = Brushes.Transparent;
            return row;
        }

        private static void CopyAgain(ClipItem it)
        {
            switch (it.Kind)
            {
                case ClipKind.Files when !it.IsPathText: ClipboardWatcher.SetFiles(it.Files); break;
                case ClipKind.Image when it.Image != null: ClipboardWatcher.SetImage(it.Image); break;
                default: if (it.Text != null) ClipboardWatcher.SetText(it.Text); else if (it.Files.Length > 0) ClipboardWatcher.SetText(string.Join(Environment.NewLine, it.Files)); break;
            }
        }

        // -------------------------------------------------------------- clipboard settings

        private FrameworkElement ClipboardPage()
        {
            var s = Settings.Current;
            var root = PageRoot("Clipboard", "Toasts", "What shows up in the corner when the clipboard changes.");
            var a = new StackPanel();
            a.Children.Add(Switch("Clipboard toasts", "Show a toast for every copy.", s.ClipboardEnabled, v => s.ClipboardEnabled = v));
            a.Children.Add(Switch("Keep open on hover", "Hovering holds the toast and reveals the actions.", s.KeepOpenOnHover, v => s.KeepOpenOnHover = v));
            a.Children.Add(Switch("Render Markdown", "Headings, lists, code and links render in the preview.", s.RenderMarkdown, v => s.RenderMarkdown = v));
            a.Children.Add(Switch("Skip password managers", "Copies marked private by the source app never pop up or reach history.", s.IgnoreSensitiveSources, v => s.IgnoreSensitiveSources = v));
            a.Children.Add(Switch("Quick commands", "Run :mp4, :push and your own shortcuts when they land on the clipboard.", s.QuickCommandsEnabled, v => s.QuickCommandsEnabled = v));
            a.Children.Add(Choice("Toast duration", new[] { ("2 s", 2.0), ("3 s", 3.0), ("5 s", 5.0), ("8 s", 8.0) }, s.ToastSeconds, v => s.ToastSeconds = v));
            a.Children.Add(Choice("History size", new[] { ("20", 20), ("50", 50), ("100", 100) }, s.HistorySize, v => s.HistorySize = v));
            root.Children.Add(Card(a));

            var m = new StackPanel();
            m.Children.Add(CardTitle("Media folder", "Saved clipboard images, audio and text go here."));
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var box = Input(s.ResolvedMediaFolder, 460);
            row.Children.Add(box);
            row.Children.Add(Ui.Button("Save", () => { s.MediaSaveFolder = box.Text.Trim(); Settings.Save(); }, "Btn.Accent"));
            row.Children.Add(Ui.Button("Open", () => ActionProvider.Shell(s.ResolvedMediaFolder)));
            m.Children.Add(row);
            root.Children.Add(Card(m));
            return root;
        }

        // -------------------------------------------------------------- shortcuts and script editor

        private FrameworkElement ShortcutsPage()
        {
            var s = Settings.Current;
            var root = PageRoot("Shortcuts", "Shortcuts and scripts",
                "Make your own :commands. Copy any path or text with :name on the end and your batch or PowerShell script runs with it.");

            // pre-dew message for :push
            var push = new StackPanel();
            push.Children.Add(CardTitle(":push message", "Built in. Stages everything, commits with this message and pushes the current branch."));
            var pr = new StackPanel { Orientation = Orientation.Horizontal };
            pr.Children.Add(new Border { Background = Ui.Res("B.Sunken"), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 0, 10, 8),
                Child = Ui.Mono(string.IsNullOrWhiteSpace(s.CommitTemplate) ? "not set yet: the first :push will ask" : s.CommitTemplate, Ui.Res(string.IsNullOrWhiteSpace(s.CommitTemplate) ? "B.Amber" : "B.Text"), 13) });
            pr.Children.Add(Ui.Button("Edit", () =>
            {
                var d = new CommitTemplateWindow(continueAfter: false);
                d.Closed += (o, e) => Navigate("shortcuts", animate: false);
                d.Show();
            }));
            push.Children.Add(pr);
            root.Children.Add(Card(push));

            // list
            var list = new StackPanel();
            var head = new Grid();
            head.Children.Add(CardTitle("Your shortcuts", s.Shortcuts.Count == 0 ? "None yet." : null));
            var add = Ui.Button("New shortcut", NewShortcut, "Btn.Accent", Icons.Plus);
            add.HorizontalAlignment = HorizontalAlignment.Right; add.VerticalAlignment = VerticalAlignment.Top;
            head.Children.Add(add);
            list.Children.Add(head);
            var wrap = new WrapPanel();
            foreach (var sc in s.Shortcuts)
            {
                var x = sc;
                var chip = Ui.Button(":" + sc.Name, () => { _editing = x; Navigate("shortcuts", animate: false); }, "Btn.Chip");
                if (_editing != null && _editing.Name == sc.Name) { chip.Background = Ui.Res("B.Mint"); chip.Foreground = Ui.Res("B.MintInk"); }
                chip.ToolTip = $"{sc.Shell} · {sc.Description}";
                wrap.Children.Add(chip);
            }
            list.Children.Add(wrap);
            root.Children.Add(Card(list));

            if (_editing == null && s.Shortcuts.Count > 0) _editing = s.Shortcuts[0];
            if (_editing != null) root.Children.Add(Editor(_editing));
            return root;
        }

        private void NewShortcut()
        {
            var s = Settings.Current;
            int n = 1;
            string name;
            do { name = "script" + n++; } while (Shortcuts.Find(name) != null);
            var sc = new Shortcut { Name = name, Shell = ScriptShell.PowerShell, Description = "" };
            s.Shortcuts.Add(sc);
            Shortcuts.WriteScript(sc, Shortcuts.Template(sc.Shell));
            Settings.Save();
            _editing = sc;
            Navigate("shortcuts", animate: false);
        }

        private FrameworkElement Editor(Shortcut sc)
        {
            var root = new StackPanel();
            var top = new WrapPanel();
            var nameBox = Input(sc.Name, 180, "Name: letters, digits, - and _");
            var descBox = Input(sc.Description, 340, "What it does (shows as the chip tooltip)");
            top.Children.Add(new StackPanel { Children = { Ui.Mono("NAME", Ui.Res("B.TextMute"), 11), nameBox } });
            top.Children.Add(new StackPanel { Children = { Ui.Mono("DESCRIPTION", Ui.Res("B.TextMute"), 11), descBox } });
            root.Children.Add(top);

            var shell = sc.Shell;
            root.Children.Add(Choice("Runs with", new[] { ("PowerShell", ScriptShell.PowerShell), ("Batch (.bat)", ScriptShell.Batch) }, shell, v => shell = v));
            bool showWin = sc.ShowWindow, elev = sc.RunElevated;
            var flags = new WrapPanel();
            var sw = Switch("Show a console window", null, showWin, v => showWin = v); ((FrameworkElement)sw).Margin = new Thickness(0, 0, 28, 8);
            flags.Children.Add(sw);
            flags.Children.Add(Switch("Run as administrator", "Prompts once per run unless the service already has admin rights.", elev, v => elev = v, "B.Amber"));
            root.Children.Add(flags);

            // editor with a line-number gutter
            var code = new TextBox
            {
                Text = Shortcuts.ReadScript(sc), AcceptsReturn = true, AcceptsTab = true, FontFamily = Ui.Font("F.Mono"), FontSize = 13,
                Foreground = Ui.Res("B.Text"), Background = Brushes.Transparent, BorderThickness = new Thickness(0), CaretBrush = Ui.Res("B.Mint"),
                SelectionBrush = Ui.Res("B.Mint"), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                TextWrapping = TextWrapping.NoWrap, MinHeight = 260, MaxHeight = 420, Padding = new Thickness(4, 10, 10, 10)
            };
            var gutter = new TextBlock { FontFamily = Ui.Font("F.Mono"), FontSize = 13, Foreground = Ui.Res("B.TextMute"), TextAlignment = TextAlignment.Right, Padding = new Thickness(10, 10, 8, 10), MinWidth = 40 };
            var gutterScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, Content = gutter, IsHitTestVisible = false };
            void Lines() => gutter.Text = string.Join("\n", Enumerable.Range(1, Math.Max(1, code.LineCount < 1 ? code.Text.Split('\n').Length : code.LineCount)));
            code.TextChanged += (s, e) => Lines();
            code.Loaded += (s, e) => Lines();
            code.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((s, e) => gutterScroll.ScrollToVerticalOffset(e.VerticalOffset)));
            var edGrid = new Grid();
            edGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            edGrid.ColumnDefinitions.Add(new ColumnDefinition());
            edGrid.Children.Add(new Border { Background = Ui.Res("B.Ground"), CornerRadius = new CornerRadius(12, 0, 0, 12), Child = gutterScroll });
            Grid.SetColumn(code, 1);
            edGrid.Children.Add(code);
            var editorFrame = new Border { Background = Ui.Res("B.Sunken"), CornerRadius = new CornerRadius(12), BorderBrush = Ui.Res("B.LineStrong"), BorderThickness = new Thickness(1), Child = edGrid, Margin = new Thickness(0, 4, 0, 10) };
            code.GotKeyboardFocus += (s, e) => editorFrame.BorderBrush = Ui.Res("B.Mint");
            code.LostKeyboardFocus += (s, e) => editorFrame.BorderBrush = Ui.Res("B.LineStrong");
            root.Children.Add(editorFrame);

            // snippet inserts
            var snippets = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
            snippets.Children.Add(Ui.Mono("INSERT", Ui.Res("B.TextMute"), 11));
            void Snip(string label, string ps, string bat)
            {
                var b = Ui.Button(label, () =>
                {
                    var text = shell == ScriptShell.Batch ? bat : ps;
                    var at = code.CaretIndex;
                    code.Text = code.Text.Insert(at, text);
                    code.CaretIndex = at + text.Length;
                    code.Focus();
                }, "Btn.Chip");
                b.Height = 26;
                snippets.Children.Add(b);
            }
            Snip("input path", "$Target", "%~1");
            Snip("folder of input", "(Split-Path -Parent $Target)", "%~dp1");
            Snip("name only", "[IO.Path]::GetFileNameWithoutExtension($Target)", "%~n1");
            Snip("today", "(Get-Date -Format 'yyyy-MM-dd')", "%date%");
            Snip("open in VS Code", "code \"$Target\"\r\n", "code \"%~1\"\r\n");
            Snip("zip next to it", "Compress-Archive -Path \"$Target\" -DestinationPath \"$Target.zip\" -Force\r\n", "tar -a -c -f \"%~1.zip\" \"%~1\"\r\n");
            Snip("copy to clipboard", "Set-Clipboard -Value \"$Target\"\r\n", "echo %~1| clip\r\n");
            root.Children.Add(snippets);

            // test run
            var test = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            var input = Input("", 380, "Test input (a path or any text)");
            var output = new TextBlock { Style = Ui.Style("T.Mono"), Foreground = Ui.Res("B.TextSoft"), Margin = new Thickness(0, 6, 0, 0) };
            var status = Ui.Mono("", Ui.Res("B.TextMute"), 11);

            Shortcut Commit()
            {
                var newName = nameBox.Text.Trim();
                if (!Shortcuts.IsValidName(newName) || Shortcuts.Reserved.Contains(newName.ToLowerInvariant()) || Formats.IsKnown(newName) && false)
                { status.Text = "Name must start with a letter: letters, digits, - and _ only, not 'push'."; status.Foreground = Ui.Res("B.Amber"); return null; }
                var clash = Shortcuts.Find(newName);
                if (clash != null && clash != sc) { status.Text = $":{newName} already exists."; status.Foreground = Ui.Res("B.Amber"); return null; }
                var oldPath = sc.ScriptPath;
                sc.Name = newName; sc.Description = descBox.Text.Trim(); sc.Shell = shell; sc.ShowWindow = showWin; sc.RunElevated = elev;
                if (!string.Equals(oldPath, sc.ScriptPath, StringComparison.OrdinalIgnoreCase)) try { File.Delete(oldPath); } catch { }
                Shortcuts.WriteScript(sc, code.Text);
                Settings.Save();
                status.Text = $"Saved. Copy something with :{sc.Name} on the end to run it."; status.Foreground = Ui.Res("B.Mint");
                return sc;
            }

            test.Children.Add(input);
            test.Children.Add(Ui.Button("Save and run", async () =>
            {
                if (Commit() == null) return;
                output.Text = "running…";
                var r = await Shortcuts.RunAsync(sc, input.Text);
                output.Text = $"exit {r.ExitCode} · {r.Took.TotalSeconds:0.00} s\n{ActionProvider.Tail(r.Output, 14)}";
            }, "Btn.Ghost", Icons.Play));
            root.Children.Add(test);

            var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            bar.Children.Add(Ui.Button("Save", () => { if (Commit() != null) Navigate("shortcuts", animate: false); }, "Btn.Accent"));
            bar.Children.Add(Ui.Button("Open in Notepad", () => { Commit(); System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("notepad.exe", $"\"{sc.ScriptPath}\"") { UseShellExecute = true }); }));
            bar.Children.Add(Ui.Button("Open scripts folder", () => ActionProvider.Shell(Shortcuts.Folder)));
            bar.Children.Add(Ui.Button("Delete", () =>
            {
                try { File.Delete(sc.ScriptPath); } catch { }
                Settings.Current.Shortcuts.Remove(sc);
                Settings.Save();
                _editing = null;
                Navigate("shortcuts", animate: false);
            }, "Btn.Danger"));
            root.Children.Add(bar);
            root.Children.Add(status);
            root.Children.Add(output);

            var card = new StackPanel();
            card.Children.Add(CardTitle("Edit :" + sc.Name));
            card.Children.Add(root);
            return Card(card);
        }

        // -------------------------------------------------------------- network and DNS

        private FrameworkElement NetworkPage()
        {
            var s = Settings.Current;
            var root = PageRoot("Network", "Network and DNS", "The offline dialog stays on top until the line comes back or you dismiss it.");
            var a = new StackPanel();
            a.Children.Add(Switch("Network alerts", "Show the dialog when the internet drops.", s.NetworkEnabled, v => s.NetworkEnabled = v));
            a.Children.Add(Choice("Check every", new[] { ("2 s", 2), ("3 s", 3), ("5 s", 5), ("10 s", 10) }, s.ProbeIntervalSeconds, v => s.ProbeIntervalSeconds = v));
            a.Children.Add(Choice("Alert after", new[] { ("1 failed check", 1), ("2 in a row", 2), ("3 in a row", 3) }, s.FailuresBeforeAlert, v => s.FailuresBeforeAlert = v));
            var tools = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            tools.Children.Add(Ui.Button("Test now", () => App.Net.ProbeNow(), "Btn.Ghost", Icons.Wifi));
            tools.Children.Add(Ui.Button("Release & renew IP", async () =>
            {
                var rc = await Elevated.RunAsync("ipconfig /release", "ipconfig /renew", "ipconfig /flushdns");
                App.Toasts.ShowNotice(rc == 0 ? "IP address renewed" : rc == -2 ? "Cancelled" : "ipconfig finished", rc == 0 ? "Released, renewed and flushed DNS." : $"Exit code {rc}.", rc == 0 ? App.Toasts.Mint : App.Toasts.Amber, 4, icon: Icons.Bolt);
                App.Net.ProbeNow();
            }, "Btn.Ghost", Icons.Bolt));
            a.Children.Add(tools);
            root.Children.Add(Card(a));

            // DNS profiles
            var d = new StackPanel();
            var active = DnsSwitcher.ActiveProfileName();
            d.Children.Add(CardTitle("DNS profiles", $"Now using {active ?? "unknown"} ({DnsSwitcher.CurrentServers()}). Switching needs admin rights: one prompt, or none when the service runs elevated."));
            foreach (var p in s.DnsProfiles.ToList())
            {
                var prof = p;
                var g = new Grid { Margin = new Thickness(0, 0, 0, 8) };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
                g.ColumnDefinitions.Add(new ColumnDefinition());
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var nm = new TextBlock { Text = p.Name, Foreground = p.Name == active ? Ui.Res("B.Mint") : Ui.Res("B.Text"), FontSize = 14, FontFamily = Ui.Font("F.Body"), VerticalAlignment = VerticalAlignment.Center, FontWeight = p.Name == active ? FontWeights.SemiBold : FontWeights.Normal };
                g.Children.Add(nm);
                var addr = Ui.Mono(p.ToString(), Ui.Res("B.TextSoft"));
                addr.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(addr, 1); g.Children.Add(addr);
                var btns = new StackPanel { Orientation = Orientation.Horizontal };
                var use = Ui.Button(p.Name == active ? "In use" : "Use", async () =>
                {
                    var rc = await DnsSwitcher.ApplyAsync(prof);
                    if (rc == 0) App.Toasts.ShowNotice($"DNS is now {prof.Name}", prof.ToString(), App.Toasts.Mint, 4, icon: Icons.Globe);
                    App.Net.ProbeNow();
                    Navigate("network", animate: false);
                }, p.Name == active ? "Btn.Ghost" : "Btn.Accent");
                use.Margin = new Thickness(0, 0, 8, 0);
                btns.Children.Add(use);
                if (!p.IsAutomatic)
                {
                    var del = Ui.Button("Remove", () => { s.DnsProfiles.Remove(prof); Settings.Save(); Navigate("network", animate: false); });
                    del.Margin = new Thickness(0);
                    btns.Children.Add(del);
                }
                Grid.SetColumn(btns, 2); g.Children.Add(btns);
                d.Children.Add(g);
            }
            var addRow = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            var n = Input("", 150, "Name"); var p1 = Input("", 160, "Primary, e.g. 1.1.1.1"); var p2 = Input("", 160, "Secondary (optional)");
            addRow.Children.Add(n); addRow.Children.Add(p1); addRow.Children.Add(p2);
            var msg = Ui.Mono("", Ui.Res("B.Amber"), 11);
            addRow.Children.Add(Ui.Button("Save profile", () =>
            {
                bool Ip(string x) => System.Net.IPAddress.TryParse(x.Trim(), out var ip) && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;
                if (string.IsNullOrWhiteSpace(n.Text) || !Ip(p1.Text) || (p2.Text.Trim().Length > 0 && !Ip(p2.Text)))
                { msg.Text = "Give it a name and valid IPv4 addresses."; return; }
                s.DnsProfiles.RemoveAll(x => x.Name.Equals(n.Text.Trim(), StringComparison.OrdinalIgnoreCase));
                s.DnsProfiles.Add(new DnsProfile { Name = n.Text.Trim(), Primary = p1.Text.Trim(), Secondary = p2.Text.Trim() });
                Settings.Save();
                Navigate("network", animate: false);
            }, "Btn.Ghost", Icons.Plus));
            d.Children.Add(addRow);
            d.Children.Add(msg);
            root.Children.Add(Card(d));
            return root;
        }

        // -------------------------------------------------------------- game mode

        private FrameworkElement GamePage()
        {
            var s = Settings.Current;
            var root = PageRoot("Game mode", "Suppress popups while gaming",
                "On by default. Toasts are held back while a listed game runs. An outage still going when you stop playing shows then. Quick commands still run.");
            var a = new StackPanel();
            a.Children.Add(Switch("Suppress while gaming", App.Games.IsGaming ? "Active now: " + App.Games.ActiveGame : "Standing by", s.GameModeEnabled, v => s.GameModeEnabled = v, "B.Violet"));
            a.Children.Add(Switch("Any fullscreen app counts", "Exclusive fullscreen games, fullscreen video and presentations.", s.GameModeFullscreen, v => s.GameModeFullscreen = v, "B.Violet"));
            root.Children.Add(Card(a, "#FF3A2F5A"));

            var g = new StackPanel();
            g.Children.Add(CardTitle("Watched games", "Process names without .exe. Use process:Title to also match the window title (Minecraft Java runs as javaw)."));
            var wrap = new WrapPanel();
            foreach (var p in s.GameProcesses.ToList())
            {
                var proc = p;
                var chip = new Border { Height = 32, CornerRadius = new CornerRadius(16), Background = Ui.Res("B.VioletWash"), BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x2F, 0x5A)), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(12, 0, 4, 0) };
                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                sp.Children.Add(new TextBlock { Text = proc, Foreground = new SolidColorBrush(Color.FromRgb(0xD8, 0xCC, 0xFF)), FontSize = 12, FontFamily = Ui.Font("F.Mono"), VerticalAlignment = VerticalAlignment.Center });
                var x = new Button { Style = Ui.Style("Btn.Row"), Width = 24, Height = 24, Padding = new Thickness(0), Margin = new Thickness(6, 0, 0, 0), Content = Ui.Icon(Icons.Close, Ui.Res("B.Violet"), 9, 2.5), ToolTip = "Remove" };
                x.Click += (o, e) => { s.GameProcesses.Remove(proc); Settings.Save(); Navigate("game", animate: false); };
                sp.Children.Add(x);
                chip.Child = sp;
                wrap.Children.Add(chip);
            }
            g.Children.Add(wrap);
            var add = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            var box = Input("", 260, "e.g. Valheim or javaw:Minecraft");
            add.Children.Add(box);
            add.Children.Add(Ui.Button("Add game", () =>
            {
                var v = box.Text.Trim();
                if (v.Length == 0) return;
                if (!s.GameProcesses.Contains(v, StringComparer.OrdinalIgnoreCase)) s.GameProcesses.Add(v);
                Settings.Save();
                Navigate("game", animate: false);
            }, "Btn.Ghost", Icons.Plus));
            add.Children.Add(Ui.Button("Restore defaults", () => { s.GameProcesses = new List<string>(Settings.GameDefaults); Settings.Save(); Navigate("game", animate: false); }));
            g.Children.Add(add);
            root.Children.Add(Card(g));
            return root;
        }

        // -------------------------------------------------------------- conversion

        private FrameworkElement ConversionPage()
        {
            var s = Settings.Current;
            var root = PageRoot("Conversion", "Quick conversion", "Copy a file path, add :format on the end, and the converted file appears next to it with the same name.");
            var ff = Converter.FindFfmpeg();
            var a = new StackPanel();
            a.Children.Add(CardTitle("FFmpeg", ff == null ? "Not found. Video and audio conversion needs it." : ff));
            var row = new WrapPanel();
            if (ff == null)
                row.Children.Add(Ui.Button("Install with winget", () => App.Toasts.ShowTask("Installing FFmpeg", "winget · Gyan.FFmpeg", async () =>
                {
                    var p = await Converter.InstallFfmpegAsync();
                    Dispatcher.Invoke(() => Navigate("conversion", animate: false));
                    return (p != null, p != null ? "FFmpeg installed" : "Install did not finish", p ?? "Try winget install Gyan.FFmpeg in a terminal.");
                }), "Btn.Accent", Icons.Bolt));
            row.Children.Add(Ui.Button("Pick ffmpeg.exe", () =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "ffmpeg.exe|ffmpeg.exe", Title = "Find ffmpeg.exe" };
                if (dlg.ShowDialog(this) == true) { s.FfmpegPath = dlg.FileName; Settings.Save(); Navigate("conversion", animate: false); }
            }));
            a.Children.Add(row);
            root.Children.Add(Card(a, ff == null ? "#FF4A3A22" : null));

            var f = new StackPanel();
            f.Children.Add(CardTitle("Formats"));
            foreach (var group in Formats.All.Where(x => x.Id != "jpeg").GroupBy(x => x.Kind))
            {
                f.Children.Add(Ui.Mono(group.Key.ToString().ToUpperInvariant(), Ui.Res("B.TextMute"), 11));
                var wrap = new WrapPanel { Margin = new Thickness(0, 6, 0, 10) };
                foreach (var spec in group)
                {
                    var it = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 16, 8) };
                    it.Children.Add(new Border { Background = Ui.Res("B.MintWash"), CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 3, 8, 4), Child = Ui.Mono(":" + spec.Id, Ui.Res("B.Mint")) });
                    it.Children.Add(new TextBlock { Text = spec.Label, Style = Ui.Style("T.Body"), Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
                    wrap.Children.Add(it);
                }
                f.Children.Add(wrap);
            }
            f.Children.Add(Ui.Text("An existing file is never overwritten: the new one gets a (1), (2) suffix."));
            root.Children.Add(Card(f));
            return root;
        }

        // -------------------------------------------------------------- service

        private FrameworkElement ServicePage()
        {
            var s = Settings.Current;
            var root = PageRoot("Service", "Background service", "Runs at sign-in under a small supervisor that restarts it if it ever crashes.");
            var a = new StackPanel();
            a.Children.Add(Switch("Start with Windows", "Launch at sign-in.", s.StartWithWindows, v => { s.StartWithWindows = v; Settings.Save(); ServiceHost.ApplyAutostart(); }));
            var adm = new CheckBox { Style = Ui.Style("Switch"), IsChecked = s.RunElevated, Tag = Ui.Res("B.Amber"), Margin = new Thickness(0, 4, 0, 10) };
            var admText = new StackPanel();
            admText.Children.Add(new TextBlock { Text = "Administrator rights", Foreground = Ui.Res("B.Text"), FontSize = 14, FontFamily = Ui.Font("F.Body") });
            admText.Children.Add(new TextBlock { Text = "Off by default. Turning it on asks once, then the service starts elevated at every sign-in until you turn it off here. Lets DNS switching and release and renew run without prompts.", Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute"), Margin = new Thickness(0, 2, 0, 0) });
            adm.Content = admText;
            RoutedEventHandler flip = null;
            flip = async (o, e) =>
            {
                bool want = adm.IsChecked == true;
                var ok = await System.Threading.Tasks.Task.Run(() => ServiceHost.RequestElevation(want));
                if (!ok)
                {
                    adm.Checked -= flip; adm.Unchecked -= flip;
                    adm.IsChecked = !want;
                    adm.Checked += flip; adm.Unchecked += flip;
                    App.Toasts.ShowNotice("Administrator rights unchanged", "The prompt was declined or failed.", App.Toasts.Amber, 4, icon: Icons.Shield);
                    return;
                }
                Settings.Load();
                ServiceHost.RelaunchForElevationChange();
                App.Current.Quit(ServiceHost.ExitQuit);
            };
            adm.Checked += flip; adm.Unchecked += flip;
            a.Children.Add(adm);
            a.Children.Add(Ui.Mono($"running {(ServiceHost.IsElevated ? "elevated" : "as a standard user")} from {ServiceHost.ServiceExe}", Ui.Res("B.TextMute"), 11));
            root.Children.Add(Card(a));

            var b = new WrapPanel();
            b.Children.Add(Ui.Button("Restart service", () => App.Current.Quit(ServiceHost.ExitRestart), "Btn.Ghost", Icons.Restart));
            b.Children.Add(Ui.Button("Open log folder", () => ActionProvider.Shell(Log.Folder), "Btn.Ghost", Icons.Lines));
            b.Children.Add(Ui.Button("Open settings file", () => ActionProvider.Reveal(Path.Combine(Settings.DataDir, "settings.json")), "Btn.Ghost", Icons.File));
            b.Children.Add(Ui.Button("Quit until next sign-in", () => App.Current.Quit(ServiceHost.ExitQuit), "Btn.Ghost", Icons.Power));
            root.Children.Add(Card(b));

            var u = new StackPanel();
            u.Children.Add(CardTitle("Uninstall", "Stops the service, removes it from start-up and deletes the program."));
            u.Children.Add(Ui.Button("Uninstall service…", () =>
            {
                var dlg = new ConfirmWindow("Uninstall Monitoring Tools?",
                    "This stops the background service, removes it from start-up and deletes the program.",
                    "Uninstall", "Also remove settings, logs and scripts", danger: true);
                dlg.Confirmed += removeData =>
                {
                    ServiceHost.Uninstall(removeData);
                    App.Current.Quit(ServiceHost.ExitQuit);
                };
                dlg.Show();
            }, "Btn.Danger", Icons.Trash));
            root.Children.Add(Card(u, "#FF4A2525"));
            return root;
        }

        // -------------------------------------------------------------- chrome

        private void OnMin(object s, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void OnMax(object s, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        private void OnClose(object s, RoutedEventArgs e) => Close();
    }
}
