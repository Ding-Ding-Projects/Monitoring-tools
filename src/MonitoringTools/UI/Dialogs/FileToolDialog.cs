using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    public enum ToolState { Ready, Working, Done, Error }

    /// <summary>
    /// The window every file tool dialog shares: a borderless card with a header (tool tile, tool name, file name and one-line summary,
    /// close), a scrolling body that the dialog swaps between Ready, Working, Done and Error views, and a footer with a live status
    /// line, a secondary action, Cancel and the primary action. Esc cancels running work or closes; Enter runs the primary action
    /// unless focus is somewhere that uses Enter itself. Work runs off the UI thread and Cancel removes any unfinished output.
    /// </summary>
    public abstract class FileToolDialog : Window
    {
        private static readonly List<FileToolDialog> Open = new List<FileToolDialog>();
        internal static IReadOnlyList<FileToolDialog> OpenDialogs => Open;

        public string ToolName { get; }
        public ToolState State { get; private set; } = ToolState.Ready;
        protected CancellationTokenSource Cts = new CancellationTokenSource();

        private readonly TextBlock _fileLine = new TextBlock(), _summary = new TextBlock(), _status = new TextBlock();
        private readonly StackPanel _fileRow = new StackPanel { Orientation = Orientation.Horizontal };
        private readonly ContentControl _body = new ContentControl { IsTabStop = false };
        private readonly ScrollViewer _scroll;
        private readonly StackPanel _leftActions = new StackPanel { Orientation = Orientation.Horizontal };
        private readonly Button _cancel, _primary, _close;
        private readonly Grid _footer = new Grid();

        internal Button PrimaryButton => _primary;
        internal Button CancelButton => _cancel;
        internal Button CloseButton => _close;
        internal string StatusText => _status.Text;
        internal FrameworkElement CurrentBody => _body.Content as FrameworkElement;
        /// <summary>The task of the last primary action, so a check can wait for the real work to finish.</summary>
        internal Task Running { get; set; } = Task.CompletedTask;

        protected string PrimaryLabel { get; private set; }

        protected FileToolDialog(string toolName, string icon, int width, string primaryLabel, string primaryIcon)
        {
            ToolName = toolName; PrimaryLabel = primaryLabel;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize; Width = width + 32; SizeToContent = SizeToContent.Height;
            MaxHeight = Math.Max(420, SystemParameters.WorkArea.Height);
            WindowStartupLocation = WindowStartupLocation.CenterScreen; Topmost = true; ShowInTaskbar = true;
            Title = toolName;
            SourceInitialized += (s, e) => Native.ApplyDarkChrome(this);
            Closed += (s, e) => { try { Cts.Cancel(); } catch { } Open.Remove(this); OnClosed2(); };
            MouseLeftButtonDown += (s, e) => { if (e.ButtonState == MouseButtonState.Pressed && e.OriginalSource is Border) try { DragMove(); } catch { } };
            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape) { e.Handled = true; CancelOrClose(); }
                else if (e.Key == Key.Enter && State == ToolState.Ready && _primary.IsEnabled && !(Keyboard.FocusedElement is TextBox tb && tb.AcceptsReturn)
                         && !(Keyboard.FocusedElement is Button) && !(Keyboard.FocusedElement is CheckBox) && !(Keyboard.FocusedElement is ListBoxItem) && !(Keyboard.FocusedElement is TextBox))
                { e.Handled = true; _primary.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
            };

            _cancel = Ui.Button("Cancel", CancelOrClose, "Btn.Ghost"); _cancel.Margin = new Thickness(8, 0, 0, 0);
            _primary = Ui.Button(primaryLabel, () => PrimaryPressed(), "Btn.Accent", primaryIcon); _primary.Margin = new Thickness(8, 0, 0, 0);
            _close = Ui.Button("Close", Close, "Btn.Accent"); _close.Margin = new Thickness(8, 0, 0, 0); _close.Visibility = Visibility.Collapsed;
            AutomationProperties.SetName(_close, "Close");

            _scroll = new ScrollViewer { Content = _body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false };
            Content = Frame(icon);
            ElementMenu.Attach(this, null, ElementMenu.Options.Copy);
            Open.Add(this);
        }

        protected virtual void OnClosed2() { }

        // ------------------------------------------------------------ layout

        private FrameworkElement Frame(string icon)
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var head = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.ColumnDefinitions.Add(new ColumnDefinition());
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var tile = new Border { Width = 44, Height = 44, CornerRadius = new CornerRadius(14), Background = Ui.Res("B.TertiaryContainer"), Child = Ui.Icon(icon, Ui.Res("B.OnTertiaryContainer"), 22, 1.8), VerticalAlignment = VerticalAlignment.Top };
            var texts = new StackPanel { Margin = new Thickness(14, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            texts.Children.Add(new TextBlock { Text = ToolName, FontSize = 20, FontFamily = Ui.Font("F.Display"), Foreground = Ui.Res("B.Text"), FontWeight = FontWeights.Medium });
            _fileLine.FontSize = 13; _fileLine.FontWeight = FontWeights.Medium; _fileLine.Foreground = Ui.Res("B.Text"); _fileLine.FontFamily = Ui.Font("F.Body"); _fileLine.TextTrimming = TextTrimming.CharacterEllipsis; _fileLine.Margin = new Thickness(0, 0, 8, 0);
            Core.Language.SetLiteral(_fileLine, true);
            _summary.FontSize = 13; _summary.Foreground = Ui.Res("B.TextSoft"); _summary.FontFamily = Ui.Font("F.Body"); _summary.TextWrapping = TextWrapping.Wrap;
            _fileRow.Children.Add(_fileLine); _fileRow.Children.Add(_summary);
            texts.Children.Add(_fileRow);
            var closeBtn = Ui.Button("", Close, "Btn.Ghost", Icons.Close);
            closeBtn.Width = 40; closeBtn.Padding = new Thickness(0); closeBtn.Margin = new Thickness(0); closeBtn.VerticalAlignment = VerticalAlignment.Top;
            AutomationProperties.SetName(closeBtn, "Close");
            closeBtn.ToolTip = "Closes this window (Esc). Unfinished work is cancelled.";
            Grid.SetColumn(texts, 1); Grid.SetColumn(closeBtn, 2);
            head.Children.Add(tile); head.Children.Add(texts); head.Children.Add(closeBtn);
            root.Children.Add(head);

            Grid.SetRow(_scroll, 1);
            root.Children.Add(_scroll);

            _footer.Margin = new Thickness(0, 12, 0, 0);
            _footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _footer.ColumnDefinitions.Add(new ColumnDefinition());
            _footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _status.FontSize = 13; _status.Foreground = Ui.Res("B.TextSoft"); _status.FontFamily = Ui.Font("F.Body"); _status.TextWrapping = TextWrapping.Wrap; _status.VerticalAlignment = VerticalAlignment.Center; _status.Margin = new Thickness(4, 0, 12, 0);
            AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
            _leftActions.VerticalAlignment = VerticalAlignment.Center;
            var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            right.Children.Add(_cancel); right.Children.Add(_primary); right.Children.Add(_close);
            Grid.SetColumn(_status, 1); Grid.SetColumn(right, 2);
            _footer.Children.Add(_leftActions); _footer.Children.Add(_status); _footer.Children.Add(right);
            Grid.SetRow(_footer, 2);
            root.Children.Add(_footer);

            return new Grid
            {
                Margin = new Thickness(16),
                Children = { new Border { Background = Ui.Res("B.Raised"), CornerRadius = new CornerRadius(28), Padding = new Thickness(24, 20, 20, 18), Child = root, Effect = (System.Windows.Media.Effects.Effect)Application.Current.Resources["Fx.Shadow"] } }
            };
        }

        // ------------------------------------------------------------ what a subclass sets

        /// <summary>The file line under the tool name: the file name (shown the same in every language) and a summary.</summary>
        protected void SetFile(string fileName, string summary)
        {
            _fileLine.Text = FT.Middle(fileName, 44);
            _fileLine.ToolTip = fileName;
            _summary.Text = summary ?? "";
            var name = ToolName + ": " + fileName;
            AutomationProperties.SetName(this, name);
            Title = name;
        }

        protected void SetSummary(string summary) => _summary.Text = summary ?? "";

        protected void SetStatus(string text) { _status.Text = text ?? ""; }

        /// <summary>Puts a view in the body and the footer in the matching mode.</summary>
        protected void Show(ToolState state, FrameworkElement body, string status = null)
        {
            State = state;
            _body.Content = body;
            _scroll.ScrollToTop();
            _cancel.Visibility = state == ToolState.Done || state == ToolState.Error ? Visibility.Collapsed : Visibility.Visible;
            _primary.Visibility = state == ToolState.Done || state == ToolState.Error ? Visibility.Collapsed : Visibility.Visible;
            _close.Visibility = state == ToolState.Done || state == ToolState.Error ? Visibility.Visible : Visibility.Collapsed;
            _leftActions.Visibility = state == ToolState.Ready ? Visibility.Visible : Visibility.Collapsed;
            if (state == ToolState.Working) _primary.IsEnabled = false;
            if (status != null) SetStatus(status);
            _status.Foreground = state == ToolState.Error ? Ui.Res("B.Danger") : Ui.Res("B.TextSoft");
        }

        protected void SetPrimary(bool enabled, string label = null)
        {
            _primary.IsEnabled = enabled;
            if (label != null) { PrimaryLabel = label; SetButtonText(_primary, label); }
        }

        protected void SetLeftActions(params UIElement[] items)
        {
            _leftActions.Children.Clear();
            foreach (var i in items) _leftActions.Children.Add(i);
        }

        protected static void SetButtonText(Button b, string text)
        {
            if (b.Content is StackPanel sp && sp.Children.OfType<TextBlock>().FirstOrDefault() is TextBlock tb) tb.Text = text;
            else b.Content = text;
        }

        protected virtual void PrimaryPressed() { }

        /// <summary>Esc and Cancel: stop running work (the engine removes unfinished output), otherwise close.</summary>
        protected virtual void CancelOrClose()
        {
            if (State == ToolState.Working) { try { Cts.Cancel(); } catch { } return; }
            Close();
        }

        protected void NewRun() { try { Cts.Dispose(); } catch { } Cts = new CancellationTokenSource(); }

        /// <summary>Runs the primary action as a task the checks can await.</summary>
        protected void Run(Func<Task> work)
        {
            var t = work();
            Running = t;
        }

        protected void Ui2(Action a) { if (Dispatcher.CheckAccess()) a(); else Dispatcher.BeginInvoke(a); }

        // ------------------------------------------------------------ shared views

        protected FT.ProgressBlock WorkingView(string accessibleName, string primaryLabel, string cancelNote, out Action<double, string, string> report)
        {
            var pb = new FT.ProgressBlock(accessibleName);
            var sp = new StackPanel();
            sp.Children.Add(pb);
            Show(ToolState.Working, sp, cancelNote);
            report = (v, cap, det) => pb.Set(v, cap, det);
            return pb;
        }

        protected StackPanel ErrorView(string title, string text, string notChanged, Action retry, string retryLabel, string details)
        {
            var sp = new StackPanel();
            string detail = null;
            foreach (var prefix in new[] { "FFmpeg reported: ", "The imaging component reported: " })
                if (text != null && text.StartsWith(prefix)) { detail = text.Substring(prefix.Length); text = prefix.TrimEnd(); break; }
            sp.Children.Add(FT.Banner(BannerKind.Error, title, text, null, detail));
            var inner = new StackPanel();
            inner.Children.Add(FT.Eyebrow("What was not changed", new Thickness(0, 0, 0, 6)));
            inner.Children.Add(FT.Body(notChanged, 13, Ui.Res("B.Text")));
            sp.Children.Add(FT.Card(inner));
            var row = new WrapPanel();
            if (retry != null) row.Children.Add(FT.Accent(retryLabel, retry, MediaIcons.Refresh));
            if (row.Children.Count > 0) ((Button)row.Children[0]).Margin = new Thickness(0, 0, 8, 8);
            var copy = FT.Tonal("Copy details", () => { try { Clipboard.SetText(details ?? text); SetStatus("Details copied."); } catch { } }, MediaIcons.Copy);
            copy.Margin = new Thickness(0, 0, 8, 8);
            row.Children.Add(copy);
            sp.Children.Add(row);
            return sp;
        }

        protected static void Reveal(string path)
        {
            try { System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\""); } catch { }
        }

        protected static void Play(string path)
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true }); } catch { }
        }

        protected static string FolderChoice(Window owner, string title)
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = title };
            return dlg.ShowDialog(owner) == true ? dlg.FolderName : null;
        }

        /// <summary>Another open dialog of the same kind for the same file: it is brought forward instead of opening a second.</summary>
        protected static T Existing<T>(Func<T, bool> same) where T : FileToolDialog => Open.OfType<T>().FirstOrDefault(same);

        /// <summary>All visible text of the current body, for checks.</summary>
        internal string AllText()
        {
            var sb = new System.Text.StringBuilder();
            void Walk(DependencyObject d)
            {
                if (d is TextBlock tb) sb.AppendLine(tb.Inlines.Count > 0 && string.IsNullOrEmpty(tb.Text) ? new System.Windows.Documents.TextRange(tb.ContentStart, tb.ContentEnd).Text : tb.Text);
                if (d is TextBox tx) sb.AppendLine(tx.Text);
                int n = d is Visual || d is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetChildrenCount(d) : 0;
                for (int i = 0; i < n; i++) Walk(VisualTreeHelper.GetChild(d, i));
            }
            Walk(this);
            return sb.ToString();
        }

        internal T FindByName<T>(string automationName) where T : DependencyObject
        {
            T hit = null;
            void Walk(DependencyObject d)
            {
                if (hit != null) return;
                if (d is T t && d is UIElement && (d.ReadLocalValue(AutomationProperties.NameProperty) as string) == automationName) { hit = t; return; }
                int n = d is Visual || d is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetChildrenCount(d) : 0;
                for (int i = 0; i < n; i++) Walk(VisualTreeHelper.GetChild(d, i));
            }
            Walk(this);
            return hit;
        }
    }
}
