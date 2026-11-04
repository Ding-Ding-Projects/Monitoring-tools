using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    public partial class ToastWindow : Window
    {
        public event Action Gone;
        public bool IsClosing { get; private set; }

        private double _seconds;
        private DateTime _deadline;
        private TimeSpan _remaining;
        private bool _paused;
        private bool _persistent;
        private bool _hasActions;
        private bool _expanded;
        private readonly DispatcherTimer _tick = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        private Color _accent;
        private readonly List<(ToastAction Action, Button Button)> _actionButtons = new List<(ToastAction, Button)>();
        private bool _menuOpen;

        /// <summary>Toasts are not shown until this moment passes. Set from the toast menu; it lasts until the app closes.</summary>
        public static DateTime PausedUntil { get; private set; } = DateTime.MinValue;
        public static bool IsPaused => DateTime.Now < PausedUntil;
        public static void PauseFor(TimeSpan span) => PausedUntil = DateTime.Now + span;
        public static void Resume() => PausedUntil = DateTime.MinValue;

        public ToastWindow()
        {
            InitializeComponent();
            _accent = Ui.C("C.Mint");
            SourceInitialized += (s, e) => Native.MakeToolWindow(this, noActivate: true);
            MouseEnter += (s, e) => OnHover(true);
            MouseLeave += (s, e) => OnHover(false);
            _tick.Tick += (s, e) => Tick();
            Loaded += (s, e) => Enter();
            // A toast never takes focus, so the menu lets it be activated while open and holds the countdown meanwhile.
            ElementMenu.Attach(this, _ => MenuItems(), new ElementMenu.Options
            {
                NoActivate = true,
                OnMenu = open => { _menuOpen = open; if (!open) _deadline = DateTime.UtcNow + TimeSpan.FromSeconds(Math.Max(1.4, _remaining.TotalSeconds)); }
            });
        }

        private IEnumerable<ElementMenu.Item> MenuItems()
        {
            foreach (var (a, b) in _actionButtons.ToList())
            {
                var action = a; var button = b;
                yield return new ElementMenu.Item(action.Label, action.Icon ?? Icons.Bolt, null, () => RunAction(button, action));
            }
            yield return new ElementMenu.Item("Dismiss", Icons.Close, null, CloseAnimated);
            yield return new ElementMenu.Item("Pause toasts for 15 minutes", Icons.Pause, null, () => { PauseFor(TimeSpan.FromMinutes(15)); CloseAnimated(); });
        }

        // -------------------------------------------------------------- content

        public void SetAccent(Color accent, Color wash)
        {
            _accent = accent;
            TimerBrush.Color = accent;
            HaloBrush.Color = accent;
        }

        public void SetHeader(string icon, string title, string meta)
        {
            var accent = new SolidColorBrush(_accent);
            var wash = new SolidColorBrush(Color.FromArgb(0x26, _accent.R, _accent.G, _accent.B));
            IconHost.Content = icon == null ? null : Ui.IconChip(icon, accent, wash);
            title = Core.Language.Title(title);
            var (spokenEn, spokenYue) = Core.Language.SpeechParts(title);
            Core.Narrator.Speak(spokenEn, spokenYue);
            TitleText.Text = title;
            MetaText.Text = meta ?? "";
            // The header is rewritten after the window loaded (a job finishing, a task result), so translate it again.
            Core.Language.Refresh(TitleText);
            Core.Language.Refresh(MetaText);
        }

        public void SetBody(UIElement body) => BodyHost.Content = body;

        public void SetHint(string hint)
        {
            HintText.Text = hint ?? "";
            HintText.Visibility = string.IsNullOrEmpty(hint) ? Visibility.Collapsed : Visibility.Visible;
        }

        public void SetWhen(string text) => WhenText.Text = text;

        public void SetActions(IList<ToastAction> actions)
        {
            ActionsPanel.Children.Clear();
            ChipsPanel.Children.Clear();
            _actionButtons.Clear();
            _hasActions = actions != null && actions.Count > 0;
            if (!_hasActions) return;

            // Keep the toast calm: four buttons and one row of chips up front, the rest behind More.
            const int maxButtons = 4, maxChips = 7;
            var buttons = actions.Where(a => !a.Chip).OrderByDescending(a => a.Primary).ToList();
            var chips = actions.Where(a => a.Chip).ToList();
            var overflow = new WrapPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 2, 0, 0) };
            var host = (Panel)ActionsPanel.Parent;
            host.Children.Insert(host.Children.IndexOf(ChipsPanel) + 1, overflow);

            Button Make(ToastAction a)
            {
                var style = a.Chip ? "Btn.Chip" : a.Primary ? "Btn.Accent" : "Btn.Ghost";
                var b = Ui.Button(a.Label, null, style, a.Chip ? null : a.Icon);
                b.Click += (s, e) => RunAction(b, a);
                _actionButtons.Add((a, b));
                if (!string.IsNullOrEmpty(a.Tip))
                {
                    b.MouseEnter += (s, e) => TipText.Text = a.Tip;
                    b.MouseLeave += (s, e) => TipText.Text = "";
                }
                return b;
            }

            foreach (var a in buttons.Take(maxButtons)) ActionsPanel.Children.Add(Make(a));
            foreach (var a in chips.Take(maxChips)) ChipsPanel.Children.Add(Make(a));
            var rest = buttons.Skip(maxButtons).Concat(chips.Skip(maxChips)).ToList();
            foreach (var a in rest) overflow.Children.Add(Make(a));

            if (rest.Count > 0)
            {
                var more = Ui.Button($"More · {rest.Count}", null, "Btn.Ghost");
                more.Foreground = Ui.Res("B.TextMute");
                more.Click += (s, e) =>
                {
                    bool open = overflow.Visibility != Visibility.Visible;
                    overflow.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
                    more.Content = open ? "Less" : $"More · {rest.Count}";
                    if (open) { int i = 0; foreach (FrameworkElement c in overflow.Children) Ui.Rise(c, i++ * 22, 8, 280); }
                };
                ActionsPanel.Children.Add(more);
            }
        }

        /// <summary>Show the action row from the start (used by prompts and finished jobs).</summary>
        public void ExpandNow()
        {
            _expanded = true;
            ActionsHost.MaxHeight = 600;
            ActionsHost.Opacity = 1;
        }

        private async void RunAction(Button b, ToastAction a)
        {
            try
            {
                a.Run?.Invoke();
                if (a.HandsOff) { CloseAnimated(); return; }
                Sounds.Play(Cue.Success);
                b.Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        new ContentControl { Content = Ui.Icon(Icons.Check, b.Foreground, 12, 3), Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center },
                        new TextBlock { Text = a.Done, VerticalAlignment = VerticalAlignment.Center }
                    }
                };
                Ui.Rise(b, 0, 4, 220);
                await System.Threading.Tasks.Task.Delay(750);
                CloseAnimated();
            }
            catch (Exception ex)
            {
                Log.Warn($"Action '{a.Label}' failed: {ex.Message}");
                Sounds.Play(Cue.Warning);
                b.Content = "Failed";
                b.Foreground = Ui.Res("B.Amber");
                TipText.Text = ex.Message;
            }
        }

        // -------------------------------------------------------------- lifetime

        public void StartTimer(double seconds)
        {
            _persistent = seconds <= 0;
            _seconds = Math.Max(0.5, seconds);
            _remaining = TimeSpan.FromSeconds(_seconds);
            _deadline = DateTime.UtcNow + _remaining;
            TimerScale.ScaleX = 1;
            if (_persistent) { TimerFill.Opacity = 0.35; _tick.Stop(); return; }
            TimerFill.Opacity = 1;
            _paused = false;
            _tick.Start();
        }

        public void SetProgress(double p)
        {
            _persistent = true;
            _tick.Stop();
            TimerFill.Opacity = 1;
            TimerScale.BeginAnimation(ScaleTransform.ScaleXProperty,
                new DoubleAnimation(Math.Clamp(p, 0, 1), TimeSpan.FromMilliseconds(260)) { EasingFunction = Ui.Out });
        }

        private void Tick()
        {
            if (_menuOpen) { _deadline += _tick.Interval; return; }
            if (_paused || IsClosing) return;
            var left = _deadline - DateTime.UtcNow;
            TimerScale.ScaleX = Math.Clamp(left.TotalSeconds / _seconds, 0, 1);
            if (left <= TimeSpan.Zero) { _tick.Stop(); CloseAnimated(); }
        }

        private void OnHover(bool over)
        {
            if (IsClosing) return;
            var hold = Settings.Current.KeepOpenOnHover;

            Ui.ColorTo(CardBorder, over ? _accent : Ui.C("C.Line"), 180);
            Ui.Fade(Halo, over ? 0.12 : 0, 200);
            Ui.Fade(CloseBtn, over ? 1 : 0, 160);

            if (!_persistent && hold)
            {
                if (over)
                {
                    _paused = true;
                    _remaining = _deadline - DateTime.UtcNow;
                    Ui.Fade(Held, 1, 160); Ui.Fade(WhenText, 0, 120);
                }
                else
                {
                    _paused = false;
                    // Give a short grace period after the pointer leaves
                    var rest = TimeSpan.FromSeconds(Math.Max(1.4, _remaining.TotalSeconds));
                    _seconds = Math.Max(_seconds, rest.TotalSeconds);
                    _deadline = DateTime.UtcNow + rest;
                    Ui.Fade(Held, 0, 160); Ui.Fade(WhenText, 1, 160);
                }
            }

            if (over && _hasActions && !_expanded) Expand();
        }

        private void Expand()
        {
            _expanded = true;
            ActionsHost.BeginAnimation(MaxHeightProperty, new DoubleAnimation(0, 600, TimeSpan.FromMilliseconds(420)) { EasingFunction = Ui.Out });
            Ui.Fade(ActionsHost, 1, 220);
            int i = 0;
            foreach (FrameworkElement c in ActionsPanel.Children) Ui.Rise(c, 40 + i++ * 28, 8, 300);
            foreach (FrameworkElement c in ChipsPanel.Children) Ui.Rise(c, 40 + i++ * 22, 8, 300);
        }

        private void Enter()
        {
            if (Still) { RootShift.X = 0; Root.Opacity = 1; Native.ForceTopmost(this); return; }
            // Slide in with the decelerate curve (fast start, soft landing).
            RootShift.X = 80;
            Root.Opacity = 0;
            RootShift.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(80, 0, TimeSpan.FromMilliseconds(420)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            Root.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));
            if (BodyHost.Content is FrameworkElement fe) Ui.Rise(fe, 90, 6, 360);
            Native.ForceTopmost(this);
        }

        public void CloseAnimated()
        {
            if (IsClosing) return;
            IsClosing = true;
            _tick.Stop();
            if (Still) { Gone?.Invoke(); Close(); return; }
            var shift = new DoubleAnimation(0, 60, TimeSpan.FromMilliseconds(240)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(220));
            fade.Completed += (s, e) => { Gone?.Invoke(); Close(); };
            RootShift.BeginAnimation(TranslateTransform.XProperty, shift);
            RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.96, TimeSpan.FromMilliseconds(240)));
            Root.BeginAnimation(OpacityProperty, fade);
        }

        private static bool Still => Core.Appearance.ReduceMotion || Core.Modes.ReduceMotion;

        private void OnCloseClick(object sender, RoutedEventArgs e) => CloseAnimated();

        public void SlideTo(double top)
        {
            if (double.IsNaN(Top) || Math.Abs(Top - top) < 0.5 || !IsVisible) { BeginAnimation(TopProperty, null); Top = top; return; }
            BeginAnimation(TopProperty, new DoubleAnimation(Top, top, TimeSpan.FromMilliseconds(320)) { EasingFunction = Ui.Out });
        }

        // -------------------------------------------------------------- drag out

        /// <summary>Lets the element be dragged into any drop target (Explorer, chat apps, editors).</summary>
        public static void EnableDrag(FrameworkElement el, Func<DataObject> make)
        {
            Point start = default;
            bool armed = false;
            el.Cursor = Cursors.SizeAll;
            el.PreviewMouseLeftButtonDown += (s, e) => { start = e.GetPosition(el); armed = true; };
            el.PreviewMouseLeftButtonUp += (s, e) => armed = false;
            el.PreviewMouseMove += (s, e) =>
            {
                if (!armed || e.LeftButton != MouseButtonState.Pressed) return;
                var p = e.GetPosition(el);
                if (Math.Abs(p.X - start.X) < SystemParameters.MinimumHorizontalDragDistance &&
                    Math.Abs(p.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
                armed = false;
                try
                {
                    var data = make();
                    if (data != null) DragDrop.DoDragDrop(el, data, DragDropEffects.Copy | DragDropEffects.Link);
                }
                catch (Exception ex) { Log.Warn("Drag failed: " + ex.Message); }
            };
        }
    }
}
