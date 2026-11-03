using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace MonitoringTools.UI
{
    /// <summary>Stroke icon geometry, 24×24 grid, drawn as outlines like the design.</summary>
    public static class Icons
    {
        public const string Text = "M4 6h16M4 12h10M4 18h13";
        public const string Link = "M10 14a4 4 0 0 0 5.7 0l3-3a4 4 0 0 0-5.7-5.7l-1 1M14 10a4 4 0 0 0-5.7 0l-3 3a4 4 0 0 0 5.7 5.7l1-1";
        public const string File = "M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8zM14 3v5h5";
        public const string Folder = "M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z";
        public const string Image = "M4 5h16v14H4zM4 16l5-5 4 4 2-2 5 5M15 9.5a1.5 1.5 0 1 1 0 .01";
        public const string Video = "M3 6h13v12H3zM16 10l5-3v10l-5-3";
        public const string Audio = "M9 18V6l11-2v12M9 18a3 3 0 1 1-3-3 3 3 0 0 1 3 3zM20 16a3 3 0 1 1-3-3 3 3 0 0 1 3 3z";
        public const string Mail = "M3 6h18v12H3zM3 7l9 6 9-6";
        public const string Palette = "M12 3a9 9 0 1 0 0 18c1.5 0 2-1 2-2s-1-1.5-1-2.5 1-1.5 2-1.5h2a4 4 0 0 0 4-4c0-4.4-4-8-9-8z";
        public const string Braces = "M8 4c-2 0-3 1-3 3v2c0 1-1 2-2 2 1 0 2 1 2 2v2c0 2 1 3 3 3M16 4c2 0 3 1 3 3v2c0 1 1 2 2 2-1 0-2 1-2 2v2c0 2-1 3-3 3";
        public const string Bolt = "M13 3L4 14h7l-1 7 9-11h-7z";
        public const string Wifi = "M2 8.8a15 15 0 0 1 20 0M5 12.6a10 10 0 0 1 14 0M8.5 16.4a5 5 0 0 1 7 0M12 20h.01";
        public const string WifiOff = "M2 8.8a15 15 0 0 1 20 0M5 12.6a10 10 0 0 1 14 0M8.5 16.4a5 5 0 0 1 7 0M3 3l18 18";
        public const string Close = "M6 6l12 12M18 6L6 18";
        public const string Check = "M5 12l5 5 9-10";
        public const string Pulse = "M3 12h4l3-7 4 14 3-7h4";
        public const string Monitor = "M3 4h18v14H3zM8 21h8";
        public const string Clipboard = "M7 3h10v4H7zM4 5h16v16H4z";
        public const string Clock = "M12 3a9 9 0 1 1 0 18 9 9 0 0 1 0-18zM12 7v5l3 2";
        public const string Restart = "M3 12a9 9 0 1 0 3-6.7L3 8M3 3v5h5";
        public const string Lines = "M4 7h16M4 12h16M4 17h10";
        public const string Trash = "M4 7h16M9 7V4h6v3M6 7l1 13h10l1-13";
        public const string Power = "M12 3v9M6.3 6.3a8 8 0 1 0 11.4 0";
        public const string Pause = "M9 5v14M15 5v14";
        public const string Game = "M6 9h4M8 7v4M15 10h.01M18 8h.01M5 5h14a3 3 0 0 1 3 3l-1 8a3 3 0 0 1-5 1.5L14 16h-4l-2 1.5A3 3 0 0 1 3 16L2 8a3 3 0 0 1 3-3z";
        public const string Shield = "M12 3l8 3v6c0 5-3.5 8-8 9-4.5-1-8-4-8-9V6z";
        public const string Git = "M6 3v12M18 9a3 3 0 1 0 0-.01M6 21a3 3 0 1 0 0-.01M18 12c0 4-6 3-12 6";
        public const string Box = "M12 3l8 4.5v9L12 21l-8-4.5v-9zM12 12l8-4.5M12 12v9M12 12L4 7.5";
        public const string Code = "M8 8l-4 4 4 4M16 8l4 4-4 4M14 5l-4 14";
        public const string Globe = "M12 3a9 9 0 1 1 0 18 9 9 0 0 1 0-18zM3 12h18M12 3c3 3 3 15 0 18M12 3c-3 3-3 15 0 18";
        public const string Plus = "M12 5v14M5 12h14";
        public const string Play = "M7 5l12 7-12 7z";
        public const string Drag = "M9 5h.01M15 5h.01M9 12h.01M15 12h.01M9 19h.01M15 19h.01";
    }

    public static class Ui
    {
        public static Brush Res(string key) => (Brush)Application.Current.Resources[key];
        public static Style Style(string key) => (Style)Application.Current.Resources[key];
        public static FontFamily Font(string key) => (FontFamily)Application.Current.Resources[key];
        public static Color C(string key) => (Color)Application.Current.Resources[key];

        public static Path Icon(string data, Brush stroke, double size = 16, double thickness = 2)
        {
            return new Path
            {
                Data = Geometry.Parse(data),
                Stroke = stroke,
                StrokeThickness = thickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Width = size, Height = size,
                Stretch = Stretch.Uniform,
                SnapsToDevicePixels = false
            };
        }

        public static Border IconChip(string data, Brush stroke, Brush wash, double box = 28, double icon = 16)
        {
            return new Border
            {
                Width = box, Height = box, CornerRadius = new CornerRadius(box * 0.29),
                Background = wash,
                Child = Icon(data, stroke, icon)
            };
        }

        public static TextBlock Text(string text, string style = "T.Body")
        {
            return new TextBlock { Text = text, Style = Style(style) };
        }

        public static TextBlock Mono(string text, Brush fg = null, double size = 12)
        {
            var t = new TextBlock { Text = text, Style = Style("T.Mono"), FontSize = size };
            if (fg != null) t.Foreground = fg;
            return t;
        }

        public static Button Button(string label, Action onClick, string style = "Btn.Ghost", string icon = null)
        {
            object content = label;
            if (icon != null)
            {
                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                var fg = style == "Btn.Accent" ? Res("B.MintInk") : style == "Btn.Alert" ? Res("B.AmberInk") : style == "Btn.Danger" ? Res("B.Danger") : Res("B.TextSoft");
                sp.Children.Add(new ContentControl { Content = Icon(icon, fg, 14), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
                sp.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
                content = sp;
            }
            var b = new Button { Content = content, Style = Style(style), Margin = new Thickness(0, 0, 8, 8) };
            b.Click += (s, e) => onClick?.Invoke();
            return b;
        }

        // -------------------------------------------------------------- motion helpers

        public static readonly IEasingFunction Out = new CubicEase { EasingMode = EasingMode.EaseOut };
        public static readonly IEasingFunction Spring = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 };

        public static void Fade(UIElement e, double to, double ms, double delayMs = 0, Action done = null)
        {
            var a = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = Out, BeginTime = TimeSpan.FromMilliseconds(delayMs) };
            if (done != null) a.Completed += (s, ev) => done();
            e.BeginAnimation(UIElement.OpacityProperty, a);
        }

        /// <summary>Rise-and-fade entrance used for staggered rows and cards.</summary>
        public static void Rise(FrameworkElement e, double delayMs, double distance = 10, double ms = 340)
        {
            var tt = new TranslateTransform(0, distance);
            e.RenderTransform = tt;
            e.Opacity = 0;
            tt.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(distance, 0, TimeSpan.FromMilliseconds(ms)) { EasingFunction = Spring, BeginTime = TimeSpan.FromMilliseconds(delayMs) });
            e.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(ms * 0.8)) { EasingFunction = Out, BeginTime = TimeSpan.FromMilliseconds(delayMs) });
        }

        public static void ColorTo(SolidColorBrush b, Color to, double ms)
        {
            b.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = Out });
        }

        public static string Bytes(long b)
        {
            if (b < 1024) return b + " B";
            if (b < 1024 * 1024) return (b / 1024.0).ToString("0.#") + " KB";
            if (b < 1024L * 1024 * 1024) return (b / 1024.0 / 1024).ToString("0.#") + " MB";
            return (b / 1024.0 / 1024 / 1024).ToString("0.##") + " GB";
        }
    }
}
