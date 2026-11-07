using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    /// <summary>
    /// Makes an anchored or floating panel resizable from its right edge, bottom edge and corner, and a floating one
    /// movable by its header. On the keyboard, Alt+Shift with the arrow keys resizes and Alt with the arrow keys moves a
    /// floating panel, while focus is inside it. The size and place are kept for each kind of panel; double-clicking the
    /// corner, or Alt+Shift+Home, puts it back as it was.
    /// </summary>
    public static class PanelSizer
    {
        private const double Step = 24;

        /// <param name="card">The panel's outer border.</param>
        /// <param name="tall">The part that takes the height, usually the scroll viewer inside.</param>
        /// <param name="key">Which kind of panel this is, such as "regex-builder"; its size is kept under this.</param>
        /// <param name="popup">The popup that holds a floating panel, to move it; null for one that only resizes.</param>
        /// <param name="header">What the pointer drags to move a floating panel.</param>
        /// <param name="defaultWidth">The width it starts at and goes back to; NaN to size it to its content.</param>
        public static void Attach(Border card, FrameworkElement tall, string key, double minWidth, double minHeight, Popup popup = null, FrameworkElement header = null, double defaultWidth = double.NaN)
        {
            if (!double.IsNaN(defaultWidth)) card.Width = defaultWidth;
            var inner = card.Child as UIElement;
            var host = new Grid();
            card.Child = host;
            if (inner != null) host.Children.Add(inner);
            var pad = card.Padding;

            Thumb Edge(Cursor cursor, HorizontalAlignment h, VerticalAlignment v, double w, double ht, Thickness margin)
            {
                var face = new FrameworkElementFactory(typeof(Border));
                face.SetValue(Border.BackgroundProperty, Brushes.Transparent);
                var t = new Thumb { Cursor = cursor, HorizontalAlignment = h, VerticalAlignment = v, Width = w, Height = ht, Margin = margin, Focusable = false, Template = new ControlTemplate(typeof(Thumb)) { VisualTree = face } };
                host.Children.Add(t);
                return t;
            }
            var right = Edge(Cursors.SizeWE, HorizontalAlignment.Right, VerticalAlignment.Stretch, 8, double.NaN, new Thickness(0, 0, -pad.Right, 0));
            var bottom = Edge(Cursors.SizeNS, HorizontalAlignment.Stretch, VerticalAlignment.Bottom, double.NaN, 8, new Thickness(0, 0, 0, -pad.Bottom));
            var corner = new Thumb { Cursor = Cursors.SizeNWSE, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Width = 18, Height = 18, Margin = new Thickness(0, 0, -pad.Right + 4, -pad.Bottom + 4), Focusable = false, ToolTip = "Drag to resize. Double-click to put it back." };
            var grip = new FrameworkElementFactory(typeof(System.Windows.Shapes.Path));
            grip.SetValue(System.Windows.Shapes.Path.DataProperty, Geometry.Parse("M16,6 L6,16 M16,11 L11,16"));
            grip.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "B.TextMute");
            grip.SetValue(System.Windows.Shapes.Shape.StrokeThicknessProperty, 1.6);
            grip.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right);
            grip.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Bottom);
            var cornerBox = new FrameworkElementFactory(typeof(Grid));
            cornerBox.SetValue(Panel.BackgroundProperty, Brushes.Transparent);
            cornerBox.AppendChild(grip);
            corner.Template = new ControlTemplate(typeof(Thumb)) { VisualTree = cornerBox };
            AutomationProperties.SetName(corner, "Resize");
            host.Children.Add(corner);

            var area = Ui.WorkArea;
            double W() => double.IsNaN(card.Width) ? card.ActualWidth : card.Width;
            double H() => double.IsNaN(tall.Height) ? tall.ActualHeight : tall.Height;
            void Size(double w, double h, bool save)
            {
                if (!double.IsNaN(w)) card.Width = Math.Max(minWidth, Math.Min(area.Width - 24, w));
                if (!double.IsNaN(h)) { tall.Height = Math.Max(minHeight, Math.Min(area.Height - 120, h)); tall.ClearValue(FrameworkElement.MaxHeightProperty); }
                if (save) Keep(key, card, tall, popup);
            }
            void Move(double dx, double dy)
            {
                if (popup == null) return;
                popup.HorizontalOffset += dx; popup.VerticalOffset += dy;
                Keep(key, card, tall, popup);
            }
            void Reset()
            {
                if (double.IsNaN(defaultWidth)) card.ClearValue(FrameworkElement.WidthProperty); else card.Width = defaultWidth;
                tall.ClearValue(FrameworkElement.HeightProperty);
                Settings.Current.PanelSizes?.Remove(key);
                Settings.Save();
            }
            right.DragDelta += (s, e) => Size(W() + e.HorizontalChange, double.NaN, false);
            bottom.DragDelta += (s, e) => Size(double.NaN, H() + e.VerticalChange, false);
            corner.DragDelta += (s, e) => Size(W() + e.HorizontalChange, H() + e.VerticalChange, false);
            foreach (var t in new[] { right, bottom, corner }) t.DragCompleted += (s, e) => Keep(key, card, tall, popup);
            corner.MouseDoubleClick += (s, e) => { Reset(); e.Handled = true; };

            if (popup != null && header != null)
            {
                Point? from = null;
                header.Cursor = Cursors.SizeAll;
                header.MouseLeftButtonDown += (s, e) => { if (e.OriginalSource is DependencyObject d && IsControl(d, header)) return; from = e.GetPosition(null); header.CaptureMouse(); e.Handled = true; };
                header.MouseMove += (s, e) => { if (from == null) return; var p = e.GetPosition(null); popup.HorizontalOffset += p.X - from.Value.X; popup.VerticalOffset += p.Y - from.Value.Y; };
                header.MouseLeftButtonUp += (s, e) => { if (from == null) return; from = null; header.ReleaseMouseCapture(); Keep(key, card, tall, popup); };
            }

            card.PreviewKeyDown += (s, e) =>
            {
                var k = e.Key == Key.System ? e.SystemKey : e.Key;
                var mods = Keyboard.Modifiers;
                if (mods == (ModifierKeys.Alt | ModifierKeys.Shift))
                {
                    switch (k)
                    {
                        case Key.Right: Size(W() + Step, double.NaN, true); e.Handled = true; break;
                        case Key.Left: Size(W() - Step, double.NaN, true); e.Handled = true; break;
                        case Key.Down: Size(double.NaN, H() + Step, true); e.Handled = true; break;
                        case Key.Up: Size(double.NaN, H() - Step, true); e.Handled = true; break;
                        case Key.Home: Reset(); e.Handled = true; break;
                    }
                }
                else if (mods == ModifierKeys.Alt && popup != null && header != null)
                {
                    switch (k)
                    {
                        case Key.Right: Move(Step, 0); e.Handled = true; break;
                        case Key.Left: Move(-Step, 0); e.Handled = true; break;
                        case Key.Down: Move(0, Step); e.Handled = true; break;
                        case Key.Up: Move(0, -Step); e.Handled = true; break;
                    }
                }
            };
            var help = AutomationProperties.GetHelpText(card);
            AutomationProperties.SetHelpText(card, (string.IsNullOrEmpty(help) ? "" : help + " ") + (popup != null && header != null
                ? "Alt with the arrow keys moves it, Alt+Shift with the arrow keys resizes it, and Alt+Shift+Home puts it back."
                : "Alt+Shift with the arrow keys resizes it, and Alt+Shift+Home puts it back."));

            // what was kept from last time
            if (Settings.Current.PanelSizes != null && Settings.Current.PanelSizes.TryGetValue(key, out var kept) && kept != null && kept.Length >= 2)
            {
                if (kept[0] > 0) card.Width = Math.Max(minWidth, Math.Min(area.Width - 24, kept[0]));
                if (kept[1] > 0) { tall.Height = Math.Max(minHeight, Math.Min(area.Height - 120, kept[1])); tall.ClearValue(FrameworkElement.MaxHeightProperty); }
                if (popup != null && header != null && kept.Length >= 4) { popup.HorizontalOffset = kept[2]; popup.VerticalOffset = kept[3]; }
            }
        }

        private static bool IsControl(DependencyObject d, DependencyObject stop)
        {
            for (; d != null && d != stop; d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
                if (d is ButtonBase || d is TextBox) return true;
            return false;
        }

        private static void Keep(string key, Border card, FrameworkElement tall, Popup popup)
        {
            Settings.Current.PanelSizes ??= new Dictionary<string, double[]>();
            Settings.Current.PanelSizes[key] = new[]
            {
                double.IsNaN(card.Width) ? 0 : card.Width, double.IsNaN(tall.Height) ? 0 : tall.Height,
                popup?.HorizontalOffset ?? 0, popup?.VerticalOffset ?? 0
            };
            Settings.Save();
        }
    }
}
