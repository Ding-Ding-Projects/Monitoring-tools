using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Media;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    /// <summary>
    /// The live response-time chart in the Liveline style: a smoothly easing line with a pulsing live dot that moves with
    /// time, a fill fading from the series colour, a quiet grid, an axis range and value badge that ease to new values,
    /// and a crosshair with the time and values under the pointer or the arrow keys. It says plainly when it is waiting
    /// for its first probes, paused or stale, and never invents a value to look live. With reduced motion or Low
    /// stimulation it snaps to each new value and does not pulse or drift; every value stays readable.
    /// One instance lives with the window and is moved into each rebuilt page, so its motion and scrub survive.
    /// </summary>
    public sealed class LiveChart : FrameworkElement
    {
        public readonly record struct Sample(DateTime At, long Gateway, long Dns, bool Online);

        private List<Sample> _data = new List<Sample>();
        private double _yMax = 20, _headDns = double.NaN, _headGw = double.NaN;
        private DateTime _frozen;          // the moment the view stopped at, while paused
        private int? _scrub;
        private bool _keyboardScrub, _hooked;
        private DateTime _lastFrame = DateTime.MinValue;
        private double _pulse;
        private const double Pad = 46, Bottom = 22, Top = 8;

        public bool Paused { get; private set; }
        /// <summary>Raised when pausing changes, so the card can relabel its button and status.</summary>
        public event Action StateChanged;

        public LiveChart()
        {
            Height = 176;
            Focusable = true;
            FocusVisualStyle = null;
            ClipToBounds = true;
            Cursor = Cursors.Cross;
            AutomationProperties.SetName(this, "Response times chart");
            AutomationProperties.SetHelpText(this, "Use the left and right arrow keys to read each probe, Home and End to jump, and Escape to stop.");
            AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
            Loaded += (s, e) => Hook(true);
            Unloaded += (s, e) => Hook(false);
            IsVisibleChanged += (s, e) => Hook(IsVisible);
            GotKeyboardFocus += (s, e) => InvalidateVisual();
            LostKeyboardFocus += (s, e) => { _keyboardScrub = false; _scrub = null; InvalidateVisual(); };
        }

        private static bool Calm => Ui.Reduced;

        private void Hook(bool on)
        {
            on = on && IsLoaded && IsVisible && !Calm;
            if (on == _hooked) return;
            _hooked = on;
            if (on) CompositionTarget.Rendering += OnFrame; else CompositionTarget.Rendering -= OnFrame;
        }

        /// <summary>New probes. Ignored while paused, so the paused view holds still.</summary>
        public void Update(IEnumerable<Sample> samples)
        {
            if (Paused) return;
            _data = samples.ToList();
            if (Calm) { var (d, g) = Latest(); _headDns = d; _headGw = g; _yMax = TargetMax(); }
            else if (double.IsNaN(_headDns)) { var (d, g) = Latest(); _headDns = d; _headGw = g; _yMax = TargetMax(); }
            Hook(IsVisible);
            Summarise();
            InvalidateVisual();
        }

        public void SetPaused(bool paused)
        {
            if (paused == Paused) return;
            Paused = paused;
            _frozen = DateTime.Now;
            Summarise();
            StateChanged?.Invoke();
            InvalidateVisual();
        }

        // ------------------------------------------------------------------ figures

        private (double Dns, double Gw) Latest()
        {
            var on = _data.Where(x => x.Online && x.Dns >= 0).ToList();
            double d = on.Count > 0 ? on[^1].Dns : double.NaN;
            var g = _data.Where(x => x.Online && x.Gateway >= 0).ToList();
            return (d, g.Count > 0 ? g[^1].Gateway : double.NaN);
        }

        private double TargetMax()
        {
            double m = 10;
            foreach (var s in _data) { if (s.Dns > m) m = s.Dns; if (s.Gateway > m) m = s.Gateway; }
            if (!double.IsNaN(_headDns)) m = Math.Max(m, _headDns);
            double[] steps = { 10, 15, 20, 25, 30, 40, 50, 60, 80, 100, 150, 200, 300, 500, 1000, 2000, 5000 };
            foreach (var st in steps) if (st >= m * 1.15) return st;
            return Math.Ceiling(m * 1.15 / 1000) * 1000;
        }

        /// <summary>The seconds between probes, from the samples themselves.</summary>
        private double Interval()
        {
            if (_data.Count < 2) return Math.Max(1, Settings.Current.ProbeIntervalSeconds);
            var gaps = new List<double>();
            for (int i = Math.Max(1, _data.Count - 10); i < _data.Count; i++) gaps.Add((_data[i].At - _data[i - 1].At).TotalSeconds);
            gaps.Sort();
            return Math.Max(0.5, gaps[gaps.Count / 2]);
        }

        /// <summary>How long since the last probe arrived, when that is longer than three intervals; zero otherwise.</summary>
        public double StaleSeconds
        {
            get
            {
                if (_data.Count == 0 || Paused) return 0;
                double age = (DateTime.Now - _data[^1].At).TotalSeconds;
                return age > Math.Max(10, Interval() * 3) ? age : 0;
            }
        }

        private void Summarise()
        {
            var dns = _data.Where(x => x.Online && x.Dns >= 0).Select(x => (double)x.Dns).ToList();
            var gw = _data.Where(x => x.Online && x.Gateway >= 0).Select(x => (double)x.Gateway).ToList();
            int offline = _data.Count(x => !x.Online);
            var lines = new List<string>();
            if (_data.Count < 2) lines.Add("Waiting for the first probes.");
            else
            {
                double span = (_data[^1].At - _data[0].At).TotalMinutes;
                lines.Add(dns.Count > 0 ? $"DNS answers in {dns[^1]:0} ms now, {dns.Average():0} ms on average over the last {Math.Max(1, Math.Round(span)):0} minutes, {dns.Max():0} ms at most." : "No DNS answers in this window.");
                lines.Add(gw.Count > 0 ? $"The gateway answers in {gw[^1]:0} ms." : "The gateway did not answer.");
                if (offline > 0) lines.Add(offline == 1 ? "1 probe found the connection down." : $"{offline} probes found the connection down.");
                if (Paused) lines.Add("Paused.");
            }
            // one sentence a line, so each one is translated on its own
            Summary = string.Join("\n", lines);
            AutomationProperties.SetItemStatus(this, Core.Language.T(Summary));
        }

        /// <summary>The figures in words, for screen readers and the card's text.</summary>
        public string Summary { get; private set; } = "Waiting for the first probes.";

        // ------------------------------------------------------------------ motion

        private void OnFrame(object sender, EventArgs e)
        {
            var now = DateTime.Now;
            double dt = _lastFrame == DateTime.MinValue ? 0.016 : Math.Min(0.1, (now - _lastFrame).TotalSeconds);
            _lastFrame = now;
            var (d, g) = Latest();
            double k = 1 - Math.Exp(-dt * 9);   // eases most of the way in about a third of a second
            if (!double.IsNaN(d)) _headDns = double.IsNaN(_headDns) ? d : _headDns + (d - _headDns) * k;
            if (!double.IsNaN(g)) _headGw = double.IsNaN(_headGw) ? g : _headGw + (g - _headGw) * k;
            _yMax += (TargetMax() - _yMax) * (1 - Math.Exp(-dt * 5));
            _pulse = (_pulse + dt / 1.6) % 1;
            InvalidateVisual();
        }

        // ------------------------------------------------------------------ drawing

        private Brush B(string key) => Ui.Res(key);

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            if (w < 40 || h < 40) return;
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var mono = new Typeface(Ui.Font("F.Mono"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            var body = new Typeface(Ui.Font("F.Body"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            FormattedText Text(string s, double size, Brush brush, Typeface tf = null) => new FormattedText(s, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, tf ?? mono, size, brush, dpi);

            var plot = new Rect(Pad, Top, Math.Max(10, w - Pad - 8), Math.Max(10, h - Top - Bottom));
            dc.DrawRoundedRectangle(B("B.Sunken"), null, new Rect(0, 0, w, h), 12, 12);
            if (_data.Count < 2)
            {
                var wait = Text(Core.Language.T("Waiting for the first probes"), 13, B("B.TextSoft"), body);
                dc.DrawText(wait, new Point((w - wait.Width) / 2, (h - wait.Height) / 2));
                return;
            }

            double stale = StaleSeconds;
            var now = Paused ? _frozen : Calm ? _data[^1].At : DateTime.Now;
            double span = Math.Max(30, Math.Min((now - _data[0].At).TotalSeconds, Interval() * 120));
            double X(DateTime t) => plot.Right - (now - t).TotalSeconds / span * plot.Width;
            double ymax = Calm || double.IsNaN(_yMax) ? TargetMax() : _yMax;
            double Y(double v) => plot.Bottom - Math.Max(0, Math.Min(1, v / ymax)) * plot.Height;

            // the quiet grid and the axis range
            var gridPen = new Pen(B("B.Line"), 1) { DashStyle = new DashStyle(new double[] { 2, 4 }, 0) };
            for (int i = 0; i <= 4; i++)
            {
                double v = ymax * i / 4, y = Y(v);
                if (i > 0) dc.DrawLine(gridPen, new Point(plot.Left, y), new Point(plot.Right, y));
                var label = Text(i == 4 ? $"{v:0} ms" : $"{v:0}", 10.5, B("B.TextMute"));
                dc.DrawText(label, new Point(Pad - 8 - label.Width, y - label.Height / 2));
            }
            var ago = Text(Core.Language.T($"{span / 60:0.#} min ago"), 10.5, B("B.TextMute"));
            dc.DrawText(ago, new Point(plot.Left, plot.Bottom + 4));
            var nowLabel = Text(Core.Language.T(Paused ? "paused" : "now"), 10.5, B("B.TextMute"));
            dc.DrawText(nowLabel, new Point(plot.Right - nowLabel.Width, plot.Bottom + 4));

            dc.PushClip(new RectangleGeometry(plot));
            // offline stretches, marked rather than drawn as zero
            double dx = Interval() / span * plot.Width;
            var amber = (B("B.Amber") as SolidColorBrush)?.Color ?? Colors.Orange;
            var offFill = new SolidColorBrush(Color.FromArgb(0x2E, amber.R, amber.G, amber.B));
            for (int i = 0; i < _data.Count; i++)
            {
                if (_data[i].Online) continue;
                int j = i; while (j + 1 < _data.Count && !_data[j + 1].Online) j++;
                double l = X(_data[i].At) - dx / 2, r = X(_data[j].At) + dx / 2;
                dc.DrawRectangle(offFill, null, new Rect(l, plot.Top, Math.Max(2, r - l), plot.Height));
                if (r - l > 70) dc.DrawText(Text(Core.Language.T("Offline"), 10.5, B("B.Amber"), body), new Point(l + 6, plot.Top + 4));
                i = j;
            }

            var dnsBrush = stale > 0 ? B("B.TextMute") : B("B.Mint");
            var gwBrush = B("B.OnTertiaryContainer");
            Series(dc, s => s.Online && s.Dns >= 0 ? s.Dns : double.NaN, _headDns, X, Y, dnsBrush, 2.4, plot, fill: stale == 0);
            Series(dc, s => s.Online && s.Gateway >= 0 ? s.Gateway : double.NaN, _headGw, X, Y, gwBrush, 1.6, plot, fill: false);
            dc.Pop();

            // the live dot and the value badge on the newest probe
            var last = _data.LastOrDefault(s => s.Online && s.Dns >= 0);
            if (last.At != default && !double.IsNaN(_headDns))
            {
                var p = new Point(Math.Min(plot.Right, X(last.At)), Y(_headDns));
                if (!Calm && !Paused && stale == 0)
                {
                    var ring = new Pen(B("B.Mint"), 2);
                    double grow = 4 + 10 * _pulse;
                    dc.PushOpacity(Math.Max(0, 0.9 * (1 - _pulse)));
                    dc.DrawEllipse(null, ring, p, grow, grow);
                    dc.Pop();
                }
                if (stale > 0) dc.DrawEllipse(B("B.Sunken"), new Pen(B("B.TextMute"), 2), p, 5, 5);
                else dc.DrawEllipse(B("B.Mint"), new Pen(B("B.Sunken"), 2), p, 5.5, 5.5);
                var badge = Text($"{_headDns:0} ms", 11.5, stale > 0 ? B("B.TextSoft") : B("B.MintInk"));
                var box = new Rect(plot.Right - badge.Width - 16, Math.Max(plot.Top, Math.Min(plot.Bottom - badge.Height - 6, p.Y - badge.Height / 2 - 3)), badge.Width + 12, badge.Height + 6);
                if (box.Left < p.X + 10 && box.Right > p.X - 10) box.Y = Math.Max(plot.Top, p.Y - box.Height - 10);
                dc.DrawRoundedRectangle(stale > 0 ? B("B.Raised") : B("B.Mint"), null, box, 8, 8);
                dc.DrawText(badge, new Point(box.X + 6, box.Y + 3));
            }

            // the crosshair and its tooltip
            if (_scrub is int si && si >= 0 && si < _data.Count)
            {
                var s = _data[si];
                double x = X(s.At);
                dc.DrawLine(new Pen(B("B.LineStrong"), 1.5), new Point(x, plot.Top), new Point(x, plot.Bottom));
                var t1 = Text(s.At.ToString("HH:mm:ss"), 11.5, B("B.Text"));
                var t2 = Text(ScrubWords(s), 12, B("B.TextSoft"), body);
                double bw = Math.Max(t1.Width, t2.Width) + 20, bh = t1.Height + t2.Height + 14;
                double bx = x + 10 + bw > plot.Right ? x - 10 - bw : x + 10;
                var tip = new Rect(Math.Max(2, bx), plot.Top + 6, bw, bh);
                dc.DrawRoundedRectangle(B("B.Raised"), new Pen(B("B.Line"), 1), tip, 10, 10);
                dc.DrawText(t1, new Point(tip.X + 10, tip.Y + 6));
                dc.DrawText(t2, new Point(tip.X + 10, tip.Y + 8 + t1.Height));
                if (!double.IsNaN(Val(s.Dns, s.Online))) dc.DrawEllipse(B("B.Mint"), null, new Point(x, Y(s.Dns)), 4, 4);
            }
            if (IsKeyboardFocused) dc.DrawRoundedRectangle(null, new Pen(B("B.Mint"), 2), new Rect(1, 1, w - 2, h - 2), 12, 12);
        }

        private static double Val(long v, bool online) => online && v >= 0 ? v : double.NaN;

        private static string ScrubWords(Sample s) => Core.Language.T(!s.Online ? "Offline, no answer" :
            (s.Dns >= 0 ? $"DNS {s.Dns} ms" : "DNS did not answer") + ", " + (s.Gateway >= 0 ? $"gateway {s.Gateway} ms" : "gateway did not answer"));

        private void Series(DrawingContext dc, Func<Sample, double> pick, double head, Func<DateTime, double> X, Func<double, double> Y, Brush brush, double thick, Rect plot, bool fill)
        {
            // runs of answered probes; a gap is left where a probe failed, never drawn as zero
            var runs = new List<List<Point>>();
            List<Point> cur = null;
            int lastIndex = -1;
            for (int i = 0; i < _data.Count; i++) if (!double.IsNaN(pick(_data[i]))) lastIndex = i;
            for (int i = 0; i < _data.Count; i++)
            {
                double v = pick(_data[i]);
                if (double.IsNaN(v)) { cur = null; continue; }
                if (i == lastIndex && !double.IsNaN(head)) v = head;   // the newest point eases to its value
                if (cur == null) { cur = new List<Point>(); runs.Add(cur); }
                cur.Add(new Point(X(_data[i].At), Y(v)));
            }
            var pen = new Pen(brush, thick) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            foreach (var run in runs)
            {
                if (run.Count < 2) { dc.DrawEllipse(brush, null, run[0], thick, thick); continue; }
                var line = Smooth(run, false, plot.Bottom);
                if (fill)
                {
                    var c = brush is SolidColorBrush sb ? sb.Color : Colors.Gray;
                    var grad = new LinearGradientBrush(Color.FromArgb(0x55, c.R, c.G, c.B), Color.FromArgb(0x00, c.R, c.G, c.B), 90);
                    dc.DrawGeometry(grad, null, Smooth(run, true, plot.Bottom));
                }
                dc.DrawGeometry(null, pen, line);
            }
        }

        /// <summary>A curve through every point (Catmull-Rom as cubic Béziers), closed down to the base for the fill.</summary>
        private static StreamGeometry Smooth(List<Point> p, bool close, double baseY)
        {
            var g = new StreamGeometry();
            using (var c = g.Open())
            {
                c.BeginFigure(close ? new Point(p[0].X, baseY) : p[0], close, close);
                if (close) c.LineTo(p[0], false, false);
                for (int i = 0; i < p.Count - 1; i++)
                {
                    var p0 = i > 0 ? p[i - 1] : p[i];
                    var p1 = p[i]; var p2 = p[i + 1];
                    var p3 = i + 2 < p.Count ? p[i + 2] : p2;
                    var c1 = new Point(p1.X + (p2.X - p0.X) / 6, p1.Y + (p2.Y - p0.Y) / 6);
                    var c2 = new Point(p2.X - (p3.X - p1.X) / 6, p2.Y - (p3.Y - p1.Y) / 6);
                    c.BezierTo(c1, c2, p2, true, true);
                }
                if (close) c.LineTo(new Point(p[^1].X, baseY), false, false);
            }
            g.Freeze();
            return g;
        }

        // ------------------------------------------------------------------ scrubbing

        private int Nearest(double x)
        {
            if (_data.Count == 0) return -1;
            double w = ActualWidth;
            var plotRight = w - 8; var plotLeft = Pad;
            var now = Paused ? _frozen : Calm ? _data[^1].At : DateTime.Now;
            double span = Math.Max(30, Math.Min((now - _data[0].At).TotalSeconds, Interval() * 120));
            int best = -1; double bd = double.MaxValue;
            for (int i = 0; i < _data.Count; i++)
            {
                double xi = plotRight - (now - _data[i].At).TotalSeconds / span * (plotRight - plotLeft);
                double d = Math.Abs(xi - x);
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_keyboardScrub) return;
            int i = Nearest(e.GetPosition(this).X);
            if (i != _scrub) { _scrub = i; InvalidateVisual(); }
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            if (_keyboardScrub) return;
            _scrub = null; InvalidateVisual();
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) { base.OnMouseLeftButtonDown(e); Focus(); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (_data.Count == 0) return;
            int cur = _scrub ?? _data.Count - 1;
            int next = e.Key switch { Key.Left => cur - 1, Key.Right => cur + 1, Key.Home => 0, Key.End => _data.Count - 1, _ => int.MinValue };
            if (e.Key == Key.Escape && _scrub != null) { _scrub = null; _keyboardScrub = false; e.Handled = true; InvalidateVisual(); return; }
            if (next == int.MinValue) return;
            e.Handled = true;
            _keyboardScrub = true;
            _scrub = Math.Max(0, Math.Min(_data.Count - 1, next));
            var s = _data[_scrub.Value];
            AutomationProperties.SetName(this, s.At.ToString("HH:mm:ss") + ", " + ScrubWords(s));
            try { UIElementAutomationPeer.CreatePeerForElement(this)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged); } catch { }
            InvalidateVisual();
        }
    }
}
