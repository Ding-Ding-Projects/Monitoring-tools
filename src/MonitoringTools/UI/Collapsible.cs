using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    /// <summary>
    /// A search bar, filter row or statistics panel that folds away. The header is one keyboard stop with a visible
    /// focus ring; screen readers hear it expand and collapse. Whether it is open is kept per panel. A panel that only
    /// describes a collection starts closed, and a closed panel that hides an active filter says so in its header.
    /// </summary>
    public sealed class Collapsible : Expander
    {
        private readonly string _key;
        private readonly TextBlock _summary;
        private readonly Border _badge;
        private readonly TextBlock _badgeText;
        private readonly RotateTransform _turn = new RotateTransform(0);
        private bool _ready;

        private const string TemplateXaml =
            "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Expander'>" +
            "<StackPanel>" +
            "<ToggleButton x:Name='Head' IsChecked='{Binding IsExpanded, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}' Cursor='Hand' FocusVisualStyle='{x:Null}' MinHeight='44' HorizontalContentAlignment='Stretch'>" +
            "<ToggleButton.Template><ControlTemplate TargetType='ToggleButton'><Grid>" +
            "<Border x:Name='Hi' CornerRadius='16' Background='{DynamicResource B.Text}' Opacity='0'/>" +
            "<Border x:Name='Ring' CornerRadius='16' BorderBrush='{DynamicResource B.Mint}' BorderThickness='2' Opacity='0'/>" +
            "<ContentPresenter Margin='12,6,12,6' VerticalAlignment='Center' HorizontalAlignment='Stretch'/></Grid>" +
            "<ControlTemplate.Triggers>" +
            "<Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Hi' Property='Opacity' Value='0.06'/></Trigger>" +
            "<Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Ring' Property='Opacity' Value='1'/></Trigger>" +
            "</ControlTemplate.Triggers></ControlTemplate></ToggleButton.Template>" +
            "<ContentPresenter Content='{TemplateBinding Header}'/>" +
            "</ToggleButton>" +
            "<ContentPresenter x:Name='Body' Visibility='Collapsed'/>" +
            "</StackPanel>" +
            "<ControlTemplate.Triggers><Trigger Property='IsExpanded' Value='True'><Setter TargetName='Body' Property='Visibility' Value='Visible'/></Trigger></ControlTemplate.Triggers>" +
            "</ControlTemplate>";

        private static ControlTemplate _template;

        /// <param name="key">Where the open or closed state is kept, such as "overview.stats".</param>
        /// <param name="startOpen">How it starts the first time: closed for a panel that only describes a collection.</param>
        public Collapsible(string key, string title, FrameworkElement body, bool startOpen)
        {
            _key = key;
            Template = _template ??= (ControlTemplate)System.Windows.Markup.XamlReader.Parse(TemplateXaml);
            Content = body;
            Margin = new Thickness(0, 0, 0, Appearance.Gap(8));

            var head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.ColumnDefinitions.Add(new ColumnDefinition());
            var chevron = Ui.Icon("M9 6l6 6-6 6", Ui.Res("B.TextSoft"), 16, 2);
            chevron.RenderTransformOrigin = new Point(0.5, 0.5);
            chevron.RenderTransform = _turn;
            chevron.Margin = new Thickness(0, 2, 10, 0);
            chevron.VerticalAlignment = VerticalAlignment.Top;
            head.Children.Add(chevron);
            var words = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
            words.Children.Add(new TextBlock { Text = title, FontFamily = Ui.Font("F.Body"), FontWeight = FontWeights.Medium, FontSize = 14.5, Foreground = Ui.Res("B.Text"), Margin = new Thickness(0, 0, 12, 0), TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center });
            _summary = new TextBlock { FontFamily = Ui.Font("F.Body"), FontSize = 12.5, Foreground = Ui.Res("B.TextMute"), TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            words.Children.Add(_summary);
            _badgeText = new TextBlock { FontFamily = Ui.Font("F.Body"), FontSize = 12, FontWeight = FontWeights.Medium, TextWrapping = TextWrapping.Wrap };
            _badgeText.SetResourceReference(TextBlock.ForegroundProperty, "B.Amber");
            _badge = new Border { CornerRadius = new CornerRadius(11), Padding = new Thickness(9, 2, 9, 3), Child = _badgeText, Visibility = Visibility.Collapsed, VerticalAlignment = VerticalAlignment.Center };
            _badge.SetResourceReference(Border.BackgroundProperty, "B.AmberWash");
            words.Children.Add(_badge);
            Grid.SetColumn(words, 1);
            head.Children.Add(words);
            Header = head;

            var folds = Settings.Current.Folds;
            IsExpanded = folds != null && folds.TryGetValue(key, out var open) ? open : startOpen;
            _turn.Angle = IsExpanded ? 90 : 0;
            AutomationProperties.SetName(this, title);
            Expanded += (s, e) => Changed();
            Collapsed += (s, e) => Changed();
            Loaded += (s, e) => { _ready = true; Status(); };
        }

        private void Changed()
        {
            if (Ui.Reduced || !_ready) _turn.Angle = IsExpanded ? 90 : 0;
            else _turn.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(IsExpanded ? 90 : 0, TimeSpan.FromMilliseconds(180)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            if (!_ready) return;
            Settings.Current.Folds ??= new System.Collections.Generic.Dictionary<string, bool>();
            Settings.Current.Folds[_key] = IsExpanded;
            Settings.Save();
            Status();
        }

        private void Status()
        {
            if (GetTemplateChild("Head") is System.Windows.Controls.Primitives.ToggleButton head)
            {
                AutomationProperties.SetName(head, AutomationProperties.GetName(this) + (_badge.Visibility == Visibility.Visible ? ", " + _badgeText.Text : ""));
                AutomationProperties.SetItemStatus(head, IsExpanded ? "expanded" : "collapsed");
            }
        }

        /// <summary>A short line beside the title, such as the figures a closed statistics panel holds.</summary>
        public void SetSummary(string text)
        {
            _summary.Text = text ?? "";
            _summary.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
        }

        /// <summary>Says an active filter is in force, so closing the panel never hides it silently. Null clears it.</summary>
        public void SetBadge(string text)
        {
            _badgeText.Text = text ?? "";
            _badge.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
            Status();
        }
    }
}
