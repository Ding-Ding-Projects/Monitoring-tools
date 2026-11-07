using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using MonitoringTools.Clip;
using MonitoringTools.Clip.Smart;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    /// <summary>
    /// A copy pinned on screen: text, a table or a picture in a small window that stays on top. Drag it anywhere, make it
    /// see-through, or let clicks pass through to the app below. Esc or ✕ unpins it; Ctrl+C copies it again.
    /// </summary>
    public sealed class PinWindow : Window
    {
        private static readonly List<PinWindow> Open = new List<PinWindow>();
        private readonly ClipItem _item;
        private readonly Border _card;
        private readonly StackPanel _options;
        private bool _clickThrough;

        public static PinWindow Show(ClipItem item)
        {
            var w = new PinWindow(item);
            Open.Add(w);
            w.Closed += (s, e) => Open.Remove(w);
            w.Show();
            return w;
        }

        private PinWindow(ClipItem item)
        {
            _item = item;
            Title = "Pinned copy"; WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            Topmost = true; ShowInTaskbar = false; SizeToContent = SizeToContent.WidthAndHeight; ResizeMode = ResizeMode.NoResize;
            var area = Ui.WorkArea; Left = area.Left + 40 + Open.Count * 24; Top = area.Top + 60 + Open.Count * 24;
            MaxWidth = 640; MaxHeight = area.Height * 0.8;
            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape) Close();
                else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control) { ClipPower.Restore(_item); e.Handled = true; }
            };

            var close = Ui.Button("", Close, "Btn.Icon", Icons.Close);
            AutomationProperties.SetName(close, "Unpin (Esc)");
            var gear = Ui.Button("", () => _options.Visibility = _options.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible, "Btn.Icon", Icons.Wand);
            AutomationProperties.SetName(gear, "Options for this pin");
            var label = Ui.Mono(Describe(item) + " · drag to move", Ui.Res("B.TextSoft"), 11);
            label.VerticalAlignment = VerticalAlignment.Center; label.Margin = new Thickness(12, 0, 8, 0);
            var head = new DockPanel { Background = Ui.Res("B.Sunken"), Cursor = Cursors.SizeAll };
            DockPanel.SetDock(close, Dock.Right); DockPanel.SetDock(gear, Dock.Right);
            head.Children.Add(close); head.Children.Add(gear); head.Children.Add(label);
            head.MouseLeftButtonDown += (s, e) => { try { DragMove(); } catch { } };

            var opacity = new Slider { Minimum = 0.3, Maximum = 1, Value = 0.95, Width = 180, Margin = new Thickness(10, 0, 0, 0) };
            AutomationProperties.SetName(opacity, "How see-through");
            opacity.ValueChanged += (s, e) => _card.Opacity = opacity.Value;
            var through = new CheckBox { Style = Ui.Style("Switch"), Content = new TextBlock { Text = "Clicks pass through", Foreground = Ui.Res("B.TextSoft"), FontSize = 12.5 }, Margin = new Thickness(0, 8, 0, 0) };
            through.Click += (s, e) =>
            {
                _clickThrough = through.IsChecked == true;
                NativeInput.SetClickThrough(new WindowInteropHelper(this).Handle, _clickThrough);
                if (_clickThrough) App.Toasts.ShowNotice("Clicks pass through the pin", "Press " + Settings.Current.HotkeyPin + " to pin again, or close it from the tray.", App.Toasts.Mint, 4, null, Icons.Clipboard);
            };
            var copy = Ui.Button("Copy again", () => ClipPower.Restore(_item), "Btn.Accent", Icons.Clipboard);
            copy.ToolTip = "Ctrl+C"; copy.Margin = new Thickness(0, 10, 0, 0); copy.HorizontalAlignment = HorizontalAlignment.Right;
            var opRow = new StackPanel { Orientation = Orientation.Horizontal };
            opRow.Children.Add(new TextBlock { Text = "See-through", Foreground = Ui.Res("B.TextSoft"), FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center });
            opRow.Children.Add(opacity);
            _options = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(12, 10, 12, 12) };
            _options.Children.Add(opRow); _options.Children.Add(through); _options.Children.Add(copy);

            var stack = new StackPanel();
            stack.Children.Add(head);
            stack.Children.Add(Body(item));
            stack.Children.Add(_options);
            _card = new Border
            {
                Margin = new Thickness(12), CornerRadius = new CornerRadius(18), Background = Ui.Res("B.Raised"), ClipToBounds = true,
                BorderBrush = Ui.Res("B.Mint"), BorderThickness = new Thickness(2), Opacity = 0.95, Child = stack,
                Effect = (System.Windows.Media.Effects.Effect)Application.Current.Resources["Fx.Shadow"],
            };
            Content = _card;
        }

        private static string Describe(ClipItem i) => (i.BestFact?.Kind == "table" ? "Table" : i.Kind == ClipKind.Image ? "Picture" : "Text") + (i.SourceApp != null ? " · from " + i.SourceApp : "");

        private static FrameworkElement Body(ClipItem item)
        {
            if (item.Kind == ClipKind.Image && item.Image != null)
                return new Image { Source = item.Image, MaxWidth = 600, MaxHeight = 500, Stretch = Stretch.Uniform };
            var rows = item.Text != null ? Developer2.ReadTable(item.Text) : null;
            if (rows != null)
            {
                var g = new Grid { Margin = new Thickness(0) };
                for (int c = 0; c < rows[0].Length; c++) g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                for (int r = 0; r < Math.Min(rows.Count, 40); r++)
                {
                    g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    for (int c = 0; c < rows[r].Length; c++)
                    {
                        var cell = new Border { Background = r == 0 ? Ui.Res("B.Sunken") : null, Padding = new Thickness(12, 5, 12, 5), Child = Ui.Mono(rows[r][c], Ui.Res("B.Text"), 12.5) };
                        Grid.SetRow(cell, r); Grid.SetColumn(cell, c); g.Children.Add(cell);
                    }
                }
                return g;
            }
            var t = Ui.Mono(item.Text ?? item.Preview, Ui.Res("B.Text"), 13);
            t.TextWrapping = TextWrapping.Wrap; t.Margin = new Thickness(14, 10, 14, 12); t.MaxWidth = 560;
            Core.Language.SetLiteral(t, true);
            return new ScrollViewer { Content = t, MaxHeight = 480, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }
    }
}
