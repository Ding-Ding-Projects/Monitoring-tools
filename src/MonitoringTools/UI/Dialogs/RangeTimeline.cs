using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MonitoringTools.Media;

namespace MonitoringTools.UI
{
    /// <summary>
    /// The timeline of the trim dialogs: a strip of video frames or an audio waveform, two mint handles that mark the start and the
    /// end of the selection, a white playhead, the part outside the selection dimmed, and a time ruler underneath. The handles are
    /// focusable sliders: Left and Right move one frame, Ctrl moves one second, Shift moves ten frames, Home and End jump to the
    /// ends of the media. Clicking or dragging on the strip moves the playhead.
    /// </summary>
    public sealed class RangeTimeline : Grid
    {
        private sealed class Track : FrameworkElement
        {
            public RangeTimeline Owner;
            public Track() { Focusable = true; FocusVisualStyle = null; Cursor = Cursors.IBeam; }
            protected override void OnRender(DrawingContext dc) => Owner.Draw(dc, new Size(ActualWidth, ActualHeight));
            protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) { Focus(); CaptureMouse(); Owner.MovePlayhead(e.GetPosition(this).X, true); e.Handled = true; }
            protected override void OnMouseMove(MouseEventArgs e) { if (IsMouseCaptured) Owner.MovePlayhead(e.GetPosition(this).X, true); }
            protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { if (IsMouseCaptured) ReleaseMouseCapture(); }
            protected override void OnKeyDown(KeyEventArgs e)
            {
                double step = Owner.FrameStep * (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) ? 30 : Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1);
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) step = 1;
                if (e.Key == Key.Left) { Owner.SetPlayhead(Owner.Playhead - step, true); e.Handled = true; }
                else if (e.Key == Key.Right) { Owner.SetPlayhead(Owner.Playhead + step, true); e.Handled = true; }
                else if (e.Key == Key.Home) { Owner.SetPlayhead(0, true); e.Handled = true; }
                else if (e.Key == Key.End) { Owner.SetPlayhead(Owner.Duration, true); e.Handled = true; }
            }
            protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e) { base.OnGotKeyboardFocus(e); InvalidateVisual(); }
        }

        private sealed class Handle : Border
        {
            public RangeTimeline Owner; public bool IsStart;
            public Handle()
            {
                Width = 14; CornerRadius = new CornerRadius(7); Background = Ui.Res("B.Mint"); Focusable = true; FocusVisualStyle = null; Cursor = Cursors.SizeWE;
                Child = new Border { Width = 2, Background = Ui.Res("B.MintInk"), CornerRadius = new CornerRadius(1), Height = 18, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                BorderBrush = Brushes.Transparent; BorderThickness = new Thickness(2);
            }
            protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) { Focus(); CaptureMouse(); e.Handled = true; }
            protected override void OnMouseMove(MouseEventArgs e)
            {
                if (!IsMouseCaptured) return;
                var x = e.GetPosition(Owner._track).X;
                Owner.MoveHandle(IsStart, Owner.TimeAt(x), true);
            }
            protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { if (IsMouseCaptured) ReleaseMouseCapture(); }
            protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e) { BorderBrush = Ui.Res("B.Text"); }
            protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e) { BorderBrush = Brushes.Transparent; }
            protected override void OnKeyDown(KeyEventArgs e)
            {
                double step = Owner.FrameStep;
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) step = 1; else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) step = Owner.FrameStep * 10;
                double cur = IsStart ? Owner.Start : Owner.End;
                double? to = e.Key == Key.Left ? cur - step : e.Key == Key.Right ? cur + step : e.Key == Key.Home ? 0 : e.Key == Key.End ? Owner.Duration : (double?)null;
                if (to == null) return;
                e.Handled = true;
                Owner.MoveHandle(IsStart, to.Value, true);
            }
        }

        private readonly Track _track;
        private readonly Handle _hStart, _hEnd;
        private readonly Canvas _overlay = new Canvas { IsHitTestVisible = true, Background = null };
        private readonly Grid _ruler = new Grid { Height = 22 };
        private IReadOnlyList<BitmapSource> _strip;
        private float[] _peaks;
        private const double HandleW = 14;

        public double Duration { get; private set; } = 1;
        public double Start { get; private set; }
        public double End { get; private set; } = 1;
        public double Playhead { get; private set; }
        public double FrameStep { get; set; } = 1.0 / 25;
        public double MinGap { get; set; } = 0.1;
        /// <summary>Dim the selection instead of the outside (used while the selection is going to be cut out).</summary>
        public bool DimInside { get; set; }
        public bool IsAudio { get; set; }
        public bool Loading { get; private set; } = true;

        public event Action<double, double, bool> RangeChanged;      // start, end, byUser
        public event Action<double, bool> PlayheadChanged;            // time, byUser

        public RangeTimeline(double duration, bool audio, double trackHeight = 64)
        {
            IsAudio = audio; Duration = Math.Max(0.1, duration); End = Duration;
            RowDefinitions.Add(new RowDefinition { Height = new GridLength(trackHeight + 8) });
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _track = new Track { Owner = this, Margin = new Thickness(HandleW / 2, 4, HandleW / 2, 4), Height = trackHeight, ClipToBounds = true };
            AutomationProperties.SetName(_track, audio ? "Waveform. Left and Right move the playhead." : "Timeline. Left and Right move the playhead.");
            _hStart = new Handle { Owner = this, IsStart = true, Height = trackHeight + 8 };
            _hEnd = new Handle { Owner = this, IsStart = false, Height = trackHeight + 8 };
            AutomationProperties.SetName(_hStart, audio ? "Start marker" : "Start handle");
            AutomationProperties.SetName(_hEnd, audio ? "End marker" : "End handle");
            _overlay.Children.Add(_hStart); _overlay.Children.Add(_hEnd);
            var stack = new Grid();
            stack.Children.Add(_track); stack.Children.Add(_overlay);
            Children.Add(stack);
            SetRow(_ruler, 1);
            _ruler.Margin = new Thickness(HandleW / 2, 4, HandleW / 2, 0);
            Children.Add(_ruler);
            SizeChanged += (s, e) => { Layout(); BuildRuler(); };
            Layout(); BuildRuler();
        }

        public void SetStrip(IReadOnlyList<BitmapSource> strip) { _strip = strip; Loading = false; _track.InvalidateVisual(); }
        public void SetPeaks(float[] peaks) { _peaks = peaks; Loading = false; _track.InvalidateVisual(); }
        public void Failed() { Loading = false; _track.InvalidateVisual(); }

        public double TimeAt(double x) => _track.ActualWidth <= 0 ? 0 : Math.Clamp(x / _track.ActualWidth, 0, 1) * Duration;
        private double XOf(double t) => Duration <= 0 ? 0 : t / Duration * _track.ActualWidth;

        private void Layout()
        {
            double tw = _track.ActualWidth;
            Canvas.SetLeft(_hStart, XOf(Start)); Canvas.SetLeft(_hEnd, XOf(End) + 0);
            _hStart.Margin = new Thickness(0); _hEnd.Margin = new Thickness(0);
            Canvas.SetTop(_hStart, 0); Canvas.SetTop(_hEnd, 0);
            AutomationProperties.SetItemStatus(_hStart, MediaTime.Format(Start));
            AutomationProperties.SetItemStatus(_hEnd, MediaTime.Format(End));
            _track.InvalidateVisual();
        }

        private void BuildRuler()
        {
            _ruler.Children.Clear(); _ruler.ColumnDefinitions.Clear();
            int ticks = 5;
            for (int i = 0; i < ticks; i++)
            {
                _ruler.ColumnDefinitions.Add(new ColumnDefinition());
                var t = i * Duration / (ticks - 1);
                var tb = new TextBlock { Text = MediaTime.Format(t, 0), FontFamily = Ui.Font("F.Mono"), FontSize = 12, Foreground = Ui.Res("B.TextSoft"), HorizontalAlignment = i == 0 ? HorizontalAlignment.Left : i == ticks - 1 ? HorizontalAlignment.Right : HorizontalAlignment.Center };
                Core.Language.SetLiteral(tb, true);
                SetColumn(tb, i); _ruler.Children.Add(tb);
            }
        }

        public void SetRange(double start, double end)
        {
            Start = Math.Clamp(start, 0, Duration); End = Math.Clamp(end, Start, Duration);
            Layout();
            RangeChanged?.Invoke(Start, End, false);
        }

        public void SetPlayhead(double t, bool byUser)
        {
            Playhead = Math.Clamp(t, 0, Duration);
            _track.InvalidateVisual();
            PlayheadChanged?.Invoke(Playhead, byUser);
        }

        private void MovePlayhead(double x, bool byUser) => SetPlayhead(TimeAt(x), byUser);

        private void MoveHandle(bool isStart, double t, bool byUser)
        {
            if (isStart) Start = Math.Clamp(t, 0, Math.Max(0, End - MinGap)); else End = Math.Clamp(t, Math.Min(Duration, Start + MinGap), Duration);
            Layout();
            RangeChanged?.Invoke(Start, End, byUser);
        }

        internal void FocusStart() => _hStart.Focus();
        internal void FocusEnd() => _hEnd.Focus();

        private void Draw(DrawingContext dc, Size size)
        {
            if (size.Width <= 0) return;
            var rect = new Rect(0, 0, size.Width, size.Height);
            var round = new RectangleGeometry(rect, 8, 8);
            dc.PushClip(round);
            dc.DrawRectangle(Ui.Res("B.Sunken"), null, rect);
            if (!IsAudio)
            {
                if (_strip != null && _strip.Count > 0)
                {
                    double w = size.Width / _strip.Count;
                    for (int i = 0; i < _strip.Count; i++)
                        dc.DrawImage(_strip[i], new Rect(i * w, 0, w + 0.5, size.Height));
                }
                else
                {
                    var g = new LinearGradientBrush(Ui.C("C.Raised"), Ui.C("C.Surface"), 0); dc.DrawRectangle(g, null, rect);
                }
            }
            else if (_peaks != null && _peaks.Length > 0)
            {
                double mid = size.Height / 2;
                int bars = (int)Math.Max(10, size.Width / 6);
                double bw = size.Width / bars;
                for (int i = 0; i < bars; i++)
                {
                    int a = (int)((long)_peaks.Length * i / bars), b = Math.Max(a + 1, (int)((long)_peaks.Length * (i + 1) / bars));
                    float peak = 0; for (int k = a; k < b && k < _peaks.Length; k++) peak = Math.Max(peak, _peaks[k]);
                    double h = Math.Max(2, Math.Sqrt(peak) * (size.Height - 8));
                    double cx = i * bw;
                    double t = (cx + bw / 2) / size.Width * Duration;
                    bool inside = t >= Start && t <= End;
                    dc.DrawRoundedRectangle(inside ? Ui.Res("B.Mint") : Ui.Res("B.TextMute"), null, new Rect(cx + 1, mid - h / 2, Math.Max(1.5, bw - 2), h), 1.5, 1.5);
                }
            }
            else if (Loading)
            {
                dc.DrawText(new FormattedText(Core.Language.T("Reading the file…"), CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface(Ui.Font("F.Body"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), 13, Ui.Res("B.TextSoft"), 1.0), new Point(12, size.Height / 2 - 8));
            }
            // dimming
            var dim = new SolidColorBrush(Color.FromArgb(IsAudio ? (byte)0 : (byte)150, 8, 9, 12)); dim.Freeze();
            double xs = XOf(Start), xe = XOf(End);
            if (!DimInside)
            {
                if (!IsAudio) { dc.DrawRectangle(dim, null, new Rect(0, 0, xs, size.Height)); dc.DrawRectangle(dim, null, new Rect(xe, 0, Math.Max(0, size.Width - xe), size.Height)); }
            }
            else dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(170, 8, 9, 12)), null, new Rect(xs, 0, Math.Max(0, xe - xs), size.Height));
            dc.Pop();
            double px = XOf(Playhead);
            dc.DrawRectangle(Brushes.White, null, new Rect(Math.Clamp(px - 1, 0, size.Width - 2), 0, 2, size.Height));
            if (_track.IsKeyboardFocused) dc.DrawRoundedRectangle(null, new Pen(Ui.Res("B.Mint"), 2), new Rect(1, 1, size.Width - 2, size.Height - 2), 8, 8);
        }
    }
}
