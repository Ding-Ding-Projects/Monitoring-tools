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

namespace MonitoringTools.UI
{
    public enum BannerKind { Success, Info, Warning, Error }

    /// <summary>The small building blocks the file tool dialogs share: banners, cards, fields, segmented controls, chips, switches,
    /// sliders and the progress block. Everything uses the app's own brushes and styles, so the dialogs follow the theme.</summary>
    public static class FT
    {
        // ------------------------------------------------------------ text

        public static TextBlock Label(string text, double size = 12, Brush fg = null, FontWeight? weight = null, Thickness? margin = null) =>
            new TextBlock { Text = text, FontSize = TypeScale.Snap(size), FontFamily = Ui.Font("F.Body"), Foreground = fg ?? Ui.Res("B.TextSoft"), FontWeight = weight ?? FontWeights.Medium, TextWrapping = TextWrapping.Wrap, Margin = margin ?? new Thickness(0, 0, 0, 5) };

        public static TextBlock Eyebrow(string text, Thickness? margin = null) =>
            new TextBlock { Text = text, FontSize = 12, FontWeight = FontWeights.Medium, FontFamily = Ui.Font("F.Body"), Foreground = Ui.Res("B.Mint"), Margin = margin ?? new Thickness(0, 0, 0, 8) };

        public static TextBlock Body(string text, double size = 12, Brush fg = null, Thickness? margin = null) =>
            new TextBlock { Text = text, FontSize = TypeScale.Snap(size), FontFamily = Ui.Font("F.Body"), Foreground = fg ?? Ui.Res("B.TextSoft"), TextWrapping = TextWrapping.Wrap, Margin = margin ?? new Thickness(0) };

        public static TextBlock Mono(string text, double size = 12, Brush fg = null) =>
            new TextBlock { Text = text, FontSize = TypeScale.Snap(size), FontFamily = Ui.Font("F.Mono"), Foreground = fg ?? Ui.Res("B.Text") };

        /// <summary>The middle of a long file name replaced by an ellipsis, keeping the start and the extension.</summary>
        public static string Middle(string name, int max = 40)
        {
            if (string.IsNullOrEmpty(name) || name.Length <= max) return name;
            var ext = System.IO.Path.GetExtension(name);
            int keep = Math.Max(4, max - ext.Length - 1);
            int head = keep * 2 / 3 + 1, tail = Math.Max(2, keep - head);
            var stem = System.IO.Path.GetFileNameWithoutExtension(name);
            return stem.Substring(0, Math.Min(head, stem.Length)) + "…" + stem.Substring(Math.Max(0, stem.Length - tail)) + ext;
        }

        // ------------------------------------------------------------ banners and cards

        public static Border Banner(BannerKind kind, string title, string text, Button action = null, string detail = null)
        {
            Brush bg, fg; string icon;
            switch (kind)
            {
                case BannerKind.Success: bg = Ui.Res("B.MintWash"); fg = Ui.Res("B.OnPrimaryContainer"); icon = Icons.Check; break;
                case BannerKind.Warning: bg = Ui.Res("B.AmberWash"); fg = Ui.Res("B.Amber"); icon = MediaIcons.Warn; break;
                case BannerKind.Error: bg = Ui.Res("B.DangerWash"); fg = Ui.Res("B.Danger"); icon = MediaIcons.Warn; break;
                default: bg = Ui.Res("B.TertiaryContainer"); fg = Ui.Res("B.OnTertiaryContainer"); icon = MediaIcons.Info; break;
            }
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var ic = Ui.Icon(icon, fg, 22, 2);
            ic.VerticalAlignment = VerticalAlignment.Top; ic.Margin = new Thickness(0, 2, 14, 0);
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeights.Medium, FontFamily = Ui.Font("F.Body"), Foreground = fg, TextWrapping = TextWrapping.Wrap });
            if (!string.IsNullOrEmpty(text)) sp.Children.Add(new TextBlock { Text = text, FontSize = 12, FontFamily = Ui.Font("F.Body"), Foreground = fg, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
            if (!string.IsNullOrEmpty(detail))
            {
                var d = new TextBlock { Text = detail, FontSize = 12, FontFamily = Ui.Font("F.Mono"), Foreground = fg, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0), MaxWidth = 640 };
                Core.Language.SetLiteral(d, true);   // the engine's own message, shown exactly as it came
                sp.Children.Add(d);
            }
            Grid.SetColumn(sp, 1);
            g.Children.Add(ic); g.Children.Add(sp);
            if (action != null) { action.Margin = new Thickness(12, 0, 0, 0); action.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(action, 2); g.Children.Add(action); }
            var b = new Border { CornerRadius = new CornerRadius(16), Background = bg, Padding = new Thickness(16, 12, 16, 12), Child = g, Margin = new Thickness(0, 0, 0, 12) };
            AutomationProperties.SetName(b, title + (string.IsNullOrEmpty(text) ? "" : "\n" + text));
            AutomationProperties.SetLiveSetting(b, kind == BannerKind.Error ? AutomationLiveSetting.Assertive : AutomationLiveSetting.Polite);
            return b;
        }

        public static Border Card(UIElement child, Thickness? margin = null) =>
            new Border { CornerRadius = new CornerRadius(20), Background = Ui.Res("B.Sunken"), Padding = new Thickness(16, 12, 16, 12), Child = child, Margin = margin ?? new Thickness(0, 0, 0, 12) };

        /// <summary>A card of label and value rows (Length, Size, Method...).</summary>
        public static Border FactsCard(IEnumerable<(string Label, string Value)> rows, double labelWidth = 130)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(labelWidth) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            int r = 0;
            foreach (var (l, v) in rows)
            {
                g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var a = Body(l, 13, Ui.Res("B.TextSoft"), new Thickness(0, 3, 0, 3));
                var b = Body(v, 13, Ui.Res("B.Text"), new Thickness(0, 3, 0, 3));
                Grid.SetRow(a, r); Grid.SetRow(b, r); Grid.SetColumn(b, 1);
                g.Children.Add(a); g.Children.Add(b);
                r++;
            }
            return Card(g);
        }

        public static Button Tonal(string label, Action click, string icon = null, string name = null)
        {
            var b = Ui.Button(label, click, "Btn.Ghost", icon);
            b.Margin = new Thickness(0, 0, 8, 0);
            if (name != null) AutomationProperties.SetName(b, name);
            return b;
        }

        public static Button Outline(string label, Action click)
        {
            var b = Ui.Button(label, click, "Btn.Chip");
            b.Height = 40; b.Padding = new Thickness(16, 0, 16, 0);
            b.Foreground = Ui.Res("B.Mint"); b.BorderBrush = Ui.Res("B.LineStrong");
            b.Margin = new Thickness(0, 0, 8, 0);
            return b;
        }

        public static Button Accent(string label, Action click, string icon = null)
        {
            var b = Ui.Button(label, click, "Btn.Accent", icon);
            b.Margin = new Thickness(0);
            return b;
        }

        // ------------------------------------------------------------ fields

        /// <summary>A labelled stack: label above, control below.</summary>
        public static StackPanel Field(string label, UIElement control, string hint = null, Thickness? margin = null)
        {
            var sp = new StackPanel { Margin = margin ?? new Thickness(0, 0, 0, 12) };
            sp.Children.Add(Label(label));
            sp.Children.Add(control);
            if (hint != null) sp.Children.Add(Body(hint, 12, Ui.Res("B.TextMute"), new Thickness(0, 5, 0, 0)));
            return sp;
        }

        /// <summary>A 42 px text field. onCommit runs on Enter and when focus leaves; Escape puts the last committed text back.</summary>
        public static TextBox TextField(string accessibleName, string text, Action<string> onCommit, bool mono = true)
        {
            var tb = new TextBox { Style = Ui.Style("Input"), Text = text ?? "", Height = 42, FontFamily = Ui.Font(mono ? "F.Mono" : "F.Body"), FontSize = 14, Tag = text ?? "" };
            AutomationProperties.SetName(tb, accessibleName);
            Core.Language.SetLiteral(tb, true);
            void Commit() { tb.Tag = tb.Text; onCommit?.Invoke(tb.Text); }
            tb.LostKeyboardFocus += (s, e) => { if ((string)tb.Tag != tb.Text) Commit(); };
            tb.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) { e.Handled = true; Commit(); }
                else if (e.Key == Key.Escape && (string)tb.Tag != tb.Text) { e.Handled = true; tb.Text = (string)tb.Tag; tb.CaretIndex = tb.Text.Length; }
            };
            return tb;
        }

        /// <summary>A whole-number field with a unit. Up and Down change it by step; a value outside min and max is refused with a message
        /// through onInvalid and the last good value stays.</summary>
        public sealed class IntBox
        {
            public TextBox Box; public Grid Host; public int Value;
            private readonly int _min, _max;
            private readonly Action<int> _commit; private readonly Action<string> _invalid;
            public IntBox(string name, int value, string unit, int min, int max, int step, Action<int> commit, Action<string> invalid = null)
            {
                _min = min; _max = max; _commit = commit; _invalid = invalid; Value = value;
                Box = TextField(name, value.ToString(), s => Commit(s), mono: false);
                Host = UnitField(Box, unit);
                Box.PreviewKeyDown += (s, e) =>
                {
                    int d = e.Key == Key.Up ? step : e.Key == Key.Down ? -step : 0;
                    if (d == 0) return;
                    e.Handled = true; Set(Math.Clamp(Value + d, _min, _max), true);
                };
            }
            public void Set(int v, bool raise)
            {
                Value = v; Box.Text = v.ToString(); Box.Tag = Box.Text;
                if (raise) _commit?.Invoke(v);
            }
            private void Commit(string s)
            {
                if (int.TryParse(s.Trim(), out var v) && v >= _min && v <= _max) { Value = v; Box.Tag = Box.Text = v.ToString(); _commit?.Invoke(v); }
                else { _invalid?.Invoke($"Type a whole number from {_min} to {_max}."); Box.Text = Value.ToString(); Box.Tag = Box.Text; }
            }
            public void Type(string text) { Box.Text = text; Commit(text); }
        }

        /// <summary>A text field with a unit written at its right edge (px, fps, MB).</summary>
        public static Grid UnitField(TextBox box, string unit)
        {
            var g = new Grid();
            g.Children.Add(box);
            box.Padding = new Thickness(0, 0, 40, 0);
            g.Children.Add(new TextBlock { Text = unit, FontSize = 12, Foreground = Ui.Res("B.TextSoft"), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0), IsHitTestVisible = false });
            return g;
        }

        /// <summary>A file name field with a small file icon at its left.</summary>
        public static Grid FileNameField(TextBox box)
        {
            var g = new Grid();
            box.Padding = new Thickness(30, 0, 0, 0);
            g.Children.Add(box);
            var ic = Ui.Icon(Icons.File, Ui.Res("B.TextSoft"), 15, 1.8);
            ic.HorizontalAlignment = HorizontalAlignment.Left; ic.VerticalAlignment = VerticalAlignment.Center; ic.Margin = new Thickness(13, 0, 0, 0); ic.IsHitTestVisible = false;
            g.Children.Add(ic);
            return g;
        }

        // ------------------------------------------------------------ switches, chips, segments

        public static CheckBox SwitchRow(string title, string sub, bool on, Action<bool> changed)
        {
            var label = new StackPanel();
            label.Children.Add(new TextBlock { Text = title, Foreground = Ui.Res("B.Text"), FontSize = 14, FontWeight = FontWeights.Medium, FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap });
            if (!string.IsNullOrEmpty(sub)) label.Children.Add(new TextBlock { Text = sub, Foreground = Ui.Res("B.TextSoft"), FontSize = 12, TextWrapping = TextWrapping.Wrap, FontFamily = Ui.Font("F.Body"), Margin = new Thickness(0, 2, 0, 0) });
            var cb = new CheckBox { Style = Ui.Style("Switch"), IsChecked = on, Content = label, Margin = new Thickness(0, 0, 0, 12) };
            AutomationProperties.SetName(cb, title);
            if (!string.IsNullOrEmpty(sub)) AutomationProperties.SetHelpText(cb, sub);
            cb.Checked += (s, e) => changed?.Invoke(true);
            cb.Unchecked += (s, e) => changed?.Invoke(false);
            return cb;
        }

        /// <summary>A toggle chip with a check mark when on. Reads as a toggle button to assistive technology.</summary>
        public static Button Chip(string label, bool on, Action<bool> changed, string name = null)
        {
            var b = new Button { Style = Ui.Style("Btn.Chip"), Margin = new Thickness(0, 0, 8, 8), Tag = on };
            void Paint()
            {
                bool v = (bool)b.Tag;
                var fg = v ? Ui.Res("B.OnSecondaryContainer") : Ui.Res("B.TextSoft");
                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                if (v) sp.Children.Add(new ContentControl { Content = Ui.Icon(Icons.Check, fg, 13, 2), Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center });
                sp.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
                b.Content = sp;
                b.Background = v ? Ui.Res("B.SecondaryContainer") : Brushes.Transparent;
                b.Foreground = fg;
                b.BorderBrush = v ? Brushes.Transparent : Ui.Res("B.LineStrong");
                AutomationProperties.SetItemStatus(b, v ? "on" : "off");
            }
            AutomationProperties.SetName(b, name ?? label);
            Paint();
            b.Click += (s, e) => { b.Tag = !(bool)b.Tag; Paint(); changed?.Invoke((bool)b.Tag); };
            return b;
        }

        private static Style _checkStyle;

        /// <summary>A square Material check box (22 px, mint when ticked) for lists. Space toggles it.</summary>
        public static CheckBox Check(bool on, string accessibleName, Action<bool> changed)
        {
            _checkStyle ??= (Style)XamlReader.Parse(
                "<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='CheckBox'>" +
                "<Setter Property='FocusVisualStyle' Value='{x:Null}'/><Setter Property='Cursor' Value='Hand'/>" +
                "<Setter Property='Template'><Setter.Value><ControlTemplate TargetType='CheckBox'><Grid Background='Transparent' Width='26' Height='26'>" +
                "<Border x:Name='Ring' CornerRadius='9' BorderBrush='{DynamicResource B.Mint}' BorderThickness='2' Margin='-1' Opacity='0'/>" +
                "<Border x:Name='Box' Width='22' Height='22' CornerRadius='6' BorderThickness='2' BorderBrush='{DynamicResource B.LineStrong}' Background='Transparent'>" +
                "<Path x:Name='Tick' Data='M5 12l5 5 9-10' Stroke='{DynamicResource B.MintInk}' StrokeThickness='2.4' StrokeStartLineCap='Round' StrokeEndLineCap='Round' StrokeLineJoin='Round' Stretch='Uniform' Margin='2' Visibility='Collapsed'/></Border></Grid>" +
                "<ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='Box' Property='Background' Value='{DynamicResource B.Mint}'/><Setter TargetName='Box' Property='BorderBrush' Value='{DynamicResource B.Mint}'/><Setter TargetName='Tick' Property='Visibility' Value='Visible'/></Trigger>" +
                "<Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Ring' Property='Opacity' Value='1'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>");
            var cb = new CheckBox { Style = _checkStyle, IsChecked = on, VerticalAlignment = VerticalAlignment.Center };
            AutomationProperties.SetName(cb, accessibleName);
            cb.Checked += (s, e) => changed?.Invoke(true);
            cb.Unchecked += (s, e) => changed?.Invoke(false);
            return cb;
        }

        /// <summary>A small rounded label such as "Location" or "Clean", with an icon and a word (colour is never the only signal).</summary>
        public static Border Badge(string text, BannerKind kind, string icon = null)
        {
            Brush bg, fg;
            switch (kind)
            {
                case BannerKind.Success: bg = Ui.Res("B.MintWash"); fg = Ui.Res("B.OnPrimaryContainer"); break;
                case BannerKind.Warning: bg = Ui.Res("B.AmberWash"); fg = Ui.Res("B.Amber"); break;
                case BannerKind.Error: bg = Ui.Res("B.DangerWash"); fg = Ui.Res("B.Danger"); break;
                default: bg = Ui.Res("B.Sunken"); fg = Ui.Res("B.TextSoft"); break;
            }
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            if (icon != null) sp.Children.Add(new ContentControl { Content = Ui.Icon(icon, fg, 12, 1.8), Margin = new Thickness(0, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center });
            sp.Children.Add(new TextBlock { Text = text, FontSize = 12, FontWeight = FontWeights.Medium, Foreground = fg, VerticalAlignment = VerticalAlignment.Center });
            return new Border { CornerRadius = new CornerRadius(8), Background = bg, Padding = new Thickness(8, 2, 8, 3), Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center, Child = sp };
        }

        /// <summary>A group of chips where exactly one is selected (a radio group). Arrow keys move inside it.</summary>
        public sealed class ChipGroup : WrapPanel
        {
            private readonly List<Button> _chips = new List<Button>();
            private readonly string[] _labels;
            public int Index { get; private set; }
            public event Action<int> Changed;

            public ChipGroup(string[] labels, int selected, string accessibleName)
            {
                _labels = labels; Index = selected;
                AutomationProperties.SetName(this, accessibleName);
                for (int i = 0; i < labels.Length; i++)
                {
                    int idx = i;
                    var b = new Button { Style = Ui.Style("Btn.Chip"), Margin = new Thickness(0, 0, 8, 8) };
                    b.Click += (s, e) => Select(idx, true);
                    b.PreviewKeyDown += (s, e) =>
                    {
                        int to = e.Key == Key.Right ? idx + 1 : e.Key == Key.Left ? idx - 1 : -1;
                        if (to < 0 || to >= _chips.Count) return;
                        e.Handled = true; Select(to, true); _chips[to].Focus();
                    };
                    _chips.Add(b); Children.Add(b);
                }
                Paint();
            }

            public void Select(int i, bool raise)
            {
                if (i < 0 || i >= _chips.Count) return;
                bool changed = i != Index;
                Index = i; Paint();
                if (raise && changed) Changed?.Invoke(i);
            }

            private void Paint()
            {
                for (int i = 0; i < _chips.Count; i++)
                {
                    bool on = i == Index;
                    var fg = on ? Ui.Res("B.OnSecondaryContainer") : Ui.Res("B.TextSoft");
                    var sp = new StackPanel { Orientation = Orientation.Horizontal };
                    if (on) sp.Children.Add(new ContentControl { Content = Ui.Icon(Icons.Check, fg, 13, 2), Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center });
                    sp.Children.Add(new TextBlock { Text = _labels[i], VerticalAlignment = VerticalAlignment.Center });
                    _chips[i].Content = sp;
                    _chips[i].Background = on ? Ui.Res("B.SecondaryContainer") : Brushes.Transparent;
                    _chips[i].Foreground = fg;
                    _chips[i].BorderBrush = on ? Brushes.Transparent : Ui.Res("B.LineStrong");
                    AutomationProperties.SetName(_chips[i], _labels[i]);
                    AutomationProperties.SetItemStatus(_chips[i], on ? "selected" : "");
                }
            }
        }

        /// <summary>A segmented control: one part is selected, arrows move between parts.</summary>
        public sealed class Segmented : Border
        {
            private readonly List<Button> _parts = new List<Button>();
            private readonly string[] _labels;
            public int Index { get; private set; }
            public event Action<int> Changed;

            public Segmented(string[] labels, int selected, string accessibleName = null)
            {
                _labels = labels; Index = selected;
                CornerRadius = new CornerRadius(20); BorderBrush = Ui.Res("B.LineStrong"); BorderThickness = new Thickness(1);
                HorizontalAlignment = HorizontalAlignment.Left; Padding = new Thickness(2);
                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                for (int i = 0; i < labels.Length; i++)
                {
                    int idx = i;
                    var b = new Button { Style = Ui.Style("Btn.Ghost"), Height = 36, Padding = new Thickness(16, 0, 16, 0), Margin = new Thickness(0), FontSize = 12 };
                    b.Click += (s, e) => Select(idx, true);
                    b.PreviewKeyDown += (s, e) =>
                    {
                        int to = e.Key == Key.Right ? idx + 1 : e.Key == Key.Left ? idx - 1 : -1;
                        if (to < 0 || to >= _parts.Count) return;
                        e.Handled = true; Select(to, true); _parts[to].Focus();
                    };
                    _parts.Add(b); sp.Children.Add(b);
                }
                Child = sp;
                if (accessibleName != null) AutomationProperties.SetName(this, accessibleName);
                Paint();
            }

            public void Select(int i, bool raise = false)
            {
                if (i < 0 || i >= _parts.Count) return;
                bool changed = i != Index;
                Index = i; Paint();
                if (raise && changed) Changed?.Invoke(i);
            }

            public void SetEnabled(int i, bool enabled) { if (i >= 0 && i < _parts.Count) _parts[i].IsEnabled = enabled; }

            private void Paint()
            {
                for (int i = 0; i < _parts.Count; i++)
                {
                    bool on = i == Index;
                    var fg = on ? Ui.Res("B.OnSecondaryContainer") : Ui.Res("B.Text");
                    var sp = new StackPanel { Orientation = Orientation.Horizontal };
                    if (on) sp.Children.Add(new ContentControl { Content = Ui.Icon(Icons.Check, fg, 13, 2), Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center });
                    sp.Children.Add(new TextBlock { Text = _labels[i], VerticalAlignment = VerticalAlignment.Center });
                    _parts[i].Content = sp;
                    _parts[i].Background = on ? Ui.Res("B.SecondaryContainer") : Brushes.Transparent;
                    _parts[i].Foreground = fg;
                    AutomationProperties.SetName(_parts[i], _labels[i]);
                    AutomationProperties.SetItemStatus(_parts[i], on ? "selected" : "");
                }
            }
        }

        /// <summary>A border that clips its content to its rounded corners.</summary>
        public static Border Rounded(UIElement child, double radius, Brush background = null)
        {
            var b = new Border { CornerRadius = new CornerRadius(radius), Background = background ?? Ui.Res("B.Sunken"), Child = child };
            b.SizeChanged += (s, e) => b.Clip = new RectangleGeometry(new Rect(0, 0, b.ActualWidth, b.ActualHeight), radius, radius);
            return b;
        }

        /// <summary>A round, dark, translucent icon button laid over a picture (the play button of a preview).</summary>
        public static Button OverlayButton(string icon, string accessibleName, Action click, double size = 45)
        {
            var b = new Button { Style = Ui.Style("Btn.Icon"), Width = size, Height = size, Background = new SolidColorBrush(Color.FromArgb(180, 20, 24, 28)), Foreground = Brushes.White, Content = Ui.Icon(icon, Brushes.White, size * 0.45, 2), Margin = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            b.Click += (s, e) => click?.Invoke();
            AutomationProperties.SetName(b, accessibleName);
            return b;
        }

        // ------------------------------------------------------------ slider and progress

        private static ControlTemplate _sliderTemplate, _progressTemplate;

        private static ControlTemplate SliderTemplate() => _sliderTemplate ??= (ControlTemplate)XamlReader.Parse(
            "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Slider'>" +
            "<Grid Height='28' Background='Transparent'>" +
            "<Border Height='4' CornerRadius='2' Background='{DynamicResource B.Line}' VerticalAlignment='Center'/>" +
            "<Track x:Name='PART_Track'>" +
            "<Track.DecreaseRepeatButton><RepeatButton Command='Slider.DecreaseLarge' Focusable='False'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Height='4' CornerRadius='2' Background='{DynamicResource B.Mint}'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton>" +
            "<Track.IncreaseRepeatButton><RepeatButton Command='Slider.IncreaseLarge' Focusable='False'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Background='Transparent' Height='28'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton>" +
            "<Track.Thumb><Thumb Width='22' Height='22' Focusable='False'><Thumb.Template><ControlTemplate TargetType='Thumb'><Grid><Border x:Name='Ring' CornerRadius='14' BorderBrush='{DynamicResource B.Mint}' BorderThickness='2' Margin='-3' Opacity='0'/><Border CornerRadius='11' Background='{DynamicResource B.Mint}' BorderBrush='{DynamicResource B.Raised}' BorderThickness='3'/></Grid>" +
            "<ControlTemplate.Triggers><DataTrigger Binding='{Binding IsKeyboardFocusWithin, RelativeSource={RelativeSource AncestorType=Slider}}' Value='True'><Setter TargetName='Ring' Property='Opacity' Value='1'/></DataTrigger></ControlTemplate.Triggers></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>" +
            "</Track></Grid></ControlTemplate>");

        public static Slider MakeSlider(string accessibleName, double min, double max, double value, double step, Action<double> changed)
        {
            var s = new Slider { Minimum = min, Maximum = max, Value = value, SmallChange = step, LargeChange = step * 5, TickFrequency = step, IsSnapToTickEnabled = true, Template = SliderTemplate(), Focusable = true, FocusVisualStyle = null, Height = 28, Margin = new Thickness(0, 0, 0, 4) };
            AutomationProperties.SetName(s, accessibleName);
            s.ValueChanged += (a, b) => changed?.Invoke(s.Value);
            return s;
        }

        public static ProgressBar MakeBar(string accessibleName)
        {
            _progressTemplate ??= (ControlTemplate)XamlReader.Parse(
                "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ProgressBar'>" +
                "<Grid Height='8'><Border x:Name='PART_Track' CornerRadius='4' Background='{DynamicResource B.Line}'/><Border x:Name='PART_Indicator' CornerRadius='4' HorizontalAlignment='Left' Background='{DynamicResource B.Mint}'/></Grid></ControlTemplate>");
            var p = new ProgressBar { Minimum = 0, Maximum = 100, Template = _progressTemplate, Height = 8, Margin = new Thickness(0, 12, 0, 12) };
            AutomationProperties.SetName(p, accessibleName);
            return p;
        }

        /// <summary>The Working state: a big percentage, what is happening, the bar, and a detail line under it.</summary>
        public sealed class ProgressBlock : Border
        {
            private readonly TextBlock _pct, _caption, _detail;
            private readonly ProgressBar _bar;
            public double Value { get; private set; }
            public string Caption => _caption.Text;
            public string Detail => _detail.Text;

            public ProgressBlock(string accessibleName)
            {
                CornerRadius = new CornerRadius(20); Background = Ui.Res("B.Sunken"); Padding = new Thickness(16, 14, 16, 16); Margin = new Thickness(0, 0, 0, 12);
                var sp = new StackPanel();
                var top = new StackPanel { Orientation = Orientation.Horizontal };
                _pct = new TextBlock { Text = "0%", FontSize = 28, FontFamily = Ui.Font("F.Display"), Foreground = Ui.Res("B.Text"), VerticalAlignment = VerticalAlignment.Bottom };
                _caption = new TextBlock { FontSize = 12, Foreground = Ui.Res("B.TextSoft"), Margin = new Thickness(12, 0, 0, 4), VerticalAlignment = VerticalAlignment.Bottom, TextWrapping = TextWrapping.Wrap };
                top.Children.Add(_pct); top.Children.Add(_caption);
                _bar = MakeBar(accessibleName);
                _detail = new TextBlock { FontSize = 12, Foreground = Ui.Res("B.TextSoft"), TextWrapping = TextWrapping.Wrap };
                sp.Children.Add(top); sp.Children.Add(_bar); sp.Children.Add(_detail);
                Child = sp;
                AutomationProperties.SetLiveSetting(_caption, AutomationLiveSetting.Polite);
            }

            /// <summary>Safe from any thread.</summary>
            public void Set(double fraction, string caption = null, string detail = null)
            {
                void Apply()
                {
                    Value = Math.Clamp(fraction, 0, 1);
                    _pct.Text = Math.Round(Value * 100) + "%";
                    _bar.Value = Value * 100;
                    if (caption != null) _caption.Text = caption;
                    if (detail != null) _detail.Text = detail;
                }
                if (Dispatcher.CheckAccess()) Apply(); else Dispatcher.BeginInvoke((Action)Apply);
            }
        }
    }
}
