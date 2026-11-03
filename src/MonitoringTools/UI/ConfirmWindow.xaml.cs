using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MonitoringTools.UI
{
    public partial class ConfirmWindow : Window
    {
        public event Action<bool> Confirmed;

        public ConfirmWindow(string title, string body, string ok, string extra = null, bool danger = false)
        {
            InitializeComponent();
            TitleText.Text = title;
            BodyText.Text = body;
            OkBtn.Content = ok;
            if (!danger) OkBtn.Style = Ui.Style("Btn.Accent");
            IconHost.Content = danger
                ? Ui.IconChip(Icons.Trash, Ui.Res("B.Danger"), Ui.Res("B.DangerWash"), 44, 22)
                : Ui.IconChip(Icons.Shield, Ui.Res("B.Mint"), Ui.Res("B.MintWash"), 44, 22);
            if (extra == null) Extra.Visibility = Visibility.Collapsed;
            else Extra.Content = new TextBlock { Text = extra, Foreground = Ui.Res("B.TextSoft"), FontSize = 13, FontFamily = Ui.Font("F.Body") };
            if (danger) Edge.Color = Color.FromRgb(0x4A, 0x25, 0x25);
            SourceInitialized += (s, e) => Core.Native.ApplyDarkChrome(this);
            Loaded += (s, e) =>
            {
                RootScale.ScaleX = RootScale.ScaleY = 0.92;
                Root.Opacity = 0;
                RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(380)) { EasingFunction = Ui.Spring });
                RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(380)) { EasingFunction = Ui.Spring });
                Ui.Fade(Root, 1, 200);
                Activate();
            };
        }

        private void OnOk(object s, RoutedEventArgs e)
        {
            var extra = Extra.IsChecked == true;
            Close();
            Confirmed?.Invoke(extra);
        }

        private void OnCancel(object s, RoutedEventArgs e) => Close();
    }
}
