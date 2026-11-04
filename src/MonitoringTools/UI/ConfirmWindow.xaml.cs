using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
            if (danger) { Edge.Color = Ui.C("C.DangerWash"); BuildGate(ok); }
            SourceInitialized += (s, e) => Core.Native.ApplyDarkChrome(this);
            // Copy text only: no menu entry can press the confirm button or step around its two-key gate.
            ElementMenu.Attach(this, null, ElementMenu.Options.Copy);
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

        // ---- two-key super confirmation for destructive actions
        // Two independent keys unlock a full-range slider; only a slider dragged all the way confirms.
        // Emergency exit and Escape always cancel, and nothing happens until every step is done.

        private ToggleButton _key1, _key2;
        private Slider _slider;
        private Border _fill;
        private TextBlock _gateStatus;
        private bool _done;

        private void BuildGate(string ok)
        {
            OkBtn.Visibility = Visibility.Collapsed;
            CancelBtn.Content = "Emergency exit";
            Gate.Visibility = Visibility.Visible;
            Gate.Children.Add(new TextBlock { Text = "To go ahead, turn both keys, then slide all the way.", Style = Ui.Style("T.Body"), Foreground = Ui.Res("B.Text"), Margin = new Thickness(0, 0, 0, 10) });
            var keys = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
            _key1 = Key("First key");
            _key2 = Key("Second key");
            _key2.Margin = new Thickness(10, 0, 0, 0);
            keys.Children.Add(_key1);
            keys.Children.Add(_key2);
            Gate.Children.Add(keys);

            var track = new Grid { Height = 48 };
            var bg = new Border { CornerRadius = new CornerRadius(24), Background = Ui.Res("B.Sunken") };
            _fill = new Border { CornerRadius = new CornerRadius(24), Background = Ui.Res("B.Danger"), HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };
            _slider = new Slider { Minimum = 0, Maximum = 100, IsEnabled = false, VerticalAlignment = VerticalAlignment.Stretch, Margin = new Thickness(4, 0, 4, 0), SmallChange = 5, LargeChange = 25, Focusable = true };
            _slider.Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(
                "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Slider'>" +
                "<Grid Background='Transparent'><Track x:Name='PART_Track' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>" +
                "<Track.Thumb><Thumb Width='40' Height='40'><Thumb.Template><ControlTemplate TargetType='Thumb'>" +
                "<Border CornerRadius='20' Background='{DynamicResource B.Text}' BorderBrush='{DynamicResource B.Danger}' BorderThickness='3'/>" +
                "</ControlTemplate></Thumb.Template></Thumb></Track.Thumb></Track></Grid></ControlTemplate>");
            AutomationProperties.SetName(_slider, ok + ": slide to 100 to confirm");
            var label = new TextBlock { Text = "Slide to " + ok.ToLowerInvariant(), Foreground = Ui.Res("B.TextSoft"), FontFamily = Ui.Font("F.Body"), FontWeight = FontWeights.Medium, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
            label.Foreground = Ui.Res("B.Text");
            track.Children.Add(bg);
            track.Children.Add(_fill);
            track.Children.Add(_slider);
            track.Children.Add(label);
            Gate.Children.Add(track);
            _gateStatus = new TextBlock { Style = Ui.Style("T.Body"), FontSize = 12, Margin = new Thickness(4, 8, 0, 0), Text = "Both keys are off." };
            AutomationProperties.SetLiveSetting(_gateStatus, AutomationLiveSetting.Assertive);
            Gate.Children.Add(_gateStatus);

            void Keys()
            {
                bool both = _key1.IsChecked == true && _key2.IsChecked == true;
                _slider.IsEnabled = both;
                if (!both) _slider.Value = 0;
                _gateStatus.Text = both ? "Both keys are on. Slide all the way to confirm."
                    : _key1.IsChecked == true || _key2.IsChecked == true ? "One key is on. Turn the other key too." : "Both keys are off.";
                track.Opacity = both ? 1 : 0.5;
            }
            _key1.Click += (s, e) => Keys();
            _key2.Click += (s, e) => Keys();
            Keys();
            _slider.ValueChanged += (s, e) =>
            {
                _fill.Width = Math.Max(0, track.ActualWidth * _slider.Value / 100);
                if (!Core.Appearance.ReduceMotion) RootScale.ScaleX = RootScale.ScaleY = 1 + _slider.Value / 100 * 0.02;
                if (_slider.Value >= 100 && !_done && _key1.IsChecked == true && _key2.IsChecked == true) Complete();
            };
            // A drag let go before the end springs back, so a half-done slide never counts.
            _slider.PreviewMouseLeftButtonUp += (s, e) => { if (!_done) _slider.Value = 0; };
            PreviewKeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Escape) { e.Handled = true; Close(); } };
        }

        private ToggleButton Key(string name)
        {
            var t = new ToggleButton { Content = name, Height = 48, MinWidth = 150, Cursor = System.Windows.Input.Cursors.Hand, FontFamily = Ui.Font("F.Body"), FontWeight = FontWeights.Medium, FontSize = 14, Foreground = Ui.Res("B.Text") };
            AutomationProperties.SetName(t, name);
            var tpl = new ControlTemplate(typeof(ToggleButton));
            var bd = new FrameworkElementFactory(typeof(Border), "Bd");
            bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(24));
            bd.SetValue(Border.BackgroundProperty, Ui.Res("B.Sunken"));
            bd.SetValue(Border.BorderBrushProperty, Ui.Res("B.LineStrong"));
            bd.SetValue(Border.BorderThicknessProperty, new Thickness(2));
            bd.SetValue(Border.PaddingProperty, new Thickness(18, 0, 18, 0));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            bd.AppendChild(cp);
            tpl.VisualTree = bd;
            var on = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
            on.Setters.Add(new Setter(Border.BackgroundProperty, Ui.Res("B.DangerWash"), "Bd"));
            on.Setters.Add(new Setter(Border.BorderBrushProperty, Ui.Res("B.Danger"), "Bd"));
            on.Setters.Add(new Setter(Border.CornerRadiusProperty, new CornerRadius(14), "Bd"));
            tpl.Triggers.Add(on);
            var focus = new Trigger { Property = IsKeyboardFocusedProperty, Value = true };
            focus.Setters.Add(new Setter(Border.BorderBrushProperty, Ui.Res("B.Mint"), "Bd"));
            tpl.Triggers.Add(focus);
            t.Template = tpl;
            t.Checked += (s, e) => t.Content = name + " · on";
            t.Unchecked += (s, e) => t.Content = name;
            return t;
        }

        /// <summary>For the screenshot run only: turns both keys on and moves the slider part way, without confirming.</summary>
        public void ArmForPreview(double value)
        {
            if (_key1 == null) return;
            _key1.IsChecked = true; _key2.IsChecked = true;
            _key1.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            _slider.Value = Math.Min(value, 99);
        }

        private void Complete()
        {
            _done = true;
            _gateStatus.Text = "Confirmed.";
            _slider.IsEnabled = false;
            var extra = Extra.IsChecked == true;
            void Finish() { Close(); Confirmed?.Invoke(extra); }
            if (Core.Appearance.ReduceMotion) { Finish(); return; }
            RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.06, 1, TimeSpan.FromMilliseconds(420)) { EasingFunction = Ui.Spring });
            RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.06, 1, TimeSpan.FromMilliseconds(420)) { EasingFunction = Ui.Spring });
            Ui.Fade(Root, 0, 260, 420, Finish);
        }

    }
}
