using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using MonitoringTools.Clip;
using MonitoringTools.Core;
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI.Dialogs
{
    /// <summary>
    /// Data viewer and converter for CSV, TSV, JSON and YAML: a table of the first 500 rows or the text, a validity check that names the line and
    /// column of the first error, and conversion between the three formats. The original file is never changed unless Format in place is chosen,
    /// and that keeps a .bak backup.
    /// </summary>
    internal sealed class DataViewerDialog : DataToolWindow
    {
        internal static DataViewerDialog Current { get; private set; }

        private readonly string _path;
        private readonly DataKind _format;
        private DataProbe _probe;
        private char? _delimiter;
        private readonly BannerBox _banner = new BannerBox();
        private readonly ContentControl _view = new ContentControl();
        private readonly ContentControl _chipsHost = new ContentControl();
        private readonly ContentControl _errorHost = new ContentControl();
        private readonly ContentControl _convertHost = new ContentControl();
        private readonly ContentControl _doneHost = new ContentControl();
        private readonly SearchField _search = new SearchField("data-viewer", "Search rows", compact: true);
        private Choice _viewChoice, _targetChoice;
        private bool _textView;
        private DataKind _target;
        private bool _pretty = true;
        private TextBox _saveAs, _text;
        private CheckBox _prettySwitch;
        private ListBox _rows;
        private int _cell = -1;
        private string _lastName;
        private string _written;
        private readonly TaskCompletionSource<bool> _ready = new TaskCompletionSource<bool>();
        private TaskCompletionSource<bool> _reload = new TaskCompletionSource<bool>();

        internal DataProbe Probe => _probe;
        internal BannerBox Banner => _banner;
        internal TextBox SaveAsBox => _saveAs;
        internal Choice TargetChoice => _targetChoice;
        internal Choice ViewChoice => _viewChoice;
        internal string Written => _written;
        internal int RowsShown => _rows?.Items.Count ?? 0;
        internal TextBox TextBoxView => _text;
        internal SearchField Search => _search;
        internal Task Ready => _ready.Task;
        internal Task Reloaded => _reload.Task;
        internal void ResetReloaded() => _reload = new TaskCompletionSource<bool>();
        internal ConvertStats LastStats;

        public static DataViewerDialog Open(string path)
        {
            if (!File.Exists(path)) { App.Toasts.ShowNotice("Nothing to open", "The file is no longer there.", App.Toasts.Amber, 4, icon: Icons.Bolt); return null; }
            var d = new DataViewerDialog(path);
            Current = d; d.Present();
            d.Load();
            return d;
        }

        private DataViewerDialog(string path) : base("Data viewer and converter", path, 800, DataToolIcons.Table)
        {
            _path = path;
            _format = DataEngine.FormatOf(path);
            _target = _format == DataKind.Csv ? DataKind.Json : _format == DataKind.Json ? DataKind.Yaml : DataKind.Json;
            Body.Children.Add(_banner);
            _banner.Set(DataBannerKind.Info, "Reading the file…", "Checking that it is valid.");
            Body.Children.Add(_errorHost);
            var controls = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
            _viewChoice = Segmented("View", new[] { "Table", "Text" }, 0, i => { _textView = i == 1; RenderView(); });
            controls.Children.Add(_viewChoice.Element);
            controls.Children.Add(_chipsHost);
            Body.Children.Add(controls);
            _search.Filter += () => { if (!_textView) RenderTable(); };
            Body.Children.Add(_view);
            Body.Children.Add(_convertHost);
            Body.Children.Add(_doneHost);
            FooterSecondary.Children.Add(Action("Format in place", DataToolIcons.Lines, AskFormatInPlace));
            FooterSecondary.Children[0].SetValue(IsEnabledProperty, false);
            SetCancel("Cancel");
            SetPrimary("Convert", DataToolIcons.Refresh, Convert, false);
            Status("Convert saves a new file. Format in place rewrites this one.");
            PreviewKeyDown += (s, e) => { if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) { _search.Box.Focus(); e.Handled = true; } };
        }

        // ------------------------------------------------------------------ loading

        private async void Load()
        {
            _reload = new TaskCompletionSource<bool>();
            var delim = _delimiter;
            try { _probe = await Task.Run(() => DataEngine.Probe(_path, delim)); }
            catch (Exception ex) { _probe = new DataProbe { Path = _path, Format = _format, ErrorMessage = ex.Message }; }
            Describe();
            _ready.TrySetResult(true);
            _reload.TrySetResult(true);
        }

        private static string Plural(long n, string one, string many) => n.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " " + (n == 1 ? one : many);

        private void Describe()
        {
            var p = _probe;
            string kind = DataEngine.Name(p.Format);
            SetSummary(p.Valid
                ? (p.Format == DataKind.Csv ? Plural(p.Rows, "row", "rows") + " · " + Plural(p.Columns, "column", "columns") : p.Format == DataKind.Json || p.Format == DataKind.Yaml ? Plural(p.Rows, p.Rows == 1 ? "item" : "items", "items") : "") + " · " + Ui.Bytes(p.Size)
                : Ui.Bytes(p.Size));
            _errorHost.Content = null;
            _doneHost.Content = null;
            if (p.Valid)
            {
                string tail = p.Format == DataKind.Csv && p.Rows > DataEngine.PreviewRows ? " Shown: the first " + DataEngine.PreviewRows + " rows." : "";
                if (p.Warnings.Count > 0) _banner.Set(DataBannerKind.Warning, "Valid " + kind + ", with a note", p.Summary + " " + string.Join(" ", p.Warnings) + tail);
                else _banner.Set(DataBannerKind.Success, "Valid " + kind, p.Summary + tail);
            }
            else
            {
                var where = p.ErrorLine > 0 ? " at line " + p.ErrorLine + (p.ErrorColumn > 0 ? ", column " + p.ErrorColumn : "") : "";
                _banner.Set(DataBannerKind.Error, "Invalid " + kind + where, p.ErrorMessage, literalBody: p.Format != DataKind.Csv);
                _errorHost.Content = ErrorPanel(p);
            }
            // separator chips for CSV
            if (p.Format == DataKind.Csv && (p.Valid || p.ErrorLine > 0))
            {
                var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
                panel.Children.Add(new TextBlock { Text = "Separator", FontSize = 12, Foreground = Ui.Res("B.TextSoft"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 8) });
                int sel = _delimiter == null ? 3 : _delimiter == ',' ? 0 : _delimiter == ';' ? 1 : 2;
                var chips = Chips("Separator", new[] { "Comma", "Semicolon", "Tab", "Auto" }, sel, i => { _delimiter = i == 0 ? ',' : i == 1 ? ';' : i == 2 ? (char?)'\t' : null; Load(); });
                panel.Children.Add(chips.Element);
                _chipsHost.Content = panel;
            }
            else _chipsHost.Content = null;
            RenderView();
            BuildConvert();
            PrimaryButton.IsEnabled = p.Valid;
            FooterSecondary.Children[0].SetValue(IsEnabledProperty, p.Valid);
            Status(p.Valid ? "Convert saves a new file. Format in place rewrites this one." : "Fix the file first. Nothing was changed.", !p.Valid);
        }

        private FrameworkElement ErrorPanel(DataProbe p)
        {
            var sp = new StackPanel();
            if (p.ErrorLine > 0) sp.Children.Add(Note("Nothing can be converted until it is fixed.", new Thickness(0, 0, 0, 8)));
            if (p.Context.Length > 0)
            {
                var block = new TextBlock { FontFamily = Ui.Font("F.Mono"), FontSize = 12, Foreground = Ui.Res("B.Text") };
                for (int i = 0; i < p.Context.Length; i++)
                {
                    int ln = p.ContextFirstLine + i;
                    bool bad = ln == p.ErrorLine;
                    var num = new Run(ln.ToString().PadLeft(4) + "  ");
                    num.Foreground = bad ? Ui.Res("B.Danger") : Ui.Res("B.TextMute");
                    if (bad) num.Background = Ui.Res("B.DangerWash");
                    block.Inlines.Add(num);
                    var line = p.Context[i]; if (line.Length > 160) line = line[..160] + "…";
                    int col = Math.Max(1, Math.Min(p.ErrorColumn, line.Length + 1));
                    if (bad && col <= line.Length)
                    {
                        block.Inlines.Add(new Run(line[..(col - 1)]));
                        block.Inlines.Add(new Run(line.Substring(col - 1, Math.Min(Math.Max(1, line.Length - col + 1), 12))) { Background = Ui.Res("B.DangerWash"), Foreground = Ui.Res("B.Danger") });
                        if (col - 1 + 12 < line.Length) block.Inlines.Add(new Run(line[(col - 1 + 12)..]));
                    }
                    else block.Inlines.Add(new Run(line));
                    if (i < p.Context.Length - 1) block.Inlines.Add(new LineBreak());
                }
                Core.Language.SetLiteral(block, true); sp.Children.Add(CodeBlock(block, 140));
            }
            var row = new WrapPanel();
            row.Children.Add(Action("Go to the error", DataToolIcons.Search, GoToError));
            row.Children.Add(Action("Copy message", DataToolIcons.Copy, () => { ClipboardWatcher.SetText(ErrorText()); Status("Copied the message."); }));
            sp.Children.Add(row);
            return sp;
        }

        private string ErrorText() => "Invalid " + DataEngine.Name(_probe.Format) + (_probe.ErrorLine > 0 ? " at line " + _probe.ErrorLine + ", column " + _probe.ErrorColumn : "") + ": " + _probe.ErrorMessage;

        private void GoToError()
        {
            _viewChoice.Select(1, notify: true);
            if (_text == null || _probe.ErrorLine <= 0) return;
            try
            {
                var all = _text.Text;
                int line = Math.Max(0, _probe.ErrorLine - 1), start = 0;
                for (int i = 0; i < line; i++) { int nl = all.IndexOf('\n', start); if (nl < 0) break; start = nl + 1; }
                int end = all.IndexOf('\n', start); if (end < 0) end = all.Length;
                int len = Math.Max(0, end - start); if (len > 0 && all[start + len - 1] == '\r') len--;
                _text.Focus();
                _text.Select(start, len);
                _text.UpdateLayout();
                try { _text.ScrollToLine(_text.GetLineIndexFromCharacterIndex(start)); } catch { }
            }
            catch { }
        }

        // ------------------------------------------------------------------ the table and the text

        private void RenderView()
        {
            if (_probe == null) return;
            if (_textView) { _view.Content = TextView(); }
            else RenderTable();
        }

        private FrameworkElement TextView()
        {
            _text = new TextBox
            {
                Text = _probe.Text, IsReadOnly = true, IsReadOnlyCaretVisible = true, FontFamily = Ui.Font("F.Mono"), FontSize = 12, Foreground = Ui.Res("B.Text"), Background = Brushes.Transparent,
                BorderThickness = new Thickness(0), AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 300, MinHeight = 120, CaretBrush = Ui.Res("B.Mint"), SelectionBrush = Ui.Res("B.Mint"),
            };
            AutomationProperties.SetName(_text, "File text");
            var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            sp.Children.Add(new Border { CornerRadius = new CornerRadius(16), Background = Ui.Res("B.Sunken"), Padding = new Thickness(14, 10, 14, 10), Child = _text });
            if (_probe.TextTruncated) sp.Children.Add(Note("Showing the first " + _probe.Text.Length.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " characters. Conversion uses the whole file.", new Thickness(0, 6, 0, 0)));
            return sp;
        }

        private void RenderTable()
        {
            var p = _probe;
            (_search.Parent as Panel)?.Children.Remove(_search);
            var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            if (!p.Valid && p.Preview.Count == 0)
            {
                sp.Children.Add(Note("There is no table until the file is valid. Switch to Text to see the file as it is."));
                _view.Content = sp; _rows = null; return;
            }
            sp.Children.Add(_search);
            var all = p.Preview;
            var shown = all.Where(r => !_search.State.IsActive || _search.State.Matches(string.Join(" ", r))).ToList();
            _search.SetCount(shown.Count, all.Count, "rows");
            int cols = p.Columns;
            var widths = new double[cols];
            for (int c = 0; c < cols; c++)
            {
                int max = p.Header.Length > c ? p.Header[c].Length : 4;
                foreach (var r in all.Take(120)) if (c < r.Length) max = Math.Max(max, r[c].Length);
                widths[c] = Math.Max(64, Math.Min(260, max * 7.6 + 28));
            }
            bool right(int c) => all.Take(120).Where(r => c < r.Length && r[c].Length > 0).All(r => decimal.TryParse(r[c], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _));
            var hdr = new Grid { Margin = new Thickness(0, 0, 0, 2) };
            var aligns = Enumerable.Range(0, cols).Select(right).ToArray();
            for (int c = 0; c < cols; c++)
            {
                hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(widths[c]) });
                var h = new TextBlock { Text = p.Header[c], FontSize = 12, Foreground = Ui.Res("B.TextSoft"), Margin = new Thickness(10, 8, 10, 8), TextTrimming = TextTrimming.CharacterEllipsis, TextAlignment = aligns[c] ? TextAlignment.Right : TextAlignment.Left };
                Core.Language.SetLiteral(h, true); Grid.SetColumn(h, c); hdr.Children.Add(h);
            }
            _rows = new ListBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0), MaxHeight = 250, Padding = new Thickness(0), SelectionMode = SelectionMode.Single };
            ScrollViewer.SetHorizontalScrollBarVisibility(_rows, ScrollBarVisibility.Disabled);
            AutomationProperties.SetName(_rows, "Table of the first " + p.Preview.Count + " rows");
            foreach (var r in shown)
            {
                var g = new Grid();
                for (int c = 0; c < cols; c++)
                {
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(widths[c]) });
                    var t = new TextBlock
                    {
                        Text = c < r.Length ? r[c] : "", FontSize = 12, Foreground = Ui.Res("B.Text"), Margin = new Thickness(10, 7, 10, 7), TextTrimming = TextTrimming.CharacterEllipsis,
                        FontFamily = aligns[c] ? Ui.Font("F.Mono") : Ui.Font("F.Body"), TextAlignment = aligns[c] ? TextAlignment.Right : TextAlignment.Left,
                    };
                    int cc = c;
                    t.MouseLeftButtonDown += (s, e) => _cell = cc;
                    Core.Language.SetLiteral(t, true); Grid.SetColumn(t, c); g.Children.Add(t);
                }
                var item = new ListBoxItem { Content = g, Padding = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Left, Foreground = Ui.Res("B.Text") };
                item.Resources.Add(SystemColors.HighlightBrushKey, Ui.Res("B.SecondaryContainer"));
                item.Resources.Add(SystemColors.InactiveSelectionHighlightBrushKey, Ui.Res("B.SecondaryContainer"));
                item.Tag = r; Core.Language.SetLiteral(item, true);
                AutomationProperties.SetName(item, string.Join(", ", r.Select((v, i) => (i < p.Header.Length ? p.Header[i] : "column") + " " + v)));
                _rows.Items.Add(item);
            }
            _rows.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control && _rows.SelectedItem is ListBoxItem it && it.Tag is string[] row)
                {
                    var text = _cell >= 0 && _cell < row.Length ? row[_cell] : string.Join("\t", row);
                    ClipboardWatcher.SetText(text); Status("Copied " + (_cell >= 0 && _cell < row.Length ? "the cell" : "the row") + ".");
                    e.Handled = true;
                }
                else if (e.Key == Key.Left && _cell > 0) { _cell--; e.Handled = true; }
                else if (e.Key == Key.Right && _cell < cols - 1) { _cell++; e.Handled = true; }
            };
            var holder = new StackPanel(); holder.Children.Add(hdr); holder.Children.Add(_rows);
            var scroll = new ScrollViewer { Content = holder, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
            sp.Children.Add(new Border { CornerRadius = new CornerRadius(16), Background = Ui.Res("B.Sunken"), Padding = new Thickness(4), Child = scroll });
            if (p.Rows > p.Preview.Count) sp.Children.Add(Note("Showing the first " + p.Preview.Count + " of " + p.Rows.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " rows. Conversion uses all of them.", new Thickness(0, 6, 0, 0)));
            sp.Children.Add(Note("Select a row and press Ctrl+C to copy it; click a cell first to copy just that cell.", new Thickness(0, 4, 0, 0)));
            _view.Content = sp;
        }

        // ------------------------------------------------------------------ convert

        private void BuildConvert()
        {
            var p = _probe;
            var sp = new StackPanel();
            var card = new Border { CornerRadius = new CornerRadius(20), Background = Ui.Res("B.Sunken"), Padding = new Thickness(16, 12, 16, 10), Margin = new Thickness(0, 0, 0, 8) };
            var inner = new StackPanel();
            inner.Children.Add(Label("Convert to", true));
            var targets = new[] { DataKind.Json, DataKind.Yaml, DataKind.Csv };
            var row = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
            _targetChoice = Segmented("Convert to", targets.Select(DataEngine.Name).ToArray(), Array.IndexOf(targets, _target), i => { _target = targets[i]; SyncTarget(true); });
            for (int i = 0; i < targets.Length; i++) _targetChoice.Enable(i, targets[i] != p.Format);
            row.Children.Add(_targetChoice.Element);
            _prettySwitch = Switch("Pretty print", "Indent 2 spaces", _pretty, on => { _pretty = on; });
            _prettySwitch.Margin = new Thickness(0, 0, 0, 8);
            row.Children.Add(_prettySwitch);
            inner.Children.Add(row);
            inner.Children.Add(Label("Save as", false, new Thickness(0, 0, 0, 6)));
            _saveAs = new TextBox { Style = Ui.Style("Input"), Height = 42, MaxLength = 200 };
            AutomationProperties.SetName(_saveAs, "File name to save as");
            inner.Children.Add(_saveAs);
            card.Child = inner; sp.Children.Add(card);
            _convertHost.Content = p.Valid ? sp : null;
            if (!p.Valid) return;
            if (_target == p.Format) { _target = targets.First(t => t != p.Format); _targetChoice.Select(Array.IndexOf(targets, _target)); }
            SyncTarget(true);
        }

        private void SyncTarget(bool resetName)
        {
            _prettySwitch.IsEnabled = _target == DataKind.Json;
            if (resetName || string.IsNullOrWhiteSpace(_saveAs.Text) || _saveAs.Text == _lastName)
            {
                _saveAs.Text = Path.GetFileNameWithoutExtension(_path) + DataEngine.Extension(_target);
                _lastName = _saveAs.Text;
            }
        }

        internal void Convert()
        {
            if (_probe == null || !_probe.Valid) return;
            var name = _saveAs.Text.Trim();
            if (name.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) { Status("Choose a valid file name.", true); return; }
            PrimaryButton.IsEnabled = false;
            var target = _target; var pretty = _pretty; var probe = _probe;
            var full = Path.Combine(Path.GetDirectoryName(_path), name);
            Status("Converting…");
            var stats = new ConvertStats();
            Task.Run(() => DataEngine.ConvertFile(probe, target, pretty, full, stats)).ContinueWith(t => Dispatcher.Invoke(() =>
            {
                if (t.IsFaulted)
                {
                    var ex = t.Exception.GetBaseException();
                    _banner.Set(DataBannerKind.Error, "Nothing was saved", ex.Message + " The original was not changed.");
                    Status("Nothing was saved.", true); PrimaryButton.IsEnabled = true;
                    _converted.TrySetResult(false);
                    return;
                }
                _written = t.Result; LastStats = stats;
                ShowDone(stats, probe, target);
                _converted.TrySetResult(true);
            }));
        }

        private TaskCompletionSource<bool> _converted = new TaskCompletionSource<bool>();
        internal Task WaitConverted() => _converted.Task;
        internal void ResetConverted() => _converted = new TaskCompletionSource<bool>();

        private void ShowDone(ConvertStats stats, DataProbe src, DataKind target)
        {
            long srcSize = src.Size;
            string what = src.Format == DataKind.Csv && target != DataKind.Csv
                ? Plural(stats.Items, "row", "rows") + " became " + Plural(stats.Items, "object", "objects") + " with the header names as keys."
                : target == DataKind.Csv ? Plural(stats.Items, "record", "records") + " became " + Plural(stats.Items, "row", "rows") + "."
                : "The " + DataEngine.Name(src.Format) + " content was written as " + DataEngine.Name(target) + ".";
            _banner.Set(DataBannerKind.Success, "Saved " + Path.GetFileName(_written), what);
            var rows = new List<(string, UIElement)> { ("Size", Value(Ui.Bytes(stats.Bytes) + " (" + DataEngine.Name(src.Format) + " was " + Ui.Bytes(srcSize) + ")")) };
            if (!string.IsNullOrEmpty(stats.Numbers)) rows.Add(("Numbers", Value(stats.Numbers)));
            if (src.Format != DataKind.Csv && target == DataKind.Yaml) rows.Add(("Comments", Value("YAML comments in the original are not carried over.")));
            var sp = new StackPanel();
            sp.Children.Add(Card(rows.ToArray()));
            var buttons = new WrapPanel();
            buttons.Children.Add(Action("Show in folder", DataToolIcons.Folder, () => ActionProvider.Reveal(_written)));
            buttons.Children.Add(Action("Copy to clipboard", DataToolIcons.Copy, CopyResult));
            sp.Children.Add(buttons);
            _doneHost.Content = sp;
            _convertHost.Content = null; _errorHost.Content = null;
            FooterSecondary.Children.Clear();
            SetCancel("Cancel", false);
            SetPrimary("Close", null, Close);
            Status("Saved.");
        }

        private void CopyResult()
        {
            try
            {
                var fi = new FileInfo(_written);
                if (fi.Length > 2 * 1024 * 1024) { Status("The result is larger than 2 MB, so it is not copied. It is saved as a file.", true); return; }
                ClipboardWatcher.SetText(File.ReadAllText(_written));
                Status("Copied the converted text.");
            }
            catch (Exception ex) { Status(ex.Message, true); }
        }

        // ------------------------------------------------------------------ format in place

        private void AskFormatInPlace()
        {
            if (_probe == null || !_probe.Valid) return;
            var body = "This file is rewritten tidied, and the original is first saved as a .bak copy next to it." + (_probe.Format == DataKind.Yaml ? " YAML comments are not kept." : "");
            var dlg = new ConfirmWindow("Format " + Path.GetFileName(_path) + " in place?", body, "Format");
            dlg.Confirmed += _ => FormatInPlaceNow();
            dlg.Show();
        }

        internal string FormatInPlaceNow()
        {
            try
            {
                var backup = DataEngine.FormatInPlace(_probe, _pretty);
                Status("Formatted. The original is saved as " + Path.GetFileName(backup) + ".");
                _banner.Set(DataBannerKind.Success, "Formatted in place", "The original is saved as " + Path.GetFileName(backup) + " next to the file.");
                Load();
                return backup;
            }
            catch (Exception ex) { _banner.Set(DataBannerKind.Error, "Nothing was changed", ex.Message); return null; }
        }

        protected override void OnClosedCore() { if (Current == this) Current = null; }
    }
}
