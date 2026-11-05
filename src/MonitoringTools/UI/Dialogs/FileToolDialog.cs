using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MonitoringTools.Core;

namespace MonitoringTools.UI.Dialogs
{
    /// <summary>Stroke icons for the file tool dialogs (24 px grid), kept here so lanes that add icons do not collide.</summary>
    internal static class DocIcons
    {
        public const string Disc = "M12 3a9 9 0 1 1 0 18 9 9 0 0 1 0-18zM12 10a2 2 0 1 0 0 4 2 2 0 0 0 0-4z";
        public const string Font = "M5 20L11 4h2l6 16M7.5 14h9";
        public const string Subtitle = "M3 6h18v12H3zM7 11h4M13 11h4M7 14.5h6";
        public const string Eject = "M12 5l7 8H5zM5 17h14";
        public const string Lock = "M6 11h12v9H6zM8 11V8a4 4 0 0 1 8 0v3";
        public const string Eye = "M2 12s4-7 10-7 10 7 10 7-4 7-10 7S2 12 2 12zM12 9.5a2.5 2.5 0 1 0 0 5 2.5 2.5 0 0 0 0-5z";
        public const string Copy = "M9 9h11v11H9zM5 15V4h11";
        public const string Warn = "M12 4l9 16H3zM12 10v4M12 17h.01";
        public const string Info = "M12 3a9 9 0 1 1 0 18 9 9 0 0 1 0-18zM12 11v5M12 8h.01";
        public const string Chevron = "M9 6l6 6-6 6";
        public const string Down = "M12 4v11M7 11l5 5 5-5M5 20h14";
        public const string Refresh = "M3 12a9 9 0 1 0 3-6.7L3 8M3 3v5h5";
    }

    /// <summary>The kinds of banner a file tool dialog shows.</summary>
    internal enum BannerKind { Ok, Info, Warn, Bad }

    /// <summary>
    /// The shared window of every file tool dialog (design/file-tools/HANDOFF.md, "Shared window spec"): a borderless
    /// card with a header (tile, tool name, file name, one-line summary, close), a body that scrolls, and a footer with
    /// a live status line, a secondary action, Cancel and the primary action. Each tool derives from this and owns its
    /// own states.
    /// </summary>
    public abstract class FileToolDialog : Window
    {
        private readonly TextBlock _fileLine = new TextBlock();
        private readonly TextBlock _summary = new TextBlock();
        private readonly StackPanel _bodyHost = new StackPanel();
        private readonly TextBlock _status = new TextBlock();
        private readonly Grid _footer = new Grid { Margin = new Thickness(0, 14, 0, 0) };
        private readonly StackPanel _footLeft = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        private readonly StackPanel _footRight = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        private Button _primary;
        private bool _primaryIsEnabled;
        private readonly Border _tile;
        private readonly TextBlock _title = new TextBlock();

        /// <summary>The tool name, the window title and the start of its accessible name.</summary>
        public string ToolName { get; }
        /// <summary>The file this window acts on, always shown so several windows can be told apart.</summary>
        public string FilePath { get; }

        protected FileToolDialog(string toolName, string filePath, string icon, double width)
        {
            ToolName = toolName; FilePath = filePath;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize; Width = width; SizeToContent = SizeToContent.Height;
            MaxHeight = Math.Max(420, SystemParameters.WorkArea.Height - 48);
            WindowStartupLocation = WindowStartupLocation.CenterScreen; Topmost = true; ShowInTaskbar = true;
            var name = System.IO.Path.GetFileName(filePath ?? "");
            Title = toolName;
            AutomationProperties.SetName(this, toolName + ": " + name);
            SourceInitialized += (s, e) => Native.ApplyDarkChrome(this);
            PreviewKeyDown += OnKey;
            MouseLeftButtonDown += (s, e) => { if (e.ButtonState == MouseButtonState.Pressed && e.OriginalSource is Border) try { DragMove(); } catch { } };

            _tile = new Border
            {
                Width = 44, Height = 44, CornerRadius = new CornerRadius(14), VerticalAlignment = VerticalAlignment.Top,
                Background = Ui.Res("B.TertiaryContainer"), Child = Ui.Icon(icon, Ui.Res("B.OnTertiaryContainer"), 22, 1.8)
            };
            _title.Text = toolName; _title.Style = Ui.Style("T.Title"); _title.FontSize = 20; _title.FontWeight = FontWeights.Medium;
            _fileLine.Text = name; _fileLine.FontSize = 13; _fileLine.FontWeight = FontWeights.Medium; _fileLine.FontFamily = Ui.Font("F.Body");
            _fileLine.Foreground = Ui.Res("B.Text"); _fileLine.TextTrimming = TextTrimming.CharacterEllipsis; _fileLine.ToolTip = filePath;
            _summary.FontSize = 13; _summary.FontFamily = Ui.Font("F.Body"); _summary.Foreground = Ui.Res("B.TextSoft"); _summary.TextWrapping = TextWrapping.Wrap;
            AutomationProperties.SetLiveSetting(_summary, AutomationLiveSetting.Polite);

            var close = Ui.Button("", Close, "Btn.Icon", Icons.Close);
            close.Margin = new Thickness(0); close.VerticalAlignment = VerticalAlignment.Top;
            AutomationProperties.SetName(close, "Close"); close.ToolTip = "Closes this window (Esc).";
            close.IsTabStop = true;
            _closeButton = close;

            var head = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.ColumnDefinitions.Add(new ColumnDefinition());
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var titles = new StackPanel { Margin = new Thickness(14, 0, 8, 0) };
            titles.Children.Add(_title);
            var line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 0) };
            line.Children.Add(new Border { MaxWidth = 300, Child = _fileLine, Margin = new Thickness(0, 0, 8, 0) });
            titles.Children.Add(line);
            titles.Children.Add(_summary);
            Grid.SetColumn(titles, 1); Grid.SetColumn(close, 2);
            head.Children.Add(_tile); head.Children.Add(titles); head.Children.Add(close);

            var scroll = new ScrollViewer { Content = _bodyHost, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            _status.FontSize = 13; _status.FontFamily = Ui.Font("F.Body"); _status.Foreground = Ui.Res("B.TextSoft"); _status.TextWrapping = TextWrapping.Wrap; _status.VerticalAlignment = VerticalAlignment.Center;
            AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
            _footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _footer.ColumnDefinitions.Add(new ColumnDefinition());
            _footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _status.Margin = new Thickness(0, 0, 12, 0);
            Grid.SetColumn(_status, 1); Grid.SetColumn(_footRight, 2);
            _footer.Children.Add(_footLeft); _footer.Children.Add(_status); _footer.Children.Add(_footRight);

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(scroll, 1); Grid.SetRow(_footer, 2);
            root.Children.Add(head); root.Children.Add(scroll); root.Children.Add(_footer);

            Content = new Grid
            {
                Margin = new Thickness(16),
                Children = { new Border { Background = Ui.Res("B.Raised"), CornerRadius = new CornerRadius(28), Padding = new Thickness(24, 20, 20, 18), Child = root, Effect = (System.Windows.Media.Effects.Effect)Application.Current.Resources["Fx.Shadow"] } }
            };
            ElementMenu.Attach(this, null, ElementMenu.Options.Copy);
        }

        private readonly Button _closeButton;

        // ------------------------------------------------------------ what a derived dialog drives

        /// <summary>The one-line summary under the file name (what the file is, or what state the dialog is in).</summary>
        protected void SetSummary(string text) { _summary.Text = text; }

        /// <summary>The summary line as shown, for checks.</summary>
        public string SummaryText => _summary.Text;

        /// <summary>The live status line in the footer, for checks.</summary>
        public string StatusText => _status.Text;

        /// <summary>Replaces the body with these elements.</summary>
        protected void SetBody(params UIElement[] parts)
        {
            _bodyHost.Children.Clear();
            foreach (var p in parts) if (p != null) _bodyHost.Children.Add(p);
            UpdateLayout();
        }

        /// <summary>Lays out the footer: the status text, secondary actions on the left, the buttons on the right. The last button on the right is the primary one.</summary>
        protected void SetFooter(string status, IEnumerable<Button> left, IEnumerable<Button> right, Button primary = null)
        {
            _status.Text = status ?? "";
            _footLeft.Children.Clear(); _footRight.Children.Clear();
            foreach (var b in left ?? Array.Empty<Button>()) { b.Margin = new Thickness(0, 0, 8, 0); _footLeft.Children.Add(b); }
            foreach (var b in right ?? Array.Empty<Button>()) { b.Margin = new Thickness(8, 0, 0, 0); _footRight.Children.Add(b); }
            _primary = primary;
            if (primary != null) { primary.IsDefault = true; }
        }

        /// <summary>Done and error states replace Cancel with a single accent Close.</summary>
        protected void SetFooterClose(string status, params Button[] left)
        {
            var close = Ui.Button("Close", Close, "Btn.Accent");
            SetFooter(status, left, new[] { close }, close);
        }

        /// <summary>Keeps the status line current without rebuilding the footer.</summary>
        protected void SetStatus(string text) { _status.Text = text ?? ""; }

        /// <summary>Whether a control in this window can take focus when Enter runs the primary action.</summary>
        protected virtual bool EnterRunsPrimary => true;

        protected virtual void OnEscape() => Close();

        private void OnKey(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { e.Handled = true; OnEscape(); return; }
            if (e.Key == Key.Enter && EnterRunsPrimary && _primary != null && _primary.IsEnabled && _primary.IsVisible)
            {
                var f = Keyboard.FocusedElement;
                if (f is TextBox tb && tb.AcceptsReturn) return;
                if (f is System.Windows.Controls.Primitives.ButtonBase) return;   // Enter on a button presses that button
                if (f is ListBoxItem || f is ListBox || f is TreeViewItem) return;
                e.Handled = true;
                _primary.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            }
        }

        // ------------------------------------------------------------ small builders shared by every tool

        internal static TextBlock Text(string text, double size = 13, Brush fg = null, FontWeight? weight = null, TextWrapping wrap = TextWrapping.Wrap)
        {
            var t = new TextBlock { Text = text, FontSize = size, FontFamily = Ui.Font("F.Body"), Foreground = fg ?? Ui.Res("B.Text"), TextWrapping = wrap };
            if (weight.HasValue) t.FontWeight = weight.Value;
            return t;
        }

        internal static TextBlock FieldLabel(string text) => new TextBlock
        {
            Text = text, FontSize = 12, FontWeight = FontWeights.Medium, FontFamily = Ui.Font("F.Body"), Foreground = Ui.Res("B.TextSoft"), Margin = new Thickness(0, 12, 0, 5)
        };

        internal static TextBlock Hint(string text, Brush fg = null) => new TextBlock
        {
            Text = text, FontSize = 12, FontFamily = Ui.Font("F.Body"), Foreground = fg ?? Ui.Res("B.TextMute"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0)
        };

        /// <summary>A status banner: icon, a bold title and one plain sentence. A live region (assertive for an error).</summary>
        internal static Border Banner(BannerKind kind, string title, string text, string icon = null)
        {
            Brush bg, fg, ink;
            switch (kind)
            {
                case BannerKind.Ok: bg = Ui.Res("B.MintWash"); fg = Ui.Res("B.OnPrimaryContainer"); ink = fg; icon ??= Icons.Check; break;
                case BannerKind.Warn: bg = Ui.Res("B.AmberWash"); fg = Ui.Res("B.Amber"); ink = fg; icon ??= DocIcons.Warn; break;
                case BannerKind.Bad: bg = Ui.Res("B.DangerWash"); fg = Ui.Res("B.Danger"); ink = fg; icon ??= DocIcons.Warn; break;
                default: bg = Ui.Res("B.TertiaryContainer"); fg = Ui.Res("B.OnTertiaryContainer"); ink = fg; icon ??= DocIcons.Info; break;
            }
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            var ic = Ui.Icon(icon, ink, 20, 1.8); ic.VerticalAlignment = VerticalAlignment.Top; ic.Margin = new Thickness(0, 1, 12, 0);
            var col = new StackPanel();
            col.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeights.Medium, FontFamily = Ui.Font("F.Body"), Foreground = fg, TextWrapping = TextWrapping.Wrap });
            if (!string.IsNullOrEmpty(text)) col.Children.Add(new TextBlock { Text = text, FontSize = 13, FontFamily = Ui.Font("F.Body"), Foreground = fg, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
            Grid.SetColumn(col, 1);
            g.Children.Add(ic); g.Children.Add(col);
            var b = new Border { CornerRadius = new CornerRadius(16), Background = bg, Padding = new Thickness(14, 12, 14, 12), Margin = new Thickness(0, 0, 0, 10), Child = g };
            AutomationProperties.SetLiveSetting(b, kind == BannerKind.Bad ? AutomationLiveSetting.Assertive : AutomationLiveSetting.Polite);
            return b;
        }

        /// <summary>A rounded card of label and value rows.</summary>
        internal static Border KeyValues(IEnumerable<(string Key, string Value)> rows, bool mono = false)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            int r = 0;
            foreach (var (k, v) in rows)
            {
                g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var kt = Text(k, 13, Ui.Res("B.TextSoft")); kt.Margin = new Thickness(0, 3, 8, 3); Grid.SetRow(kt, r);
                var vt = Text(v, 13, Ui.Res("B.Text")); vt.Margin = new Thickness(0, 3, 0, 3); Grid.SetRow(vt, r); Grid.SetColumn(vt, 1);
                if (mono) vt.FontFamily = Ui.Font("F.Mono");
                g.Children.Add(kt); g.Children.Add(vt); r++;
            }
            return new Border { CornerRadius = new CornerRadius(16), Background = Ui.Res("B.Surface"), Padding = new Thickness(16, 10, 16, 10), Margin = new Thickness(0, 0, 0, 10), Child = g };
        }

        /// <summary>A pill-shaped segmented control; the selected part shows a check. Behaves as a radio group.</summary>
        internal static FrameworkElement Segmented(string groupName, IList<string> options, int selected, Action<int> changed, IList<string> names = null)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };
            AutomationProperties.SetName(row, groupName);
            var buttons = new List<Button>();
            for (int i = 0; i < options.Count; i++)
            {
                int idx = i; bool on = i == selected;
                var content = new StackPanel { Orientation = Orientation.Horizontal };
                if (on) content.Children.Add(new ContentControl { Content = Ui.Icon(Icons.Check, Ui.Res("B.OnSecondaryContainer"), 14, 2), Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
                content.Children.Add(new TextBlock { Text = options[i], VerticalAlignment = VerticalAlignment.Center });
                var b = new Button { Style = Ui.Style("Btn.Chip"), Content = content, Height = 40, Padding = new Thickness(16, 0, 16, 0), Margin = new Thickness(0, 0, i == options.Count - 1 ? 0 : 2, 0) };
                b.Background = on ? Ui.Res("B.SecondaryContainer") : Brushes.Transparent;
                b.Foreground = on ? Ui.Res("B.OnSecondaryContainer") : Ui.Res("B.Text");
                AutomationProperties.SetName(b, names != null ? names[i] : options[i]);
                AutomationProperties.SetItemStatus(b, on ? "selected" : "");
                b.Click += (s, e) => { if (idx != selected) changed(idx); };
                row.Children.Add(b); buttons.Add(b);
            }
            row.PreviewKeyDown += (s, e) =>
            {
                if (e.Key != Key.Left && e.Key != Key.Right) return;
                int at = buttons.FindIndex(x => x.IsKeyboardFocused);
                if (at < 0) return;
                int next = Math.Clamp(at + (e.Key == Key.Right ? 1 : -1), 0, buttons.Count - 1);
                buttons[next].Focus(); e.Handled = true;
            };
            return row;
        }

        /// <summary>A text field in the design's shape (42 px, sunken). Mono for times and codes.</summary>
        internal static TextBox Field(string accessibleName, string text = "", bool mono = false)
        {
            var t = new TextBox { Style = Ui.Style("Input"), Text = text, Height = 42, FontFamily = Ui.Font(mono ? "F.Mono" : "F.Body"), FontSize = mono ? 13 : 14 };
            AutomationProperties.SetName(t, accessibleName);
            return t;
        }

        /// <summary>A progress bar with a name and either a value or "working".</summary>
        internal static ProgressBar Progress(string name, bool indeterminate)
        {
            var p = new ProgressBar { Height = 6, Minimum = 0, Maximum = 100, IsIndeterminate = indeterminate, Margin = new Thickness(0, 8, 0, 8) };
            AutomationProperties.SetName(p, name);
            try { p.Foreground = Ui.Res("B.Mint"); p.Background = Ui.Res("B.Sunken"); p.BorderThickness = new Thickness(0); } catch { }
            return p;
        }

        internal static Border Card(UIElement child, double bottom = 10) => new Border
        {
            CornerRadius = new CornerRadius(16), Background = Ui.Res("B.Surface"), Padding = new Thickness(16, 12, 16, 12), Margin = new Thickness(0, 0, 0, bottom), Child = child
        };

        internal static string FileSize(long b) => Ui.Bytes(b);

        /// <summary>The first button in this window whose label or accessible name matches, for checks and captures.</summary>
        internal Button FindButton(string name) => Drive.Find<Button>(this, name);
    }
}
