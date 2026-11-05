using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    /// <summary>
    /// The dim sum surprise card: small, never activated, never focused, gone after a few seconds.
    /// It sits bottom-left so it never covers the toast corner.
    /// </summary>
    public sealed class ModesDimSumWindow : Window
    {
        public ModesDimSumWindow(DimSumDish dish, string languageMode)
        {
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            ShowInTaskbar = false; ShowActivated = false; Topmost = true; Focusable = false; ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;

            string name = languageMode == "en" ? dish.En : languageMode == "yue" ? dish.Zh : dish.En + " · " + dish.Zh;
            var bmp = new BitmapImage();
            bmp.BeginInit(); bmp.UriSource = new Uri(dish.ImagePath); bmp.DecodePixelWidth = 280; bmp.CacheOption = BitmapCacheOption.OnLoad; bmp.EndInit();
            var img = new Image { Source = bmp, Width = 140, Height = 140, Stretch = Stretch.UniformToFill, Margin = new Thickness(0, 0, 14, 0) };
            var alt = string.IsNullOrWhiteSpace(dish.AltEn) ? "Photograph of " + dish.En : dish.AltEn;
            AutomationProperties.SetName(img, alt);

            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, MaxWidth = 220 };
            text.Children.Add(new TextBlock { Text = "Dim sum", Style = Ui.Style("T.Eyebrow"), FontSize = 11 });
            text.Children.Add(new TextBlock { Text = name, Style = Ui.Style("T.Title"), FontSize = 16, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 6) });
            text.Children.Add(new TextBlock { Text = "Image from " + Modes.DimSumSource, Style = Ui.Style("T.Body"), FontSize = 11, Foreground = Ui.Res("B.TextMute"), TextWrapping = TextWrapping.Wrap });
            var close = Ui.Button("Dismiss", Close, "Btn.Ghost");
            close.Margin = new Thickness(0, 8, 0, 0); close.Focusable = false;
            text.Children.Add(close);

            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(img); row.Children.Add(text);
            var card = new Border
            {
                Background = Ui.Res("B.Surface"), BorderBrush = Ui.Res("B.LineStrong"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16), Padding = new Thickness(14), Child = row, Margin = new Thickness(12)
            };
            AutomationProperties.SetName(card, "Dim sum surprise: " + name);
            AutomationProperties.SetLiveSetting(card, AutomationLiveSetting.Polite);
            Content = card;
            ElementMenu.Attach(this);

            Loaded += (s, e) =>
            {
                var area = SystemParameters.WorkArea;
                Left = area.Left + 12; Top = area.Bottom - ActualHeight - 12;
                if (!Modes.ReduceMotion) Ui.Fade(card, 1, 260); else card.Opacity = 1;
            };
            card.Opacity = Modes.ReduceMotion ? 1 : 0;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
            timer.Tick += (s, e) => { timer.Stop(); if (IsLoaded) Close(); };
            timer.Start();
            Closed += (s, e) => timer.Stop();
        }
    }
}
