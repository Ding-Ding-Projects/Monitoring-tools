using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    /// <summary>
    /// Ctrl+Shift+F everywhere in the app: every page, every setting (with its live control
    /// inline) and every action. Plain-text search by default, with an adjacent regex mode.
    /// Choosing a result teleports to the exact control and highlights it.
    /// </summary>
    public static class CommandPalette
    {
        public sealed class Item
        {
            public string Group, Title, Detail, Icon, Page;
            public Action Run;
            public Func<FrameworkElement> Inline;   // live control rendered in the row
        }

        private static Window _open;
        public static Window Current => _open;

        public static void Open(Window owner = null)
        {
            if (_open != null) { _open.Activate(); return; }
            var w = new PaletteWindow(Items());
            if (owner != null && owner.IsVisible) w.Owner = owner;
            _open = w;
            w.Closed += (s, e) => _open = null;
            w.Show();
        }

        /// <summary>Hook Ctrl+Shift+F on a window.</summary>
        public static void Attach(Window w)
        {
            w.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.F && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)) { e.Handled = true; Open(w); }
            };
        }

        private static void Go(string page, string label = null) => App.Current.OpenControlCentre(page, label);

        public static List<Item> Items()
        {
            var s = Settings.Current;
            var list = new List<Item>();
            void Page(string id, string title, string icon, string detail) =>
                list.Add(new Item { Group = "Pages", Title = title, Detail = detail, Icon = icon, Page = id, Run = () => Go(id) });
            void Toggle(string title, string page, Func<bool> get, Action<bool> set, string detail = null) =>
                list.Add(new Item
                {
                    Group = "Settings", Title = title, Detail = detail ?? "Control centre · " + page, Icon = Icons.Shield, Page = page,
                    Run = () => Go(page, title),
                    Inline = () =>
                    {
                        var cb = new CheckBox { Style = Ui.Style("Switch"), IsChecked = get(), Tag = Ui.Res("B.Mint"), Focusable = false };
                        AutomationProperties.SetName(cb, title);
                        cb.Checked += (o, e) => { set(true); Settings.Save(); App.Tray.Refresh(); };
                        cb.Unchecked += (o, e) => { set(false); Settings.Save(); App.Tray.Refresh(); };
                        return cb;
                    }
                });
            void Choice<T>(string title, string page, (string, T)[] options, Func<T> get, Action<T> set) =>
                list.Add(new Item
                {
                    Group = "Settings", Title = title, Detail = "Control centre · " + page, Icon = Icons.Shield, Page = page,
                    Run = () => Go(page, title),
                    Inline = () =>
                    {
                        var wrap = new StackPanel { Orientation = Orientation.Horizontal };
                        var buttons = new List<(Button, T)>();
                        void Paint() { foreach (var (b, v) in buttons) { bool on = EqualityComparer<T>.Default.Equals(v, get()); b.Background = Ui.Res(on ? "B.Mint" : "B.Raised"); b.Foreground = Ui.Res(on ? "B.MintInk" : "B.TextSoft"); } }
                        foreach (var (l, v) in options)
                        {
                            var val = v;
                            var b = Ui.Button(l, null, "Btn.Chip"); b.Height = 26; b.Margin = new Thickness(0, 0, 4, 0); b.Focusable = false;
                            b.Click += (o, e) => { set(val); Settings.Save(); Paint(); };
                            buttons.Add((b, v)); wrap.Children.Add(b);
                        }
                        Paint();
                        return wrap;
                    }
                });
            void Act(string title, string icon, Action run, string detail = null, string group = "Actions") =>
                list.Add(new Item { Group = group, Title = title, Detail = detail, Icon = icon, Run = run });

            Page("overview", "Overview", Icons.Pulse, "Live latency, outages and activity");
            Page("activity", "Activity", Icons.Lines, "Everything the service has done");
            Page("history", "Clipboard history", Icons.Clipboard, "Recent copies, kept in memory only");
            Page("clipboard", "Clipboard settings", Icons.Text, "Toasts, Markdown, media folder");
            Page("shortcuts", "Shortcuts and scripts", Icons.Code, "Your :commands and the script editor");
            Page("network", "Network and DNS", Icons.Wifi, "Public IP history, DNS profiles, alerts");
            Page("game", "Game mode", Icons.Game, "Watched games and fullscreen");
            Page("conversion", "Conversion", Icons.Bolt, "Command playground, FFmpeg, formats");
            Page("minecraft", "Minecraft servers", Icons.Box, "Create and run servers here, in Docker or over SSH");
            Page("service", "Service", Icons.Shield, "Start-up, administrator rights, uninstall");
            Page("about", "About and updates", Icons.Globe, "Version, bundled tools, update check");

            Toggle("Clipboard toasts", "clipboard", () => s.ClipboardEnabled, v => s.ClipboardEnabled = v);
            Toggle("Keep open on hover", "clipboard", () => s.KeepOpenOnHover, v => s.KeepOpenOnHover = v);
            Toggle("Render Markdown", "clipboard", () => s.RenderMarkdown, v => s.RenderMarkdown = v);
            Toggle("Skip password managers", "clipboard", () => s.IgnoreSensitiveSources, v => s.IgnoreSensitiveSources = v);
            Toggle("Quick commands", "clipboard", () => s.QuickCommandsEnabled, v => s.QuickCommandsEnabled = v);
            Choice("Toast duration", "clipboard", new[] { ("2 s", 2.0), ("3 s", 3.0), ("5 s", 5.0), ("8 s", 8.0) }, () => s.ToastSeconds, v => s.ToastSeconds = v);
            Toggle("Network alerts", "network", () => s.NetworkEnabled, v => s.NetworkEnabled = v);
            Toggle("Watch the public IP", "network", () => s.PublicIpEnabled, v => s.PublicIpEnabled = v);
            Choice("Check every", "network", new[] { ("2 s", 2), ("3 s", 3), ("5 s", 5), ("10 s", 10) }, () => s.ProbeIntervalSeconds, v => s.ProbeIntervalSeconds = v);
            Toggle("Suppress while gaming", "game", () => s.GameModeEnabled, v => s.GameModeEnabled = v);
            Toggle("Any fullscreen app counts", "game", () => s.GameModeFullscreen, v => s.GameModeFullscreen = v);
            Toggle("Sound effects", "clipboard", () => s.SoundsEnabled, v => s.SoundsEnabled = v);
            Toggle("Start with Windows", "service", () => s.StartWithWindows, v => { s.StartWithWindows = v; ServiceHost.ApplyAutostart(); });
            Choice("Volume", "clipboard", new[] { ("Low", 0.3), ("Medium", 0.6), ("Full", 1.0) }, () => s.SoundVolume, v => s.SoundVolume = v);
            list.Add(new Item { Group = "Settings", Title = "Personal wording", Detail = PersonalWording.Active ? $"Loaded, {PersonalWording.Count} phrases" : "No file loaded", Icon = Icons.Text, Page = "service", Run = () => Go("service", "Personal wording") });
            Act("Choose a personal wording file", Icons.Text, () => WordingLayer.PickAndApply(App.Current.MainWindow));
            if (PersonalWording.Active) Act("Clear personal wording", Icons.Restart, PersonalWording.Clear);

            Act("Test connection now", Icons.Wifi, () => App.Net.ProbeNow());
            Act("Check public IP now", Icons.Globe, async () => { await App.PublicIp.CheckAsync(); Go("network", "Public IP"); });
            Act("Release and renew IP", Icons.Bolt, async () => { await Elevated.RunAsync("ipconfig /release", "ipconfig /renew", "ipconfig /flushdns"); App.Net.ProbeNow(); });
            foreach (var p in s.DnsProfiles)
            {
                var prof = p;
                Act($"Switch DNS to {p.Name}", Icons.Globe, async () => { await DnsSwitcher.ApplyAsync(prof); App.Net.ProbeNow(); }, p.ToString());
            }
            Act("Snooze 1 hour", Icons.Clock, () => { App.SnoozeUntil = DateTime.Now.AddHours(1); App.Tray.Refresh(); });
            Act("Run setup again", Icons.Play, () => App.Current.ShowSetup());
            Act("Take the tour", Icons.Play, () => App.Current.ShowWelcome());
            Act("Preview the offline dialog", Icons.WifiOff, Demo.PreviewOffline);
            Act("Show sample toasts", Icons.Clipboard, Demo.TryClipboard);
            Act("Open log folder", Icons.Lines, () => ActionProvider.Shell(Log.Folder));
            Act("Open IP history repository", Icons.Git, () => ActionProvider.Shell(Net.PublicIpMonitor.RepoDir));
            Act("Restart service", Icons.Restart, () => App.Current.Quit(ServiceHost.ExitRestart));
            Act("Uninstall service", Icons.Trash, () => Go("service", "Uninstall"));
            foreach (var sc in s.Shortcuts)
            {
                var x = sc;
                Act(":" + sc.Name, Icons.Code, () => { Go("shortcuts"); }, string.IsNullOrWhiteSpace(sc.Description) ? "Edit this shortcut" : sc.Description, "Shortcuts");
            }
            return list;
        }

        // -------------------------------------------------------------- the window

        private sealed class PaletteWindow : Window
        {
            private readonly List<Item> _all;
            private readonly TextBox _query;
            private readonly StackPanel _list;
            private readonly TextBlock _status;
            private readonly ToggleButton _regex, _case;
            private List<Item> _shown = new List<Item>();
            private int _sel;
            private readonly Border _card;

            public PaletteWindow(List<Item> items)
            {
                _all = items;
                WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
                ResizeMode = ResizeMode.NoResize; Topmost = true; ShowInTaskbar = false; Title = "Command palette";
                ApplySize();

                _query = new TextBox { Style = Ui.Style("Input"), Height = 44, FontSize = 15, FontFamily = Ui.Font("F.Body") };
                AutomationProperties.SetName(_query, "Search commands, pages and settings");
                _regex = Flag(".*", "Regex mode");
                _case = Flag("Aa", "Match case");
                var size = Ui.Button(Settings.Current.PaletteFullWindow ? "Card" : "Full", () =>
                {
                    Settings.Current.PaletteFullWindow = !Settings.Current.PaletteFullWindow;
                    Settings.Save();
                    ApplySize();
                }, "Btn.Ghost");
                size.Height = 34; size.Margin = new Thickness(6, 0, 0, 0); size.ToolTip = "Switch between a card and the full window";

                var top = new Grid();
                top.ColumnDefinitions.Add(new ColumnDefinition());
                top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                top.Children.Add(_query);
                var flags = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 0, 0, 0) };
                flags.Children.Add(_regex); flags.Children.Add(_case); flags.Children.Add(size);
                Grid.SetColumn(flags, 1); top.Children.Add(flags);

                _list = new StackPanel();
                _status = new TextBlock { Style = Ui.Style("T.Mono"), FontSize = 11, Foreground = Ui.Res("B.TextMute"), Margin = new Thickness(4, 10, 0, 0) };
                var scroller = new ScrollViewer { Content = _list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 12, 0, 0) };
                var root = new DockPanel();
                DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
                DockPanel.SetDock(_status, Dock.Bottom); root.Children.Add(_status);
                root.Children.Add(scroller);

                _card = new Border
                {
                    Background = Ui.Res("B.Surface"), BorderBrush = Ui.Res("B.Line"), BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(18), Padding = new Thickness(14), Margin = new Thickness(20),
                    Effect = (System.Windows.Media.Effects.Effect)Application.Current.Resources["Fx.Shadow"], Child = root,
                    RenderTransformOrigin = new Point(0.5, 0), RenderTransform = new ScaleTransform(0.97, 0.97)
                };
                Content = _card;

                _query.TextChanged += (s, e) => Filter();
                _regex.Click += (s, e) => Filter();
                _case.Click += (s, e) => Filter();
                PreviewKeyDown += OnKey;
                Deactivated += (s, e) => { if (IsVisible) Close(); };
                Loaded += (s, e) =>
                {
                    ((ScaleTransform)_card.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(260)) { EasingFunction = Ui.Spring });
                    ((ScaleTransform)_card.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(260)) { EasingFunction = Ui.Spring });
                    _card.Opacity = 0; Ui.Fade(_card, 1, 160);
                    Activate(); _query.Focus();
                };
                Filter();
            }

            private void ApplySize()
            {
                var area = SystemParameters.WorkArea;
                if (Settings.Current.PaletteFullWindow)
                {
                    Left = area.Left; Top = area.Top; Width = area.Width; Height = area.Height;
                }
                else
                {
                    Width = 680; Height = 560;
                    Left = area.Left + (area.Width - Width) / 2; Top = area.Top + area.Height * 0.12;
                }
            }

            private static ToggleButton Flag(string text, string name)
            {
                var t = new ToggleButton
                {
                    Content = text, Width = 38, Height = 34, Margin = new Thickness(6, 0, 0, 0), Cursor = Cursors.Hand, FocusVisualStyle = null,
                    FontFamily = Ui.Font("F.Mono"), FontSize = 12, Foreground = Ui.Res("B.TextSoft"), ToolTip = name
                };
                AutomationProperties.SetName(t, name);
                var tpl = new ControlTemplate(typeof(ToggleButton));
                var bd = new FrameworkElementFactory(typeof(Border), "Bd");
                bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
                bd.SetValue(Border.BackgroundProperty, Ui.Res("B.Raised"));
                bd.SetValue(Border.BorderBrushProperty, Ui.Res("B.LineStrong"));
                bd.SetValue(Border.BorderThicknessProperty, new Thickness(1));
                var cp = new FrameworkElementFactory(typeof(ContentPresenter));
                cp.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
                cp.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
                bd.AppendChild(cp);
                tpl.VisualTree = bd;
                var on = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
                on.Setters.Add(new Setter(Border.BackgroundProperty, Ui.Res("B.MintWash"), "Bd"));
                on.Setters.Add(new Setter(Border.BorderBrushProperty, Ui.Res("B.Mint"), "Bd"));
                tpl.Triggers.Add(on);
                t.Template = tpl;
                return t;
            }

            private void Filter()
            {
                var q = _query.Text.Trim();
                Func<Item, bool> match = _ => true;
                _status.Foreground = Ui.Res("B.TextMute");
                if (q.Length > 0)
                {
                    if (_regex.IsChecked == true)
                    {
                        try
                        {
                            var rx = new Regex(q, _case.IsChecked == true ? RegexOptions.None : RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200));
                            match = i => rx.IsMatch(i.Title) || (i.Detail != null && rx.IsMatch(i.Detail)) || rx.IsMatch(i.Group);
                        }
                        catch (ArgumentException ex)
                        {
                            _status.Text = "Pattern is not valid: " + ex.Message.Split('\n')[0];
                            _status.Foreground = Ui.Res("B.Amber");
                            return;
                        }
                    }
                    else
                    {
                        var cmp = _case.IsChecked == true ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                        var words = q.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        match = i => words.All(w => i.Title.Contains(w, cmp) || (i.Detail ?? "").Contains(w, cmp) || i.Group.Contains(w, cmp));
                    }
                }
                _shown = _all.Where(match).ToList();
                _sel = 0;
                Render();
                _status.Text = $"{_shown.Count} of {_all.Count} · ↑↓ to move · Enter to go · Esc to close" + (_regex.IsChecked == true ? " · regex" : "");
            }

            private void Render()
            {
                _list.Children.Clear();
                string group = null;
                for (int i = 0; i < _shown.Count; i++)
                {
                    var it = _shown[i];
                    if (it.Group != group)
                    {
                        group = it.Group;
                        _list.Children.Add(new TextBlock { Text = group.ToUpperInvariant(), Style = Ui.Style("T.Eyebrow"), Margin = new Thickness(8, i == 0 ? 0 : 12, 0, 6) });
                    }
                    _list.Children.Add(Row(it, i == _sel, i));
                }
            }

            private FrameworkElement Row(Item it, bool selected, int index)
            {
                var g = new Grid { Margin = new Thickness(8, 0, 8, 0), MinHeight = 44 };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition());
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.Children.Add(Ui.IconChip(it.Icon ?? Icons.Bolt, Ui.Res("B.Mint"), Ui.Res("B.MintWash"), 28, 14));
                var text = new StackPanel { Margin = new Thickness(12, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
                text.Children.Add(new TextBlock { Text = it.Title, Foreground = Ui.Res("B.Text"), FontSize = 14, FontFamily = Ui.Font("F.Body") });
                if (!string.IsNullOrEmpty(it.Detail)) text.Children.Add(new TextBlock { Text = it.Detail, Style = Ui.Style("T.Mono"), FontSize = 11, Foreground = Ui.Res("B.TextMute"), TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis });
                Grid.SetColumn(text, 1); g.Children.Add(text);
                if (it.Inline != null)
                {
                    var c = it.Inline(); c.VerticalAlignment = VerticalAlignment.Center;
                    Grid.SetColumn(c, 2); g.Children.Add(c);
                }
                var row = new Border { CornerRadius = new CornerRadius(12), Padding = new Thickness(0, 4, 0, 4), Background = selected ? Ui.Res("B.Raised") : Brushes.Transparent, Child = g, Cursor = Cursors.Hand };
                AutomationProperties.SetName(row, $"{it.Group}: {it.Title}");
                row.MouseEnter += (s, e) => { if (_sel != index) { _sel = index; Render(); } };
                row.MouseLeftButtonUp += (s, e) =>
                {
                    if (e.OriginalSource is DependencyObject d && IsInside<ButtonBase>(d)) return; // inline control handled it
                    Choose(it);
                };
                return row;
            }

            private static bool IsInside<T>(DependencyObject d) where T : DependencyObject
            {
                while (d != null) { if (d is T) return true; d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d); }
                return false;
            }

            private void Choose(Item it)
            {
                Close();
                try { it.Run?.Invoke(); } catch (Exception ex) { Log.Warn("Palette action failed: " + ex.Message); }
            }

            private void OnKey(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Escape) { e.Handled = true; Close(); }
                else if (e.Key == Key.Down && _shown.Count > 0) { e.Handled = true; _sel = (_sel + 1) % _shown.Count; Render(); }
                else if (e.Key == Key.Up && _shown.Count > 0) { e.Handled = true; _sel = (_sel - 1 + _shown.Count) % _shown.Count; Render(); }
                else if (e.Key == Key.Enter && _shown.Count > 0) { e.Handled = true; Choose(_shown[_sel]); }
            }
        }
    }
}
