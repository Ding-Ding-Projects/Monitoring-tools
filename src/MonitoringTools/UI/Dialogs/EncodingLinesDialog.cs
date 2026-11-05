using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using MonitoringTools.Clip;
using MonitoringTools.Core;

namespace MonitoringTools.UI.Dialogs
{
    /// <summary>
    /// Encoding and line endings: detects the text encoding (UTF-8, UTF-8 with BOM, UTF-16, or a legacy code page that is said to be a guess) and the
    /// line endings (CRLF, LF, CR, mixed) of one or many files, and converts them with an optional .bak backup. A byte that is not valid in the
    /// detected encoding stops that file and nothing is written; binary files are never touched.
    /// </summary>
    internal sealed class EncodingLinesDialog : DataToolWindow
    {
        internal static EncodingLinesDialog Current { get; private set; }

        private sealed class Row
        {
            public TextInfo Info;
            public CheckBox Check;
            public Border Box;
            public int? Hand;
            public TextBlock Enc, Ends;
            public ContentControl Badge;
        }

        private readonly List<Row> _rows = new List<Row>();
        private readonly StackPanel _table = new StackPanel();
        private readonly BannerBox _banner = new BannerBox();
        private readonly ContentControl _problem = new ContentControl();
        private readonly TextBlock _previewLabel = Label("", false, new Thickness(0, 6, 0, 6));
        private readonly TextBlock _preview = new TextBlock { FontFamily = Ui.Font("F.Mono"), FontSize = 12.5, Foreground = Ui.Res("B.Text") };
        static EncodingLinesDialog() { }
        private readonly ContentControl _tableHost = new ContentControl();
        private readonly ContentControl _doneHost = new ContentControl();
        private readonly StackPanel _options = new StackPanel();
        private Choice _enc, _ends;
        private CheckBox _backup;
        private TargetEncoding _target = TargetEncoding.Utf8;
        private TargetEnds _targetEnds = TargetEnds.Lf;
        private bool _keepBackup = true;
        private Row _selected;
        private bool _showMixed;
        private List<TextConvertResult> _results = new List<TextConvertResult>();

        internal IReadOnlyList<TextInfo> Infos => _rows.Select(r => r.Info).ToList();
        internal BannerBox Banner => _banner;
        internal Choice EncodingChoice => _enc;
        internal Choice EndsChoice => _ends;
        internal CheckBox BackupSwitch => _backup;
        internal IReadOnlyList<TextConvertResult> Results => _results;
        internal string PreviewText => string.Concat(_preview.Inlines.OfType<Run>().Select(r => r.Text));
        internal void SelectRow(int i) { if (i >= 0 && i < _rows.Count) Select(_rows[i]); }
        internal void CheckRow(int i, bool on) { if (i >= 0 && i < _rows.Count) { _rows[i].Check.IsChecked = on; } }
        internal void ReadAs(int row, int codePage) { if (row >= 0 && row < _rows.Count) { _rows[row].Hand = codePage; Reanalyse(_rows[row]); } }
        internal void ShowMixed(bool on) { _showMixed = on; RenderPreview(); }

        public static EncodingLinesDialog Open(string[] files)
        {
            files = (files ?? Array.Empty<string>()).Where(File.Exists).Take(300).ToArray();
            if (files.Length == 0) { App.Toasts.ShowNotice("Nothing to convert", "The file is no longer there.", App.Toasts.Amber, 4, icon: Icons.Bolt); return null; }
            var d = new EncodingLinesDialog(files);
            Current = d; d.Present();
            return d;
        }

        private EncodingLinesDialog(string[] files) : base("Encoding and line endings", files.Length == 1 ? files[0] : null, 840, DataToolIcons.Lines)
        {
            if (files.Length > 1) { SetSummary(files.Length + " files"); }
            foreach (var f in files)
            {
                var info = TextEngine.Analyse(f);
                _rows.Add(NewRow(info));
            }
            var sum = files.Length == 1 ? Ui.Bytes(_rows[0].Info.Size) : Plural(_rows.Count(r => !r.Info.Binary), "text file", "text files") + (_rows.Any(r => r.Info.Binary) ? ", " + _rows.Count(r => r.Info.Binary) + " skipped" : "");
            SetSummary(sum);
            Body.Children.Add(_banner);
            Body.Children.Add(_problem);
            Body.Children.Add(_tableHost);
            _tableHost.Content = BuildTable();
            _options.Orientation = Orientation.Vertical;
            var optRow = new WrapPanel();
            var encBox = new StackPanel { Margin = new Thickness(0, 0, 24, 0) };
            encBox.Children.Add(Label("Text encoding", false, new Thickness(0, 2, 0, 6)));
            _enc = Segmented("Text encoding", new[] { "UTF-8", "UTF-8 with BOM", "UTF-16" }, 0, i => { _target = (TargetEncoding)i; Refresh(); });
            encBox.Children.Add(_enc.Element);
            var endBox = new StackPanel();
            endBox.Children.Add(Label("Line endings", false, new Thickness(0, 2, 0, 6)));
            _ends = Segmented("Line endings", new[] { "Keep", "LF", "CRLF" }, 1, i => { _targetEnds = (TargetEnds)i; Refresh(); });
            endBox.Children.Add(_ends.Element);
            optRow.Children.Add(encBox); optRow.Children.Add(endBox);
            Body.Children.Add(optRow);
            Body.Children.Add(_previewLabel);
            Core.Language.SetLiteral(_preview, true);
            Body.Children.Add(CodeBlock(_preview, 150));
            _backup = Switch("Keep a backup", "Original saved as a .bak file next to each file", true, on => { _keepBackup = on; });
            Body.Children.Add(_backup);
            Body.Children.Add(_doneHost);
            SetCancel("Cancel");
            SetPrimary("Convert", DataToolIcons.Check, ConvertNow, false);
            Select(_rows.FirstOrDefault(r => !r.Info.Binary) ?? _rows[0]);
            Refresh();
        }

        private static string Plural(long n, string one, string many) => n + " " + (n == 1 ? one : many);

        private Row NewRow(TextInfo info)
        {
            var r = new Row { Info = info };
            r.Check = new CheckBox { IsChecked = !info.Binary && info.DecodeError == null, IsEnabled = !info.Binary, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 10, 0) };
            AutomationProperties.SetName(r.Check, "Convert " + Path.GetFileName(info.Path));
            r.Check.Checked += (s, e) => Refresh(); r.Check.Unchecked += (s, e) => Refresh();
            r.Check.GotKeyboardFocus += (s, e) => Select(r);
            r.Enc = new TextBlock { FontSize = 13, Foreground = Ui.Res("B.Text"), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            r.Ends = new TextBlock { FontSize = 13, Foreground = Ui.Res("B.Text"), VerticalAlignment = VerticalAlignment.Center };
            r.Badge = new ContentControl { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
            return r;
        }

        private FrameworkElement BuildTable()
        {
            _table.Children.Clear();
            var head = new Grid { Margin = new Thickness(0, 0, 0, 2) };
            ColumnsFor(head);
            string[] names = { "", "File", "Encoding found", "Line ends", "Result" };
            for (int i = 1; i < names.Length; i++) { var t = new TextBlock { Text = names[i], FontSize = 12, Foreground = Ui.Res("B.TextSoft"), Margin = new Thickness(0, 6, 8, 6) }; Grid.SetColumn(t, i); head.Children.Add(t); }
            _table.Children.Add(head);
            foreach (var r in _rows)
            {
                var g = new Grid();
                ColumnsFor(g);
                var name = new TextBlock { Text = DataToolWindow.Middle(Path.GetFileName(r.Info.Path), 40), FontSize = 13, FontWeight = FontWeights.Medium, Foreground = Ui.Res("B.Text"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0), ToolTip = r.Info.Path };
                Core.Language.SetLiteral(name, true);
                Core.Language.SetLiteral(r.Info.Path == null ? name : name, true);
                Grid.SetColumn(r.Check, 0); Grid.SetColumn(name, 1); Grid.SetColumn(r.Enc, 2); Grid.SetColumn(r.Ends, 3); Grid.SetColumn(r.Badge, 4);
                g.Children.Add(r.Check); g.Children.Add(name); g.Children.Add(r.Enc); g.Children.Add(r.Ends); g.Children.Add(r.Badge);
                r.Box = new Border { Child = g, CornerRadius = new CornerRadius(10), Padding = new Thickness(0, 8, 6, 8), Background = Brushes.Transparent, Cursor = Cursors.Hand };
                var rr = r;
                r.Box.MouseLeftButtonDown += (s, e) => { if (!(e.OriginalSource is DependencyObject d && IsInside<CheckBox>(d))) Select(rr); };
                _table.Children.Add(r.Box);
            }
            return new Border { CornerRadius = new CornerRadius(16), Background = Ui.Res("B.Sunken"), Padding = new Thickness(8, 4, 8, 6), Margin = new Thickness(0, 0, 0, 12), Child = new ScrollViewer { Content = _table, MaxHeight = 190, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } };
        }

        private static bool IsInside<T>(DependencyObject d) where T : DependencyObject
        {
            while (d != null) { if (d is T) return true; d = d is Visual || d is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d); }
            return false;
        }

        private static void ColumnsFor(Grid g)
        {
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
        }

        private void Select(Row r)
        {
            _selected = r; _showMixed = false;
            foreach (var x in _rows) x.Box.Background = x == r ? Ui.Res("B.SecondaryContainer") : Brushes.Transparent;
            Refresh();
        }

        private void Reanalyse(Row r)
        {
            r.Info = TextEngine.Analyse(r.Info.Path, r.Hand);
            r.Check.IsChecked = !r.Info.Binary && r.Info.DecodeError == null;
            Refresh();
        }

        // ------------------------------------------------------------------ state

        private IEnumerable<Row> Chosen => _rows.Where(r => r.Check.IsChecked == true && !r.Info.Binary);

        private void Refresh()
        {
            if (_selected == null || _ends == null || _doneHost.Content != null) return;
            int willChange = 0, ok = 0;
            foreach (var r in _rows)
            {
                var i = r.Info;
                r.Enc.Text = i.Binary ? "Not text" : i.EncodingName + (i.HandPicked ? " (chosen)" : "");
                r.Ends.Text = i.Binary ? "" : i.DecodeError != null ? "?" : TextEngine.EndsName(i.Ends);
                if (i.Binary) r.Badge.Content = Badge("Skipped, not text", false, true);
                else if (i.DecodeError != null) r.Badge.Content = Badge("Cannot decode", false);
                else if (!TextEngine.WouldChange(i, _target, _targetEnds)) r.Badge.Content = Badge("Already ok", true);
                else if (i.Ends == LineEnds.Mixed) r.Badge.Content = Badge("Mixed ends", false, true);
                else r.Badge.Content = Tag("Will change");
                if (r.Check.IsChecked == true && !i.Binary && i.DecodeError == null && TextEngine.WouldChange(i, _target, _targetEnds)) willChange++;
                else if (!i.Binary && i.DecodeError == null && !TextEngine.WouldChange(i, _target, _targetEnds)) ok++;
            }
            _tableHost.Visibility = _rows.Count == 1 ? Visibility.Collapsed : Visibility.Visible;
            ShowProblem();
            RenderPreview();
            PrimaryButton.IsEnabled = willChange > 0;
            SetPrimary(willChange == 1 ? "Convert 1 file" : "Convert " + willChange + " files", DataToolIcons.Check, ConvertNow, willChange > 0);
            Status(willChange == 0 ? (ok == _rows.Count(r => !r.Info.Binary) ? "Nothing to change: every file already matches." : "Nothing is ticked that would change.") : willChange + " of " + _rows.Count + (_rows.Count == 1 ? " file" : " files") + " will change", false);
        }

        private static Border Tag(string text) => new Border { CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 1, 8, 2), Background = Ui.Res("B.TertiaryContainer"), Child = new TextBlock { Text = text, FontSize = 11.5, Foreground = Ui.Res("B.OnTertiaryContainer") } };

        private void ShowProblem()
        {
            var i = _selected.Info;
            _problem.Content = null;
            if (i.Binary)
            {
                _banner.Set(DataBannerKind.Warning, "This is not a text file", i.BinaryReason + " It is skipped.");
            }
            else if (i.DecodeError != null)
            {
                var enc = i.EncodingName.Replace(" (guess)", "");
                _banner.Set(DataBannerKind.Error, "Could not convert without losing characters",
                    "Byte 0x" + i.DecodeErrorByte.ToString("X2") + " at position " + i.DecodeErrorPosition.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " is not valid in " + enc + ". The file is probably a different encoding, such as Shift-JIS.");
                var sp = new StackPanel();
                var card = Card(("What was not changed", Value("Nothing was written. Choose the source encoding by hand and try again.")));
                sp.Children.Add(card);
                var row = new WrapPanel();
                var sj = Action("Read it as Shift-JIS", null, () => { _selected.Hand = 932; Reanalyse(_selected); }, "Btn.Accent");
                var choose = Action("Choose encoding", DataToolIcons.Chevron, null);
                choose.Click += (s, e) => ChooseEncodingMenu(choose);
                row.Children.Add(sj); row.Children.Add(choose);
                sp.Children.Add(row);
                _problem.Content = sp;
            }
            else if (i.Ends == LineEnds.Mixed)
            {
                int total = i.Crlf + i.Lf + i.Cr;
                string parts = string.Join(" and ", new[] { i.Lf > 0 ? i.Lf + " with LF" : null, i.Crlf > 0 ? i.Crlf + " with CRLF" : null, i.Cr > 0 ? i.Cr + " with CR" : null }.Where(x => x != null));
                _banner.Set(DataBannerKind.Warning, "This file mixes line endings", total + " lines end differently: " + parts + ". " + (_targetEnds == TargetEnds.Keep ? "Choosing LF or CRLF makes them all the same." : "Converting to " + (_targetEnds == TargetEnds.Lf ? "LF" : "CRLF") + " makes them all the same, which shell scripts need."));
                var row = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
                row.Children.Add(Action(_showMixed ? "Show the first lines" : "Show mixed lines", DataToolIcons.Eye, () => { _showMixed = !_showMixed; ShowProblem(); RenderPreview(); }));
                _problem.Content = row;
            }
            else if (i.Guess)
            {
                _banner.Set(DataBannerKind.Info, "The encoding is a guess", "No byte-order mark was found and the bytes are not valid UTF-8, so this uses the Windows code page. If characters look wrong in the preview, choose the encoding by hand.");
                var row = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
                var choose = Action("Choose encoding", DataToolIcons.Chevron, null);
                choose.Click += (s, e) => ChooseEncodingMenu(choose);
                row.Children.Add(choose);
                _problem.Content = row;
            }
            else _banner.Visibility = Visibility.Collapsed;
        }

        private void ChooseEncodingMenu(Button anchor)
        {
            var menu = new ContextMenu { PlacementTarget = anchor, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
            foreach (var (label, cp) in TextEngine.HandChoices)
            {
                var c = cp; var item = new MenuItem { Header = label };
                item.Click += (s, e) => { _selected.Hand = c; Reanalyse(_selected); };
                menu.Items.Add(item);
            }
            menu.IsOpen = true;
        }

        // ------------------------------------------------------------------ preview

        private void RenderPreview()
        {
            _preview.Inlines.Clear();
            var i = _selected?.Info;
            if (i == null || i.Binary || i.DecodeError != null)
            {
                _previewLabel.Text = i == null ? "" : "No preview: " + (i.Binary ? "this is not a text file." : "the file cannot be decoded yet.");
                return;
            }
            string text;
            try { text = TextEngine.Decode(i); } catch { text = null; }
            if (text == null) return;
            var name = Path.GetFileName(i.Path);
            _previewLabel.Text = _showMixed ? name + ", lines whose ending differs from most (visible marks on)" : name + ", first lines after the change (visible marks on)";
            // split into (line, ending) pairs
            var lines = new List<(string Text, string End)>();
            var sb = new StringBuilder();
            for (int k = 0; k < text.Length && lines.Count < 4000; k++)
            {
                char c = text[k];
                if (c == '\r') { var end = k + 1 < text.Length && text[k + 1] == '\n' ? "CRLF" : "CR"; if (end == "CRLF") k++; lines.Add((sb.ToString(), end)); sb.Clear(); }
                else if (c == '\n') { lines.Add((sb.ToString(), "LF")); sb.Clear(); }
                else sb.Append(c);
            }
            if (sb.Length > 0 || lines.Count == 0) lines.Add((sb.ToString(), ""));
            IEnumerable<(string Text, string End)> pick;
            if (_showMixed)
            {
                var major = lines.Where(l => l.End != "").GroupBy(l => l.End).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();
                pick = lines.Where(l => l.End != "" && l.End != major).Take(12);
            }
            else pick = lines.Take(12);
            foreach (var (t, end0) in pick)
            {
                var end = _showMixed ? end0 : _targetEnds == TargetEnds.Keep ? end0 : end0 == "" ? "" : _targetEnds == TargetEnds.Lf ? "LF" : "CRLF";
                var shown = t.Length > 110 ? t[..110] + "…" : t;
                var seg = new StringBuilder();
                foreach (var ch in shown)
                {
                    if (ch > 0x7F || (ch < 32 && ch != '\t'))
                    {
                        if (seg.Length > 0) { _preview.Inlines.Add(new Run(seg.ToString())); seg.Clear(); }
                        _preview.Inlines.Add(new Run(ch < 32 ? "␀" : ch.ToString()) { Background = Ui.Res("B.MintWash"), Foreground = Ui.Res("B.OnPrimaryContainer") });
                    }
                    else seg.Append(ch);
                }
                if (seg.Length > 0) _preview.Inlines.Add(new Run(seg.ToString()));
                if (end != "") _preview.Inlines.Add(new Run(" " + end + " ") { FontSize = 9, Background = Ui.Res("B.AmberWash"), Foreground = Ui.Res("B.Amber") });
                _preview.Inlines.Add(new LineBreak());
            }
            if (!_showMixed && !_preview.Inlines.Any()) _preview.Inlines.Add(new Run("(empty file)"));
            if (!_showMixed && lines.Count > 12) _preview.Inlines.Add(new Run("…") { Foreground = Ui.Res("B.TextMute") });
        }

        // ------------------------------------------------------------------ convert

        internal void ConvertNow()
        {
            var todo = Chosen.Where(r => r.Info.DecodeError == null && TextEngine.WouldChange(r.Info, _target, _targetEnds)).ToList();
            if (todo.Count == 0) return;
            PrimaryButton.IsEnabled = false;
            Status("Converting…");
            var enc = _target; var ends = _targetEnds; var backup = _keepBackup;
            var all = _rows.Select(r => r.Info).ToList();
            Task.Run(() => todo.Select(r => TextEngine.Convert(r.Info, enc, ends, backup)).ToList()).ContinueWith(t => Dispatcher.Invoke(() =>
            {
                _results = t.IsFaulted ? new List<TextConvertResult>() : t.Result;
                ShowDone(all, enc, ends, backup);
                _converted.TrySetResult(true);
            }));
        }

        internal Task WaitConverted() => _converted.Task;
        private readonly TaskCompletionSource<bool> _converted = new TaskCompletionSource<bool>();

        private void ShowDone(List<TextInfo> all, TargetEncoding enc, TargetEnds ends, bool backup)
        {
            int ok = _results.Count(r => r.Changed), failed = _results.Count(r => r.Failed);
            _problem.Content = null;
            if (failed == 0) _banner.Set(DataBannerKind.Success, "Converted " + Plural(ok, "file", "files"), backup ? "Backups were saved as .bak next to each file." : "No backups were kept, as chosen.");
            else _banner.Set(DataBannerKind.Error, ok + " converted, " + failed + " stopped", "A file that stopped was not changed. The details are below.");
            var rows = new List<(string, UIElement)>();
            foreach (var info in all)
            {
                var res = _results.FirstOrDefault(r => r.Path == info.Path);
                rows.Add((Path.GetFileName(info.Path), Value(res != null ? res.Message : info.Binary ? "Skipped, not text" : !TextEngine.WouldChange(info, enc, ends) ? "Skipped, already matches" : "Not ticked, left as it is")));
            }
            var sp = new StackPanel();
            sp.Children.Add(Card(rows.ToArray()));
            var btns = new WrapPanel();
            btns.Children.Add(Action("Show in folder", DataToolIcons.Folder, () => ActionProvider.Reveal(_results.FirstOrDefault()?.Path ?? all[0].Path)));
            if (_results.Any(r => r.Backup != null)) btns.Children.Add(Action("Undo from backups", DataToolIcons.Refresh, () => UndoBackups()));
            sp.Children.Add(btns);
            _doneHost.Content = sp;
            _tableHost.Visibility = Visibility.Collapsed;
            _preview.Inlines.Clear(); _previewLabel.Text = "";
            Body.Children.Remove(_options);
            SetCancel("Cancel", false);
            SetPrimary("Close", null, Close);
            Status("Done.");
        }

        internal int UndoBackups()
        {
            int n = 0;
            foreach (var r in _results.Where(x => x.Backup != null && File.Exists(x.Backup)))
                if (TextEngine.Restore(r.Path, r.Backup)) { n++; r.Backup = null; }
            _banner.Set(DataBannerKind.Info, "Restored " + Plural(n, "file", "files") + " from backups", "The converted copies were replaced by the originals and the .bak files were removed.");
            Status("Restored.");
            return n;
        }

        protected override void OnClosedCore() { if (Current == this) Current = null; }
    }
}
