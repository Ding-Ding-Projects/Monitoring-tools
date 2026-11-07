using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    public partial class TrayMenuWindow : Window
    {
        private bool _closing;
        private Search.SearchField _search;
        public Search.SearchField FindField => _search;

        public TrayMenuWindow()
        {
            InitializeComponent();
            Appearance.SetDesignSize(this, 372, double.NaN);
            SourceInitialized += (s, e) => Native.MakeToolWindow(this, noActivate: false);
            Deactivated += (s, e) => { if (_search?.BuilderOpen != true) CloseAnimated(); };
            KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Escape && _search?.BuilderOpen != true) CloseAnimated(); };
            Build();
            Search.SearchScope.Attach(Rows, _search, "menu items");
            ElementMenu.Attach(this);
        }

        // -------------------------------------------------------------- rows

        private void Build()
        {
            var s = Settings.Current;
            var (dot, status) = App.Tray.State();

            // header
            var head = new Grid { Margin = new Thickness(10, 8, 10, 12) };
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.ColumnDefinitions.Add(new ColumnDefinition());
            head.Children.Add(Ui.IconChip(Icons.Pulse, Ui.Res("B.Mint"), Ui.Res("B.MintWash"), 34, 18));
            var hs = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            hs.Children.Add(new TextBlock { Text = "Monitoring Tools", Style = Ui.Style("T.Title"), FontSize = 14 });
            var st = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
            st.Children.Add(new Border { Width = 7, Height = 7, CornerRadius = new CornerRadius(4), Background = new SolidColorBrush(Color.FromRgb(dot.R, dot.G, dot.B)), Margin = new Thickness(0, 1, 6, 0), VerticalAlignment = VerticalAlignment.Center });
            st.Children.Add(Ui.Mono(status, Ui.Res("B.TextMute"), 11));
            hs.Children.Add(st);
            Grid.SetColumn(hs, 1);
            head.Children.Add(hs);
            Rows.Children.Add(head);
            _search = new Search.SearchField("tray", "Filter this menu", compact: true) { Margin = new Thickness(6, 0, 6, 8) };
            Rows.Children.Add(_search);
            Rows.Children.Add(Rule());

            // primary
            Rows.Children.Add(Row(Icons.Monitor, "Open control centre", "double-click", () => App.Current.OpenControlCentre(null)));
            Rows.Children.Add(Row(Icons.Clipboard, "Clipboard history", $"{App.History.Items.Count} kept", () => App.Current.OpenControlCentre("history")));
            Rows.Children.Add(Row(Icons.Lines, "Command palette", "Ctrl+Shift+F", () => CommandPalette.Open()));
            Rows.Children.Add(Row(Icons.Play, Settings.Current.OnboardingDone ? "Run setup again" : "Continue setup", null, () => App.Current.ShowSetup()));
            Rows.Children.Add(Row(Icons.Play, "Take the tour", null, () => App.Current.ShowWelcome()));
            Rows.Children.Add(Row(Icons.Code, "Shortcuts and scripts", $"{s.Shortcuts.Count}", () => App.Current.OpenControlCentre("shortcuts")));
            Rows.Children.Add(Row(Icons.Wifi, "Test connection now", App.Net.Current?.Online == false ? "offline" : "online", () =>
            {
                App.Net.ProbeNow();
                App.Toasts.ShowNotice("Checking the connection", "Running the probe ladder now.", App.Toasts.Mint, 2.5, icon: Icons.Wifi);
            }));
            Rows.Children.Add(Rule());

            // disc images this app mounted, each with Eject
            var mounted = Core.DocTools.DiscImageTools.MountedSnapshot();
            if (mounted.Count > 0)
            {
                foreach (var (image, drive) in mounted)
                {
                    var name = System.IO.Path.GetFileName(image);
                    var row = Row(Dialogs.DocIcons.Eject, $"Eject {name}", drive, () => EjectFromTray(image, name));
                    System.Windows.Automation.AutomationProperties.SetName(row, $"Eject {name}");
                    Rows.Children.Add(row);
                }
                Rows.Children.Add(Rule());
            }

            // switches
            Rows.Children.Add(Toggle("Clipboard toasts", "popup when you copy", s.ClipboardEnabled, v => { s.ClipboardEnabled = v; Settings.Save(); App.Tray.Refresh(); }));
            Rows.Children.Add(Toggle("Network alerts", "popup when the link drops", s.NetworkEnabled, v => { s.NetworkEnabled = v; Settings.Save(); App.Tray.Refresh(); }));
            var gameNote = s.GameModeEnabled ? (App.Games.IsGaming ? App.Games.ActiveGame + " running" : "auto") : "off";
            Rows.Children.Add(Toggle("Game mode", gameNote, s.GameModeEnabled, v => { s.GameModeEnabled = v; Settings.Save(); App.Tray.Refresh(); }, "B.Violet"));

            Rows.Children.Add(ChipRow(Icons.Clock, App.IsSnoozed ? Core.Language.Headline("snooze", App.SnoozeUntil.ToString("HH:mm")) : "Snooze everything",
                new[] { ("15 m", 15), ("1 h", 60), ("4 h", 240), (App.IsSnoozed ? "Wake" : null, 0) }
                    .Where(c => c.Item1 != null).Select(c => (c.Item1, (Action)(() =>
                    {
                        App.SnoozeUntil = c.Item2 == 0 ? DateTime.MinValue : DateTime.Now.AddMinutes(c.Item2);
                        App.Tray.Refresh();
                        CloseAnimated();
                    }))).ToArray()));

            var activeDns = DnsSwitcher.ActiveProfileName();
            Rows.Children.Add(ChipRow(Icons.Globe, "DNS", s.DnsProfiles.Select(p => (p.Name == activeDns ? "● " + p.Name : p.Name, (Action)(async () =>
            {
                CloseAnimated();
                var rc = await DnsSwitcher.ApplyAsync(p);
                if (rc == 0) App.Toasts.ShowNotice($"DNS is now {p.Name}", p.ToString(), App.Toasts.Mint, 4, icon: Icons.Globe);
                else if (rc != -2) App.Toasts.ShowNotice("DNS switch failed", $"netsh exited with {rc}.", App.Toasts.Amber, 6, icon: Icons.Globe);
                App.Net.ProbeNow();
            }))).ToArray()));
            Rows.Children.Add(Rule());

            // service
            Rows.Children.Add(Toggle("Administrator rights", s.RunElevated ? "on" : "off", s.RunElevated, v => ToggleElevation(v), "B.Amber"));
            Rows.Children.Add(Row(Icons.Restart, "Restart service", null, () => App.Current.Quit(ServiceHost.ExitRestart)));
            Rows.Children.Add(Row(Icons.Lines, "Open log folder", null, () => ActionProvider.Shell(Log.Folder)));
            Rows.Children.Add(Row(Icons.Trash, "Uninstall service…", null, Uninstall, danger: true));
            Rows.Children.Add(Row(Icons.Power, "Quit until next sign-in", null, () => App.Current.Quit(ServiceHost.ExitQuit)));
        }

        private static Border Rule() => new Border { Height = 1, Background = Ui.Res("B.Line"), Margin = new Thickness(6, 6, 6, 6) };

        private Button Row(string icon, string label, string note, Action act, bool danger = false)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var fg = danger ? Ui.Res("B.Danger") : Ui.Res("B.TextSoft");
            g.Children.Add(new ContentControl { Content = Ui.Icon(icon, fg, 16), Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center });
            var t = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Foreground = danger ? Ui.Res("B.Danger") : Ui.Res("B.Text"), FontWeight = danger ? FontWeights.SemiBold : FontWeights.Normal };
            Grid.SetColumn(t, 1); g.Children.Add(t);
            if (note != null)
            {
                var n = Ui.Mono(note, Ui.Res("B.TextMute"), 11);
                n.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(n, 2); g.Children.Add(n);
            }
            var b = new Button { Style = Ui.Style("Btn.Row"), Content = g };
            if (danger) b.Background = Ui.Res("B.DangerWash");
            b.Click += (s, e) => { CloseAnimated(); act(); };
            return b;
        }

        private FrameworkElement Toggle(string label, string note, bool value, Action<bool> set, string accent = "B.Mint")
        {
            var g = new Grid { MinHeight = 38, Margin = new Thickness(10, 0, 10, 0) };
            var cb = new CheckBox { Style = Ui.Style("Switch"), IsChecked = value, Tag = Ui.Res(accent), HorizontalAlignment = HorizontalAlignment.Stretch };
            // the note sits under the label so a long one never runs into the switch
            var content = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 3, 8, 3) };
            content.Children.Add(new TextBlock { Text = label, Foreground = Ui.Res("B.Text"), FontSize = 12, FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap });
            if (note != null)
                content.Children.Add(new TextBlock { Text = note, Foreground = Ui.Res("B.TextMute"), FontSize = 11, FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 1, 0, 0) });
            cb.Content = content;
            cb.Checked += (s, e) => set(true);
            cb.Unchecked += (s, e) => set(false);
            g.Children.Add(cb);
            return g;
        }

        private FrameworkElement ChipRow(string icon, string label, (string Label, Action Act)[] chips)
        {
            var sp = new StackPanel { Margin = new Thickness(10, 6, 10, 4) };
            var top = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            top.Children.Add(new ContentControl { Content = Ui.Icon(icon, Ui.Res("B.TextSoft"), 16), Margin = new Thickness(0, 0, 10, 0) });
            top.Children.Add(new TextBlock { Text = label, Foreground = Ui.Res("B.Text"), FontSize = 12, FontFamily = Ui.Font("F.Body"), VerticalAlignment = VerticalAlignment.Center });
            sp.Children.Add(top);
            var wrap = new WrapPanel { Margin = new Thickness(26, 0, 0, 0) };
            foreach (var (l, a) in chips)
            {
                var b = Ui.Button(l, a, "Btn.Chip");
                b.Height = 26; b.Margin = new Thickness(0, 0, 6, 6);
                // the one in use is filled with the accent wash in every look; the others keep a plain outline
                if (l.StartsWith("●")) { b.Background = Ui.Res("B.MintWash"); b.BorderBrush = Ui.Res("B.Mint"); b.Foreground = Ui.Res("B.Text"); AutomationProperties.SetItemStatus(b, "in use"); }
                else { b.Background = Ui.Res("B.Raised"); b.BorderBrush = Ui.Res("B.LineStrong"); b.Foreground = Ui.Res("B.TextSoft"); }
                wrap.Children.Add(b);
            }
            sp.Children.Add(wrap);
            return sp;
        }

        // -------------------------------------------------------------- disc images

        /// <summary>Ejects an image this app mounted, off the interface thread, and says how it went.</summary>
        internal static System.Threading.Tasks.Task EjectFromTray(string image, string name) => System.Threading.Tasks.Task.Run(() =>
        {
            var err = Core.DocTools.DiscImageTools.Dismount(image);
            App.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (err == null) App.Toasts.ShowNotice($"Ejected {name}", "The drive is gone and the image file can be moved or deleted.", App.Toasts.Mint, 4, icon: Dialogs.DocIcons.Eject);
                else App.Toasts.ShowNotice($"Could not eject {name}", err, App.Toasts.Amber, 6, icon: Dialogs.DocIcons.Eject);
            }));
        });

        // -------------------------------------------------------------- service actions

        private async void ToggleElevation(bool on)
        {
            CloseAnimated();
            await System.Threading.Tasks.Task.Delay(150);
            var ok = await System.Threading.Tasks.Task.Run(() => ServiceHost.RequestElevation(on));
            if (!ok)
            {
                App.Toasts.ShowNotice("Administrator rights unchanged", "The prompt was declined or failed.", App.Toasts.Amber, 4, icon: Icons.Shield);
                return;
            }
            Settings.Load();
            ServiceHost.RelaunchForElevationChange();
            App.Current.Quit(ServiceHost.ExitQuit);
        }

        private void Uninstall()
        {
            var dlg = new ConfirmWindow("Uninstall Monitoring Tools?",
                "This stops the background service, removes it from start-up and deletes the program. Clipboard history is never stored, so nothing copied stays behind.",
                "Uninstall", "Also remove settings, logs and scripts", danger: true);
            dlg.Confirmed += removeData =>
            {
                ServiceHost.Uninstall(removeData);
                App.Toasts.ShowNotice("Uninstalled", "Monitoring Tools has been removed. Bye for now.", App.Toasts.Mint, 3, icon: Icons.Trash);
                var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
                t.Tick += (s, e) => { t.Stop(); App.Current.Quit(ServiceHost.ExitQuit); };
                t.Start();
            };
            dlg.Show();
        }

        // -------------------------------------------------------------- motion and placement

        public void ShowAtCursor()
        {
            Opacity = 0;
            Show();
            UpdateLayout();
            var cur = Native.CursorDip(this);
            var area = Ui.WorkArea;
            var left = Math.Min(cur.X - Width + 24, area.Right - Width);
            var top = Math.Min(cur.Y - ActualHeight + 16, area.Bottom - ActualHeight);
            Left = Math.Max(area.Left, left);
            Top = Math.Max(area.Top, top);
            Opacity = 1;
            Activate();
            Native.SetForegroundWindow(new WindowInteropHelper(this).Handle);

            RootScale.ScaleX = RootScale.ScaleY = 0.94;
            RootShift.Y = 14;
            Root.Opacity = 0;
            RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(340)) { EasingFunction = Ui.Spring });
            RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(340)) { EasingFunction = Ui.Spring });
            RootShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(340)) { EasingFunction = Ui.Spring });
            Ui.Fade(Root, 1, 160);
            int i = 0;
            foreach (FrameworkElement r in Rows.Children) Ui.Rise(r, 40 + i++ * 18, 8, 280);
        }

        public void CloseAnimated()
        {
            if (_closing) return;
            _closing = true;
            RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.97, TimeSpan.FromMilliseconds(140)));
            RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.97, TimeSpan.FromMilliseconds(140)));
            Ui.Fade(Root, 0, 140, 0, () => { try { Close(); } catch { } });
        }
    }
}
