using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using MonitoringTools.Core;

namespace MonitoringTools.UI.Dialogs
{
    /// <summary>Stroke icons (24 px grid, round caps) the data and security dialogs use that the shared icon set does not have yet.</summary>
    internal static class DataToolIcons
    {
        public const string Hash = "M5 9h14M5 15h14M10 4L8 20M16 4l-2 16";
        public const string Stamp = "M12 3a9 9 0 1 1 0 18 9 9 0 0 1 0-18zM9 12l2 2 4-4";
        public const string Cert = "M4 5h16v11H4zM8 9h8M8 12h5M9 16v5l3-2 3 2v-5";
        public const string Table = "M3 5h18v14H3zM3 10h18M9 5v14";
        public const string Lines = "M4 6h16M4 12h10M4 18h13";
        public const string Warn = "M12 4l9 16H3zM12 10v4M12 17h.01";
        public const string Info = "M12 3a9 9 0 1 1 0 18 9 9 0 0 1 0-18zM12 11v5M12 8h.01";
        public const string Copy = "M9 9h11v11H9zM5 15V4h11";
        public const string Lock = "M6 11h12v9H6zM8 11V8a4 4 0 0 1 8 0v3";
        public const string Eye = "M2 12s4-7 10-7 10 7 10 7-4 7-10 7S2 12 2 12zM12 9a3 3 0 1 1 0 6 3 3 0 0 1 0-6z";
        public const string Refresh = "M20 12a8 8 0 1 1-2.3-5.7L20 8M20 3v5h-5";
        public const string Chevron = "M9 6l6 6-6 6";
        public const string Check = "M5 12l5 5 9-10";
        public const string Search = "M11 4a7 7 0 1 1 0 14 7 7 0 0 1 0-14zM20 20l-4-4";
        public const string Folder = "M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z";
        public const string Trash = "M4 7h16M9 7V4h6v3M6 7l1 13h10l1-13";
        public const string Braces = "M8 4c-2 0-3 1-3 3v2c0 1-1 2-2 2 1 0 2 1 2 2v2c0 2 1 3 3 3M16 4c2 0 3 1 3 3v2c0 1 1 2 2 2-1 0-2 1-2 2v2c0 2-1 3-3 3";
        public const string Close = "M6 6l12 12M18 6L6 18";
    }

    internal enum DataBannerKind { Success, Info, Warning, Error }

    /// <summary>A state banner: an icon, a short title and a sentence, announced to assistive technology when it changes.</summary>
    internal sealed class BannerBox : Border
    {
        private readonly TextBlock _title = new TextBlock { FontSize = 14, FontWeight = FontWeights.Medium, TextWrapping = TextWrapping.Wrap };
        private readonly TextBlock _body = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
        private readonly ContentControl _icon = new ContentControl { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 1, 14, 0) };
        public DataBannerKind Kind { get; private set; }
        public string TitleText => _title.Text;
        public string BodyText => _body.Text;

        public BannerBox()
        {
            CornerRadius = new CornerRadius(16); Padding = new Thickness(16, 12, 16, 12); Margin = new Thickness(0, 0, 0, 12);
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            var text = new StackPanel(); text.Children.Add(_title); text.Children.Add(_body);
            Grid.SetColumn(text, 1);
            g.Children.Add(_icon); g.Children.Add(text);
            Child = g;
            Visibility = Visibility.Collapsed;
        }

        public void Set(DataBannerKind kind, string title, string body, bool literalBody = false)
        {
            Kind = kind;
            Brush bg, fg; string icon;
            switch (kind)
            {
                case DataBannerKind.Success: bg = Ui.Res("B.MintWash"); fg = Ui.Res("B.OnPrimaryContainer"); icon = DataToolIcons.Check; break;
                case DataBannerKind.Warning: bg = Ui.Res("B.AmberWash"); fg = Ui.Res("B.Amber"); icon = DataToolIcons.Warn; break;
                case DataBannerKind.Error: bg = Ui.Res("B.DangerWash"); fg = Ui.Res("B.Danger"); icon = DataToolIcons.Warn; break;
                default: bg = Ui.Res("B.TertiaryContainer"); fg = Ui.Res("B.OnTertiaryContainer"); icon = DataToolIcons.Info; break;
            }
            Background = bg; _title.Foreground = fg; _body.Foreground = fg;
            _icon.Content = Ui.Icon(icon, fg, 22, 1.8);
            _title.Text = title; _body.Text = body ?? "";
            Core.Language.SetLiteral(_body, literalBody);
            _body.Visibility = string.IsNullOrEmpty(body) ? Visibility.Collapsed : Visibility.Visible;
            Visibility = Visibility.Visible;
            AutomationProperties.SetName(this, title + (string.IsNullOrEmpty(body) || literalBody ? "" : ". " + body));
            AutomationProperties.SetLiveSetting(this, kind == DataBannerKind.Error ? AutomationLiveSetting.Assertive : AutomationLiveSetting.Polite);
            try { UIElementAutomationPeer.FromElement(this)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged); } catch { }
        }
    }

    /// <summary>
    /// The window every data and security dialog shares: borderless, rounded, topmost like the Convert dialog, with a header that always names the file,
    /// a body that scrolls, and a footer with one live status line, Cancel and the primary action. Work runs off the UI thread and Esc cancels it.
    /// </summary>
    internal abstract class DataToolWindow : Window
    {
        private static ResourceDictionary _styles;

        protected readonly string ToolName;
        protected readonly string FileLabel;
        protected readonly StackPanel Body = new StackPanel();
        protected readonly TextBlock StatusText = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        protected readonly StackPanel FooterSecondary = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        internal readonly Button CancelButton;
        internal readonly Button PrimaryButton;
        private readonly TextBlock _summary = new TextBlock { FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis };
        private readonly TextBlock _fileText = new TextBlock { FontSize = 12, FontWeight = FontWeights.Medium, TextTrimming = TextTrimming.CharacterEllipsis };
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly ScrollViewer _scroll;
        protected CancellationToken Token => _cts.Token;

        protected DataToolWindow(string tool, string filePath, double width, string icon)
        {
            ToolName = tool;
            FileLabel = filePath == null ? "" : System.IO.Path.GetFileName(filePath.TrimEnd('\\'));
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize; Width = width; SizeToContent = SizeToContent.Height;
            MaxHeight = Math.Max(420, SystemParameters.WorkArea.Height - 48);
            WindowStartupLocation = WindowStartupLocation.CenterScreen; Topmost = true; ShowInTaskbar = true;
            Title = tool;
            AutomationProperties.SetName(this, FileLabel.Length > 0 ? tool + ": " + FileLabel : tool);
            SourceInitialized += (s, e) => Native.ApplyDarkChrome(this);
            Closed += (s, e) => { try { _cts.Cancel(); } catch { } OnClosedCore(); };
            PreviewKeyDown += OnKey;
            MouseLeftButtonDown += (s, e) => { if (e.ButtonState == MouseButtonState.Pressed && e.OriginalSource is Border) try { DragMove(); } catch { } };

            CancelButton = Ui.Button("Cancel", () => OnCancelButton(), "Btn.Ghost");
            PrimaryButton = Ui.Button("Close", null, "Btn.Accent");
            _primaryAct = () => Close();
            PrimaryButton.Click += (s, e) => _primaryAct?.Invoke();
            _scroll = new ScrollViewer { Content = Body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(0, 0, 4, 0) };
            Content = BuildShell(icon);
            StatusText.Foreground = Ui.Res("B.TextSoft");
            AutomationProperties.SetLiveSetting(StatusText, AutomationLiveSetting.Polite);
            ElementMenu.Attach(this, null, ElementMenu.Options.Copy);
        }

        protected virtual void OnClosedCore() { }

        /// <summary>Esc and the close button: close the window, which also cancels any running work.</summary>
        protected virtual void OnCancel() => Close();

        /// <summary>The footer Cancel button: by default closes; a dialog with running work stops that work first.</summary>
        protected virtual void OnCancelButton() => OnCancel();

        protected virtual void OnKey(object s, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { e.Handled = true; OnCancel(); }
            else if (e.Key == Key.Enter && PrimaryButton.IsEnabled && PrimaryButton.IsVisible && !(Keyboard.FocusedElement is TextBox tb && tb.AcceptsReturn) && !(Keyboard.FocusedElement is Button) && !(Keyboard.FocusedElement is ListBoxItem) && !(Keyboard.FocusedElement is ComboBox))
            { e.Handled = true; PrimaryButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); }
        }

        /// <summary>Starts new cancellable work; a previous run is cancelled.</summary>
        protected CancellationToken NewRun()
        {
            try { _cts.Cancel(); } catch { }
            _cts = new CancellationTokenSource();
            return _cts.Token;
        }

        protected void CancelRun() { try { _cts.Cancel(); } catch { } }

        private FrameworkElement BuildShell(string icon)
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var head = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.ColumnDefinitions.Add(new ColumnDefinition());
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var tile = new Border { Width = 44, Height = 44, CornerRadius = new CornerRadius(16), Background = Ui.Res("B.TertiaryContainer"), Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Top, Child = Ui.Icon(icon, Ui.Res("B.OnTertiaryContainer"), 22, 1.8) };
            var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var title = new TextBlock { Text = ToolName, Style = Ui.Style("T.Title"), FontSize = 22, FontWeight = FontWeights.Medium, TextTrimming = TextTrimming.CharacterEllipsis };
            var line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
            Core.Language.SetLiteral(_fileText, true); _fileText.Text = FileLabel; _fileText.Foreground = Ui.Res("B.Text"); _fileText.MaxWidth = 420; _fileText.Margin = new Thickness(0, 0, 8, 0);
            _summary.Foreground = Ui.Res("B.TextSoft");
            line.Children.Add(_fileText); line.Children.Add(_summary);
            titles.Children.Add(title); titles.Children.Add(line);
            Grid.SetColumn(titles, 1);
            var close = Ui.Button("", () => OnCancel(), "Btn.Icon", DataToolIcons.Close);
            close.Margin = new Thickness(8, 0, 0, 0);
            AutomationProperties.SetName(close, "Close");
            close.ToolTip = "Closes this window (Esc). Work that is running is cancelled and nothing unfinished is kept.";
            Grid.SetColumn(close, 2);
            head.Children.Add(tile); head.Children.Add(titles); head.Children.Add(close);
            root.Children.Add(head);

            Grid.SetRow(_scroll, 1); root.Children.Add(_scroll);

            var foot = new Grid { Margin = new Thickness(0, 14, 0, 0) };
            foot.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            foot.ColumnDefinitions.Add(new ColumnDefinition());
            foot.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            foot.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(StatusText, 1); StatusText.Margin = new Thickness(12, 0, 12, 8);
            Grid.SetColumn(CancelButton, 2); Grid.SetColumn(PrimaryButton, 3);
            CancelButton.Margin = new Thickness(0, 0, 8, 0); PrimaryButton.Margin = new Thickness(0);
            foot.Children.Add(FooterSecondary); foot.Children.Add(StatusText); foot.Children.Add(CancelButton); foot.Children.Add(PrimaryButton);
            Grid.SetRow(foot, 2); root.Children.Add(foot);

            return new Grid
            {
                Margin = new Thickness(16),
                Children = { new Border { Background = Ui.Res("B.Raised"), CornerRadius = new CornerRadius(28), Padding = new Thickness(24, 20, 20, 18), Child = root, Effect = (System.Windows.Media.Effects.Effect)Application.Current.Resources["Fx.Shadow"] } }
            };
        }

        // ------------------------------------------------------------------ pieces

        protected void SetSummary(string text) { _summary.Text = text ?? ""; }
        internal string SummaryText => _summary.Text;
        internal string StatusLine => StatusText.Text;
        protected void Status(string text, bool warn = false) { StatusText.Text = text ?? ""; StatusText.Foreground = warn ? Ui.Res("B.Amber") : Ui.Res("B.TextSoft"); }

        /// <summary>The primary action: label, icon, handler and whether it can be pressed. Replaces the old handler.</summary>
        protected void SetPrimary(string label, string icon, Action act, bool enabled = true)
        {
            PrimaryButton.Content = ButtonContent(label, icon);
            _primaryAct = act;
            PrimaryButton.IsEnabled = enabled;
            AutomationProperties.SetName(PrimaryButton, label);
        }
        private Action _primaryAct;

        protected void SetCancel(string label, bool visible = true)
        {
            CancelButton.Content = label; CancelButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            AutomationProperties.SetName(CancelButton, label);
        }

        /// <summary>Done and error states replace Cancel with a single Close.</summary>
        protected void ShowClose(bool done = true)
        {
            CancelButton.Visibility = Visibility.Collapsed;
            SetPrimary("Close", null, () => Close());
        }

        private static object ButtonContent(string label, string icon)
        {
            if (icon == null) return label;
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new ContentControl { Content = Ui.Icon(icon, Ui.Res("B.MintInk"), 14, 1.8), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
            sp.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
            return sp;
        }

        protected static TextBlock Label(string text, bool section = false, Thickness? margin = null)
        {
            var t = new TextBlock
            {
                Text = section ? text.ToUpperInvariant() : text, FontSize = section ? 12 : 12, FontWeight = FontWeights.Medium, FontFamily = Ui.Font("F.Body"),
                Foreground = section ? Ui.Res("B.Mint") : Ui.Res("B.Text"), TextWrapping = TextWrapping.Wrap, Margin = margin ?? new Thickness(0, 4, 0, 6),
            };
            return t;
        }

        protected static TextBlock Note(string text, Thickness? margin = null) => new TextBlock
        {
            Text = text, FontSize = 12, Foreground = Ui.Res("B.TextSoft"), FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap, Margin = margin ?? new Thickness(0, 0, 0, 8),
        };

        /// <summary>A sunken card of label and value rows.</summary>
        protected static Border Card(params (string Label, UIElement Value)[] rows)
        {
            var g = new Grid { Margin = new Thickness(0) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            for (int i = 0; i < rows.Length; i++)
            {
                g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var l = new TextBlock { Text = rows[i].Label, FontSize = 12, Foreground = Ui.Res("B.TextSoft"), Margin = new Thickness(0, 4, 8, 4), VerticalAlignment = VerticalAlignment.Top, TextWrapping = TextWrapping.Wrap };
                Grid.SetRow(l, i);
                var v = rows[i].Value; if (v is FrameworkElement fe) fe.Margin = new Thickness(0, 4, 0, 4);
                Grid.SetRow(v, i); Grid.SetColumn(v, 1);
                g.Children.Add(l); g.Children.Add(v);
            }
            return new Border { CornerRadius = new CornerRadius(20), Background = Ui.Res("B.Sunken"), Padding = new Thickness(16, 10, 16, 10), Margin = new Thickness(0, 0, 0, 12), Child = g };
        }

        protected static TextBlock Value(string text, bool mono = false)
        {
            var t = new TextBlock { Text = text, FontSize = mono ? 12 : 12, FontFamily = mono ? Ui.Font("F.Mono") : Ui.Font("F.Body"), Foreground = Ui.Res("B.Text"), TextWrapping = TextWrapping.Wrap };
            if (mono) Core.Language.SetLiteral(t, true);      // hashes, serials and similar are data, never translated
            return t;
        }

        /// <summary>A value that comes from the file (a name, a subject, a publisher): shown as it is in every language.</summary>
        protected static TextBlock Data(string text, bool mono = false) { var t = Value(text, mono); Core.Language.SetLiteral(t, true); return t; }

        /// <summary>A small tag such as "Valid" or "Not trusted".</summary>
        protected static Border Badge(string text, bool good, bool warn = false)
        {
            Brush bg = good ? Ui.Res("B.MintWash") : warn ? Ui.Res("B.AmberWash") : Ui.Res("B.DangerWash");
            Brush fg = good ? Ui.Res("B.OnPrimaryContainer") : warn ? Ui.Res("B.Amber") : Ui.Res("B.Danger");
            return new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 1, 8, 2), Background = bg, VerticalAlignment = VerticalAlignment.Center, Child = new TextBlock { Text = text, FontSize = 11, Foreground = fg } };
        }

        protected static Button Action(string label, string icon, Action onClick, string style = "Btn.Ghost")
        {
            var b = Ui.Button(label, onClick, style, icon);
            b.Margin = new Thickness(0, 0, 8, 8);
            return b;
        }

        // ------------------------------------------------------------------ choice controls

        private static ResourceDictionary Styles()
        {
            if (_styles != null) return _styles;
            const string xaml = @"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
  <Style x:Key='DT.Seg' TargetType='RadioButton'>
    <Setter Property='Foreground' Value='{DynamicResource B.TextSoft}'/>
    <Setter Property='FontSize' Value='13'/><Setter Property='Height' Value='38'/><Setter Property='MinWidth' Value='64'/>
    <Setter Property='Cursor' Value='Hand'/><Setter Property='FocusVisualStyle' Value='{x:Null}'/>
    <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='RadioButton'>
      <Grid Background='Transparent'>
        <Border x:Name='Bd' Background='Transparent' CornerRadius='19' Margin='1'/>
        <Border x:Name='Focus' CornerRadius='19' BorderBrush='{DynamicResource B.Mint}' BorderThickness='2' Opacity='0'/>
        <StackPanel Orientation='Horizontal' HorizontalAlignment='Center' VerticalAlignment='Center' Margin='16,0'>
          <Path x:Name='Tick' Data='M5 12l5 5 9-10' Stroke='{DynamicResource B.OnSecondaryContainer}' StrokeThickness='1.8' Width='13' Height='13' Stretch='Uniform' Visibility='Collapsed' Margin='0,0,6,0' StrokeStartLineCap='Round' StrokeEndLineCap='Round'/>
          <ContentPresenter VerticalAlignment='Center'/>
        </StackPanel>
      </Grid>
      <ControlTemplate.Triggers>
        <Trigger Property='IsChecked' Value='True'><Setter TargetName='Bd' Property='Background' Value='{DynamicResource B.SecondaryContainer}'/><Setter TargetName='Tick' Property='Visibility' Value='Visible'/><Setter Property='Foreground' Value='{DynamicResource B.OnSecondaryContainer}'/></Trigger>
        <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Focus' Property='Opacity' Value='1'/></Trigger>
        <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Bd' Property='Opacity' Value='0.85'/></Trigger>
        <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.38'/></Trigger>
      </ControlTemplate.Triggers>
    </ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style x:Key='DT.Chip' TargetType='RadioButton'>
    <Setter Property='Foreground' Value='{DynamicResource B.TextSoft}'/>
    <Setter Property='FontSize' Value='13'/><Setter Property='Height' Value='32'/><Setter Property='Margin' Value='0,0,6,6'/>
    <Setter Property='Cursor' Value='Hand'/><Setter Property='FocusVisualStyle' Value='{x:Null}'/>
    <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='RadioButton'>
      <Grid Background='Transparent'>
        <Border x:Name='Bd' Background='Transparent' BorderBrush='{DynamicResource B.Line}' BorderThickness='1' CornerRadius='8'/>
        <Border x:Name='Focus' CornerRadius='10' BorderBrush='{DynamicResource B.Mint}' BorderThickness='2' Margin='-2' Opacity='0'/>
        <StackPanel Orientation='Horizontal' HorizontalAlignment='Center' VerticalAlignment='Center' Margin='14,0'>
          <Path x:Name='Tick' Data='M5 12l5 5 9-10' Stroke='{DynamicResource B.OnSecondaryContainer}' StrokeThickness='1.8' Width='12' Height='12' Stretch='Uniform' Visibility='Collapsed' Margin='0,0,6,0' StrokeStartLineCap='Round' StrokeEndLineCap='Round'/>
          <ContentPresenter VerticalAlignment='Center'/>
        </StackPanel>
      </Grid>
      <ControlTemplate.Triggers>
        <Trigger Property='IsChecked' Value='True'><Setter TargetName='Bd' Property='Background' Value='{DynamicResource B.SecondaryContainer}'/><Setter TargetName='Bd' Property='BorderBrush' Value='Transparent'/><Setter TargetName='Tick' Property='Visibility' Value='Visible'/><Setter Property='Foreground' Value='{DynamicResource B.OnSecondaryContainer}'/></Trigger>
        <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Focus' Property='Opacity' Value='1'/></Trigger>
        <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.38'/></Trigger>
      </ControlTemplate.Triggers>
    </ControlTemplate></Setter.Value></Setter>
  </Style>
</ResourceDictionary>";
            _styles = (ResourceDictionary)XamlReader.Parse(xaml);
            return _styles;
        }

        internal sealed class Choice
        {
            public readonly FrameworkElement Element;
            public readonly List<RadioButton> Buttons = new List<RadioButton>();
            public int Index { get; private set; }
            private readonly Action<int> _changed;
            private bool _quiet;
            public Choice(FrameworkElement element, Action<int> changed) { Element = element; _changed = changed; }
            internal void Add(RadioButton b) { Buttons.Add(b); }
            public void Select(int i, bool notify = false)
            {
                if (i < 0 || i >= Buttons.Count) return;
                _quiet = !notify; Index = i; Buttons[i].IsChecked = true; _quiet = false;
            }
            internal void Fire(int i) { if (_quiet) return; Index = i; _changed?.Invoke(i); }
            public void Enable(int i, bool on) { if (i >= 0 && i < Buttons.Count) Buttons[i].IsEnabled = on; }
        }

        /// <summary>A pill with one part per label; the selected part is filled. A radio group for assistive technology, with arrow keys inside.</summary>
        protected Choice Segmented(string name, string[] labels, int selected, Action<int> changed)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            var pill = new Border { CornerRadius = new CornerRadius(20), BorderBrush = Ui.Res("B.LineStrong"), BorderThickness = new Thickness(1), Child = panel, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 12, 8) };
            AutomationProperties.SetName(pill, name);
            var choice = new Choice(pill, changed);
            string group = name + Guid.NewGuid().ToString("N");
            for (int i = 0; i < labels.Length; i++)
            {
                int idx = i;
                var rb = new RadioButton { Content = labels[i], GroupName = group, Style = (Style)Styles()["DT.Seg"], IsChecked = i == selected };
                AutomationProperties.SetName(rb, labels[i]);
                rb.Checked += (s, e) => choice.Fire(idx);
                panel.Children.Add(rb); choice.Add(rb);
            }
            choice.Select(selected);
            return choice;
        }

        /// <summary>A row of separate chips where one is selected (algorithm, separator).</summary>
        protected Choice Chips(string name, string[] labels, int selected, Action<int> changed)
        {
            var panel = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
            AutomationProperties.SetName(panel, name);
            var choice = new Choice(panel, changed);
            string group = name + Guid.NewGuid().ToString("N");
            for (int i = 0; i < labels.Length; i++)
            {
                int idx = i;
                var rb = new RadioButton { Content = labels[i], GroupName = group, Style = (Style)Styles()["DT.Chip"], IsChecked = i == selected };
                AutomationProperties.SetName(rb, labels[i]);
                rb.Checked += (s, e) => choice.Fire(idx);
                panel.Children.Add(rb); choice.Add(rb);
            }
            choice.Select(selected);
            return choice;
        }

        protected static CheckBox Switch(string title, string detail, bool on, Action<bool> changed)
        {
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeights.Medium, Foreground = Ui.Res("B.Text") });
            if (!string.IsNullOrEmpty(detail)) sp.Children.Add(new TextBlock { Text = detail, FontSize = 12, Foreground = Ui.Res("B.TextSoft"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
            var cb = new CheckBox { Style = Ui.Style("Switch"), Content = sp, IsChecked = on, Margin = new Thickness(0, 2, 0, 8) };
            AutomationProperties.SetName(cb, title);
            cb.Checked += (s, e) => changed?.Invoke(true); cb.Unchecked += (s, e) => changed?.Invoke(false);
            return cb;
        }

        /// <summary>A sunken monospace block (lines of a file, a hash).</summary>
        protected static Border CodeBlock(UIElement content, double maxHeight = 220) => new Border
        {
            CornerRadius = new CornerRadius(16), Background = Ui.Res("B.Sunken"), Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 0, 0, 12),
            Child = new ScrollViewer { Content = content, MaxHeight = maxHeight, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto },
        };

        protected static TextBlock Mono(string text, double size = 12) { var t = new TextBlock { Text = text, FontFamily = Ui.Font("F.Mono"), FontSize = TypeScale.Snap(size), Foreground = Ui.Res("B.Text") }; Core.Language.SetLiteral(t, true); return t; }

        /// <summary>Opens the dialog and brings it to the front.</summary>
        protected void Present() { Show(); Activate(); }

        /// <summary>Shortens a long name in the middle so both ends stay readable.</summary>
        internal static string Middle(string s, int max = 48) => s == null || s.Length <= max ? s : s[..(max / 2 - 1)] + "…" + s[^(max / 2 - 1)..];

        /// <summary>Presses a button the way a click does (tests and shortcuts).</summary>
        internal static void Press(Button b) { if (b != null && b.IsEnabled && b.IsVisible) b.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); }

        /// <summary>Every button in the window with its visible label.</summary>
        internal IEnumerable<Button> AllButtons() => Descend<Button>(this);

        internal static IEnumerable<T> Descend<T>(DependencyObject root) where T : DependencyObject
        {
            if (root == null) yield break;
            int n = root is Visual || root is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetChildrenCount(root) : 0;
            for (int i = 0; i < n; i++)
            {
                var c = VisualTreeHelper.GetChild(root, i);
                if (c is T t) yield return t;
                foreach (var x in Descend<T>(c)) yield return x;
            }
        }

        internal static string LabelOf(Button b)
        {
            if (b.Content is string s) return s;
            if (b.Content is StackPanel sp) return string.Concat(sp.Children.OfType<TextBlock>().Select(t => t.Text));
            return AutomationProperties.GetName(b);
        }

        internal Button FindButton(string label) => AllButtons().FirstOrDefault(b => b.IsVisible && string.Equals(LabelOf(b), label, StringComparison.OrdinalIgnoreCase));
    }
}
