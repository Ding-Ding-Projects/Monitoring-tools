using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using MonitoringTools.Clip;
using MonitoringTools.Core;
using MonitoringTools.Net;

namespace MonitoringTools.UI
{
    public partial class OfflineWindow : Window
    {
        public event Action Dismissed;
        private NetworkState _state;
        private readonly DispatcherTimer _clock = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        private bool _closing;
        private bool _restored;

        public OfflineWindow(NetworkState state)
        {
            InitializeComponent();
            _state = state;
            SourceInitialized += (s, e) => Native.MakeToolWindow(this, noActivate: true);
            ElementMenu.Attach(this, null, new ElementMenu.Options { NoActivate = true });
            Loaded += (s, e) => Enter();
            SizeChanged += (s, e) => Place();
            _clock.Tick += (s, e) => TickClock();
            BuildActions();
            Update(state);
        }

        // -------------------------------------------------------------- content

        public void Update(NetworkState s)
        {
            _state = s;
            Headline.Text = s.Headline ?? "Internet connection lost";
            Explain.Text = s.Explanation ?? "";
            BadgeIcon.Content = Ui.Icon(s.Cause == Cause.CaptivePortal ? Icons.Globe : Icons.WifiOff, Ui.Res("B.Amber"), 24);

            Ladder.Children.Clear();
            foreach (var st in s.Stages.Take(4))
            {
                // Theme roles: container fill with its matching "on" colour, so the tiles follow the Appearance theme.
                var (bg, on) = st.Result switch
                {
                    StageResult.Ok => ("B.SecondaryContainer", "B.OnSecondaryContainer"),
                    StageResult.Fail => ("B.DangerWash", "B.Danger"),
                    StageResult.Warn => ("B.AmberWash", "B.Amber"),
                    _ => ("B.Sunken", "B.TextMute")
                };
                var name = new TextBlock { Text = st.Name, FontSize = 12, FontFamily = Ui.Font("F.Body"), Opacity = 0.85 };
                name.SetResourceReference(TextBlock.ForegroundProperty, on);
                var value = new TextBlock { Text = st.Value, FontSize = 13, FontFamily = Ui.Font("F.Mono"), Margin = new Thickness(0, 3, 0, 0) };
                value.SetResourceReference(TextBlock.ForegroundProperty, on);
                var chip = new Border
                {
                    Margin = new Thickness(4, 0, 4, 0), Padding = new Thickness(12, 10, 12, 10), CornerRadius = new CornerRadius(16),
                    Child = new StackPanel { Children = { name, value } }
                };
                chip.SetResourceReference(Border.BackgroundProperty, bg);
                Ladder.Children.Add(chip);
            }

            DetailsGrid.Children.Clear();
            var details = s.Details.ToList();
            if (s.DownSince.HasValue) details.Add(("Down since", s.DownSince.Value.ToString("HH:mm:ss")));
            foreach (var (label, value) in details)
            {
                var amber = label == "Down since" || (label == "DNS servers" && s.Cause == Cause.DnsFailing);
                DetailsGrid.Children.Add(new StackPanel
                {
                    Margin = new Thickness(0, 0, 12, 10),
                    Children =
                    {
                        new TextBlock { Text = label, FontSize = 12, Foreground = Ui.Res("B.TextMute"), FontFamily = Ui.Font("F.Body") },
                        new TextBlock { Text = value, FontSize = 12, Foreground = Ui.Res(amber ? "B.Amber" : "B.Text"), FontFamily = Ui.Font("F.Mono"),
                                        TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 2, 0, 0), ToolTip = value }
                    }
                });
            }

            BuildActions();
        }

        private void BuildActions()
        {
            Actions.Children.Clear();
            Actions.Children.Add(Ui.Button("Retry now", Retry, "Btn.Alert", Icons.Restart));
            if (_state?.Cause == Cause.CaptivePortal)
                Actions.Children.Add(Ui.Button("Open sign-in page", () => Markdown.Open("http://www.msftconnecttest.com/redirect"), "Btn.Ghost", Icons.Globe));
            Actions.Children.Add(Ui.Button("Release & renew", ReleaseRenew, "Btn.Ghost", Icons.Bolt));
            Actions.Children.Add(Ui.Button("Flush DNS", FlushDns));
            Actions.Children.Add(Ui.Button("Network settings", () => Markdown.Open("ms-settings:network-status")));
            Actions.Children.Add(Ui.Button("Copy details", () =>
            {
                ClipboardWatcher.SetText(_state?.DetailsText() ?? "");
                Status.Text = "Details copied.";
            }));

            DnsChips.Children.Clear();
            var active = DnsSwitcher.ActiveProfileName();
            foreach (var p in Settings.Current.DnsProfiles)
            {
                var prof = p;
                var b = Ui.Button(p.Name, () => SwitchDns(prof), "Btn.Chip");
                b.ToolTip = p.ToString();
                if (p.Name == active) { b.Background = Ui.Res("B.Mint"); b.Foreground = Ui.Res("B.MintInk"); }
                else if (_state?.Cause != Cause.DnsFailing) { b.Background = Ui.Res("B.Raised"); b.BorderBrush = Ui.Res("B.LineStrong"); b.Foreground = Ui.Res("B.TextSoft"); }
                DnsChips.Children.Add(b);
            }
        }

        private void Retry()
        {
            Status.Text = "Probing…";
            App.Net.ProbeNow();
        }

        private async void ReleaseRenew()
        {
            Status.Text = ServiceHost.IsElevated ? "Releasing and renewing the IP address…" : "Approve the prompt to release and renew the IP address…";
            var rc = await Elevated.RunAsync("ipconfig /release", "ipconfig /renew", "ipconfig /flushdns");
            Status.Text = rc == -2 ? "Cancelled at the prompt." : rc == 0 ? "Released and renewed. Checking again…" : $"ipconfig finished with code {rc}. Checking again…";
            App.Net.ProbeNow();
        }

        private async void FlushDns()
        {
            Status.Text = "Flushing the DNS cache…";
            // flushdns works without admin
            var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ipconfig", "/flushdns") { UseShellExecute = false, CreateNoWindow = true });
            await p.WaitForExitAsync();
            Status.Text = "DNS cache flushed. Checking again…";
            App.Net.ProbeNow();
        }

        private async void SwitchDns(DnsProfile p)
        {
            Status.Text = ServiceHost.IsElevated ? $"Switching DNS to {p.Name}…" : $"Approve the prompt to switch DNS to {p.Name}…";
            var rc = await DnsSwitcher.ApplyAsync(p);
            Status.Text = rc == -2 ? "Cancelled at the prompt." : rc == 0 ? $"DNS is now {p.Name}. Checking again…" : $"netsh finished with code {rc}.";
            App.Net.ProbeNow();
            BuildActions();
        }

        // -------------------------------------------------------------- motion

        private void Enter()
        {
            Place();
            bool still = Core.Appearance.ReduceMotion || Core.Modes.ReduceMotion;
            if (still)
            {
                Root.Opacity = 1;
                BarScale.ScaleX = 1;
                Ring1.Opacity = 0; Ring2.Opacity = 0;
            }
            else
            {
                RootShift.Y = 40;
                Root.Opacity = 0;
                RootShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(40, 0, TimeSpan.FromMilliseconds(560)) { EasingFunction = Ui.Out });
                Ui.Fade(Root, 1, 260);
                int i = 0;
                foreach (FrameworkElement c in Ladder.Children) Ui.Rise(c, 160 + i++ * 60, 10);
                foreach (FrameworkElement c in Actions.Children) Ui.Rise(c, 320 + i++ * 30, 8);
                Pulse(Ring1Scale, Ring1, 0);
                Pulse(Ring2Scale, Ring2, 1000);
                BarScale.BeginAnimation(ScaleTransform.ScaleXProperty,
                    new DoubleAnimation(0.08, 1, TimeSpan.FromMilliseconds(1400)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() });
            }
            _clock.Start();
            Sounds.Play(Cue.Warning);
        }

        private static void Pulse(ScaleTransform s, UIElement ring, double delay)
        {
            var d = TimeSpan.FromMilliseconds(2000);
            var grow = new DoubleAnimation(1, 1.4, d) { RepeatBehavior = RepeatBehavior.Forever, BeginTime = TimeSpan.FromMilliseconds(delay), EasingFunction = Ui.Out };
            s.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            s.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
            ring.BeginAnimation(OpacityProperty, new DoubleAnimation(0.7, 0, d) { RepeatBehavior = RepeatBehavior.Forever, BeginTime = TimeSpan.FromMilliseconds(delay) });
        }

        private int _topmostTick;

        private void TickClock()
        {
            if (_restored) return;
            if (_state?.DownSince != null)
            {
                var down = DateTime.Now - _state.DownSince.Value;
                Countdown.Text = $"down {(int)down.TotalMinutes} m {down.Seconds:00} s · next probe {App.Net.SecondsUntilNextProbe} s";
            }
            // other topmost windows can cover us; reassert every couple of seconds
            if (++_topmostTick % 4 == 0) Native.ForceTopmost(this);
        }

        private void Place()
        {
            var area = SystemParameters.WorkArea;
            Left = area.Right - Width;
            Top = area.Bottom - ActualHeight;
            App.Toasts.ReservedBottom = Math.Max(0, ActualHeight - 24);
            App.Toasts.Restack();
        }

        public void ShowRestored(NetworkState s)
        {
            _restored = true;
            Sounds.Play(Cue.Restored);
            var mint = Ui.C("C.Mint");
            foreach (var b in new[] { Ring1Brush, Ring2Brush, BarBrush }) Ui.ColorTo(b, mint, 400);
            Ui.ColorTo(EdgeBrush, Ui.C("C.MintWash"), 400);
            Ui.ColorTo(BadgeBrush, Ui.C("C.MintWash"), 400);
            BadgeIcon.Content = Ui.Icon(Icons.Check, Ui.Res("B.Mint"), 24, 3);
            Ui.Rise((FrameworkElement)BadgeIcon, 0, 6);
            Headline.Text = "Back online";
            var took = s.DownSince.HasValue ? DateTime.Now - s.DownSince.Value : TimeSpan.Zero;
            Explain.Text = took > TimeSpan.Zero ? $"Offline for {(int)took.TotalMinutes} m {took.Seconds:00} s." : "The connection is back.";
            Ui.Fade(Body, 0, 200, 0, () =>
            {
                Body.BeginAnimation(MaxHeightProperty, new DoubleAnimation(Body.ActualHeight, 0, TimeSpan.FromMilliseconds(360)) { EasingFunction = Ui.Out });
            });
            BarScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, 0, TimeSpan.FromSeconds(4)));
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            t.Tick += (o, e) => { t.Stop(); CloseAnimated(); };
            t.Start();
        }

        private void OnDismiss(object sender, RoutedEventArgs e)
        {
            Dismissed?.Invoke();
            CloseAnimated();
        }

        public void CloseAnimated()
        {
            if (_closing) return;
            _closing = true;
            _clock.Stop();
            var a = new DoubleAnimation(0, 50, TimeSpan.FromMilliseconds(260)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
            RootShift.BeginAnimation(TranslateTransform.YProperty, a);
            Ui.Fade(Root, 0, 240, 0, () =>
            {
                App.Toasts.ReservedBottom = 0;
                App.Toasts.Restack();
                Close();
            });
        }
    }
}
