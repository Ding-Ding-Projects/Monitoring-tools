using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    /// <summary>
    /// Shows one authenticator account as a QR code so it can be scanned into a phone. The code contains the secret, so the window
    /// says so, hides itself after a minute and is opened only after the two-key confirmation or Windows Hello. The picture is drawn
    /// on this computer; nothing is sent anywhere.
    /// </summary>
    public sealed class QrShowWindow : AnchoredCard
    {
        private const int Seconds = 60;
        private readonly DispatcherTimer _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        private readonly TextBlock _left = SafetyUi.Text("", 12, "B.TextMute", null, 6);
        private int _remaining = Seconds;

        public QrShowWindow(AuthAccount account, FrameworkElement origin) : base(origin, "QR code for " + account.Title)
        {
            var body = new StackPanel();
            body.Children.Add(new TextBlock { Text = "AUTHENTICATOR SECRET", Style = Ui.Style("T.Eyebrow"), FontSize = 11 });
            body.Children.Add(new TextBlock { Text = account.Title, Style = Ui.Style("T.Title"), FontSize = 16, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
            body.Children.Add(SafetyUi.Text("Scan this with the authenticator app on your phone. The picture holds this account's secret: anyone who sees it can generate its codes. Close it when you are done.", 12, "B.Amber", null, 6));

            var uri = Totp.BuildUri(account.ToParams());
            if (QrEncoder.TryEncode(uri, out var modules, out var version, out var error))
            {
                int scale = Math.Max(3, 320 / (modules.GetLength(0) + 8));
                var img = new Image { Source = QrEncoder.ToBitmap(modules, scale), Stretch = Stretch.None, SnapsToDevicePixels = true };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.NearestNeighbor);
                AutomationProperties.SetName(img, "QR code for " + account.Title + ", version " + version);
                var frame = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(12), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 0), Child = img };
                body.Children.Add(frame);
                body.Children.Add(SafetyUi.Text("QR version " + version + ", error correction level M. Black on white on purpose, so every scanner can read it in light or dark mode.", 11, "B.TextMute", null, 6));
            }
            else body.Children.Add(SafetyUi.Text("A QR code could not be drawn: " + error + " Use the account's secret and settings by hand instead.", 12, "B.Amber", null, 8));

            body.Children.Add(_left);
            var close = Ui.Button("Close", Close, "Btn.Accent", Icons.Check);
            close.Margin = new Thickness(0, 8, 0, 0);
            body.Children.Add(close);
            SetBody(body);

            Update();
            _timer.Tick += (s, e) => { if (--_remaining <= 0) Close(); else Update(); };
            Loaded += (s, e) => _timer.Start();
            Closed += (s, e) => _timer.Stop();
        }

        private void Update() => _left.Text = "Hides itself in " + _remaining + " seconds.";
    }
}
