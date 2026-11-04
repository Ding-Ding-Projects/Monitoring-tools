using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MonitoringTools.Clip;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    /// <summary>Colour space conversions used by the picker. All channels are 0 to 1 unless noted; hue is in degrees.</summary>
    public static class ColorMath
    {
        public static (double H, double S, double V) ToHsv(double r, double g, double b)
        {
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
            double h = 0;
            if (d > 1e-9)
            {
                if (max == r) h = 60 * (((g - b) / d) % 6);
                else if (max == g) h = 60 * ((b - r) / d + 2);
                else h = 60 * ((r - g) / d + 4);
            }
            if (h < 0) h += 360;
            return (h, max <= 0 ? 0 : d / max, max);
        }

        public static (double R, double G, double B) FromHsv(double h, double s, double v)
        {
            h = ((h % 360) + 360) % 360;
            double c = v * s, x = c * (1 - Math.Abs((h / 60) % 2 - 1)), m = v - c;
            double r, g, b;
            if (h < 60) (r, g, b) = (c, x, 0);
            else if (h < 120) (r, g, b) = (x, c, 0);
            else if (h < 180) (r, g, b) = (0, c, x);
            else if (h < 240) (r, g, b) = (0, x, c);
            else if (h < 300) (r, g, b) = (x, 0, c);
            else (r, g, b) = (c, 0, x);
            return (r + m, g + m, b + m);
        }

        public static (double H, double S, double L) ToHsl(double r, double g, double b)
        {
            var (h, _, v) = ToHsv(r, g, b);
            double min = Math.Min(r, Math.Min(g, b)), l = (v + min) / 2, d = v - min;
            double s = d < 1e-9 ? 0 : d / (1 - Math.Abs(2 * l - 1));
            return (h, Math.Clamp(s, 0, 1), l);
        }

        public static (double R, double G, double B) FromHsl(double h, double s, double l)
        {
            double v = l + s * Math.Min(l, 1 - l);
            return FromHsv(h, v <= 0 ? 0 : 2 * (1 - l / v), v);
        }

        public static (double H, double W, double B) ToHwb(double r, double g, double b)
        {
            var (h, _, v) = ToHsv(r, g, b);
            return (h, Math.Min(r, Math.Min(g, b)), 1 - v);
        }

        public static (double R, double G, double B) FromHwb(double h, double w, double bl)
        {
            if (w + bl >= 1) { var gray = w / (w + bl); return (gray, gray, gray); }
            var (r, g, b) = FromHsv(h, 1, 1);
            double f = 1 - w - bl;
            return (r * f + w, g * f + w, b * f + w);
        }

        public static (double C, double M, double Y, double K) ToCmyk(double r, double g, double b)
        {
            double k = 1 - Math.Max(r, Math.Max(g, b));
            if (k >= 1) return (0, 0, 0, 1);
            return ((1 - r - k) / (1 - k), (1 - g - k) / (1 - k), (1 - b - k) / (1 - k), k);
        }

        public static (double R, double G, double B) FromCmyk(double c, double m, double y, double k) =>
            ((1 - c) * (1 - k), (1 - m) * (1 - k), (1 - y) * (1 - k));

        private static double Lin(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        private static double Gam(double c) => c <= 0.0031308 ? 12.92 * c : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055;

        public static (double L, double A, double B) ToOklab(double r, double g, double b)
        {
            double lr = Lin(r), lg = Lin(g), lb = Lin(b);
            double l = Math.Cbrt(0.4122214708 * lr + 0.5363325363 * lg + 0.0514459929 * lb);
            double m = Math.Cbrt(0.2119034982 * lr + 0.6806995451 * lg + 0.1073969566 * lb);
            double s = Math.Cbrt(0.0883024619 * lr + 0.2817188376 * lg + 0.6299787005 * lb);
            return (0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
                    1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
                    0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s);
        }

        /// <summary>Converts OKLab to sRGB. <paramref name="clipped"/> is true when the colour lay outside the sRGB gamut and was clamped.</summary>
        public static (double R, double G, double B) FromOklab(double L, double a, double b, out bool clipped)
        {
            double l = Math.Pow(L + 0.3963377774 * a + 0.2158037573 * b, 3);
            double m = Math.Pow(L - 0.1055613458 * a - 0.0638541728 * b, 3);
            double s = Math.Pow(L - 0.0894841775 * a - 1.2914855480 * b, 3);
            double r = 4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s;
            double g = -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s;
            double bl = -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s;
            clipped = r < -0.0005 || r > 1.0005 || g < -0.0005 || g > 1.0005 || bl < -0.0005 || bl > 1.0005;
            return (Gam(Math.Clamp(r, 0, 1)), Gam(Math.Clamp(g, 0, 1)), Gam(Math.Clamp(bl, 0, 1)));
        }

        public static (double L, double C, double H) ToOklch(double r, double g, double b)
        {
            var (L, A, B) = ToOklab(r, g, b);
            double c = Math.Sqrt(A * A + B * B), h = Math.Atan2(B, A) * 180 / Math.PI;
            if (h < 0) h += 360;
            return (L, c, c < 1e-4 ? 0 : h);
        }

        public static (double R, double G, double B) FromOklch(double L, double C, double H, out bool clipped)
        {
            double rad = H * Math.PI / 180;
            return FromOklab(L, C * Math.Cos(rad), C * Math.Sin(rad), out clipped);
        }

        /// <summary>WCAG relative luminance of an sRGB colour.</summary>
        public static double Luminance(Color c) => 0.2126 * Lin(c.R / 255.0) + 0.7152 * Lin(c.G / 255.0) + 0.0722 * Lin(c.B / 255.0);

        /// <summary>WCAG contrast ratio, 1 to 21.</summary>
        public static double Contrast(Color a, Color b)
        {
            double x = Luminance(a), y = Luminance(b);
            return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
        }
    }

    /// <summary>
    /// An infinite colour picker: a saturation and brightness field, hue and alpha bars, direct entry in seven colour
    /// spaces that translate into one another, the theme's colour roles, recent colours, an on-screen eyedropper and a
    /// contrast readout. <see cref="Changed"/> fires while dragging; <see cref="Committed"/> fires when a choice is final.
    /// </summary>
    public sealed class ColorPicker : StackPanel
    {
        public event Action<Color> Changed;
        public event Action<Color> Committed;

        private double _h, _s, _v, _a = 1;
        private Color _initial;
        private Color? _against;
        private bool _syncing;

        private readonly SolidColorBrush _hueBrush = new SolidColorBrush(Colors.Red);
        private readonly Grid _square = new Grid { Height = 150, Cursor = Cursors.Cross, Focusable = true, Background = Brushes.Transparent, ClipToBounds = true };
        private readonly Ellipse _squareThumb = new Ellipse { Width = 14, Height = 14, StrokeThickness = 2, Stroke = Brushes.White, IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        private readonly Bar _hueBar, _alphaBar;
        private readonly LinearGradientBrush _alphaGradient = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
        private readonly Border _newSwatch = new Border { Height = 30, CornerRadius = new CornerRadius(0, 10, 10, 0) };
        private readonly Border _oldSwatch = new Border { Height = 30, CornerRadius = new CornerRadius(10, 0, 0, 10) };
        private readonly TextBlock _contrast = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new Thickness(0, 6, 0, 0) };
        private readonly TextBlock _note = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new Thickness(0, 4, 0, 0), Foreground = Ui.Res("B.TextMute") };
        private readonly StackPanel _recent = new StackPanel { Orientation = Orientation.Horizontal };
        private readonly List<Field> _fields = new List<Field>();

        private sealed class Field
        {
            public string Label, Hint;
            public Func<double, double, double, double, string> Format;
            public Func<string, (double R, double G, double B, double A)?> Parse;
            public TextBox Box;
        }

        public ColorPicker(Color start)
        {
            _hueBar = new Bar("Hue", 360, true);
            _alphaBar = new Bar("Opacity", 100, false);
            Orientation = Orientation.Vertical;

            // saturation and brightness field
            _square.Children.Add(new Rectangle { Fill = _hueBrush });
            _square.Children.Add(new Rectangle { Fill = new LinearGradientBrush(Colors.White, Color.FromArgb(0, 255, 255, 255), 0) });
            _square.Children.Add(new Rectangle { Fill = new LinearGradientBrush(Color.FromArgb(0, 0, 0, 0), Colors.Black, 90) });
            _square.Children.Add(_squareThumb);
            AutomationProperties.SetName(_square, "Saturation and brightness field");
            AutomationProperties.SetHelpText(_square, "Drag, or use the arrow keys. Hold Shift for larger steps.");
            _square.MouseLeftButtonDown += (s, e) => { _square.CaptureMouse(); _square.Focus(); FromSquare(e.GetPosition(_square)); };
            _square.MouseMove += (s, e) => { if (_square.IsMouseCaptured) FromSquare(e.GetPosition(_square)); };
            _square.MouseLeftButtonUp += (s, e) => { if (_square.IsMouseCaptured) { _square.ReleaseMouseCapture(); Commit(); } };
            _square.SizeChanged += (s, e) => PlaceSquareThumb();
            _square.KeyDown += (s, e) =>
            {
                double step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 0.1 : 0.01;
                switch (e.Key)
                {
                    case Key.Left: _s = Math.Clamp(_s - step, 0, 1); break;
                    case Key.Right: _s = Math.Clamp(_s + step, 0, 1); break;
                    case Key.Up: _v = Math.Clamp(_v + step, 0, 1); break;
                    case Key.Down: _v = Math.Clamp(_v - step, 0, 1); break;
                    default: return;
                }
                e.Handled = true; Refresh(true); Commit();
            };
            Children.Add(new Border { CornerRadius = new CornerRadius(12), Child = _square, ClipToBounds = true });

            _hueBar.Moved += v => { _h = v; Refresh(true); };
            _hueBar.Done += Commit;
            var rainbow = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            for (int i = 0; i <= 6; i++) rainbow.GradientStops.Add(new GradientStop(ColorFromHue(i * 60), i / 6.0));
            _hueBar.Track.Fill = rainbow;
            Children.Add(_hueBar.WithMargin(0, 10, 0, 0));

            _alphaBar.Moved += v => { _a = v; Refresh(true); };
            _alphaBar.Done += Commit;
            _alphaBar.Track.Fill = _alphaGradient;
            Children.Add(_alphaBar.WithMargin(0, 8, 0, 0));

            // old and new
            var cmp = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            cmp.ColumnDefinitions.Add(new ColumnDefinition()); cmp.ColumnDefinitions.Add(new ColumnDefinition());
            Grid.SetColumn(_newSwatch, 1);
            cmp.Children.Add(_oldSwatch); cmp.Children.Add(_newSwatch);
            AutomationProperties.SetName(cmp, "Original colour on the left, new colour on the right");
            Children.Add(cmp);
            Children.Add(Ui.Mono("ORIGINAL  ·  NEW", Ui.Res("B.TextMute"), 10).WithMargin(0, 2, 0, 0));
            Children.Add(_contrast);

            // translator fields
            BuildFields();
            var grid = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            for (int i = 0; i < _fields.Count; i++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var f = _fields[i];
                var lab = Ui.Mono(f.Label, Ui.Res("B.TextMute"), 11); lab.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetRow(lab, i); grid.Children.Add(lab);
                f.Box = new TextBox { Style = Ui.Style("Input"), Margin = new Thickness(0, 2, 6, 2), ToolTip = f.Hint };
                AutomationProperties.SetName(f.Box, f.Label + " value");
                AutomationProperties.SetHelpText(f.Box, f.Hint);
                var field = f;
                f.Box.KeyDown += (s, e) => { if (e.Key == Key.Enter) { e.Handled = true; ApplyField(field); } };
                f.Box.LostKeyboardFocus += (s, e) => ApplyField(field);
                Grid.SetRow(f.Box, i); Grid.SetColumn(f.Box, 1); grid.Children.Add(f.Box);
                var copy = new Button { Style = Ui.Style("Btn.Icon"), Width = 32, Height = 32, Content = Ui.Icon(Icons.Clipboard, Ui.Res("B.TextSoft"), 14), ToolTip = "Copy the " + f.Label + " value" };
                AutomationProperties.SetName(copy, "Copy " + f.Label);
                copy.Click += (s, e) => ClipboardWatcher.SetText(field.Box.Text);
                Grid.SetRow(copy, i); Grid.SetColumn(copy, 2); grid.Children.Add(copy);
            }
            Children.Add(grid);
            Children.Add(_note);

            // eyedropper
            var tools = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            var drop = Ui.Button("Pick from screen", StartEyedropper, "Btn.Ghost", Icons.Wand);
            drop.ToolTip = "Click any pixel on any screen to take its colour. Press Esc to cancel.";
            tools.Children.Add(drop);
            Children.Add(tools);

            // theme roles
            Children.Add(Ui.Mono("THEME COLOURS", Ui.Res("B.TextMute"), 11).WithMargin(0, 6, 0, 4));
            var roles = new WrapPanel();
            for (int i = 0; i < 17; i++)
            {
                var c = Appearance.Role(i);
                roles.Children.Add(Swatch(c, Appearance.RoleName(i)));
            }
            Children.Add(roles);

            Children.Add(Ui.Mono("RECENT", Ui.Res("B.TextMute"), 11).WithMargin(0, 8, 0, 4));
            Children.Add(_recent);
            RebuildRecent();

            _initial = start;
            _oldSwatch.Background = Checker(start);
            SetColor(start, false);
        }

        /// <summary>The colour this picker currently shows.</summary>
        public Color Value
        {
            get
            {
                var (r, g, b) = ColorMath.FromHsv(_h, _s, _v);
                return Color.FromArgb((byte)Math.Round(_a * 255), To8(r), To8(g), To8(b));
            }
        }

        /// <summary>An opaque colour the picked colour is compared with for the contrast readout.</summary>
        public Color? Against
        {
            get => _against;
            set { _against = value; UpdateContrast(); }
        }

        /// <summary>Shows a colour without raising events (or with <paramref name="raise"/>).</summary>
        public void SetColor(Color c, bool raise)
        {
            var (h, s, v) = ColorMath.ToHsv(c.R / 255.0, c.G / 255.0, c.B / 255.0);
            if (s > 0 && v > 0) _h = h; else if (_s == 0 && _v == 0) _h = h;
            _s = s; _v = v; _a = c.A / 255.0;
            _alphaBar.Set(_a); _hueBar.Set(_h / 360.0);
            Refresh(raise);
        }

        private static byte To8(double d) => (byte)Math.Round(Math.Clamp(d, 0, 1) * 255);
        private static Color ColorFromHue(double h) { var (r, g, b) = ColorMath.FromHsv(h, 1, 1); return Color.FromRgb(To8(r), To8(g), To8(b)); }

        private void FromSquare(Point p)
        {
            _s = Math.Clamp(p.X / Math.Max(1, _square.ActualWidth), 0, 1);
            _v = 1 - Math.Clamp(p.Y / Math.Max(1, _square.ActualHeight), 0, 1);
            Refresh(true);
        }

        private void PlaceSquareThumb()
        {
            _squareThumb.Margin = new Thickness(_s * _square.ActualWidth - 7, (1 - _v) * _square.ActualHeight - 7, 0, 0);
        }

        private void Refresh(bool raise)
        {
            var c = Value;
            _hueBrush.Color = ColorFromHue(_h);
            var opaque = Color.FromRgb(c.R, c.G, c.B);
            _alphaGradient.GradientStops = new GradientStopCollection { new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 0), new GradientStop(opaque, 1) };
            _newSwatch.Background = Checker(c);
            PlaceSquareThumb();
            _hueBar.Set(_h / 360.0);
            _alphaBar.Set(_a);
            SyncFields(null);
            UpdateContrast();
            if (raise) Changed?.Invoke(c);
        }

        private void Commit()
        {
            var c = Value;
            AddRecent(c);
            Committed?.Invoke(c);
        }

        private static Brush Checker(Color c)
        {
            if (c.A == 255) return new SolidColorBrush(c);
            var dg = new DrawingGroup();
            dg.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(200, 200, 200)), null, new RectangleGeometry(new Rect(0, 0, 16, 16))));
            dg.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(240, 240, 240)), null,
                new GeometryGroup { Children = { new RectangleGeometry(new Rect(0, 0, 8, 8)), new RectangleGeometry(new Rect(8, 8, 8, 8)) } }));
            var tile = new DrawingBrush(dg) { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 16, 16), ViewportUnits = BrushMappingMode.Absolute };
            var over = new DrawingGroup();
            over.Children.Add(new GeometryDrawing(tile, null, new RectangleGeometry(new Rect(0, 0, 1, 1))));
            over.Children.Add(new GeometryDrawing(new SolidColorBrush(c), null, new RectangleGeometry(new Rect(0, 0, 1, 1))));
            return new DrawingBrush(over) { Stretch = Stretch.Fill };
        }

        // ------------------------------------------------------------ translator

        private void BuildFields()
        {
            static string N(double d, int dp = 0) => Math.Round(d, dp).ToString("0." + new string('#', dp), CultureInfo.InvariantCulture);
            _fields.Add(new Field
            {
                Label = "HEX", Hint = "#RGB, #RRGGBB, or #RRGGBBAA (the last two digits are opacity).",
                Format = (r, g, b, a) => a >= 0.999 ? $"#{To8(r):X2}{To8(g):X2}{To8(b):X2}" : $"#{To8(r):X2}{To8(g):X2}{To8(b):X2}{To8(a):X2}",
                Parse = t =>
                {
                    var m = Regex.Match(t.Trim(), "^#?([0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$");
                    if (!m.Success) return null;
                    var h = m.Groups[1].Value;
                    if (h.Length == 3) h = string.Concat(h.Select(ch => new string(ch, 2)));
                    int P(int i) => Convert.ToInt32(h.Substring(i, 2), 16);
                    return (P(0) / 255.0, P(2) / 255.0, P(4) / 255.0, h.Length == 8 ? P(6) / 255.0 : 1);
                }
            });
            _fields.Add(Numeric("RGB", "Red, green and blue from 0 to 255, optionally followed by opacity from 0 to 1.", 3, 4,
                (r, g, b, a) => a >= 0.999 ? $"rgb({N(r * 255)}, {N(g * 255)}, {N(b * 255)})" : $"rgba({N(r * 255)}, {N(g * 255)}, {N(b * 255)}, {N(a, 2)})",
                n => (n[0] / 255, n[1] / 255, n[2] / 255)));
            _fields.Add(Numeric("HSL", "Hue 0 to 360, saturation and lightness 0 to 100.", 3, 4,
                (r, g, b, a) => { var (h, s, l) = ColorMath.ToHsl(r, g, b); return $"hsl({N(h)}, {N(s * 100)}%, {N(l * 100)}%)"; },
                n => ColorMath.FromHsl(n[0], n[1] / 100, n[2] / 100)));
            _fields.Add(Numeric("HSV", "Hue 0 to 360, saturation and value (brightness) 0 to 100.", 3, 4,
                (r, g, b, a) => { var (h, s, v) = ColorMath.ToHsv(r, g, b); return $"hsv({N(h)}, {N(s * 100)}%, {N(v * 100)}%)"; },
                n => ColorMath.FromHsv(n[0], n[1] / 100, n[2] / 100)));
            _fields.Add(Numeric("HWB", "Hue 0 to 360, whiteness and blackness 0 to 100.", 3, 4,
                (r, g, b, a) => { var (h, w, k) = ColorMath.ToHwb(r, g, b); return $"hwb({N(h)} {N(w * 100)}% {N(k * 100)}%)"; },
                n => ColorMath.FromHwb(n[0], n[1] / 100, n[2] / 100)));
            _fields.Add(Numeric("CMYK", "Cyan, magenta, yellow and key (black) 0 to 100. A simple conversion with no printer profile.", 4, 5,
                (r, g, b, a) => { var (c, m, y, k) = ColorMath.ToCmyk(r, g, b); return $"cmyk({N(c * 100)}%, {N(m * 100)}%, {N(y * 100)}%, {N(k * 100)}%)"; },
                n => ColorMath.FromCmyk(n[0] / 100, n[1] / 100, n[2] / 100, n[3] / 100)));
            _fields.Add(Numeric("OKLCH", "Lightness 0 to 1, chroma about 0 to 0.4, hue 0 to 360. Values outside sRGB are clipped and the picker says so.", 3, 4,
                (r, g, b, a) => { var (l, c, h) = ColorMath.ToOklch(r, g, b); return $"oklch({N(l, 3)} {N(c, 3)} {N(h, 1)})"; },
                n => { var rgb = ColorMath.FromOklch(n[0], n[1], n[2], out var clip); _clipNote = clip; return rgb; }));
        }

        private bool _clipNote;

        private static Field Numeric(string label, string hint, int count, int maxCount, Func<double, double, double, double, string> format,
            Func<double[], (double R, double G, double B)> toRgb)
        {
            return new Field
            {
                Label = label, Hint = hint, Format = format,
                Parse = t =>
                {
                    var nums = Regex.Matches(t, @"-?\d*\.?\d+").Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)).ToArray();
                    if (nums.Length < count || nums.Length > maxCount) return null;
                    double a = 1;
                    if (nums.Length == count + 1 && label == "RGB") a = Math.Clamp(nums[count], 0, 1);
                    var (r, g, b) = toRgb(nums.Take(count).ToArray());
                    return (Math.Clamp(r, 0, 1), Math.Clamp(g, 0, 1), Math.Clamp(b, 0, 1), a);
                }
            };
        }

        private void SyncFields(Field except)
        {
            _syncing = true;
            var (r, g, b) = ColorMath.FromHsv(_h, _s, _v);
            foreach (var f in _fields)
            {
                if (f == except) { f.Box.ClearValue(Control.BorderBrushProperty); continue; }
                f.Box.Text = f.Format(r, g, b, _a);
                f.Box.ClearValue(Control.BorderBrushProperty);
            }
            _syncing = false;
            var name = NameOf(Value);
            _note.Text = (name != null ? $"Named colour: {name}. " : "") + "Colour space: sRGB. Edit any field and press Enter; the others follow.";
        }

        private void ApplyField(Field f)
        {
            if (_syncing) return;
            _clipNote = false;
            var parsed = f.Parse(f.Box.Text);
            if (parsed == null)
            {
                f.Box.BorderBrush = Ui.Res("B.Danger");
                _note.Text = $"{f.Label} could not be read. {f.Hint}";
                return;
            }
            var (r, g, b, a) = parsed.Value;
            var (h, s, v) = ColorMath.ToHsv(r, g, b);
            if (s > 0 && v > 0) _h = h;
            _s = s; _v = v; _a = a;
            Refresh(true);
            SyncFields(null);
            if (_clipNote) _note.Text = "That OKLCH value is outside the sRGB range, so it was clipped to the nearest displayable colour.";
            Commit();
        }

        private static string NameOf(Color c)
        {
            if (c.A != 255) return null;
            foreach (var p in typeof(Colors).GetProperties())
                if (p.GetValue(null) is Color k && k == c && p.Name != "Transparent") return p.Name;
            return null;
        }

        private void UpdateContrast()
        {
            if (_against is not Color bg) { _contrast.Text = ""; return; }
            var fg = Value;
            // composite a translucent colour over the comparison colour first
            double a = fg.A / 255.0;
            var mixed = Color.FromRgb((byte)Math.Round(fg.R * a + bg.R * (1 - a)), (byte)Math.Round(fg.G * a + bg.G * (1 - a)), (byte)Math.Round(fg.B * a + bg.B * (1 - a)));
            double ratio = ColorMath.Contrast(mixed, Color.FromRgb(bg.R, bg.G, bg.B));
            bool pass = ratio >= 4.5;
            _contrast.Text = $"Contrast {ratio:0.0#}:1 against the other colour. {(pass ? "Passes" : "Fails")} the 4.5:1 minimum for normal text.";
            _contrast.Foreground = pass ? Ui.Res("B.Text") : Ui.Res("B.Danger");
        }

        // ------------------------------------------------------------ swatches and recents

        private Button Swatch(Color c, string name)
        {
            var b = new Button
            {
                Width = 28, Height = 28, Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(0), Cursor = Cursors.Hand,
                Background = Checker(c), BorderBrush = Ui.Res("B.LineStrong"), BorderThickness = new Thickness(1), ToolTip = $"{name}  {c}",
                Template = SwatchTemplate()
            };
            AutomationProperties.SetName(b, $"Use colour {name}");
            b.Click += (s, e) => { SetColor(c, true); Commit(); };
            return b;
        }

        private static ControlTemplate SwatchTemplate()
        {
            var t = new ControlTemplate(typeof(Button));
            var f = new FrameworkElementFactory(typeof(Border));
            f.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            f.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            f.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            f.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            t.VisualTree = f;
            return t;
        }

        private void AddRecent(Color c)
        {
            var list = Settings.Current.RecentColours ??= new List<string>();
            var hex = c.ToString();
            list.Remove(hex);
            list.Insert(0, hex);
            if (list.Count > 12) list.RemoveRange(12, list.Count - 12);
            Settings.Save();
            RebuildRecent();
        }

        private void RebuildRecent()
        {
            _recent.Children.Clear();
            var list = Settings.Current.RecentColours ?? new List<string>();
            if (list.Count == 0) { _recent.Children.Add(new TextBlock { Text = "Colours you choose appear here.", FontSize = 12, Foreground = Ui.Res("B.TextMute") }); return; }
            var wrap = new WrapPanel { MaxWidth = 340 };
            foreach (var hex in list)
                if (ElementStyles.TryColour(hex, out var c)) wrap.Children.Add(Swatch(c, "recent"));
            _recent.Children.Add(wrap);
        }

        // ------------------------------------------------------------ eyedropper

        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
        [DllImport("gdi32.dll")] private static extern uint GetPixel(IntPtr dc, int x, int y);

        private void StartEyedropper()
        {
            // A near-invisible full-screen window takes the click so it never reaches the app underneath.
            var w = new Window
            {
                WindowStyle = WindowStyle.None, AllowsTransparency = true, ShowInTaskbar = false, Topmost = true, ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)), Cursor = Cursors.Cross,
                Left = SystemParameters.VirtualScreenLeft, Top = SystemParameters.VirtualScreenTop,
                Width = SystemParameters.VirtualScreenWidth, Height = SystemParameters.VirtualScreenHeight
            };
            var hint = new Border
            {
                Background = Ui.Res("B.Raised"), CornerRadius = new CornerRadius(14), Padding = new Thickness(14, 8, 14, 8),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 24, 0, 0),
                Child = new TextBlock { Text = "Click a pixel to pick its colour. Esc cancels.", Foreground = Ui.Res("B.Text"), FontSize = 14 }
            };
            w.Content = new Grid { Children = { hint } };
            w.MouseLeftButtonDown += (s, e) =>
            {
                Native.GetCursorPos(out var p);
                w.Hide();
                Color? picked = null;
                var dc = GetDC(IntPtr.Zero);
                try
                {
                    uint px = GetPixel(dc, p.X, p.Y);
                    if (px != 0xFFFFFFFF) picked = Color.FromRgb((byte)(px & 0xFF), (byte)((px >> 8) & 0xFF), (byte)((px >> 16) & 0xFF));
                }
                finally { ReleaseDC(IntPtr.Zero, dc); }
                w.Close();
                if (picked is Color c) { SetColor(c, true); Commit(); }
                else _note.Text = "That point could not be read from the screen.";
            };
            w.KeyDown += (s, e) => { if (e.Key == Key.Escape) w.Close(); };
            w.Show();
            w.Activate();
            w.Focus();
        }

        // ------------------------------------------------------------ slider bar

        /// <summary>A horizontal draggable bar with a thumb, a keyboard model and a name for screen readers.</summary>
        private sealed class Bar : Grid
        {
            public event Action<double> Moved;
            public event Action Done;
            public readonly Rectangle Track = new Rectangle { RadiusX = 9, RadiusY = 9, Height = 18, IsHitTestVisible = false };
            private readonly Border _thumb = new Border { Width = 8, Height = 24, CornerRadius = new CornerRadius(4), BorderThickness = new Thickness(2), BorderBrush = Brushes.White, Background = Brushes.Transparent, HorizontalAlignment = HorizontalAlignment.Left, IsHitTestVisible = false };
            private double _value;
            private readonly double _max;
            private readonly string _name;

            public Bar(string name, double max, bool wide)
            {
                _name = name; _max = max;
                Height = 26; Background = Brushes.Transparent; Focusable = true; Cursor = Cursors.Hand;
                Children.Add(Track); Children.Add(_thumb);
                _thumb.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 3, ShadowDepth = 0, Opacity = 0.7 };
                AutomationProperties.SetName(this, name);
                AutomationProperties.SetHelpText(this, "Drag, or use the left and right arrow keys.");
                MouseLeftButtonDown += (s, e) => { CaptureMouse(); Focus(); At(e.GetPosition(this)); };
                MouseMove += (s, e) => { if (IsMouseCaptured) At(e.GetPosition(this)); };
                MouseLeftButtonUp += (s, e) => { if (IsMouseCaptured) { ReleaseMouseCapture(); Done?.Invoke(); } };
                SizeChanged += (s, e) => Place();
                KeyDown += (s, e) =>
                {
                    double step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 0.1 : 0.01;
                    if (e.Key == Key.Left || e.Key == Key.Down) _value = Math.Clamp(_value - step, 0, 1);
                    else if (e.Key == Key.Right || e.Key == Key.Up) _value = Math.Clamp(_value + step, 0, 1);
                    else return;
                    e.Handled = true; Place(); Moved?.Invoke(_value * _max); Done?.Invoke();
                };
            }

            public Bar WithMargin(double l, double t, double r, double b) { Margin = new Thickness(l, t, r, b); return this; }

            public void Set(double fraction)
            {
                _value = Math.Clamp(fraction, 0, 1);
                Place();
                AutomationProperties.SetItemStatus(this, $"{_name} {Math.Round(_value * _max)}");
            }

            private void At(Point p)
            {
                _value = Math.Clamp(p.X / Math.Max(1, ActualWidth), 0, 1);
                Place();
                Moved?.Invoke(_value * _max);
            }

            private void Place() => _thumb.Margin = new Thickness(_value * Math.Max(0, ActualWidth - 8), 0, 0, 0);
        }
    }

    internal static class PickerExtensions
    {
        public static T WithMargin<T>(this T e, double l, double t, double r, double b) where T : FrameworkElement
        {
            e.Margin = new Thickness(l, t, r, b);
            return e;
        }
    }
}
