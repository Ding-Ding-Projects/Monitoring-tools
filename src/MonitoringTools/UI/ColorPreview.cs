using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using MonitoringTools.Clip;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    /// <summary>
    /// Colour previews for copied text and code: a strip of swatches, one per colour written in the copy.
    /// Each swatch shows the colour on a checkerboard (so transparency is visible), the text as written,
    /// and copies its hex value when pressed.
    /// </summary>
    public static class ColorPreview
    {
        public const int MaxShown = 12;

        /// <summary>A brush that shows a translucent colour over a light checkerboard; a solid colour paints as is.</summary>
        public static Brush Checker(Color c)
        {
            if (c.A == 255) return new SolidColorBrush(c);
            var dg = new DrawingGroup();
            dg.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(200, 200, 200)), null, new RectangleGeometry(new Rect(0, 0, 8, 8))));
            dg.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(240, 240, 240)), null,
                new GeometryGroup { Children = { new RectangleGeometry(new Rect(0, 0, 4, 4)), new RectangleGeometry(new Rect(4, 4, 4, 4)) } }));
            var tile = new DrawingBrush(dg) { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 8, 8), ViewportUnits = BrushMappingMode.Absolute };
            var over = new DrawingGroup();
            over.Children.Add(new GeometryDrawing(tile, null, new RectangleGeometry(new Rect(0, 0, 1, 1))));
            over.Children.Add(new GeometryDrawing(new SolidColorBrush(c), null, new RectangleGeometry(new Rect(0, 0, 1, 1))));
            return new DrawingBrush(over) { Stretch = Stretch.Fill };
        }

        /// <summary>One line describing a colour: hex, rgb and hsl.</summary>
        public static string Describe(Color c)
        {
            var f = new ColorScan.Found { Color = c };
            var (h, s, l) = ColorScan.Hsl(c);
            return $"{f.Hex} · {f.Rgb} · hsl({h}, {s}%, {l}%)";
        }

        /// <summary>The strip for every colour in <paramref name="text"/>, or null when it has none.</summary>
        public static FrameworkElement StripFor(string text, double topMargin = 10)
        {
            var found = ColorScan.Find(text, MaxShown + 1);
            return found.Count == 0 ? null : Strip(found, topMargin);
        }

        public static FrameworkElement Strip(IList<ColorScan.Found> found, double topMargin = 10)
        {
            var sp = new StackPanel { Margin = new Thickness(0, topMargin, 0, 0) };
            AutomationProperties.SetName(sp, found.Count == 1 ? "Colour in this copy" : "Colours in this copy");
            var label = new TextBlock
            {
                Text = found.Count == 1 ? "Colour in this copy" : (found.Count > MaxShown ? $"Colours in this copy · first {MaxShown}" : $"Colours in this copy · {found.Count}"),
                FontSize = 11.5, Foreground = Ui.Res("B.TextMute"), FontFamily = Ui.Font("F.Body"), Margin = new Thickness(0, 0, 0, 6)
            };
            sp.Children.Add(label);
            var wrap = new WrapPanel();
            int i = 0;
            foreach (var f in found.Take(MaxShown))
            {
                var chip = Chip(f);
                wrap.Children.Add(chip);
                Ui.Rise(chip, 60 + Math.Min(i++, 12) * 30, 6, 320);
            }
            sp.Children.Add(wrap);
            return sp;
        }

        private static FrameworkElement Chip(ColorScan.Found f)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new Border
            {
                Width = 22, Height = 22, CornerRadius = new CornerRadius(6), Background = Checker(f.Color),
                BorderBrush = Ui.Res("B.LineStrong"), BorderThickness = new Thickness(1), VerticalAlignment = VerticalAlignment.Center
            });
            var written = f.Text.Length > 28 ? f.Text.Substring(0, 27) + "…" : f.Text;
            var t = Ui.Mono(written, Ui.Res("B.Text"), 11.5);
            t.Margin = new Thickness(8, 0, 2, 0); t.VerticalAlignment = VerticalAlignment.Center;
            Language.SetLiteral(t, true);   // the copied text is data, never translated
            row.Children.Add(t);
            var b = new Button
            {
                Content = row, Padding = new Thickness(5, 4, 10, 4), Margin = new Thickness(0, 0, 6, 6), Cursor = System.Windows.Input.Cursors.Hand,
                Background = Ui.Res("B.Raised"), BorderThickness = new Thickness(0), Focusable = true,
                ToolTip = Describe(f.Color) + "\nPress to copy " + f.Hex
            };
            b.Template = ChipTemplate();
            AutomationProperties.SetName(b, $"Colour {f.Text}, {f.Hex}. Copy hex");
            b.Click += (s, e) =>
            {
                ClipboardWatcher.SetText(f.Hex);
                t.Text = "copied " + f.Hex;
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.4) };
                timer.Tick += (o, ev) => { timer.Stop(); t.Text = written; };
                timer.Start();
                e.Handled = true;
            };
            return b;
        }

        private static ControlTemplate _template;
        private static ControlTemplate ChipTemplate()
        {
            if (_template != null) return _template;
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            border.AppendChild(cp);
            _template = new ControlTemplate(typeof(Button)) { VisualTree = border };
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("B.SecondaryContainer")));
            var focus = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
            focus.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("B.SecondaryContainer")));
            _template.Triggers.Add(hover); _template.Triggers.Add(focus);
            return _template;
        }
    }
}
