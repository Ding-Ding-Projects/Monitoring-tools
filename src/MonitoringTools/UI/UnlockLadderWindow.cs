using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    /// <summary>Small helpers shared by the lock windows.</summary>
    internal static class SafetyUi
    {
        public static TextBlock Text(string text, double size = 13, string brush = "B.Text", FontWeight? weight = null, double top = 0)
        {
            var t = new TextBlock { Text = text, FontSize = size, Foreground = Ui.Res(brush), FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, top, 0, 0) };
            if (weight != null) t.FontWeight = weight.Value;
            return t;
        }

        /// <summary>A status line that screen readers announce when it changes.</summary>
        public static TextBlock Live(string name)
        {
            var t = Text("", 13, "B.TextSoft", null, 8);
            AutomationProperties.SetLiveSetting(t, AutomationLiveSetting.Polite);
            AutomationProperties.SetName(t, name);
            return t;
        }

        public static (PasswordBox Box, Border Frame) Pass(string name)
        {
            var box = new PasswordBox
            {
                Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Ui.Res("B.Text"), CaretBrush = Ui.Res("B.Mint"),
                FontFamily = Ui.Font("F.Mono"), FontSize = 14, VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(0)
            };
            var t = new ControlTemplate(typeof(PasswordBox));
            var host = new FrameworkElementFactory(typeof(ScrollViewer), "PART_ContentHost");
            host.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            t.VisualTree = host;
            box.Template = t;
            AutomationProperties.SetName(box, name);
            var frame = new Border { Height = 42, CornerRadius = new CornerRadius(12), Background = Ui.Res("B.Sunken"), BorderBrush = Ui.Res("B.LineStrong"), BorderThickness = new Thickness(1), Padding = new Thickness(12, 0, 12, 0), Child = box, Margin = new Thickness(0, 6, 0, 6) };
            box.GotKeyboardFocus += (s, e) => frame.BorderBrush = Ui.Res("B.Mint");
            box.LostKeyboardFocus += (s, e) => frame.BorderBrush = Ui.Res("B.LineStrong");
            return (box, frame);
        }

        public static string Clock(int seconds) => $"{seconds / 60}:{seconds % 60:00}";

        /// <summary>
        /// The Windows Hello route as a small block: a button when Windows Hello is available, otherwise the exact availability state.
        /// It stays hidden when the person turned the route off in Settings. The Hello dialog can hide behind a topmost window, so the
        /// host drops its topmost flag while the dialog is open.
        /// </summary>
        public static StackPanel HelloRow(Window host, string label, string reason, Action onVerified, TextBlock msg)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed };
            var btn = Ui.Button(label, null, "Btn.Ghost", Icons.Shield);
            btn.ToolTip = "Asks Windows Hello to confirm that it is you. " + WindowsHello.Disclosure;
            var note = Text("", 12, "B.TextMute", null, 4);
            panel.Children.Add(btn); panel.Children.Add(note);
            async void Check()
            {
                if (!Settings.Current.LockOfferWindowsHello) { panel.Visibility = Visibility.Collapsed; return; }
                var (ok, state) = await WindowsHello.CheckAsync();
                if (!host.IsLoaded) return;
                panel.Visibility = Visibility.Visible;
                btn.Visibility = ok ? Visibility.Visible : Visibility.Collapsed;
                note.Text = ok ? WindowsHello.Disclosure : state;
            }
            btn.Click += async (s, e) =>
            {
                btn.IsEnabled = false;
                bool top = host.Topmost; host.Topmost = false;
                var r = await WindowsHello.VerifyAsync(reason);
                if (!host.IsLoaded) return;
                host.Topmost = top; btn.IsEnabled = true;
                if (r.Verified) onVerified();
                else { msg.Text = r.Message; msg.Foreground = Ui.Res("B.Amber"); }
            };
            host.Loaded += (s, e) => Check();
            return panel;
        }
    }

    /// <summary>A non-modal card that opens beside the element it belongs to and hands focus back when it closes.</summary>
    public abstract class AnchoredCard : Window
    {
        private readonly FrameworkElement _origin;
        private readonly Border _frame;

        protected AnchoredCard(FrameworkElement origin, string name)
        {
            _origin = origin;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            ShowInTaskbar = false; Topmost = true; ResizeMode = ResizeMode.NoResize; SizeToContent = SizeToContent.WidthAndHeight;
            var owner = origin == null ? null : GetWindow(origin);
            if (owner != null && owner.IsVisible) Owner = owner;
            _frame = new Border
            {
                Background = Ui.Res("B.Raised"), BorderBrush = Ui.Res("B.LineStrong"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(28),
                Padding = new Thickness(20), Margin = new Thickness(14), Width = 400,
                Effect = (System.Windows.Media.Effects.Effect)Application.Current.Resources["Fx.Shadow"]
            };
            AutomationProperties.SetName(_frame, name);
            Content = _frame;
            // Covers the lock prompt, the lock wizard, the unlock ladder and the QR window. Copy text only, so no entry can press a step or lock the lock screens themselves.
            ElementMenu.Attach(this, null, ElementMenu.Options.Copy);
            PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape) { e.Handled = true; Close(); } };
            Loaded += (s, e) => Place();
            Closed += (s, e) => { try { _origin?.Focus(); } catch { } };
        }

        protected void SetBody(UIElement body) => _frame.Child = body;

        private void Place()
        {
            var area = SystemParameters.WorkArea;
            double x = area.Left + (area.Width - ActualWidth) / 2, y = area.Top + (area.Height - ActualHeight) / 3;
            try
            {
                var src = _origin == null ? null : PresentationSource.FromVisual(_origin);
                if (src != null && _origin.IsVisible)
                {
                    var p = src.CompositionTarget.TransformFromDevice.Transform(_origin.PointToScreen(new Point(0, _origin.ActualHeight)));
                    x = p.X - 14; y = p.Y - 6;
                }
            }
            catch { }
            Left = Math.Max(area.Left, Math.Min(x, area.Right - ActualWidth));
            Top = Math.Max(area.Top, Math.Min(y, area.Bottom - ActualHeight));
            Activate();
        }
    }

    /// <summary>The unlock prompt for one lock: ordered steps, a PIN keypad that shares the manual validator, the wait, and the ways out.</summary>
    public sealed class LockPromptWindow : AnchoredCard
    {
        private readonly LockEntry _e;
        private readonly Locks.Session _session;
        private readonly Action _done;
        private readonly TextBlock _step = SafetyUi.Text("", 13, "B.TextSoft", null, 6);
        private readonly TextBlock _msg = SafetyUi.Live("Unlock status");
        private readonly PasswordBox _pw;
        private readonly Border _pwFrame;
        private readonly TextBox _code = new TextBox { Style = Ui.Style("Input"), Margin = new Thickness(0, 6, 0, 6), MaxLength = 8 };
        private readonly StackPanel _keypadHost = new StackPanel();
        private readonly StackPanel _wait = new StackPanel { Visibility = Visibility.Collapsed };
        private readonly TextBlock _waitText = SafetyUi.Text("", 14, "B.Amber", FontWeights.SemiBold);
        private readonly Button _ladderBtn;
        private readonly Button _verify;
        private readonly DispatcherTimer _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        private bool _shuffle;

        public LockPromptWindow(LockEntry entry, FrameworkElement origin, Action unlocked) : base(origin, "Unlock " + entry.Label)
        {
            _e = entry; _done = unlocked; _session = new Locks.Session(entry);
            (_pw, _pwFrame) = SafetyUi.Pass("PIN or password");
            AutomationProperties.SetName(_code, "Authenticator code");

            var body = new StackPanel();
            body.Children.Add(new TextBlock { Text = "UNLOCK", Style = Ui.Style("T.Eyebrow"), FontSize = 11 });
            var label = entry.Label.Length > 70 ? entry.Label.Substring(0, 70) + "…" : entry.Label;
            body.Children.Add(new TextBlock { Text = label, Style = Ui.Style("T.Title"), FontSize = 16, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
            body.Children.Add(SafetyUi.Text(Locks.Describe(entry.Policy) + " lock", 12, "B.TextMute", null, 2));
            body.Children.Add(_step);
            body.Children.Add(_pwFrame);
            body.Children.Add(_code);
            body.Children.Add(_keypadHost);
            body.Children.Add(_msg);

            _verify = Ui.Button("Unlock", Submit, "Btn.Accent", Icons.Check);
            var cancel = Ui.Button("Cancel", Close, "Btn.Ghost");
            var row = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            row.Children.Add(_verify); row.Children.Add(cancel);
            body.Children.Add(row);
            body.Children.Add(SafetyUi.HelloRow(this, "Unlock with Windows Hello", "Unlock " + entry.Label, () => { Locks.OpenWithHello(_e.Id); Close(); _done?.Invoke(); }, _msg));

            _wait.Children.Add(_waitText);
            _ladderBtn = Ui.Button("Play your way out", () => OpenLadder(), "Btn.Ghost", Icons.Game);
            _ladderBtn.ToolTip = "Win a few quick games to end the wait early. It never skips your credential. At most " + Locks.LadderPerHour + " waits can be skipped per hour.";
            _wait.Children.Add(_ladderBtn);
            body.Children.Add(_wait);

            var forgot = Ui.Button("Forgotten your password?", () => { App.Current.OpenControlCentre("safety"); Close(); }, "Btn.Chip", Icons.Help);
            forgot.ToolTip = "Opens Support Tickets, the local recovery desk";
            body.Children.Add(forgot);
            body.Children.Add(SafetyUi.Text(Locks.Disclosure, 12, "B.TextSoft", null, 4));
            body.Children.Add(SafetyUi.Text(Locks.RecoveryLine, 12, "B.TextMute", null, 4));
            SetBody(body);

            _pw.KeyDown += (s, e) => { if (e.Key == Key.Enter) { e.Handled = true; Submit(); } };
            _code.KeyDown += (s, e) => { if (e.Key == Key.Enter) { e.Handled = true; Submit(); } };
            _timer.Tick += (s, e) => RefreshWait();
            Closed += (s, e) => _timer.Stop();
            Loaded += (s, e) => { ShowStep(); RefreshWait(); _timer.Start(); };
        }

        private void ShowStep()
        {
            var f = _session.Current;
            _step.Text = $"Step {Math.Min(_session.Step + 1, _session.StepCount)} of {_session.StepCount}: enter your {Locks.Describe(f).ToLowerInvariant()}";
            _pw.Password = ""; _code.Text = "";
            bool pass = f != LockFactor.Totp;
            _pwFrame.Visibility = pass ? Visibility.Visible : Visibility.Collapsed;
            _code.Visibility = pass ? Visibility.Collapsed : Visibility.Visible;
            AutomationProperties.SetName(_pw, f == LockFactor.Pin ? "PIN" : "Password");
            BuildKeypad(f == LockFactor.Pin);
            (pass ? (UIElement)_pw : _code).Focus();
        }

        private void BuildKeypad(bool show)
        {
            _keypadHost.Children.Clear();
            if (!show) return;
            var digits = Enumerable.Range(0, 10).Select(i => i.ToString()).ToList();
            if (_shuffle) digits = digits.OrderBy(_ => Guid.NewGuid()).ToList();
            var grid = new UniformGrid { Columns = 3, Margin = new Thickness(0, 4, 0, 4) };
            Button Key(string text, string name, Action act)
            {
                var b = new Button { Content = text, Style = Ui.Style("Btn.Ghost"), Height = 44, Margin = new Thickness(3), FontSize = 16, MinWidth = 56 };
                AutomationProperties.SetName(b, name);
                b.Click += (s, e) => { act(); _pw.Focus(); };
                return b;
            }
            for (int i = 0; i < 9; i++) { var d = digits[i]; grid.Children.Add(Key(d, "Digit " + d, () => { if (_pw.Password.Length < 8) _pw.Password += d; })); }
            grid.Children.Add(Key("⌫", "Backspace", () => { if (_pw.Password.Length > 0) _pw.Password = _pw.Password.Substring(0, _pw.Password.Length - 1); }));
            var last = digits[9]; grid.Children.Add(Key(last, "Digit " + last, () => { if (_pw.Password.Length < 8) _pw.Password += last; }));
            grid.Children.Add(Key("Clear", "Clear", () => _pw.Password = ""));
            _keypadHost.Children.Add(grid);
            var shuffle = new CheckBox { Content = "Shuffle the keys", IsChecked = _shuffle, Foreground = Ui.Res("B.TextSoft"), Margin = new Thickness(3, 0, 0, 4), ToolTip = "Randomises the keypad layout each time. Off by default." };
            shuffle.Checked += (s, e) => { _shuffle = true; BuildKeypad(true); };
            shuffle.Unchecked += (s, e) => { _shuffle = false; BuildKeypad(true); };
            _keypadHost.Children.Add(shuffle);
        }

        private void Submit()
        {
            var f = _session.Current;
            var value = f == LockFactor.Totp ? _code.Text : _pw.Password;
            var r = _session.Submit(value);
            _msg.Text = r.Message;
            _msg.Foreground = Ui.Res(r.Status == SubmitStatus.Next || r.Status == SubmitStatus.Done ? "B.Mint" : "B.Amber");
            switch (r.Status)
            {
                case SubmitStatus.Done: Close(); _done?.Invoke(); break;
                case SubmitStatus.Next: ShowStep(); break;
                default: ShowStep(); RefreshWait(); break;
            }
        }

        private void RefreshWait()
        {
            int w = Locks.WaitSeconds(_e.Id);
            _wait.Visibility = w > 0 ? Visibility.Visible : Visibility.Collapsed;
            _verify.IsEnabled = w <= 0;
            if (w > 0)
            {
                _waitText.Text = "Wait " + SafetyUi.Clock(w) + " before the next try. Escalates with each lockout.";
                _ladderBtn.Visibility = Locks.LadderOffered(_e.Id) ? Visibility.Visible : Visibility.Collapsed;
            }
            else if (_msg.Text.Contains("Wait") || _msg.Text.Contains("wait")) _msg.Text = "The wait is over. You have " + Locks.AttemptsLeft(_e.Id) + " tries.";
        }

        private void OpenLadder()
        {
            // The ladder can also end with Windows Hello opening the lock outright, in which case this prompt is finished too.
            var w = new UnlockLadderWindow(_e, _ladderBtn, () =>
            {
                if (Locks.IsOpen(_e)) { Close(); _done?.Invoke(); }
                else RefreshWait();
            });
            w.Show();
        }
    }

    /// <summary>
    /// The unlock ladder: dim sum, ten easy sums, whack-a-mole, then the clock. Winning ends the wait and nothing more. It never signs
    /// anyone in, never returns extra attempts, and is capped per hour. The honest route, waiting, is always on screen.
    /// </summary>
    public sealed class UnlockLadderWindow : AnchoredCard
    {
        private enum Rung { DimSum, Sums, Moles, Clock, Won }
        private readonly LockEntry _e;
        private readonly Action _changed;
        private readonly StackPanel _stage = new StackPanel();
        private readonly TextBlock _title = new TextBlock { Style = Ui.Style("T.Title"), FontSize = 16, TextWrapping = TextWrapping.Wrap };
        private readonly TextBlock _clock = SafetyUi.Text("", 14, "B.Amber", FontWeights.SemiBold, 6);
        private readonly TextBlock _msg = SafetyUi.Live("Ladder status");
        private readonly DispatcherTimer _tick = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        private Rung _rung;
        private int _wrongDishes;
        private DimSumChallenge _dim;
        private SumChallenge _sums;
        private List<TextBox> _sumBoxes;
        private MoleChallenge _moles;
        private DispatcherTimer _moleTimer;
        private Stopwatch _clockWatch;
        private readonly List<(int Slot, int Cell, int AtMs)> _hits = new List<(int, int, int)>();
        private readonly Button[] _cells = new Button[9];
        private readonly TextBlock _score = SafetyUi.Live("Whack-a-mole score");

        public UnlockLadderWindow(LockEntry entry, FrameworkElement origin, Action changed) : base(origin, "Unlock ladder for " + entry.Label)
        {
            _e = entry; _changed = changed;
            var body = new StackPanel();
            body.Children.Add(new TextBlock { Text = "UNLOCK LADDER", Style = Ui.Style("T.Eyebrow"), FontSize = 11 });
            body.Children.Add(_title);
            body.Children.Add(_clock);
            body.Children.Add(_stage);
            body.Children.Add(_msg);
            body.Children.Add(SafetyUi.Text("Winning only ends the wait. You still have to enter your PIN or password. It does not give you extra tries, and the next wait still gets longer.", 12, "B.TextSoft", null, 8));
            var wait = Ui.Button("Skip the games and just wait", Close, "Btn.Chip", Icons.Clock);
            wait.Margin = new Thickness(0, 8, 0, 0);
            body.Children.Add(wait);
            body.Children.Add(SafetyUi.HelloRow(this, "Unlock with Windows Hello instead", "Unlock " + entry.Label, () =>
            {
                Locks.OpenWithHello(_e.Id);
                Close();
                _changed?.Invoke();
            }, _msg));
            SetBody(body);
            _tick.Tick += (s, e) => UpdateClock();
            Closed += (s, e) => { _tick.Stop(); _moleTimer?.Stop(); };
            Loaded += (s, e) =>
            {
                UpdateClock(); _tick.Start();
                // School mode removes dim sum, so the ladder starts at the sums with no mention of it.
                Go(Modes.Blocks(ModeFeature.DimSum) ? Rung.Sums : Rung.DimSum);
            };
        }

        private void UpdateClock()
        {
            int w = Locks.WaitSeconds(_e.Id);
            _clock.Text = w > 0 ? "The clock: " + SafetyUi.Clock(w) + " left" : "The wait is over.";
            if (w <= 0 && _rung != Rung.Won && _rung != Rung.Clock) Go(Rung.Clock, "The wait ended on its own.");
        }

        private void Go(Rung r, string note = null)
        {
            _rung = r; _stage.Children.Clear(); _msg.Text = note ?? "";
            _moleTimer?.Stop();
            switch (r)
            {
                case Rung.DimSum: _title.Text = "Rung 1: dim sum"; ShowDimSum(); break;
                case Rung.Sums: _title.Text = "Rung 2: ten easy sums"; ShowSums(); break;
                case Rung.Moles: _title.Text = "Rung 3: whack-a-mole"; ShowMoles(); break;
                case Rung.Clock:
                    _title.Text = "Back to the clock";
                    _stage.Children.Add(SafetyUi.Text("The ladder is not offered again for this wait. Nothing was lost: you are exactly where you started, and the clock above is still running.", 13, "B.TextSoft"));
                    break;
                case Rung.Won:
                    _title.Text = "The wait is over";
                    _stage.Children.Add(SafetyUi.Text("Close this and enter your credential in the unlock prompt.", 13, "B.TextSoft"));
                    break;
            }
        }

        private void Win()
        {
            if (Locks.ClearWaitByLadder(_e.Id)) { Go(Rung.Won); _changed?.Invoke(); }
            else Go(Rung.Clock, "The hourly limit for skipping waits is used up, so the clock is the way through.");
        }

        private void Lose(string note)
        {
            Locks.LadderFailed(_e.Id);
            Go(Rung.Clock, note);
        }

        // ------------------------------------------------------------ rung 1
        private void ShowDimSum()
        {
            _dim = Ladder.NewDimSum();
            _stage.Children.Add(SafetyUi.Text(_dim.Prompt, 14, "B.Text", FontWeights.SemiBold));
            _stage.Children.Add(SafetyUi.Text($"One dish, four choices. {5 - _wrongDishes} wrong picks left before the sums.", 12, "B.TextMute", null, 2));
            var panel = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            for (int i = 0; i < _dim.Choices.Length; i++)
            {
                int idx = i;
                var b = Ui.Button($"{i + 1}. {_dim.Choices[i]}", () => PickDish(idx), "Btn.Ghost");
                b.HorizontalContentAlignment = HorizontalAlignment.Left;
                panel.Children.Add(b);
            }
            _stage.Children.Add(panel);
        }

        private void PickDish(int idx)
        {
            if (Ladder.GradeDimSum(_dim.Nonce, idx)) { Win(); return; }
            _wrongDishes++;
            if (_wrongDishes >= 5) { Go(Rung.Sums, "Five wrong dishes. On to the sums."); return; }
            ShowStageAgain(Rung.DimSum, "Not that one. Here is a fresh dish.");
        }

        private void ShowStageAgain(Rung r, string note) { Go(r, note); }

        // ------------------------------------------------------------ rung 2
        private void ShowSums()
        {
            _sums = Ladder.NewSums(); _sumBoxes = new List<TextBox>();
            _stage.Children.Add(SafetyUi.Text("Every answer must be right. One wrong sum drops you to the next rung.", 12, "B.TextMute"));
            var grid = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition());
            for (int i = 0; i < _sums.Questions.Length; i++)
            {
                if (i % 2 == 0) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var cell = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 8, 0) };
                cell.Children.Add(new TextBlock { Text = _sums.Questions[i] + " =", Foreground = Ui.Res("B.Text"), VerticalAlignment = VerticalAlignment.Center, Width = 70, FontFamily = Ui.Font("F.Mono") });
                var tb = new TextBox { Style = Ui.Style("Input"), Width = 64, MaxLength = 4, Margin = new Thickness(0, 0, 0, 6) };
                AutomationProperties.SetName(tb, "Answer to " + _sums.Questions[i]);
                _sumBoxes.Add(tb); cell.Children.Add(tb);
                Grid.SetRow(cell, i / 2); Grid.SetColumn(cell, i % 2); grid.Children.Add(cell);
            }
            _stage.Children.Add(grid);
            _stage.Children.Add(Ui.Button("Check my answers", CheckSums, "Btn.Accent", Icons.Check));
        }

        private void CheckSums()
        {
            var given = _sumBoxes.Select(b => int.TryParse(b.Text.Trim(), out var v) ? (int?)v : null).ToArray();
            if (Ladder.GradeSums(_sums.Nonce, given)) Win();
            else Go(Rung.Moles, "A sum was wrong. On to whack-a-mole.");
        }

        // ------------------------------------------------------------ rung 3
        private void ShowMoles()
        {
            _stage.Children.Add(SafetyUi.Text("A mole pops up in a numbered square for a moment. Click it, or press that number on the keyboard. Hit enough before the 15 seconds end. A square says MOLE while a mole is up.", 12, "B.TextMute"));
            _stage.Children.Add(Ui.Button("Start the round", StartMoles, "Btn.Accent", Icons.Play));
            _stage.Children.Add(_score);
            _score.Text = "";
        }

        private void StartMoles()
        {
            _stage.Children.Clear();
            _moles = Ladder.NewMoles(); _hits.Clear();
            _clockWatch = Stopwatch.StartNew();
            var grid = new UniformGrid { Columns = 3, Margin = new Thickness(0, 6, 0, 6) };
            for (int i = 0; i < 9; i++)
            {
                int cell = i;
                var b = new Button { Style = Ui.Style("Btn.Ghost"), Content = (i + 1).ToString(), Height = 56, Margin = new Thickness(3), FontSize = 16 };
                AutomationProperties.SetName(b, "Square " + (i + 1));
                b.Click += (s, e) => Hit(cell);
                _cells[i] = b; grid.Children.Add(b);
            }
            _stage.Children.Add(grid);
            _stage.Children.Add(_score);
            PreviewKeyDown -= OnMoleKey; PreviewKeyDown += OnMoleKey;
            _moleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
            _moleTimer.Tick += (s, e) => MoleTick();
            _moleTimer.Start();
            UpdateScore();
        }

        private void OnMoleKey(object sender, KeyEventArgs e)
        {
            if (_rung != Rung.Moles || _moleTimer == null || !_moleTimer.IsEnabled) return;
            if (e.Key >= Key.D1 && e.Key <= Key.D9) { Hit(e.Key - Key.D1); e.Handled = true; }
            else if (e.Key >= Key.NumPad1 && e.Key <= Key.NumPad9) { Hit(e.Key - Key.NumPad1); e.Handled = true; }
        }

        private int ActiveSlot()
        {
            int t = (int)_clockWatch.ElapsedMilliseconds;
            for (int i = 0; i < _moles.Slots.Count; i++) if (t >= _moles.Slots[i].ShowMs && t <= _moles.Slots[i].HideMs) return i;
            return -1;
        }

        private void MoleTick()
        {
            int t = (int)_clockWatch.ElapsedMilliseconds;
            int active = ActiveSlot();
            for (int i = 0; i < 9; i++)
            {
                bool up = active >= 0 && _moles.Slots[active].Cell == i && !_hits.Any(h => h.Slot == active);
                _cells[i].Content = up ? "MOLE" : (i + 1).ToString();
                _cells[i].Background = up ? Ui.Res("B.Mint") : Ui.Res("B.Raised");
                _cells[i].Foreground = up ? Ui.Res("B.MintInk") : Ui.Res("B.TextSoft");
            }
            if (t >= _moles.DurationMs + 300) EndMoles();
        }

        private void Hit(int cell)
        {
            if (_moleTimer == null || !_moleTimer.IsEnabled) return;
            int slot = ActiveSlot();
            if (slot < 0 || _moles.Slots[slot].Cell != cell || _hits.Any(h => h.Slot == slot)) return;
            _hits.Add((slot, cell, (int)_clockWatch.ElapsedMilliseconds));
            UpdateScore();
        }

        private void UpdateScore() => _score.Text = $"Hits: {_hits.Count} of {_moles.Needed} needed";

        private void EndMoles()
        {
            _moleTimer.Stop();
            if (Ladder.GradeMoles(_moles.Nonce, _hits)) Win();
            else Lose("Not enough moles this time. Back to the clock.");
        }
    }
}
