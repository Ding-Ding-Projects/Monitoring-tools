using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace MonitoringTools.UI.Dialogs
{
    /// <summary>One choice in a segmented control or a dropdown. A disabled choice carries the reason it is off.</summary>
    public sealed class Choice
    {
        public string Id, Label, DisabledReason;
        public Choice(string id, string label, string disabledReason = null) { Id = id; Label = label; DisabledReason = disabledReason; }
        public bool Enabled => DisabledReason == null;
    }

    /// <summary>A pill-shaped segmented control. One tab stop; the arrow keys move the selection; the chosen segment shows a check.</summary>
    public sealed class Segmented : Border
    {
        private readonly List<Choice> _choices;
        private readonly List<Border> _segments = new List<Border>();
        private readonly string _name;
        public string SelectedId { get; private set; }
        public event Action<string> Changed;

        public Segmented(string name, IEnumerable<Choice> choices, string selected)
        {
            _name = name; _choices = choices.ToList(); SelectedId = selected;
            Focusable = true; KeyboardNavigation.SetIsTabStop(this, true); FocusVisualStyle = null;
            BorderBrush = Ui.Res("B.LineStrong"); BorderThickness = new Thickness(1); CornerRadius = new CornerRadius(20); Height = 40;
            HorizontalAlignment = HorizontalAlignment.Left; SnapsToDevicePixels = true;
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var c in _choices)
            {
                bool firstSeg = _segments.Count == 0, lastSeg = _segments.Count == _choices.Count - 1;
                var seg = new Border { Tag = c, Padding = new Thickness(18, 0, 18, 0), MinWidth = 64, Cursor = Cursors.Hand, Background = Brushes.Transparent, CornerRadius = new CornerRadius(firstSeg ? 19 : 0, lastSeg ? 19 : 0, lastSeg ? 19 : 0, firstSeg ? 19 : 0) };
                var inner = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                seg.Child = inner;
                seg.MouseLeftButtonUp += (s, e) => { if (c.Enabled) Select(c.Id); Focus(); };
                _segments.Add(seg); row.Children.Add(seg);
            }
            Child = row;
            AutomationProperties.SetName(this, name);
            PreviewKeyDown += (s, e) =>
            {
                int dir = e.Key == Key.Right || e.Key == Key.Down ? 1 : e.Key == Key.Left || e.Key == Key.Up ? -1 : 0;
                if (dir == 0) return;
                e.Handled = true;
                int i = _choices.FindIndex(c => c.Id == SelectedId);
                for (int step = 1; step <= _choices.Count; step++)
                {
                    var next = _choices[((i + dir * step) % _choices.Count + _choices.Count) % _choices.Count];
                    if (next.Enabled) { Select(next.Id); break; }
                }
            };
            GotKeyboardFocus += (s, e) => Paint();
            LostKeyboardFocus += (s, e) => Paint();
            Paint();
        }

        public void Select(string id, bool raise = true)
        {
            if (id == SelectedId) return;
            if (!_choices.Any(c => c.Id == id && c.Enabled)) return;
            SelectedId = id; Paint();
            if (raise) Changed?.Invoke(id);
        }

        /// <summary>Turns a choice on or off with the reason it is off. A selected choice that becomes unavailable moves to the first enabled one.</summary>
        public void SetDisabled(string id, string reason)
        {
            var c = _choices.First(x => x.Id == id);
            c.DisabledReason = reason;
            if (reason != null && SelectedId == id) { var alt = _choices.FirstOrDefault(x => x.Enabled); if (alt != null) Select(alt.Id); }
            Paint();
        }

        private void Paint()
        {
            BorderBrush = IsKeyboardFocused ? Ui.Res("B.Mint") : Ui.Res("B.LineStrong");
            BorderThickness = new Thickness(IsKeyboardFocused ? 2 : 1);
            for (int i = 0; i < _segments.Count; i++)
            {
                var c = _choices[i]; var seg = _segments[i]; bool on = c.Id == SelectedId;
                var inner = (StackPanel)seg.Child; inner.Children.Clear();
                seg.Background = on ? Ui.Res("B.SecondaryContainer") : Brushes.Transparent;
                var fg = on ? Ui.Res("B.OnSecondaryContainer") : Ui.Res("B.TextSoft");
                if (on) inner.Children.Add(new System.Windows.Shapes.Path { Data = Geometry.Parse(Icons.Check), Stroke = fg, StrokeThickness = 2, Width = 14, Height = 14, Stretch = Stretch.Uniform, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, Margin = new Thickness(0, 0, 8, 0) });
                inner.Children.Add(new TextBlock { Text = c.Label, Foreground = fg, FontFamily = Ui.Font("F.Body"), FontSize = 14, FontWeight = on ? FontWeights.Medium : FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center });
                seg.Opacity = c.Enabled ? 1 : 0.45;
                seg.ToolTip = c.Enabled ? null : c.DisabledReason;
            }
            var sel = _choices.FirstOrDefault(c => c.Id == SelectedId);
            AutomationProperties.SetItemStatus(this, sel == null ? "" : "selected: " + sel.Label);
            AutomationProperties.SetHelpText(this, string.Join(", ", _choices.Select(c => c.Label + (c.Enabled ? "" : " (unavailable: " + c.DisabledReason + ")"))));
        }
    }

    /// <summary>A closed field that opens a list. Up and Down change the value without opening it; Enter or Space opens the list.</summary>
    public sealed class Dropdown : Border
    {
        private readonly List<Choice> _choices;
        private readonly TextBlock _text = new TextBlock { VerticalAlignment = VerticalAlignment.Center, FontFamily = Ui.Font("F.Body"), FontSize = 14, Foreground = Ui.Res("B.Text"), TextTrimming = TextTrimming.CharacterEllipsis };
        private readonly Popup _popup;
        private readonly StackPanel _list = new StackPanel();
        public string SelectedId { get; private set; }
        public event Action<string> Changed;
        public bool IsOpen => _popup.IsOpen;

        public Dropdown(string name, IEnumerable<Choice> choices, string selected)
        {
            _choices = choices.ToList(); SelectedId = selected;
            Focusable = true; KeyboardNavigation.SetIsTabStop(this, true); FocusVisualStyle = null; Height = 48; MinWidth = 120;
            CornerRadius = new CornerRadius(12); Background = Ui.Res("B.Sunken"); BorderThickness = new Thickness(2); BorderBrush = Brushes.Transparent; Cursor = Cursors.Hand;
            var g = new Grid { Margin = new Thickness(14, 0, 12, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(_text);
            var chev = Ui.Icon("M6 9l6 6 6-6", Ui.Res("B.TextSoft"), 16);
            Grid.SetColumn(chev, 1); g.Children.Add(chev);
            Child = g;
            AutomationProperties.SetName(this, name);
            KeyboardNavigation.SetDirectionalNavigation(_list, KeyboardNavigationMode.Cycle);
            var card = new Border { Background = Ui.Res("B.Raised"), BorderBrush = Ui.Res("B.LineStrong"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(16), Padding = new Thickness(6), Margin = new Thickness(0, 6, 12, 12), Child = new ScrollViewer { Content = _list, MaxHeight = 280, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, Effect = (System.Windows.Media.Effects.Effect)Application.Current.Resources["Fx.Shadow"] };
            _popup = new Popup { Child = card, PlacementTarget = this, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true, PopupAnimation = PopupAnimation.Fade };
            _popup.Opened += (s, e) => { card.MinWidth = Math.Max(0, ActualWidth - 12); BuildList(); };
            _popup.Closed += (s, e) => Paint();
            MouseLeftButtonUp += (s, e) => { Focus(); Toggle(); };
            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter || e.Key == Key.Space) { e.Handled = true; Toggle(); }
                else if ((e.Key == Key.Down || e.Key == Key.Up) && !_popup.IsOpen) { e.Handled = true; Step(e.Key == Key.Down ? 1 : -1); }
                else if (e.Key == Key.Escape && _popup.IsOpen) { e.Handled = true; _popup.IsOpen = false; Focus(); }
            };
            GotKeyboardFocus += (s, e) => Paint(); LostKeyboardFocus += (s, e) => Paint(); IsEnabledChanged += (s, e) => Paint();
            Paint();
        }

        private void Toggle() { if (IsEnabled) _popup.IsOpen = !_popup.IsOpen; }

        private void Step(int dir)
        {
            int i = _choices.FindIndex(c => c.Id == SelectedId);
            for (int step = 1; step <= _choices.Count; step++)
            {
                var n = _choices[((i + dir * step) % _choices.Count + _choices.Count) % _choices.Count];
                if (n.Enabled) { Select(n.Id); return; }
            }
        }

        private void BuildList()
        {
            _list.Children.Clear();
            Button focus = null;
            foreach (var c in _choices)
            {
                var b = new Button { Style = Ui.Style("Btn.Row"), HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(12, 8, 12, 8), Height = double.NaN, IsEnabled = c.Enabled };
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                row.Children.Add(new TextBlock { Text = c.Label, FontFamily = Ui.Font("F.Body"), FontSize = 14, VerticalAlignment = VerticalAlignment.Center });
                if (!c.Enabled) row.Children.Add(new TextBlock { Text = " · " + c.DisabledReason, FontSize = 12, Foreground = Ui.Res("B.TextSoft"), VerticalAlignment = VerticalAlignment.Center });
                b.Content = row;
                if (c.Id == SelectedId) { b.Background = Ui.Res("B.SecondaryContainer"); b.Foreground = Ui.Res("B.OnSecondaryContainer"); focus = b; }
                AutomationProperties.SetName(b, c.Label + (c.Id == SelectedId ? ", selected" : ""));
                var id = c.Id;
                b.Click += (s, e) => { _popup.IsOpen = false; Select(id); Focus(); };
                _list.Children.Add(b);
            }
            Dispatcher.BeginInvoke(new Action(() => (focus ?? _list.Children.OfType<Button>().FirstOrDefault())?.Focus()), System.Windows.Threading.DispatcherPriority.Input);
        }

        public void Select(string id, bool raise = true)
        {
            if (id == SelectedId || !_choices.Any(c => c.Id == id && c.Enabled)) return;
            SelectedId = id; Paint();
            if (raise) Changed?.Invoke(id);
        }

        public void SetChoices(IEnumerable<Choice> choices, string selected)
        {
            _choices.Clear(); _choices.AddRange(choices);
            SelectedId = _choices.Any(c => c.Id == selected && c.Enabled) ? selected : _choices.FirstOrDefault(c => c.Enabled)?.Id;
            Paint();
        }

        private void Paint()
        {
            _text.Text = _choices.FirstOrDefault(c => c.Id == SelectedId)?.Label ?? "";
            BorderBrush = IsKeyboardFocused || _popup.IsOpen ? Ui.Res("B.Mint") : Brushes.Transparent;
            Opacity = IsEnabled ? 1 : 0.45;
            AutomationProperties.SetItemStatus(this, "selected: " + _text.Text);
        }
    }

    internal static class DialogParts
    {
        public static TextBlock Label(string text) => new TextBlock { Text = text, FontFamily = Ui.Font("F.Body"), FontSize = 12, Foreground = Ui.Res("B.TextSoft"), Margin = new Thickness(0, 14, 0, 6), TextWrapping = TextWrapping.Wrap };

        public static TextBlock Hint(string text) => new TextBlock { Text = text, FontFamily = Ui.Font("F.Body"), FontSize = 12, Foreground = Ui.Res("B.TextSoft"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 6, 0, 0) };

        public static FrameworkElement Section(string iconPath, string title) => new StackPanel
        {
            Orientation = Orientation.Horizontal, Margin = new Thickness(0, 18, 0, 0),
            Children =
            {
                Ui.Icon(iconPath, Ui.Res("B.Mint"), 16, 2),
                new TextBlock { Text = title, FontFamily = Ui.Font("F.Body"), FontSize = 14, FontWeight = FontWeights.Medium, Foreground = Ui.Res("B.Mint"), Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center }
            }
        };

        /// <summary>A switch with a title and a line of explanation. The title and line are the control's accessible name and help.</summary>
        public static CheckBox SwitchRow(string title, string sub, bool on, out FrameworkElement row)
        {
            var text = new StackPanel();
            text.Children.Add(new TextBlock { Text = title, FontFamily = Ui.Font("F.Body"), FontSize = 14, Foreground = Ui.Res("B.Text") });
            var subText = new TextBlock { Text = sub, FontFamily = Ui.Font("F.Body"), FontSize = 12, Foreground = Ui.Res("B.TextSoft"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
            text.Children.Add(subText);
            var cb = new CheckBox { Style = Ui.Style("Switch"), IsChecked = on, Content = text, HorizontalContentAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 16, 0, 0) };
            AutomationProperties.SetName(cb, title);
            AutomationProperties.SetHelpText(cb, sub);
            row = cb;
            return cb;
        }

        /// <summary>The text of a switch row's explanation, so a dialog can change it with the state.</summary>
        public static void SetSub(CheckBox row, string sub)
        {
            if (row.Content is StackPanel sp && sp.Children.Count > 1 && sp.Children[1] is TextBlock t) t.Text = sub;
            AutomationProperties.SetHelpText(row, sub);
        }

        public static Border Banner(string iconPath, string text, string tone)
        {
            Brush wash = tone == "amber" ? Ui.Res("B.AmberWash") : tone == "danger" ? Ui.Res("B.DangerWash") : Ui.Res("B.Sunken");
            Brush ink = tone == "amber" ? Ui.Res("B.Amber") : tone == "danger" ? Ui.Res("B.Danger") : Ui.Res("B.TextSoft");
            var tb = new TextBlock { Text = text, FontFamily = Ui.Font("F.Body"), FontSize = 12, Foreground = Ui.Res("B.Text"), TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition());
            var icon = Ui.Icon(iconPath, ink, 18); icon.Margin = new Thickness(0, 0, 12, 0); icon.VerticalAlignment = VerticalAlignment.Top;
            g.Children.Add(icon); Grid.SetColumn(tb, 1); g.Children.Add(tb);
            return new Border { Background = wash, CornerRadius = new CornerRadius(16), Padding = new Thickness(14, 12, 14, 12), Child = g };
        }

        public static Border Pill(string text, string tone = "mint")
        {
            Brush bg = tone == "mint" ? Ui.Res("B.MintWash") : tone == "danger" ? Ui.Res("B.DangerWash") : tone == "amber" ? Ui.Res("B.AmberWash") : Ui.Res("B.Sunken");
            Brush fg = tone == "mint" ? Ui.Res("B.OnPrimaryContainer") : tone == "danger" ? Ui.Res("B.Danger") : tone == "amber" ? Ui.Res("B.Amber") : Ui.Res("B.TextSoft");
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            if (tone == "mint") sp.Children.Add(new ContentControl { Content = Ui.Icon(Icons.Check, fg, 12, 2), Margin = new Thickness(0, 0, 6, 0), Focusable = false, VerticalAlignment = VerticalAlignment.Center });
            sp.Children.Add(new TextBlock { Text = text, FontSize = 12, FontWeight = FontWeights.Medium, Foreground = fg, FontFamily = Ui.Font("F.Body"), VerticalAlignment = VerticalAlignment.Center });
            return new Border { Background = bg, CornerRadius = new CornerRadius(12), Padding = new Thickness(10, 3, 10, 4), HorizontalAlignment = HorizontalAlignment.Left, Child = sp };
        }

        public static ProgressBar Bar() => new ProgressBar
        {
            Height = 8, Minimum = 0, Maximum = 1000, Foreground = Ui.Res("B.Mint"), Background = Ui.Res("B.Sunken"), BorderThickness = new Thickness(0),
            Template = (ControlTemplate)XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ProgressBar'><Grid><Border x:Name='PART_Track' CornerRadius='4' Background='{TemplateBinding Background}'/><Border x:Name='PART_Indicator' CornerRadius='4' Background='{TemplateBinding Foreground}' HorizontalAlignment='Left'/></Grid></ControlTemplate>")
        };

        /// <summary>A slider whose stops are named. The labels sit under the stops; arrow keys step one stop.</summary>
        public static FrameworkElement StopSlider(string name, string[] labels, int value, Action<int> changed, out Slider slider)
        {
            var sl = new Slider { Minimum = 0, Maximum = labels.Length - 1, Value = value, TickFrequency = 1, IsSnapToTickEnabled = true, SmallChange = 1, LargeChange = 1, Focusable = true, FocusVisualStyle = null, Template = SliderTemplate(), Margin = new Thickness(0, 0, 0, 0) };
            AutomationProperties.SetName(sl, name);
            AutomationProperties.SetItemStatus(sl, labels[Math.Clamp(value, 0, labels.Length - 1)]);
            var ticks = new Canvas { Height = 8, IsHitTestVisible = false };
            var texts = new Canvas { Height = 20, IsHitTestVisible = false };
            var blocks = labels.Select(l => new TextBlock { Text = l, FontFamily = Ui.Font("F.Body"), FontSize = 12 }).ToList();
            foreach (var b in blocks) texts.Children.Add(b);
            var dots = labels.Select(_ => new System.Windows.Shapes.Ellipse { Width = 4, Height = 4, Fill = Ui.Res("B.LineStrong") }).ToList();
            foreach (var d in dots) ticks.Children.Add(d);
            void Layout()
            {
                double w = sl.ActualWidth; if (w <= 0) return;
                double half = 10, span = w - 2 * half;
                for (int i = 0; i < labels.Length; i++)
                {
                    double x = half + span * i / (labels.Length - 1);
                    Canvas.SetLeft(dots[i], x - 2); Canvas.SetTop(dots[i], 2);
                    blocks[i].Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    double left = Math.Clamp(x - blocks[i].DesiredSize.Width / 2, 0, Math.Max(0, w - blocks[i].DesiredSize.Width));
                    Canvas.SetLeft(blocks[i], left);
                    bool on = i == (int)Math.Round(sl.Value);
                    blocks[i].Foreground = on ? Ui.Res("B.Text") : Ui.Res("B.TextSoft");
                    blocks[i].FontWeight = on ? FontWeights.Medium : FontWeights.Normal;
                }
            }
            sl.SizeChanged += (s, e) => Layout();
            sl.ValueChanged += (s, e) => { var v = (int)Math.Round(sl.Value); AutomationProperties.SetItemStatus(sl, labels[v]); Layout(); changed?.Invoke(v); };
            var host = new StackPanel();
            host.Children.Add(sl); host.Children.Add(ticks); host.Children.Add(texts);
            slider = sl;
            host.Loaded += (s, e) => Layout();
            return host;
        }

        private static ControlTemplate _slider;
        private static ControlTemplate SliderTemplate()
        {
            if (_slider != null) return _slider;
            const string xaml = @"
<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Slider'>
  <Grid Height='30' Background='Transparent'>
    <Border x:Name='Ring' BorderBrush='{DynamicResource B.Mint}' BorderThickness='1' CornerRadius='10' Margin='-6,0,-6,0' Visibility='Collapsed'/>
    <Border Height='6' CornerRadius='3' Background='{DynamicResource B.Sunken}' BorderBrush='{DynamicResource B.LineStrong}' BorderThickness='1' VerticalAlignment='Center'/>
    <Track x:Name='PART_Track'>
      <Track.DecreaseRepeatButton>
        <RepeatButton Command='Slider.DecreaseLarge' Focusable='False' IsTabStop='False'>
          <RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Height='6' CornerRadius='3' Background='{DynamicResource B.Mint}' VerticalAlignment='Center'/></ControlTemplate></RepeatButton.Template>
        </RepeatButton>
      </Track.DecreaseRepeatButton>
      <Track.IncreaseRepeatButton>
        <RepeatButton Command='Slider.IncreaseLarge' Focusable='False' IsTabStop='False'>
          <RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Height='30' Background='Transparent'/></ControlTemplate></RepeatButton.Template>
        </RepeatButton>
      </Track.IncreaseRepeatButton>
      <Track.Thumb>
        <Thumb Width='20' Height='20' Focusable='False' Cursor='Hand'>
          <Thumb.Template><ControlTemplate TargetType='Thumb'><Ellipse Fill='{DynamicResource B.Mint}' Stroke='{DynamicResource B.Raised}' StrokeThickness='3'/></ControlTemplate></Thumb.Template>
        </Thumb>
      </Track.Thumb>
    </Track>
  </Grid>
  <ControlTemplate.Triggers>
    <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Ring' Property='Visibility' Value='Visible'/></Trigger>
    <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.5'/></Trigger>
  </ControlTemplate.Triggers>
</ControlTemplate>";
            return _slider = (ControlTemplate)XamlReader.Parse(xaml);
        }

        public static string Duration(TimeSpan t)
        {
            if (t.TotalSeconds < 1) return "under a second";
            if (t.TotalMinutes < 1) return $"{(int)Math.Ceiling(t.TotalSeconds)} s";
            if (t.TotalHours < 1) return $"{(int)t.TotalMinutes} min {t.Seconds} s";
            return $"{(int)t.TotalHours} h {t.Minutes} min";
        }

        public const string DiskIcon = "M4 6h16a1 1 0 0 1 1 1v3H3V7a1 1 0 0 1 1-1zM3 10h18v4H3zM3 14h18v3a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1zM7 8h.01M7 12h.01M7 16h.01";
        public const string Chevron = "M6 9l6 6 6-6";
        public const string Eye = "M2 12s4-7 10-7 10 7 10 7-4 7-10 7-10-7-10-7zM12 9a3 3 0 1 0 0 6 3 3 0 0 0 0-6z";
        public const string Lock = "M7 11V8a5 5 0 0 1 10 0v3M5 11h14v10H5z";
        public const string Split = "M6 3v6a3 3 0 0 0 3 3h6a3 3 0 0 1 3 3v6M6 3L3 6M6 3l3 3M18 21l-3-3M18 21l3-3";
        public const string Power = Icons.Power;
        public const string Info = "M12 3a9 9 0 1 1 0 18 9 9 0 0 1 0-18zM12 11v6M12 7.5h.01";
        public const string Warn = "M12 3l10 18H2zM12 10v5M12 18h.01";
        public const string Copy = "M8 8h12v12H8zM4 4h12v2M4 4v12h2";
        public const string Volume = "M12 4a8 8 0 1 0 0 16 8 8 0 0 0 0-16zM12 9a3 3 0 1 0 0 6 3 3 0 0 0 0-6z";
    }
}
