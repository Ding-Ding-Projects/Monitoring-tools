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
using Line = System.Windows.Shapes.Line;
using Polyline = System.Windows.Shapes.Polyline;
using Rectangle = System.Windows.Shapes.Rectangle;
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
            ("activity", "Activity", Icons.Lines),
            ("history", "Clipboard history", Icons.Clipboard),
            ("clipboard", "Clipboard", Icons.Text),
            ("shortcuts", "Shortcuts and scripts", Icons.Code),
            ("network", "Network and DNS", Icons.Wifi),
            ("game", "Game mode", Icons.Game),
            ("conversion", "Conversion", Icons.Bolt),
            ("service", "Service", Icons.Shield),
            ("about", "About and updates", Icons.Globe),
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
            Activity.Changed += () => { if (_current == "activity" || _current == "overview") Navigate(_current, animate: false); };
            App.Net.Sampled += () => { if (_current == "overview" && IsActive) Navigate(_current, animate: false); };
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
                "activity" => ActivityPage(),
                "about" => AboutPage(),
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
            root.Children.Add(LiveCard());

            var lists = new UniformGrid { Columns = 2, Margin = new Thickness(0, 0, -14, 0) };
            lists.Children.Add(Wrap(OutagesCard(4)));
            lists.Children.Add(Wrap(ActivityCard(5, null, "Latest activity")));
            root.Children.Add(lists);

            var cheat = new StackPanel();
            cheat.Children.Add(CardTitle("Quick commands", "Copy a path with a command on the end. The command runs the moment it lands on the clipboard."));
            var g = new WrapPanel();
            foreach (var (cmd, what) in new[]
            {
                (":mp4", "video or audio to MP4"), (":gif", "video to animated GIF"), (":mp3", "extract or convert audio"),
                (":webp", "image to WebP"), (":png", "image or first frame to PNG"), (":push", "commit and push the repo"),
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

            // commit message for :push
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
            root.Children.Add(PublicIpCard());
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
            root.Children.Add(Playground());
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

        // -------------------------------------------------------------- live data cards

        private static FrameworkElement Wrap(FrameworkElement card)
        {
            card.Margin = new Thickness(0, 0, 14, 14);
            return card;
        }

        private FrameworkElement LiveCard()
        {
            var net = App.Net;
            List<(DateTime At, long Gateway, long Dns, bool Online)> samples;
            lock (net.Samples) samples = net.Samples.ToList();
            var up = DateTime.Now - net.StartedAt;
            var gws = samples.Where(x => x.Gateway >= 0).Select(x => x.Gateway).ToList();
            var dnss = samples.Where(x => x.Dns >= 0).Select(x => x.Dns).ToList();

            var sp = new StackPanel();
            var head = new Grid();
            head.Children.Add(CardTitle("Live", "Every probe of the last few minutes. Gaps are failed checks."));
            var legend = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
            legend.Children.Add(Dot(Ui.Res("B.Mint"))); legend.Children.Add(Ui.Mono("gateway", Ui.Res("B.TextMute"), 11));
            legend.Children.Add(new Border { Width = 14 });
            legend.Children.Add(Dot(new SolidColorBrush(Color.FromRgb(0x8A, 0xB4, 0xFF)))); legend.Children.Add(Ui.Mono("dns", Ui.Res("B.TextMute"), 11));
            head.Children.Add(legend);
            sp.Children.Add(head);

            var stats = new UniformGrid { Columns = 5, Margin = new Thickness(0, 0, 0, 14) };
            stats.Children.Add(Stat("Uptime", up.TotalHours >= 1 ? $"{(int)up.TotalHours} h {up.Minutes} m" : $"{up.Minutes} m {up.Seconds:00} s"));
            stats.Children.Add(Stat("Probes", net.ProbeCount > 0 ? net.ProbeCount.ToString("N0") : samples.Count.ToString("N0")));
            stats.Children.Add(Stat("Copies today", App.History.Today.ToString("N0")));
            stats.Children.Add(Stat("Gateway avg", gws.Count > 0 ? $"{gws.Average():0} ms" : "–"));
            stats.Children.Add(Stat("DNS avg", dnss.Count > 0 ? $"{dnss.Average():0} ms" : "–"));
            sp.Children.Add(stats);
            sp.Children.Add(Chart(samples));
            return Card(sp);
        }

        private static FrameworkElement Dot(Brush b) => new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = b, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };

        private static FrameworkElement Stat(string label, string value)
        {
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = label, Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute") });
            sp.Children.Add(new TextBlock { Text = value, Style = Ui.Style("T.Title"), FontSize = 18, Margin = new Thickness(0, 4, 0, 0) });
            return sp;
        }

        /// <summary>Latency sparkline: drawn on a fixed 600×96 canvas and stretched to the card's width.</summary>
        private static FrameworkElement Chart(List<(DateTime At, long Gateway, long Dns, bool Online)> samples)
        {
            const double W = 600, H = 96;
            var canvas = new Canvas { Width = W, Height = H };
            for (int i = 1; i <= 3; i++)
                canvas.Children.Add(new Line { X1 = 0, X2 = W, Y1 = H * i / 4, Y2 = H * i / 4, Stroke = Ui.Res("B.Line"), StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 2, 4 } });

            if (samples.Count < 2)
            {
                var wait = Ui.Mono("collecting the first probes…", Ui.Res("B.TextMute"), 11);
                Canvas.SetLeft(wait, 8); Canvas.SetTop(wait, H / 2 - 8);
                canvas.Children.Add(wait);
            }
            else
            {
                long max = Math.Max(10, samples.Select(x => Math.Max(x.Gateway, x.Dns)).Max());
                double step = W / Math.Max(1, samples.Count - 1);
                for (int i = 0; i < samples.Count; i++)
                    if (!samples[i].Online)
                    {
                        var r = new Rectangle { Width = Math.Max(step, 3), Height = H, Fill = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xB5, 0x47)) };
                        Canvas.SetLeft(r, i * step - step / 2);
                        canvas.Children.Add(r);
                    }
                void Series(Func<(DateTime At, long Gateway, long Dns, bool Online), long> pick, Brush stroke, double thick)
                {
                    PointCollection pts = null;
                    for (int i = 0; i < samples.Count; i++)
                    {
                        var v = pick(samples[i]);
                        if (v < 0)
                        {
                            if (pts != null && pts.Count > 1) canvas.Children.Add(new Polyline { Points = pts, Stroke = stroke, StrokeThickness = thick, StrokeLineJoin = PenLineJoin.Round });
                            pts = null; continue;
                        }
                        pts ??= new PointCollection();
                        pts.Add(new Point(i * step, H - 6 - (H - 12) * Math.Min(1, v / (double)max)));
                    }
                    if (pts != null && pts.Count > 1) canvas.Children.Add(new Polyline { Points = pts, Stroke = stroke, StrokeThickness = thick, StrokeLineJoin = PenLineJoin.Round });
                }
                Series(x => x.Dns, new SolidColorBrush(Color.FromRgb(0x8A, 0xB4, 0xFF)), 1.6);
                Series(x => x.Gateway, Ui.Res("B.Mint"), 2.2);
                var top = Ui.Mono($"{max} ms", Ui.Res("B.TextMute"), 10);
                Canvas.SetLeft(top, W - 44); Canvas.SetTop(top, 2);
                canvas.Children.Add(top);
            }
            return new Border
            {
                Background = Ui.Res("B.Sunken"), CornerRadius = new CornerRadius(12), Padding = new Thickness(10), BorderBrush = Ui.Res("B.Line"), BorderThickness = new Thickness(1),
                Child = new Viewbox { Stretch = Stretch.Fill, Height = 110, Child = canvas }
            };
        }

        private static FrameworkElement OutagesCard(int n)
        {
            var sp = new StackPanel();
            var list = Activity.RecentOutages(n);
            sp.Children.Add(CardTitle("Outages", list.Count == 0 ? "None since the service started. Nice and quiet." : null));
            foreach (var o in list)
            {
                var g = new Grid { Margin = new Thickness(0, 0, 0, 10) };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition());
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.Children.Add(new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Margin = new Thickness(0, 6, 12, 0), VerticalAlignment = VerticalAlignment.Top, Background = Ui.Res(o.End == null ? "B.Amber" : "B.TextMute") });
                var mid = new StackPanel();
                mid.Children.Add(new TextBlock { Text = o.Cause ?? "Connection lost", Foreground = Ui.Res("B.Text"), FontSize = 13, FontFamily = Ui.Font("F.Body"), TextTrimming = TextTrimming.CharacterEllipsis });
                mid.Children.Add(Ui.Mono($"{o.Start:ddd HH:mm}{(o.End == null ? " · ongoing" : "")}", Ui.Res("B.TextMute"), 11));
                Grid.SetColumn(mid, 1); g.Children.Add(mid);
                var len = Ui.Mono(o.Length.TotalHours >= 1 ? $"{(int)o.Length.TotalHours} h {o.Length.Minutes} m" : $"{(int)o.Length.TotalMinutes} m {o.Length.Seconds:00} s", Ui.Res(o.End == null ? "B.Amber" : "B.TextSoft"));
                Grid.SetColumn(len, 2); g.Children.Add(len);
                sp.Children.Add(g);
            }
            return Card(sp);
        }

        private static string KindIcon(ActivityKind k) => k switch
        {
            ActivityKind.Convert => Icons.Bolt, ActivityKind.Push => Icons.Git, ActivityKind.Shortcut => Icons.Code,
            ActivityKind.Files => Icons.File, ActivityKind.Network => Icons.Wifi, ActivityKind.Docker => Icons.Box, _ => Icons.Shield
        };

        private static FrameworkElement ActivityRow(ActivityEntry e)
        {
            var g = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var accent = e.Ok ? Ui.Res("B.Mint") : Ui.Res("B.Amber");
            var wash = e.Ok ? Ui.Res("B.MintWash") : Ui.Res("B.AmberWash");
            g.Children.Add(Ui.IconChip(KindIcon(e.Kind), accent, wash, 28, 14));
            var mid = new StackPanel { Margin = new Thickness(12, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
            mid.Children.Add(new TextBlock { Text = e.Title, Foreground = Ui.Res("B.Text"), FontSize = 13, FontFamily = Ui.Font("F.Body"), TextTrimming = TextTrimming.CharacterEllipsis });
            if (!string.IsNullOrWhiteSpace(e.Detail)) mid.Children.Add(new TextBlock { Text = e.Detail, Style = Ui.Style("T.Mono"), FontSize = 11, Foreground = Ui.Res("B.TextMute"), TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis });
            Grid.SetColumn(mid, 1); g.Children.Add(mid);
            var when = Ui.Mono(Ago(e.At), Ui.Res("B.TextMute"), 11);
            when.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(when, 2); g.Children.Add(when);
            return g;
        }

        private static string Ago(DateTime t)
        {
            var d = DateTime.Now - t;
            if (d.TotalSeconds < 60) return "now";
            if (d.TotalMinutes < 60) return $"{(int)d.TotalMinutes} m ago";
            if (d.TotalHours < 24) return $"{(int)d.TotalHours} h ago";
            return t.ToString("MMM d");
        }

        private static FrameworkElement ActivityCard(int n, ActivityKind? kind, string title)
        {
            var sp = new StackPanel();
            var list = Activity.Recent(n, kind);
            sp.Children.Add(CardTitle(title, list.Count == 0 ? "Conversions, pushes, shortcuts, file jobs and outages show up here." : null));
            foreach (var e in list) sp.Children.Add(ActivityRow(e));
            return Card(sp);
        }

        private ActivityKind? _activityFilter;

        private FrameworkElement ActivityPage()
        {
            var root = PageRoot("Activity", "What it has been doing", "Kept in memory while the service runs. The log folder keeps the full record.");
            var chips = new WrapPanel { Margin = new Thickness(0, 0, 0, 14) };
            void Chip(string label, ActivityKind? k)
            {
                var b = Ui.Button(label, () => { _activityFilter = k; Navigate("activity", animate: false); }, "Btn.Chip");
                bool on = _activityFilter == k;
                b.Background = on ? Ui.Res("B.Mint") : Ui.Res("B.Raised");
                b.Foreground = on ? Ui.Res("B.MintInk") : Ui.Res("B.TextSoft");
                b.BorderBrush = on ? Ui.Res("B.Mint") : Ui.Res("B.LineStrong");
                chips.Children.Add(b);
            }
            Chip("All", null);
            Chip("Conversions", ActivityKind.Convert);
            Chip("Pushes", ActivityKind.Push);
            Chip("Shortcuts", ActivityKind.Shortcut);
            Chip("Files", ActivityKind.Files);
            Chip("Network", ActivityKind.Network);
            Chip("Docker", ActivityKind.Docker);
            root.Children.Add(chips);
            root.Children.Add(ActivityCard(100, _activityFilter, _activityFilter == null ? "Everything" : _activityFilter.ToString()));
            root.Children.Add(Ui.Button("Open log folder", () => ActionProvider.Shell(Log.Folder), "Btn.Ghost", Icons.Lines));
            return root;
        }

        // -------------------------------------------------------------- public IP

        private FrameworkElement PublicIpCard()
        {
            var s = Settings.Current;
            var h = App.PublicIp.History;
            var sp = new StackPanel();
            var head = new Grid();
            head.Children.Add(CardTitle("Public IP", h.Current == null
                ? (App.PublicIp.LastError != null ? "Not checked yet: " + App.PublicIp.LastError : "Checking shortly…")
                : $"Last checked {h.LastChecked:HH:mm}. Every change pops a dialog and is committed to the history repository."));
            var check = Ui.Button("Check now", async () => { await App.PublicIp.CheckAsync(); Navigate("network", animate: false); }, "Btn.Ghost", Icons.Restart);
            check.HorizontalAlignment = HorizontalAlignment.Right; check.VerticalAlignment = VerticalAlignment.Top;
            head.Children.Add(check);
            sp.Children.Add(head);

            if (h.Current != null)
            {
                var cur = h.Records.FirstOrDefault(r => r.Ip == h.Current);
                var big = new WrapPanel { Margin = new Thickness(0, 0, 0, 14) };
                big.Children.Add(new TextBlock { Text = h.Current, FontFamily = Ui.Font("F.Mono"), FontSize = 24, FontWeight = FontWeights.SemiBold, Foreground = Ui.Res("B.Violet"), Margin = new Thickness(0, 0, 14, 0) });
                if (cur != null) big.Children.Add(new TextBlock { Text = string.Join(" · ", new[] { cur.Org, cur.Place }.Where(x => !string.IsNullOrWhiteSpace(x))), Style = Ui.Style("T.Body"), VerticalAlignment = VerticalAlignment.Center });
                sp.Children.Add(big);

                // history table: IP, first seen, last seen, times, network
                var g = new Grid();
                foreach (var w in new[] { 1.3, 1.2, 1.2, 0.5, 2.0 }) g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w, GridUnitType.Star) });
                void Cell(string text, int row, int col, Brush fg, bool mono = true)
                {
                    var t = new TextBlock { Text = text, Foreground = fg, FontSize = 12, FontFamily = Ui.Font(mono ? "F.Mono" : "F.Body"), Margin = new Thickness(0, 0, 10, 8), TextTrimming = TextTrimming.CharacterEllipsis };
                    Grid.SetRow(t, row); Grid.SetColumn(t, col); g.Children.Add(t);
                }
                g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                int c = 0;
                foreach (var label in new[] { "IP", "First seen", "Last seen", "Times", "Network" }) Cell(label, 0, c++, Ui.Res("B.TextMute"), false);
                int r0 = 1;
                foreach (var rec in h.Records.Take(12))
                {
                    g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    var now = rec.Ip == h.Current;
                    Cell(rec.Ip, r0, 0, Ui.Res(now ? "B.Violet" : "B.Text"));
                    Cell(rec.FirstSeen.ToString("MMM d HH:mm"), r0, 1, Ui.Res("B.TextSoft"));
                    Cell(now ? "now" : rec.LastSeen.ToString("MMM d HH:mm"), r0, 2, Ui.Res(now ? "B.Mint" : "B.TextSoft"));
                    Cell(rec.Times.ToString(), r0, 3, Ui.Res("B.TextSoft"));
                    Cell(rec.Org ?? "", r0, 4, Ui.Res("B.TextMute"), false);
                    r0++;
                }
                sp.Children.Add(new Border { Background = Ui.Res("B.Sunken"), CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 12, 14, 4), BorderBrush = Ui.Res("B.Line"), BorderThickness = new Thickness(1), Child = g, Margin = new Thickness(0, 0, 0, 14) });
            }

            sp.Children.Add(Switch("Watch the public IP", "Checks every few minutes and right after the network changes.", s.PublicIpEnabled, v => s.PublicIpEnabled = v, "B.Violet"));
            sp.Children.Add(Choice("Check every", new[] { ("1 min", 1), ("5 min", 5), ("15 min", 15), ("60 min", 60) }, s.PublicIpIntervalMinutes, v => s.PublicIpIntervalMinutes = v));

            sp.Children.Add(Ui.Mono("HISTORY REPOSITORY", Ui.Res("B.TextMute"), 11));
            var repo = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            repo.Children.Add(Ui.Mono(Net.PublicIpMonitor.RepoDir, Ui.Res("B.TextSoft")));
            sp.Children.Add(repo);
            var remoteRow = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            var remote = Input(s.IpHistoryRemote, 380, "Optional git remote URL, for example https://github.com/you/ip-history.git");
            remoteRow.Children.Add(remote);
            var status = Ui.Mono("", Ui.Res("B.TextMute"), 11);
            remoteRow.Children.Add(Ui.Button("Save remote", () => { s.IpHistoryRemote = remote.Text.Trim(); Settings.Save(); status.Text = "Saved. The next change pushes there."; }, "Btn.Ghost"));
            remoteRow.Children.Add(Ui.Button("Push now", async () =>
            {
                status.Text = "Pushing…";
                var (code, all) = await Net.PublicIpMonitor.PushNowAsync();
                status.Text = code == 0 ? "Pushed." : "Push failed: " + ActionProvider.Tail(all, 1);
            }, "Btn.Ghost", Icons.Git));
            remoteRow.Children.Add(Ui.Button("Open folder", () => ActionProvider.Shell(Net.PublicIpMonitor.RepoDir), "Btn.Ghost", Icons.Folder));
            sp.Children.Add(remoteRow);
            sp.Children.Add(status);
            return Card(sp, "#FF3A2F5A");
        }

        // -------------------------------------------------------------- command playground

        private string _playText = @"D:\Footage\clip.mov:mp4";

        private FrameworkElement Playground()
        {
            var sp = new StackPanel();
            sp.Children.Add(CardTitle("Try a command", "Type what you would copy. It shows what will happen before anything runs."));
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var box = Input(_playText);
            box.FontSize = 13; box.Height = 40;
            row.Children.Add(box);
            var run = Ui.Button("Run it", null, "Btn.Accent", Icons.Play);
            run.Height = 40; run.Margin = new Thickness(0, 0, 0, 8);
            Grid.SetColumn(run, 1);
            row.Children.Add(run);
            sp.Children.Add(row);
            var explain = new TextBlock { Style = Ui.Style("T.Body"), Margin = new Thickness(0, 4, 0, 0) };
            sp.Children.Add(explain);

            void Explain()
            {
                _playText = box.Text;
                var t = box.Text.Trim();
                var c = ClipItem.TryCommand(t);
                run.IsEnabled = c != null;
                explain.Foreground = Ui.Res(c != null ? "B.Mint" : "B.TextMute");
                if (c == null)
                {
                    explain.Text = t.LastIndexOf(':') > 2
                        ? "Not runnable yet: the path before the colon does not exist, or the word after it is not a format, :push or one of your shortcuts."
                        : "Format: a path, a colon, then a format such as mp4, gif, mp3 or webp. Also :push and your own :shortcuts.";
                    return;
                }
                switch (c.Kind)
                {
                    case CommandKind.Convert when Directory.Exists(c.SourcePath):
                        var n = Converter.CompatibleIn(c.SourcePath, c.Format).Count;
                        explain.Text = $"Converts {n} file{(n == 1 ? "" : "s")} in {Path.GetFileName(c.SourcePath.TrimEnd('\\'))} to {Formats.Find(c.Format).Label}, saved next to the originals.";
                        break;
                    case CommandKind.Convert:
                        explain.Text = $"Converts to {Formats.Find(c.Format).Label} → {Converter.OutputPathFor(c.SourcePath, Formats.Find(c.Format))}";
                        break;
                    case CommandKind.Push:
                        var repo = GitPush.FindRepoRoot(c.SourcePath);
                        explain.Text = repo == null ? "Not inside a git repository." :
                            $"Stages everything in {Path.GetFileName(repo)}, commits as \"{GitPush.Expand(Settings.Current.CommitTemplate.Length > 0 ? Settings.Current.CommitTemplate : GitPush.SuggestedTemplate, repo, "current branch", 0)}\" and pushes.";
                        break;
                    case CommandKind.Custom:
                        var sc = Shortcuts.Find(c.Format);
                        explain.Text = $"Runs :{sc.Name} ({sc.Shell}) with \"{c.SourcePath}\" as its input.";
                        break;
                }
            }
            box.TextChanged += (s, e) => Explain();
            run.Click += (s, e) => { var c = ClipItem.TryCommand(box.Text.Trim()); if (c != null) App.RunCommand(c); };
            Explain();
            return Card(sp, "#FF2B4A3F");
        }

        // -------------------------------------------------------------- about and updates

        private string _updateText;

        private FrameworkElement AboutPage()
        {
            var version = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
            var root = PageRoot("About", "Monitoring Tools " + version, "Clipboard toasts, internet watch and quick commands for Windows.");
            var a = new StackPanel();
            a.Children.Add(CardTitle("Updates", _updateText ?? "Every change to the project ships as a new release on GitHub."));
            var row = new WrapPanel();
            row.Children.Add(Ui.Button("Check for updates", async () =>
            {
                _updateText = "Checking…";
                Navigate("about", animate: false);
                _updateText = await CheckUpdates(version);
                Navigate("about", animate: false);
            }, "Btn.Accent", Icons.Restart));
            row.Children.Add(Ui.Button("Release notes", () => Markdown.Open("https://github.com/Ding-Ding-Projects/Monitoring-tools/releases"), "Btn.Ghost", Icons.Globe));
            row.Children.Add(Ui.Button("Project page", () => Markdown.Open("https://ding-ding-projects.github.io/Monitoring-tools/"), "Btn.Ghost", Icons.Link));
            a.Children.Add(row);
            root.Children.Add(Card(a));

            var p = new StackPanel();
            p.Children.Add(CardTitle("Where things live"));
            void Line(string k, string v, Action open)
            {
                var g = new Grid { Margin = new Thickness(0, 0, 0, 8) };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
                g.ColumnDefinitions.Add(new ColumnDefinition());
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.Children.Add(new TextBlock { Text = k, Foreground = Ui.Res("B.TextMute"), FontSize = 13, FontFamily = Ui.Font("F.Body"), VerticalAlignment = VerticalAlignment.Center });
                var t = Ui.Mono(v, Ui.Res("B.TextSoft")); t.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(t, 1); g.Children.Add(t);
                var b = Ui.Button("Open", open); b.Margin = new Thickness(8, 0, 0, 0);
                Grid.SetColumn(b, 2); g.Children.Add(b);
                p.Children.Add(g);
            }
            Line("Program", ServiceHost.ServiceExe, () => ActionProvider.Reveal(ServiceHost.ServiceExe));
            Line("Settings", Path.Combine(Settings.DataDir, "settings.json"), () => ActionProvider.Reveal(Path.Combine(Settings.DataDir, "settings.json")));
            Line("Scripts", Shortcuts.Folder, () => ActionProvider.Shell(Shortcuts.Folder));
            Line("Logs", Log.Folder, () => ActionProvider.Shell(Log.Folder));
            Line("Saved media", Settings.Current.ResolvedMediaFolder, () => ActionProvider.Shell(Settings.Current.ResolvedMediaFolder));
            root.Children.Add(Card(p));

            var priv = new StackPanel();
            priv.Children.Add(CardTitle("Privacy", "Clipboard history stays in memory and is gone when the service stops. Copies that password managers mark private are skipped. The only network calls are the connectivity probes and this update check."));
            root.Children.Add(Card(priv));
            return root;
        }

        private static async System.Threading.Tasks.Task<string> CheckUpdates(string current)
        {
            try
            {
                using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("MonitoringTools/" + current);
                var json = await http.GetStringAsync("https://api.github.com/repos/Ding-Ding-Projects/Monitoring-tools/releases/latest");
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var tag = doc.RootElement.GetProperty("tag_name").GetString()?.TrimStart('v') ?? "";
                if (Version.TryParse(tag, out var latest) && Version.TryParse(current, out var mine))
                    return latest > mine ? $"Version {latest} is out. You have {mine}. Grab it from Release notes." : $"You are up to date ({mine}).";
                return "Could not read the latest version.";
            }
            catch (Exception ex) { return "Update check failed: " + ex.Message; }
        }

        // -------------------------------------------------------------- chrome

        private void OnMin(object s, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void OnMax(object s, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        private void OnClose(object s, RoutedEventArgs e) => Close();
    }
}
