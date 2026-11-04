using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MonitoringTools.Core;
using MonitoringTools.Media;

namespace MonitoringTools.UI
{
    /// <summary>
    /// First-run setup. Each step performs real setup (start-up registration, a test toast,
    /// a live connection check, a DNS switch, a test conversion) and shows the actual result.
    /// Progress is saved after every move, so closing and reopening resumes at the same step.
    /// </summary>
    public partial class SetupWizardWindow : Window
    {
        private sealed class Step
        {
            public string Name, Title, Intro;
            public bool Optional;
            public Func<FrameworkElement> Build;
            public Func<string> Apply;              // runs on Next; returns a result line, or null when nothing changed
        }

        private readonly List<Step> _steps = new List<Step>();
        private readonly Dictionary<int, string> _results = new Dictionary<int, string>();
        private int _index;
        private bool _closing;

        public SetupWizardWindow()
        {
            InitializeComponent();
            Define();
            _index = Math.Clamp(Settings.Current.OnboardingStep, 0, _steps.Count - 1);
            SourceInitialized += (s, e) => Native.ApplyDarkChrome(this);
            Loaded += (s, e) => { Enter(); Show(_index, 0); };
            PreviewKeyDown += OnKeys;
        }

        // -------------------------------------------------------------- steps

        private void Define()
        {
            var s = Settings.Current;

            _steps.Add(new Step
            {
                Name = "Start-up", Title = "Start with Windows", Intro = "Monitoring Tools runs quietly in the tray. Choose whether it starts when you sign in.",
                Build = () =>
                {
                    var sp = new StackPanel();
                    sp.Children.Add(Switch("Start when I sign in", "Recommended. You can change this later in the control centre.", s.StartWithWindows, v => s.StartWithWindows = v));
                    sp.Children.Add(Note($"Installed at {Core.PathDisplay.Shown(ServiceHost.ServiceExe)}"));
                    return sp;
                },
                Apply = () =>
                {
                    Settings.Save();
                    ServiceHost.ApplyAutostart();
                    return s.StartWithWindows ? "Starts at sign-in" : "Will not start at sign-in";
                }
            });

            _steps.Add(new Step
            {
                Name = "Clipboard", Title = "Clipboard toasts", Intro = "Every copy shows a toast in the bottom-right corner. Hover it to hold it and see actions.",
                Build = () =>
                {
                    var sp = new StackPanel();
                    sp.Children.Add(Switch("Show clipboard toasts", null, s.ClipboardEnabled, v => s.ClipboardEnabled = v));
                    sp.Children.Add(Switch("Keep open while hovered", null, s.KeepOpenOnHover, v => s.KeepOpenOnHover = v));
                    sp.Children.Add(Switch("Skip copies from password managers", "Private copies never pop up or reach history.", s.IgnoreSensitiveSources, v => s.IgnoreSensitiveSources = v));
                    sp.Children.Add(Chips("Toast duration", new[] { ("2 s", 2.0), ("3 s", 3.0), ("5 s", 5.0), ("8 s", 8.0) }, s.ToastSeconds, v => s.ToastSeconds = v));
                    var result = Note("");
                    sp.Children.Add(Ui.Button("Show me a toast", () =>
                    {
                        App.Toasts.ShowClip(MonitoringTools.Clip.ClipItem.FromText(Demo.SampleUrl));
                        result.Text = "✓ A link toast appeared in the corner. Hover it to see its actions.";
                        result.Foreground = Ui.Res("B.Mint");
                    }, "Btn.Ghost", Icons.Play));
                    sp.Children.Add(result);
                    return sp;
                },
                Apply = () => { Settings.Save(); return s.ClipboardEnabled ? $"Toasts on, {s.ToastSeconds:0} s" : "Toasts off"; }
            });

            _steps.Add(new Step
            {
                Name = "Connection", Title = "Internet watch", Intro = "When the connection drops, a dialog explains where it broke and stays until it is back.",
                Build = () =>
                {
                    var sp = new StackPanel();
                    sp.Children.Add(Switch("Alert me when the internet drops", null, s.NetworkEnabled, v => s.NetworkEnabled = v));
                    sp.Children.Add(Switch("Watch my public IP address", "A dialog on every change, and a history with first and last seen times.", s.PublicIpEnabled, v => s.PublicIpEnabled = v, "B.Violet"));
                    var result = Note("");
                    var check = Ui.Button("Check my connection now", null, "Btn.Ghost", Icons.Wifi);
                    check.Click += async (o, e) =>
                    {
                        check.IsEnabled = false;
                        result.Text = "Checking adapter, gateway, DNS and the internet…";
                        result.Foreground = Ui.Res("B.TextMute");
                        var st = await App.Net.Probe();
                        var ladder = string.Join("  ·  ", st.Stages.Select(x => $"{x.Name} {x.Value}"));
                        string ip = null;
                        try { await App.PublicIp.CheckAsync(); ip = App.PublicIp.History.Current; } catch { }
                        result.Text = (st.Online ? "✓ Online. " : $"Offline: {st.Headline}. ") + ladder + (ip != null ? $"\nPublic IP {ip}" : "");
                        result.Foreground = Ui.Res(st.Online ? "B.Mint" : "B.Amber");
                        check.IsEnabled = true;
                    };
                    sp.Children.Add(check);
                    sp.Children.Add(result);
                    return sp;
                },
                Apply = () => { Settings.Save(); return (s.NetworkEnabled ? "Outage alerts on" : "Outage alerts off") + (s.PublicIpEnabled ? ", public IP watched" : ""); }
            });

            _steps.Add(new Step
            {
                Name = "DNS", Title = "DNS servers", Optional = true,
                Intro = "Optional. Pick who answers your DNS lookups. Switching asks for administrator approval once.",
                Build = () =>
                {
                    var sp = new StackPanel();
                    sp.Children.Add(Note($"Now using {DnsSwitcher.ActiveProfileName() ?? "unknown"}: {DnsSwitcher.CurrentServers()}"));
                    var result = Note("");
                    var wrap = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
                    foreach (var p in s.DnsProfiles)
                    {
                        var prof = p;
                        var b = Ui.Button(p.Name, null, "Btn.Chip");
                        b.ToolTip = p.ToString();
                        b.Click += async (o, e) =>
                        {
                            result.Text = $"Switching to {prof.Name}…";
                            var rc = await DnsSwitcher.ApplyAsync(prof);
                            result.Text = rc == 0 ? $"✓ DNS is now {prof.Name} ({prof})" : rc == -2 ? "Cancelled at the approval prompt. Nothing changed." : $"netsh finished with code {rc}.";
                            result.Foreground = Ui.Res(rc == 0 ? "B.Mint" : "B.Amber");
                            if (rc == 0) _results[_index] = $"DNS set to {prof.Name}";
                        };
                        wrap.Children.Add(b);
                    }
                    sp.Children.Add(wrap);
                    sp.Children.Add(result);
                    return sp;
                },
                Apply = () => _results.TryGetValue(_index, out var r) ? r : "DNS left as it was"
            });

            _steps.Add(new Step
            {
                Name = "Game mode", Title = "Game mode", Intro = "Popups wait while you play. On by default.",
                Build = () =>
                {
                    var sp = new StackPanel();
                    sp.Children.Add(Switch("Hold popups while a game runs", null, s.GameModeEnabled, v => s.GameModeEnabled = v, "B.Violet"));
                    sp.Children.Add(Switch("Any fullscreen app counts", "Fullscreen games, video and presentations.", s.GameModeFullscreen, v => s.GameModeFullscreen = v, "B.Violet"));
                    sp.Children.Add(Note("Watching: " + string.Join(", ", s.GameProcesses.Select(g => Game.GameDetector.Friendly(g.Split(':')[0])).Distinct().Take(10)) + ". Edit the list in the control centre."));
                    return sp;
                },
                Apply = () => { Settings.Save(); App.Games.Check(); return s.GameModeEnabled ? "Game mode on" : "Game mode off"; }
            });

            _steps.Add(new Step
            {
                Name = "Commands", Title = "Quick commands", Optional = true,
                Intro = "Optional. Copy a path with :mp4, :gif or :push on the end and it runs. Set the commit message :push uses, and try a conversion.",
                Build = () =>
                {
                    var sp = new StackPanel();
                    sp.Children.Add(new TextBlock { Text = "Commit message for :push", Style = Ui.Style("T.Body"), Foreground = Ui.Res("B.TextMute"), FontSize = 12, Margin = new Thickness(0, 0, 0, 6) });
                    var box = new TextBox { Style = Ui.Style("Input"), Text = string.IsNullOrWhiteSpace(s.CommitTemplate) ? GitPush.SuggestedTemplate : s.CommitTemplate, Height = 40, FontSize = 13 };
                    AutomationProperties.SetName(box, "Commit message for :push");
                    var preview = Note("");
                    void Refresh() => preview.Text = "Preview: " + GitPush.Expand(box.Text, @"C:\Code\my-repo", "main", 3);
                    box.TextChanged += (o, e) => { s.CommitTemplate = box.Text.Trim(); Refresh(); };
                    Refresh();
                    s.CommitTemplate = box.Text.Trim();
                    sp.Children.Add(box);
                    var chips = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
                    foreach (var (token, meaning) in GitPush.Snippets)
                    {
                        var t = token;
                        var b = Ui.Button(t, () => { var at = box.CaretIndex; box.Text = box.Text.Insert(at, t); box.CaretIndex = at + t.Length; box.Focus(); }, "Btn.Chip");
                        b.ToolTip = meaning;
                        chips.Children.Add(b);
                    }
                    sp.Children.Add(chips);
                    sp.Children.Add(preview);

                    var result = Note("");
                    var conv = Ui.Button("Try a conversion", () =>
                    {
                        var ff = Converter.FindFfmpeg();
                        Demo.TryQuickCommand();
                        result.Text = $"✓ Converting a sample picture to JPG. FFmpeg: {(ff == null ? "not needed for this one" : Converter.FfmpegOrigin)}.";
                        result.Foreground = Ui.Res("B.Mint");
                        _results[_index] = "Conversion tried";
                    }, "Btn.Ghost", Icons.Bolt);
                    conv.Margin = new Thickness(0, 14, 0, 0);
                    sp.Children.Add(conv);
                    sp.Children.Add(result);
                    return sp;
                },
                Apply = () => { Settings.Save(); return "Commit message set"; }
            });

            _steps.Add(new Step
            {
                Name = "Done", Title = "All set", Intro = "Here is what was set up. Everything can be changed later from the control centre.",
                Build = () =>
                {
                    var sp = new StackPanel();
                    for (int i = 0; i < _steps.Count - 1; i++)
                    {
                        var ok = _results.TryGetValue(i, out var r);
                        var row = new Grid { Margin = new Thickness(0, 0, 0, 10) };
                        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
                        row.ColumnDefinitions.Add(new ColumnDefinition());
                        row.Children.Add(Ui.IconChip(ok ? Icons.Check : Icons.Clock, Ui.Res(ok ? "B.Mint" : "B.TextMute"), Ui.Res(ok ? "B.MintWash" : "B.Raised"), 24, 12));
                        var n = new TextBlock { Text = _steps[i].Name, Style = Ui.Style("T.Title"), FontSize = 14, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
                        Grid.SetColumn(n, 1); row.Children.Add(n);
                        var v = new TextBlock { Text = ok ? r : (_steps[i].Optional ? "Skipped" : "Set earlier"), Style = Ui.Style("T.Body"), VerticalAlignment = VerticalAlignment.Center };
                        Grid.SetColumn(v, 2); row.Children.Add(v);
                        sp.Children.Add(row);
                    }
                    return sp;
                },
                Apply = () => "Finished"
            });
        }

        // -------------------------------------------------------------- navigation

        private void Show(int idx, int direction)
        {
            _index = idx;
            Settings.Current.OnboardingStep = idx;
            Settings.Save();
            var st = _steps[idx];
            StepEyebrow.Text = $"STEP {idx + 1} OF {_steps.Count}{(st.Optional ? " · OPTIONAL" : "")}";
            StepTitle.Text = st.Title;
            StepIntro.Text = st.Intro;
            var body = st.Build();
            StepBody.Content = body;
            BackBtn.Visibility = idx == 0 ? Visibility.Hidden : Visibility.Visible;
            SkipBtn.Visibility = st.Optional ? Visibility.Visible : Visibility.Collapsed;
            NextBtn.Content = idx == _steps.Count - 1 ? "Open control centre" : "Next";
            var width = ((FrameworkElement)ProgressFill.Parent).ActualWidth;
            if (Core.Appearance.ReduceMotion || Core.Modes.ReduceMotion) { ProgressFill.BeginAnimation(WidthProperty, null); ProgressFill.Width = width * (idx + 1) / _steps.Count; }
            else ProgressFill.BeginAnimation(WidthProperty, new DoubleAnimation(width * (idx + 1) / _steps.Count, TimeSpan.FromMilliseconds(420)) { EasingFunction = Ui.Out });
            BuildRail();

            if (direction != 0)
            {
                var shift = new TranslateTransform(direction * 30, 0);
                body.RenderTransform = shift;
                body.Opacity = 0;
                shift.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(360)) { EasingFunction = Ui.Spring });
                Ui.Fade(body, 1, 220);
            }
            else Ui.Rise(body, 60, 10, 360);
            Ui.Rise(StepTitle, 0, 6, 300);
            Dispatcher.BeginInvoke(new Action(() => body.MoveFocus(new TraversalRequest(FocusNavigationDirection.First))), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void BuildRail()
        {
            Rail.Children.Clear();
            for (int i = 0; i < _steps.Count; i++)
            {
                int idx = i;
                bool done = i < _index, now = i == _index;
                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition());
                var dot = new Border
                {
                    Width = 28, Height = 28, CornerRadius = new CornerRadius(14),
                    Background = done ? Ui.Res("B.Mint") : now ? Ui.Res("B.MintWash") : Ui.Res("B.Raised"),
                    BorderBrush = now ? Ui.Res("B.Mint") : Ui.Res("B.LineStrong"), BorderThickness = new Thickness(now ? 2 : 1),
                    Child = done ? (UIElement)Ui.Icon(Icons.Check, Ui.Res("B.MintInk"), 11, 3)
                                 : new TextBlock { Text = (i + 1).ToString(), FontSize = 11, FontFamily = Ui.Font("F.Mono"), Foreground = Ui.Res(now ? "B.Mint" : "B.TextMute"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                };
                g.Children.Add(dot);
                var t = new TextBlock { Text = _steps[i].Name + (_steps[i].Optional ? " · optional" : ""), FontSize = 13, FontFamily = Ui.Font("F.Body"), Foreground = Ui.Res(now ? "B.Text" : done ? "B.TextSoft" : "B.TextMute"), FontWeight = now ? FontWeights.SemiBold : FontWeights.Normal, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(t, 1);
                g.Children.Add(t);
                var b = new Button { Style = Ui.Style("Btn.Row"), Content = g, Margin = new Thickness(-8, 0, 0, 4), IsEnabled = i <= Math.Max(_index, Settings.Current.OnboardingStep), HorizontalContentAlignment = HorizontalAlignment.Left };
                AutomationProperties.SetName(b, $"Step {i + 1}: {_steps[i].Name}{(done ? ", done" : now ? ", current" : "")}");
                b.Click += (o, e) => { if (idx != _index) Show(idx, idx > _index ? 1 : -1); };
                Rail.Children.Add(b);
            }
        }

        /// <summary>Used by the screenshot run to show a given step.</summary>
        public void GoTo(int idx) => Show(Math.Clamp(idx, 0, _steps.Count - 1), 1);

        private void OnNext(object sender, RoutedEventArgs e)
        {
            var st = _steps[_index];
            try
            {
                var r = st.Apply?.Invoke();
                if (r != null) _results[_index] = r;
            }
            catch (Exception ex)
            {
                _results.Remove(_index);
                StepIntro.Text = "That step did not finish: " + ex.Message;
                StepIntro.Foreground = Ui.Res("B.Amber");
                return;
            }
            if (_index == _steps.Count - 1) { Finish(); return; }
            Show(_index + 1, 1);
        }

        private void OnSkip(object sender, RoutedEventArgs e)
        {
            if (!_steps[_index].Optional) return;
            _results.Remove(_index);
            Show(_index + 1, 1);
        }

        private void OnBack(object sender, RoutedEventArgs e) { if (_index > 0) Show(_index - 1, -1); }

        private void Finish()
        {
            Settings.Current.OnboardingDone = true;
            Settings.Current.OnboardingStep = 0;
            Settings.Save();
            Activity.Add(ActivityKind.Service, "Setup finished", string.Join(" · ", _results.Values.Take(4)));
            Close();
            App.Current.OpenControlCentre(null);
        }

        private void OnKeys(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { e.Handled = true; Close(); }
            else if (Keyboard.Modifiers == ModifierKeys.Alt && e.SystemKey == Key.Left) { e.Handled = true; OnBack(null, null); }
            else if (Keyboard.Modifiers == ModifierKeys.Alt && e.SystemKey == Key.Right) { e.Handled = true; OnNext(null, null); }
            else if (e.Key == Key.F && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)) { e.Handled = true; CommandPalette.Open(this); }
        }

        // -------------------------------------------------------------- building blocks

        private static TextBlock Note(string text) => new TextBlock
        {
            Text = text, Style = Ui.Style("T.Mono"), Foreground = Ui.Res("B.TextMute"), Margin = new Thickness(0, 10, 0, 0), TextWrapping = TextWrapping.Wrap
        };

        private static FrameworkElement Switch(string label, string sub, bool value, Action<bool> set, string accent = "B.Mint")
        {
            var cb = new CheckBox { Style = Ui.Style("Switch"), IsChecked = value, Tag = Ui.Res(accent), Margin = new Thickness(0, 4, 0, 12) };
            var text = new StackPanel();
            text.Children.Add(new TextBlock { Text = label, Foreground = Ui.Res("B.Text"), FontSize = 14, FontFamily = Ui.Font("F.Body") });
            if (sub != null) text.Children.Add(new TextBlock { Text = sub, Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute"), Margin = new Thickness(0, 2, 0, 0) });
            cb.Content = text;
            AutomationProperties.SetName(cb, label);
            cb.Checked += (s, e) => set(true);
            cb.Unchecked += (s, e) => set(false);
            return cb;
        }

        private static FrameworkElement Chips<T>(string label, IEnumerable<(string Label, T Value)> options, T current, Action<T> set)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 4, 0, 10) };
            sp.Children.Add(new TextBlock { Text = label, Foreground = Ui.Res("B.Text"), FontSize = 14, FontFamily = Ui.Font("F.Body"), Margin = new Thickness(0, 0, 0, 8) });
            var wrap = new WrapPanel();
            var all = new List<(Button, T)>();
            void Paint() { foreach (var (b, v) in all) { bool on = EqualityComparer<T>.Default.Equals(v, current); b.Background = Ui.Res(on ? "B.Mint" : "B.Raised"); b.Foreground = Ui.Res(on ? "B.MintInk" : "B.TextSoft"); b.BorderBrush = Ui.Res(on ? "B.Mint" : "B.LineStrong"); } }
            foreach (var (l, v) in options)
            {
                var val = v;
                var b = Ui.Button(l, null, "Btn.Chip");
                b.Click += (o, e) => { current = val; set(val); Paint(); };
                all.Add((b, v));
                wrap.Children.Add(b);
            }
            Paint();
            sp.Children.Add(wrap);
            return sp;
        }

        // -------------------------------------------------------------- chrome

        private void Enter()
        {
            if (Core.Appearance.ReduceMotion || Core.Modes.ReduceMotion) { Root.Opacity = 1; Activate(); return; }
            RootScale.ScaleX = RootScale.ScaleY = 0.95;
            RootShift.Y = 16;
            Root.Opacity = 0;
            RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(420)) { EasingFunction = Ui.Spring });
            RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(420)) { EasingFunction = Ui.Spring });
            RootShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(420)) { EasingFunction = Ui.Spring });
            Ui.Fade(Root, 1, 240);
            Activate();
        }

        private void OnClose(object sender, RoutedEventArgs e) => Close();

        private void OnDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState != MouseButtonState.Pressed) return;
            var d = e.OriginalSource as DependencyObject;
            while (d != null) { if (d is ButtonBase || d is TextBox) return; d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d); }
            try { DragMove(); } catch { }
        }
    }
}
