using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MonitoringTools.Core;
using MonitoringTools.Minecraft;
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI.Minecraft
{
    /// <summary>Small Material 3 building blocks for the server manager, drawn from the app's theme roles.</summary>
    internal static class McUi
    {

        public static Border Card(UIElement child, double pad = 20, string bg = "B.Surface") => new Border
        {
            Background = Ui.Res(bg), CornerRadius = new CornerRadius(28), Padding = new Thickness(Appearance.Gap(pad)), Margin = new Thickness(0, 0, 0, Appearance.Gap(14)), Child = child
        };

        public static TextBlock Title(string text, double size = 18) => new TextBlock { Text = text, Style = Ui.Style("T.Title"), FontSize = size, TextWrapping = TextWrapping.Wrap };

        public static TextBlock Body(string text, string fg = "B.TextSoft", double size = 13) => new TextBlock
        {
            Text = text, Style = Ui.Style("T.Body"), FontSize = size, Foreground = Ui.Res(fg), TextWrapping = TextWrapping.Wrap
        };

        /// <summary>Text the person owns or that comes from the server (names, log lines, keys): never translated.</summary>
        public static TextBlock Literal(string text, string fg = "B.Text", double size = 13, bool mono = false)
        {
            var t = new TextBlock { Text = text ?? "", FontSize = size, Foreground = Ui.Res(fg), TextWrapping = TextWrapping.Wrap, FontFamily = Ui.Font(mono ? "F.Mono" : "F.Body") };
            Core.Language.SetLiteral(t, true);
            return t;
        }

        public static Border Badge(string text, string bg, string fg, string icon = null)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            if (icon != null) sp.Children.Add(new ContentControl { Content = Ui.Icon(icon, Ui.Res(fg), 12), Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center, IsTabStop = false });
            sp.Children.Add(new TextBlock { Text = text, FontSize = 12, FontWeight = FontWeights.Medium, Foreground = Ui.Res(fg), FontFamily = Ui.Font("F.Body"), VerticalAlignment = VerticalAlignment.Center });
            return new Border { Background = Ui.Res(bg), CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 3, 8, 4), Margin = new Thickness(0, 0, 6, 0), Child = sp, VerticalAlignment = VerticalAlignment.Center };
        }

        public static (string Bg, string Fg, string Dot) StateTone(McRunState s) => s switch
        {
            McRunState.Running => ("B.MintWash", "B.Mint", "B.Mint"),
            McRunState.Starting => ("B.SecondaryContainer", "B.OnSecondaryContainer", "B.Mint"),
            McRunState.HostOffline or McRunState.Missing => ("B.AmberWash", "B.Amber", "B.Amber"),
            _ => ("B.Raised", "B.TextSoft", "B.TextMute"),
        };

        public static Border StateBadge(McRunState s, bool large = false)
        {
            var (bg, fg, dot) = StateTone(s);
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            // The shared live dot breathes while the server runs and stays still otherwise (and under reduced motion).
            var (d, _) = Ui.LiveDot(Ui.Res(dot), s == McRunState.Running || s == McRunState.Starting, 8);
            d.Margin = new Thickness(-6, -6, 0, -6);
            sp.Children.Add(d);
            sp.Children.Add(new TextBlock { Text = McState.Label(s), FontSize = large ? 14 : 12, FontWeight = FontWeights.Medium, Foreground = Ui.Res(fg), FontFamily = Ui.Font("F.Body"), VerticalAlignment = VerticalAlignment.Center });
            var b = new Border { Background = Ui.Res(bg), CornerRadius = new CornerRadius(large ? 16 : 10), Padding = large ? new Thickness(14, 6, 14, 7) : new Thickness(10, 3, 10, 4), Child = sp, VerticalAlignment = VerticalAlignment.Center };
            AutomationProperties.SetName(b, "State: " + McState.Label(s));
            return b;
        }

        public static Button Chip(string label, bool on, Action click, string accessible = null)
        {
            var b = Ui.Button(label, click, "Btn.Chip");
            b.Margin = new Thickness(0, 0, 6, 6);
            Paint(b, on);
            if (accessible != null) AutomationProperties.SetName(b, accessible);
            return b;
        }

        public static void Paint(Button b, bool on)
        {
            b.Background = Ui.Res(on ? "B.SecondaryContainer" : "B.Raised");
            b.Foreground = Ui.Res(on ? "B.OnSecondaryContainer" : "B.TextSoft");
            b.BorderBrush = Ui.Res(on ? "B.SecondaryContainer" : "B.LineStrong");
            AutomationProperties.SetItemStatus(b, on ? "selected" : "");
        }

        /// <summary>A switch with its label and a sentence saying what it does, as every switch in the app has.</summary>
        public static CheckBox Switch(string label, string sub, bool value, Action<bool> set, UIElement extra = null, bool literalLabel = false)
        {
            var cb = new CheckBox { Style = Ui.Style("Switch"), IsChecked = value, Margin = new Thickness(0, 4, 0, 8) };
            var text = new StackPanel();
            var head = new WrapPanel();
            var lt = new TextBlock { Text = label, Foreground = Ui.Res("B.Text"), FontSize = 14, FontFamily = Ui.Font("F.Body"), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            if (literalLabel) Core.Language.SetLiteral(lt, true);
            head.Children.Add(lt);
            if (extra != null) head.Children.Add(extra);
            text.Children.Add(head);
            text.Children.Add(new TextBlock { Text = sub, Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
            cb.Content = text;
            AutomationProperties.SetName(cb, label);
            AutomationProperties.SetHelpText(cb, sub);
            cb.Checked += (s, e) => set(true);
            cb.Unchecked += (s, e) => set(false);
            return cb;
        }

        /// <summary>A labelled list of choices (an enumeration) with its explanation, tagged so the settings walk finds it.</summary>
        public static StackPanel Choice<T>(string label, string sub, IEnumerable<(string Label, T Value)> options, T current, Action<T> set, bool literal = false)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 4, 0, 10) };
            sp.Children.Add(new StackPanel { Children = { new TextBlock { Text = label, Foreground = Ui.Res("B.Text"), FontSize = 14, FontFamily = Ui.Font("F.Body") } } });
            sp.Children.Add(new TextBlock { Text = sub, Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 8) });
            var wrap = new WrapPanel();
            if (literal) Core.Language.SetLiteral(wrap, true);
            AutomationProperties.SetName(wrap, label);
            var buttons = new List<(Button B, T V)>();
            foreach (var (l, v) in options)
            {
                var val = v;
                var b = Chip(l, EqualityComparer<T>.Default.Equals(v, current), null);
                b.Click += (s, e) => { current = val; foreach (var (bb, vv) in buttons) Paint(bb, EqualityComparer<T>.Default.Equals(vv, current)); set(val); };
                buttons.Add((b, v));
                wrap.Children.Add(b);
            }
            sp.Children.Add(wrap);
            SearchScope.SetItem(sp, "setting choice " + current);
            return sp;
        }

        private static ControlTemplate _row;

        /// <summary>A selectable list row: a full-width button whose shape is a named border, so its corner radius can morph.</summary>
        public static Button Row(UIElement content, string name)
        {
            _row ??= (ControlTemplate)System.Windows.Markup.XamlReader.Parse(
                "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'>" +
                "<Grid><Border x:Name='Bd' CornerRadius='16' Background='{TemplateBinding Background}'/>" +
                "<Border x:Name='Hi' CornerRadius='{Binding CornerRadius, ElementName=Bd}' Background='{DynamicResource B.Text}' Opacity='0'/>" +
                "<Border x:Name='Focus' CornerRadius='{Binding CornerRadius, ElementName=Bd}' BorderBrush='{DynamicResource B.Mint}' BorderThickness='2' Opacity='0'/>" +
                "<ContentPresenter Margin='{TemplateBinding Padding}' VerticalAlignment='Center' HorizontalAlignment='Stretch'/></Grid>" +
                "<ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Hi' Property='Opacity' Value='0.08'/></Trigger>" +
                "<Trigger Property='IsPressed' Value='True'><Setter TargetName='Hi' Property='Opacity' Value='0.12'/></Trigger>" +
                "<Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Focus' Property='Opacity' Value='1'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
            var b = new Button { Template = _row, Content = content, Background = Brushes.Transparent, Foreground = Ui.Res("B.Text"), Padding = new Thickness(12, 10, 12, 10), HorizontalContentAlignment = HorizontalAlignment.Stretch, Cursor = Cursors.Hand, FocusVisualStyle = null, Margin = new Thickness(0, 0, 0, 4) };
            AutomationProperties.SetName(b, name);
            return b;
        }

        public static Border Shape(Button row) { row.ApplyTemplate(); return row.Template?.FindName("Bd", row) as Border; }

        /// <summary>An icon-only button with an accessible name.</summary>
        public static Button IconButton(string icon, string name, Action click)
        {
            var b = new Button { Style = Ui.Style("Btn.Icon"), Content = Ui.Icon(icon, Ui.Res("B.TextSoft"), 16), ToolTip = name, Margin = new Thickness(2, 0, 2, 0) };
            AutomationProperties.SetName(b, name);
            b.Click += (s, e) => click?.Invoke();
            return b;
        }

        public static TextBox Field(string text, string name, double width = double.NaN, string hint = null)
        {
            var t = new TextBox { Style = Ui.Style("Input"), Text = text ?? "", Margin = new Thickness(0, 0, 8, 8), ToolTip = hint ?? name };
            if (!double.IsNaN(width)) { t.Width = width; t.HorizontalAlignment = HorizontalAlignment.Left; }
            AutomationProperties.SetName(t, name);
            return t;
        }

        public static string Age(DateTime when)
        {
            var d = DateTime.Now - when;
            if (d.TotalMinutes < 1) return "just now";
            if (d.TotalHours < 1) return $"{(int)d.TotalMinutes} min ago";
            if (d.TotalDays < 1) return $"{(int)d.TotalHours} h ago";
            return $"{(int)d.TotalDays} d ago";
        }

        public static string Span(TimeSpan d) => d.TotalHours >= 24 ? $"{(int)d.TotalDays} d {d.Hours} h" : d.TotalHours >= 1 ? $"{(int)d.TotalHours} h {d.Minutes} m" : $"{Math.Max(0, d.Minutes)} m";

        public static string TargetLabel(Target t) => t switch { Target.LocalProcess => "This PC", Target.LocalDocker => "Docker", Target.SshProcess => "SSH", _ => "SSH Docker" };
        public static string TargetIcon(Target t) => t switch { Target.LocalProcess => Icons.Monitor, Target.LocalDocker => Icons.Box, Target.SshProcess => Icons.Link, _ => Icons.Globe };

        /// <summary>Morphs a selected row's corner radius, as the design's selected list rows do.</summary>
        public static void Morph(Border b, double to)
        {
            if (Ui.Reduced) { b.CornerRadius = new CornerRadius(to); return; }
            double from = b.CornerRadius.TopLeft;
            var clock = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(320)) { EasingFunction = Ui.Spring };
            var anim = clock.CreateClock();
            anim.CurrentTimeInvalidated += (s, e) => { if (anim.CurrentProgress is double p) b.CornerRadius = new CornerRadius(from + (to - from) * Ui.Spring.Ease(p)); };
            anim.Completed += (s, e) => b.CornerRadius = new CornerRadius(to);
            anim.Controller?.Begin();
        }
    }

    /// <summary>
    /// A typed number field with a stepper, a unit and real bounds: the arrows and the Up and Down keys step by the step
    /// size, typing is checked as it happens, and a value outside the bounds is shown as a problem instead of being saved.
    /// Named so it does not clash with the app-wide number field.
    /// </summary>
    internal sealed class McNumberBox : Border
    {
        public long Min { get; }
        public long Max { get; }
        public long Step { get; }
        public TextBox Box { get; }
        public event Action<long> ValueChanged;
        private readonly TextBlock _problem;
        private long _value;

        public McNumberBox(string name, long value, long min, long max, long step = 1, string unit = "")
        {
            Min = min; Max = max; Step = Math.Max(1, step); _value = value;
            CornerRadius = new CornerRadius(12); Background = Ui.Res("B.Sunken"); BorderBrush = Ui.Res("B.LineStrong"); BorderThickness = new Thickness(1);
            HorizontalAlignment = HorizontalAlignment.Left; VerticalAlignment = VerticalAlignment.Center;
            var g = new Grid { Height = 40 };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 64 });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var dec = McUi.IconButton("M6 12h12", "Decrease " + name, () => Nudge(-1));
            var inc = McUi.IconButton(Icons.Plus, "Increase " + name, () => Nudge(1));
            Box = new TextBox
            {
                Text = value.ToString(CultureInfo.InvariantCulture), BorderThickness = new Thickness(0), Background = Brushes.Transparent, Foreground = Ui.Res("B.Text"),
                CaretBrush = Ui.Res("B.Mint"), FontFamily = Ui.Font("F.Mono"), FontSize = 14, VerticalContentAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Right,
                MinWidth = 64, Padding = new Thickness(4, 0, 4, 0)
            };
            Box.InputScope = new InputScope { Names = { new InputScopeName(InputScopeNameValue.Number) } };
            AutomationProperties.SetName(Box, name);
            AutomationProperties.SetHelpText(Box, $"{min} to {max}{(unit.Length > 0 ? " " + unit : "")}");
            Grid.SetColumn(dec, 0); Grid.SetColumn(Box, 1); Grid.SetColumn(inc, 3);
            g.Children.Add(dec); g.Children.Add(Box); g.Children.Add(inc);
            if (unit.Length > 0)
            {
                var u = new TextBlock { Text = unit, Foreground = Ui.Res("B.TextMute"), FontSize = 12, FontFamily = Ui.Font("F.Body"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };
                Grid.SetColumn(u, 2); g.Children.Add(u);
            }
            var stack = new StackPanel();
            stack.Children.Add(g);
            Child = stack;
            _problem = new TextBlock { FontSize = 12, Foreground = Ui.Res("B.Danger"), FontFamily = Ui.Font("F.Body"), Margin = new Thickness(10, 0, 10, 6), Visibility = Visibility.Collapsed, TextWrapping = TextWrapping.Wrap };
            AutomationProperties.SetLiveSetting(_problem, AutomationLiveSetting.Polite);
            stack.Children.Add(_problem);
            Box.TextChanged += (s, e) => Parse();
            Box.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Up) { Nudge(1); e.Handled = true; }
                else if (e.Key == Key.Down) { Nudge(-1); e.Handled = true; }
            };
        }

        public long Value => _value;
        /// <summary>When true an empty field is accepted (an optional argument left out); the stepper starts from the lowest value.</summary>
        public bool AllowEmpty { get; set; }
        public bool Valid { get; private set; } = true;
        public string Problem => _problem.Text;

        public void SetValue(long v) { Box.Text = Math.Clamp(v, Min, Max).ToString(CultureInfo.InvariantCulture); }

        private void Nudge(int dir)
        {
            long basis = Valid ? _value : Math.Clamp(_value, Min, Max);
            SetValue(basis + dir * Step);
        }

        private void Parse()
        {
            var t = Box.Text.Trim();
            string bad = null;
            if (AllowEmpty && t.Length == 0) { Valid = true; _problem.Text = ""; _problem.Visibility = Visibility.Collapsed; BorderBrush = Ui.Res("B.LineStrong"); return; }
            if (!long.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)) bad = "Type a whole number.";
            else if (v < Min) bad = $"The lowest is {Min.ToString(CultureInfo.InvariantCulture)}.";
            else if (v > Max) bad = $"The highest is {Max.ToString(CultureInfo.InvariantCulture)}.";
            Valid = bad == null;
            _problem.Text = bad ?? "";
            _problem.Visibility = bad == null ? Visibility.Collapsed : Visibility.Visible;
            BorderBrush = Ui.Res(bad == null ? "B.LineStrong" : "B.Danger");
            if (bad == null && v != _value) { _value = v; ValueChanged?.Invoke(v); }
        }
    }
}
