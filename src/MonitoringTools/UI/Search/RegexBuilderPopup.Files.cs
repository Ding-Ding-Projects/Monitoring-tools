using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using MonitoringTools.Core;

namespace MonitoringTools.UI.Search
{
    // The Files view (search and replace across files) and the expected-outcome test cases of the Test view.
    public sealed partial class RegexBuilderPopup
    {
        // ---- Files view state; kept while the builder is open so switching views does not lose the folder or the results
        private string _gFolder = "", _gMasks = "*", _gExclude = ".git;node_modules", _gMaxMb = "5", _gNameRx = "", _gPresetName = "";
        private StackPanel _gPresets, _gBackups, _gHistory;
        private bool _gSub = true, _gBinary = true, _gArchives = false;
        private string _gBinMode = "Bytes";   // how a binary file is searched when _gBinary (skip) is off: Bytes or Text
        private RegexGrep.Report _gReport;
        private Regex _gRx;
        private string _gQuery, _gRepl;
        private RegexOptions _gOpts;
        private bool _gPreviewing;
        private List<string> _gOutcome;
        private CancellationTokenSource _gCts;
        private bool _gBusy;
        private StackPanel _gResults;
        private TextBlock _gStatus;
        private Button _gSearchBtn, _gCancelBtn, _gReplaceBtn, _gExportBtn, _gRestoreBtn;
        private const int MaxRowsShown = 1000;

        private void StopFiles()
        {
            try { _gCts?.Cancel(); } catch (ObjectDisposedException) { }
        }

        private FrameworkElement FilesView()
        {
            var sp = new StackPanel();
            sp.Children.Add(Eyebrow("Search files"));
            sp.Children.Add(Note("Searches the text of every matching file in a folder with the pattern above. Searching never changes a file. Replacing is a separate step with a preview, a deliberate confirmation and a backup of every file first. Matches are found on the whole file, so a pattern can span lines."));

            sp.Children.Add(Eyebrow("Folder"));
            var folder = new TextBox { Style = Ui.Style("Input"), FontFamily = Ui.Font("F.Mono"), Text = _gFolder, Margin = new Thickness(0, 2, 0, 6) };
            AutomationProperties.SetName(folder, "Folder to search");
            folder.TextChanged += (o, e) => _gFolder = folder.Text;
            var browse = Ui.Button("Browse", () =>
            {
                var d = new Microsoft.Win32.OpenFolderDialog { Title = "Choose the folder to search" };
                if (Directory.Exists(_gFolder)) d.InitialDirectory = _gFolder;
                if (d.ShowDialog() == true) folder.Text = d.FolderName;
            }, "Btn.Ghost", Icons.Folder);
            browse.Margin = new Thickness(8, 0, 0, 6);
            var frow = new DockPanel();
            DockPanel.SetDock(browse, Dock.Right); frow.Children.Add(browse); frow.Children.Add(folder);
            sp.Children.Add(frow);

            var masks = new Grid();
            masks.ColumnDefinitions.Add(new ColumnDefinition()); masks.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) }); masks.ColumnDefinitions.Add(new ColumnDefinition());
            var l = new StackPanel(); l.Children.Add(Eyebrow("File masks (default *)"));
            var m = new TextBox { Style = Ui.Style("Input"), FontFamily = Ui.Font("F.Mono"), Text = _gMasks, ToolTip = "File name masks separated by semicolons, for example *.txt;*.md. * and ? are wildcards." };
            AutomationProperties.SetName(m, "File masks to include, separated by semicolons");
            m.TextChanged += (o, e) => _gMasks = m.Text;
            l.Children.Add(m);
            var r = new StackPanel(); r.Children.Add(Eyebrow("Exclude masks (default .git;node_modules)"));
            var x = new TextBox { Style = Ui.Style("Input"), FontFamily = Ui.Font("F.Mono"), Text = _gExclude, ToolTip = "File or folder names to leave out, separated by semicolons." };
            AutomationProperties.SetName(x, "File and folder masks to exclude, separated by semicolons");
            x.TextChanged += (o, e) => _gExclude = x.Text;
            r.Children.Add(x);
            masks.Children.Add(l); Grid.SetColumn(r, 2); masks.Children.Add(r);
            sp.Children.Add(masks);

            sp.Children.Add(Eyebrow("File name pattern (optional, a regular expression, ignores case)"));
            var nameRx = new TextBox { Style = Ui.Style("Input"), FontFamily = Ui.Font("F.Mono"), Text = _gNameRx, Margin = new Thickness(0, 2, 0, 6), ToolTip = "Only files whose name matches this pattern are searched, on top of the masks. Example: ^report-\\d{4}\\.txt$" };
            AutomationProperties.SetName(nameRx, "Regular expression for file names");
            nameRx.TextChanged += (o, e) => _gNameRx = nameRx.Text;
            sp.Children.Add(nameRx);

            var opts = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            Button sub = null, bin = null, bytes = null, astext = null, arch = null;
            void paintBinary() { Paint(bin, _gBinary); Paint(bytes, !_gBinary && _gBinMode == "Bytes"); Paint(astext, !_gBinary && _gBinMode == "Text"); }
            sub = Chip("Include subfolders", "Search inside folders below the chosen one. On by default.", () => { _gSub = !_gSub; Paint(sub, _gSub); });
            bin = Chip("Skip binary files", "Skip a file with a null byte in its first 8,000 bytes. On by default.", () => { _gBinary = true; paintBinary(); });
            bytes = Chip("Binary files as bytes", "Search a binary file one byte per character: \\x00 to \\xFF in the pattern are byte values, and each match shows its offset and hexadecimal", () => { _gBinary = false; _gBinMode = "Bytes"; paintBinary(); });
            astext = Chip("Binary files as text", "Read a binary file like any text file (UTF-8 when it is valid, otherwise Latin-1)", () => { _gBinary = false; _gBinMode = "Text"; paintBinary(); });
            arch = Chip("Search inside archives", "Open .zip, .jar and .nupkg files, and .7z, .rar, .tar, .gz, .tgz, .bz2, .xz and .cab files through the bundled 7-Zip, and search their entries. The masks and the name pattern then apply to the entries. Off by default.", () => { _gArchives = !_gArchives; Paint(arch, _gArchives); });
            Paint(sub, _gSub); paintBinary(); Paint(arch, _gArchives);
            opts.Children.Add(sub); opts.Children.Add(arch); opts.Children.Add(bin); opts.Children.Add(bytes); opts.Children.Add(astext);
            var capLabel = Ui.Mono("Skip files over (MB)", Ui.Res("B.TextMute"), 11);
            capLabel.VerticalAlignment = VerticalAlignment.Center; capLabel.Margin = new Thickness(8, 0, 6, 6);
            var cap = new TextBox { Style = Ui.Style("Input"), Width = 56, Text = _gMaxMb, Margin = new Thickness(0, 0, 0, 6), ToolTip = "Files larger than this are skipped and listed. Default 5." };
            AutomationProperties.SetName(cap, "Skip files larger than this many megabytes");
            cap.TextChanged += (o, e) => _gMaxMb = cap.Text;
            opts.Children.Add(capLabel); opts.Children.Add(cap);
            sp.Children.Add(opts);
            sp.Children.Add(FileTypesPanel());

            sp.Children.Add(Eyebrow("Replacement (used by Replace in files)"));
            var repl = new TextBox { Style = Ui.Style("Input"), FontFamily = Ui.Font("F.Mono"), Text = _s.Replacement, Margin = new Thickness(0, 2, 0, 6), ToolTip = "Same syntax as the Replace view: $1, ${name}, $0 and $$." };
            AutomationProperties.SetName(repl, "Replacement text for files");
            repl.TextChanged += (o, e) => _s.Replacement = repl.Text;
            sp.Children.Add(repl);

            var row = new WrapPanel();
            _gSearchBtn = Ui.Button("Search files", StartFilesSearch, "Btn.Accent", Icons.Search);
            _gSearchBtn.ToolTip = "Search the folder. Nothing is changed.";
            _gCancelBtn = Ui.Button("Cancel", StopFiles, "Btn.Ghost");
            _gCancelBtn.ToolTip = "Stop the running search";
            _gReplaceBtn = Ui.Button("Replace in files...", StartReplacePreview, "Btn.Ghost");
            _gReplaceBtn.ToolTip = "Preview the replacement in every file with matches. Nothing is written yet.";
            _gExportBtn = Ui.Button("Export CSV", ExportCsv, "Btn.Ghost");
            _gExportBtn.ToolTip = "Save every match as a CSV file";
            _gRestoreBtn = Ui.Button("Restore last backup...", StartRestore, "Btn.Ghost");
            _gRestoreBtn.ToolTip = "Put the files from the most recent replace run back as they were";
            row.Children.Add(_gSearchBtn); row.Children.Add(_gCancelBtn); row.Children.Add(_gReplaceBtn); row.Children.Add(_gExportBtn); row.Children.Add(_gRestoreBtn);
            sp.Children.Add(row);
            sp.Children.Add(FilesExtras());

            _gStatus = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = Ui.Res("B.TextMute"), FontFamily = Ui.Font("F.Body"), Margin = new Thickness(0, 0, 0, 4) };
            AutomationProperties.SetLiveSetting(_gStatus, AutomationLiveSetting.Polite);
            sp.Children.Add(_gStatus);
            _gResults = new StackPanel();
            sp.Children.Add(new Border
            {
                Background = Ui.Res("B.Sunken"), BorderBrush = Ui.Res("B.Line"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 8, 10, 8),
                Child = new ScrollViewer { Content = _gResults, MaxHeight = 280, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }
            });
            UpdateFilesButtons();
            RenderFiles();
            return sp;
        }

        private void UpdateFilesButtons()
        {
            if (_gSearchBtn == null) return;
            _gSearchBtn.IsEnabled = !_gBusy;
            _gCancelBtn.IsEnabled = _gBusy;
            bool hits = _gReport != null && _gReport.Files.Count > 0;
            _gReplaceBtn.IsEnabled = !_gBusy && hits;
            _gExportBtn.IsEnabled = !_gBusy && hits;
            _gRestoreBtn.IsEnabled = !_gBusy && RegexGrep.LastBackup() != null;
        }

        private void FilesStale()
        {
            if (_gStatus == null || _gBusy) return;
            if (_gReport != null && (_gQuery != _s.Query || _gOpts != _s.EngineOptions))
            {
                _gStatus.Text = "The pattern or options changed since this search. Search again before replacing.";
                _gStatus.Foreground = Ui.Res("B.Amber");
            }
        }

        private void StartFilesSearch()
        {
            var rx = _s.Regex;
            if (rx == null) { _gStatus.Text = _s.Error != null ? "Fix the pattern first: " + _s.Error : "Type a pattern first."; _gStatus.Foreground = Ui.Res("B.Amber"); return; }
            if (!Directory.Exists(_gFolder)) { _gStatus.Text = "Choose a folder that exists."; _gStatus.Foreground = Ui.Res("B.Amber"); return; }
            if (!double.TryParse(_gMaxMb, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var mb) || mb <= 0) mb = 5;
            var o = new RegexGrep.Options
            {
                Folder = _gFolder, Masks = _gMasks, ExcludeMasks = _gExclude, NameRegex = _gNameRx, Subfolders = _gSub, SkipBinary = _gBinary, BinaryMode = _gBinMode,
                SearchArchives = _gArchives, FileTypes = (Settings.Current.RegexFileTypes ?? new List<RegexFileType>()).ToList(), MaxBytes = (long)(mb * 1024 * 1024)
            };
            StopFiles();
            var cts = _gCts = new CancellationTokenSource();
            var ct = cts.Token;
            _gBusy = true; _gPreviewing = false; _gOutcome = null; _gReport = null;
            _gQuery = _s.Query; _gOpts = _s.EngineOptions;
            _gStatus.Text = "Searching..."; _gStatus.Foreground = Ui.Res("B.TextMute");
            _gResults.Children.Clear();
            UpdateFilesButtons();
            var disp = _card.Dispatcher;
            Task.Run(() => RegexGrep.Search(rx, o, ct, msg => disp.BeginInvoke(new Action(() => { if (_gCts == cts && _gBusy && _gStatus != null) _gStatus.Text = msg; }))), ct)
                .ContinueWith(t => disp.BeginInvoke(new Action(() =>
                {
                    if (_gCts != cts) return;
                    _gBusy = false;
                    if (t.IsFaulted || t.IsCanceled) { if (_gStatus != null) { _gStatus.Text = "The search could not finish."; _gStatus.Foreground = Ui.Res("B.Amber"); } UpdateFilesButtons(); return; }
                    _gReport = t.Result; _gRx = rx; _gRepl = _s.Replacement ?? "";
                    var rep = _gReport;
                    if (_gStatus != null)
                    {
                        _gStatus.Foreground = Ui.Res("B.TextSoft");
                        _gStatus.Text = (rep.Cancelled ? "Cancelled. Partial results: " : "") +
                            $"{rep.TotalMatches:N0} match(es) in {rep.FilesWithHits} of {rep.FilesScanned} file(s) scanned" +
                            (rep.ArchivesOpened > 0 ? $" · {rep.ArchivesOpened} archive(s) opened" : "") +
                            (rep.Skipped.Count > 0 ? $" · {rep.Skipped.Count} skipped" : "") +
                            (rep.Capped ? " · capped, narrow the pattern or masks to see everything" : "") + ".";
                    }
                    RecordGrepHistory(rep);
                    UpdateFilesButtons();
                    RenderFiles();
                })));
        }

        private void RenderFiles()
        {
            if (_gResults == null) return;
            _gResults.Children.Clear();
            if (_gOutcome != null)
            {
                foreach (var line in _gOutcome) _gResults.Children.Add(Ui.Mono(line, Ui.Res("B.TextSoft"), 12));
                return;
            }
            if (_gReport == null) { _gResults.Children.Add(Ui.Mono("No search yet. Choose a folder and press Search files.", Ui.Res("B.TextMute"), 12)); return; }
            if (_gPreviewing) { RenderPreview(); return; }
            if (_gReport.Files.Count == 0) _gResults.Children.Add(Ui.Mono("No matches.", Ui.Res("B.TextMute"), 12));
            int shown = 0;
            foreach (var f in _gReport.Files)
            {
                var path = f.Path;
                var file = f;
                var head = Ui.Button($"{Rel(path)}  ·  {f.Count} match(es)  ·  {f.Encoding}", () => { if (file.Archive != null) TellInsideArchive(); else ExternalEditor.OpenAndTell(path); }, "Btn.Row");
                head.HorizontalContentAlignment = HorizontalAlignment.Left; head.FontFamily = Ui.Font("F.Mono"); head.FontSize = 12; head.Margin = new Thickness(0, 4, 0, 0);
                head.ToolTip = f.Archive != null ? "This file is inside an archive. Unpack the archive to open it." : "Open this file in your editor";
                Core.Language.SetLiteral(head, true);
                _gResults.Children.Add(head);
                foreach (var h in f.Hits)
                {
                    if (shown++ >= MaxRowsShown) break;
                    _gResults.Children.Add(h.Offset >= 0 ? BinaryHitRow(f, h) : HitRow(f, h));
                }
                if (shown >= MaxRowsShown) break;
            }
            int total = _gReport.Files.Sum(f => f.Hits.Count);
            if (total > MaxRowsShown) _gResults.Children.Add(Ui.Mono($"Showing the first {MaxRowsShown:N0} of {total:N0} matches. Export CSV lists all of them.", Ui.Res("B.Amber"), 11));
            if (_gReport.Skipped.Count > 0)
            {
                _gResults.Children.Add(Eyebrow($"Skipped · {_gReport.Skipped.Count}"));
                foreach (var s in _gReport.Skipped.Take(200)) _gResults.Children.Add(Ui.Mono(Rel(s.Path) + " : " + s.Reason, Ui.Res("B.TextMute"), 11));
                if (_gReport.Skipped.Count > 200) _gResults.Children.Add(Ui.Mono($"and {_gReport.Skipped.Count - 200} more.", Ui.Res("B.TextMute"), 11));
            }
        }

        private string Rel(string path)
        {
            try { return _gFolder.Length > 0 && path.StartsWith(_gFolder, StringComparison.OrdinalIgnoreCase) ? path[_gFolder.Length..].TrimStart('\\', '/') : path; }
            catch { return path; }
        }

        private void TellInsideArchive()
        {
            if (_gStatus == null) return;
            _gStatus.Text = "That match is inside an archive. Unpack the archive to open the file; a replace never writes into archives.";
            _gStatus.Foreground = Ui.Res("B.TextSoft");
        }

        /// <summary>A match in a file searched as bytes: its offset, the printable bytes around it and their hexadecimal, with the match highlighted in both.</summary>
        private FrameworkElement BinaryHitRow(RegexGrep.FileResult f, RegexGrep.Hit h)
        {
            var sp = new StackPanel { Margin = new Thickness(14, 2, 0, 2) };
            var ascii = new TextBlock { FontFamily = Ui.Font("F.Mono"), FontSize = 12, Foreground = Ui.Res("B.TextSoft") };
            ascii.Inlines.Add(new System.Windows.Documents.Run($"0x{h.Offset:X8}  ") { Foreground = Ui.Res("B.TextMute") });
            var t = h.LineText ?? "";
            int start = Math.Clamp(h.Column - 1, 0, t.Length), len = Math.Clamp(h.Length, 0, t.Length - start);
            ascii.Inlines.Add(new System.Windows.Documents.Run(t[..start]));
            ascii.Inlines.Add(new System.Windows.Documents.Run(t.Substring(start, len)) { Background = Ui.Res("B.MintWash"), Foreground = Ui.Res("B.OnPrimaryContainer") });
            ascii.Inlines.Add(new System.Windows.Documents.Run(t[(start + len)..]));
            Core.Language.SetLiteral(ascii, true);
            var hex = new TextBlock { FontFamily = Ui.Font("F.Mono"), FontSize = 11, Foreground = Ui.Res("B.TextMute"), TextWrapping = TextWrapping.Wrap };
            var hx = h.Hex ?? "";
            int hs = Math.Clamp(h.HexStart, 0, hx.Length), hl = Math.Clamp(h.HexLength, 0, hx.Length - hs);
            hex.Inlines.Add(new System.Windows.Documents.Run("            " + hx[..hs]));
            hex.Inlines.Add(new System.Windows.Documents.Run(hx.Substring(hs, hl)) { Background = Ui.Res("B.MintWash"), Foreground = Ui.Res("B.OnPrimaryContainer") });
            hex.Inlines.Add(new System.Windows.Documents.Run(hx[(hs + hl)..]));
            Core.Language.SetLiteral(hex, true);
            sp.Children.Add(ascii); sp.Children.Add(hex);
            AutomationProperties.SetName(sp, $"Byte offset {h.Offset} in {Path.GetFileName(f.Path)}: {h.Value}");
            return sp;
        }

        private FrameworkElement HitRow(RegexGrep.FileResult f, RegexGrep.Hit h)
        {
            var path = f.Path;
            var tb = new TextBlock { FontFamily = Ui.Font("F.Mono"), FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = Ui.Res("B.TextSoft") };
            tb.Inlines.Add(new System.Windows.Documents.Run($"{h.Line,5}:{h.Column,-4} ") { Foreground = Ui.Res("B.TextMute") });
            var t = h.LineText ?? "";
            int start = Math.Clamp(h.Column - 1, 0, t.Length);
            int len = Math.Clamp(h.Length, 0, t.Length - start);
            tb.Inlines.Add(new System.Windows.Documents.Run(t[..start]));
            tb.Inlines.Add(new System.Windows.Documents.Run(t.Substring(start, len)) { Background = Ui.Res("B.MintWash"), Foreground = Ui.Res("B.OnPrimaryContainer") });
            tb.Inlines.Add(new System.Windows.Documents.Run(t[(start + len)..]));
            var b = new Button { Content = tb, Style = Ui.Style("Btn.Row"), HorizontalContentAlignment = HorizontalAlignment.Left, Margin = new Thickness(14, 0, 0, 0), ToolTip = "Open the file in your editor" };
            AutomationProperties.SetName(b, $"Line {h.Line}, column {h.Column} in {Path.GetFileName(path)}: {h.Value}");
            b.Click += (o, e) => { if (f.Archive != null) TellInsideArchive(); else OpenHit(path, h); };
            b.ToolTip = f.Archive != null ? "This file is inside an archive. Unpack the archive to open it." : "Open the file in your editor at this line when the editor can jump to a line";
            return b;
        }

        // ---------------------------------------------------------------- export

        private void ExportCsv()
        {
            if (_gReport == null || _gReport.Files.Count == 0) return;
            var d = new Microsoft.Win32.SaveFileDialog { FileName = "regex-file-matches.csv", Filter = "CSV|*.csv" };
            if (d.ShowDialog() != true) return;
            try
            {
                File.WriteAllText(d.FileName, RegexGrep.ToCsv(_gReport), new UTF8Encoding(true));
                _gStatus.Text = "Saved " + _gReport.TotalMatches.ToString("N0") + " match row(s) to " + d.FileName + (_gReport.Capped ? ". The list was capped, so it is not every match." : ".");
                _gStatus.Foreground = Ui.Res("B.TextSoft");
            }
            catch (Exception ex) { _gStatus.Text = "Could not save the CSV: " + ex.Message; _gStatus.Foreground = Ui.Res("B.Amber"); }
        }

        // ---------------------------------------------------------------- replace in files

        private void StartReplacePreview()
        {
            if (_gReport == null || _gReport.Files.Count == 0) return;
            if (_gQuery != _s.Query || _gOpts != _s.EngineOptions) { FilesStale(); return; }
            _gRepl = _s.Replacement ?? "";
            try { _gRx.Replace("", _gRepl); }
            catch (Exception ex) when (ex is ArgumentException || ex is RegexParseException) { _gStatus.Text = "The replacement text is not valid: " + ex.Message; _gStatus.Foreground = Ui.Res("B.Amber"); return; }
            _gPreviewing = true;
            RenderFiles();
        }

        private void RenderPreview()
        {
            var plan = RegexGrep.Plan(_gReport);
            int total = plan.Sum(p => p.Count);
            _gResults.Children.Add(Eyebrow("Preview. Nothing has been written."));
            _gResults.Children.Add(Note($"{total:N0} replacement(s) in {plan.Count} file(s) with the text \"{(_gRepl.Length > 60 ? _gRepl[..60] + "..." : _gRepl)}\". Each file is copied to a backup folder before it is changed. A file that changes after this preview is skipped."));
            var left = RegexGrep.NotReplaceable(_gReport);
            if (left > 0) _gResults.Children.Add(Ui.Mono($"{left} file(s) with matches inside archives or searched as bytes are not changed: a replace only writes ordinary text files.", Ui.Res("B.Amber"), 11));
            foreach (var p in plan) _gResults.Children.Add(Ui.Mono($"{p.Count,6}  {Rel(p.Path)}", Ui.Res("B.TextSoft"), 12));
            if (_gReport.Capped) _gResults.Children.Add(Ui.Mono("The search was capped, so more files or matches may exist than are listed. Only the files listed here are changed.", Ui.Res("B.Amber"), 11));
            var row = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            row.Children.Add(Ui.Button("Write changes...", ConfirmWrite, "Btn.Danger", Icons.Trash));
            row.Children.Add(Ui.Button("Back to results", () => { _gPreviewing = false; RenderFiles(); }, "Btn.Ghost"));
            _gResults.Children.Add(row);
        }

        private void ConfirmWrite()
        {
            if (_gReport == null) return;
            var rx = _gRx; var repl = _gRepl; var plan = RegexGrep.Plan(_gReport);
            int total = plan.Sum(p => p.Count);
            var dlg = new ConfirmWindow($"Change {plan.Count} file(s)?",
                $"This replaces {total:N0} match(es) on disk. A copy of each file goes to a timestamped backup folder first, and you can restore the last run afterwards. Files that changed since the preview are skipped.",
                "Write changes", danger: true);
            dlg.Confirmed += _ => RunReplace(rx, repl, plan);
            dlg.Show();
        }

        private void RunReplace(Regex rx, string repl, List<RegexGrep.ReplacePlanItem> plan)
        {
            var cts = new CancellationTokenSource();
            _gBusy = true; _gPreviewing = false; _gReport = null;
            if (_gStatus != null) { _gStatus.Text = "Writing changes..."; _gStatus.Foreground = Ui.Res("B.TextMute"); }
            UpdateFilesButtons();
            var disp = Application.Current.Dispatcher;
            Task.Run(() => RegexGrep.Apply(rx, repl, plan, cts.Token, msg => disp.BeginInvoke(new Action(() => { if (_gStatus != null && _gBusy) _gStatus.Text = msg; }))))
                .ContinueWith(t => disp.BeginInvoke(new Action(() =>
                {
                    _gBusy = false;
                    var lines = new List<string>();
                    string summary;
                    if (t.IsFaulted) { summary = "The replace run failed before finishing: " + (t.Exception?.GetBaseException().Message ?? "unknown error") + ". Check the backup folder and restore if needed."; lines.Add(summary); }
                    else
                    {
                        var o = t.Result;
                        summary = $"Changed {o.FilesChanged} file(s), {o.Replacements:N0} replacement(s)" + (o.Failed.Count > 0 ? $", {o.Failed.Count} not changed" : "") + ".";
                        lines.Add(summary);
                        if (o.BackupFolder != null) lines.Add("Backups: " + o.BackupFolder);
                        else lines.Add("No file needed a backup, so none was made.");
                        if (o.FilesChanged > 0) lines.Add("Use Restore last backup to put the files back.");
                        foreach (var f in o.Failed) lines.Add("Not changed: " + f.Path + " : " + f.Reason);
                    }
                    _gOutcome = lines;
                    if (_gStatus != null) { _gStatus.Text = summary; _gStatus.Foreground = Ui.Res("B.TextSoft"); }
                    UpdateFilesButtons();
                    RenderFiles();
                    App.Toasts?.ShowNotice("Replace in files", summary, App.Toasts.Mint, 6, icon: Icons.Check);
                })));
        }

        private void StartRestore() => StartRestore(RegexGrep.LastBackup());

        private void StartRestore(string folder)
        {
            if (folder == null) return;
            var dlg = new ConfirmWindow("Restore the last backup?",
                "This copies the backed-up files over the current files they came from. Any edits made to those files since the replace run are lost.\n\n" + folder,
                "Restore", danger: true);
            dlg.Confirmed += _ =>
            {
                var (restored, failed, f) = RegexGrep.RestoreFolder(folder);
                var lines = new List<string> { $"Restored {restored} file(s) from {f}." };
                foreach (var x in failed) lines.Add("Not restored: " + x.Path + " : " + x.Reason);
                _gOutcome = lines; _gReport = null; _gPreviewing = false;
                if (_gStatus != null) { _gStatus.Text = lines[0]; _gStatus.Foreground = Ui.Res("B.TextSoft"); }
                UpdateFilesButtons();
                RenderFiles();
                App.Toasts?.ShowNotice("Restore backup", lines[0], failed.Count > 0 ? App.Toasts.Amber : App.Toasts.Mint, 6, icon: Icons.Check);
            };
            dlg.Show();
        }

        // ---------------------------------------------------------------- expected-outcome test cases

        private StackPanel _caseRows;
        private TextBlock _caseSummary;
        private readonly List<(RegexTestCase Case, TextBlock Status)> _caseStatus = new List<(RegexTestCase, TextBlock)>();

        private FrameworkElement CasesPanel()
        {
            var sp = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
            sp.Children.Add(Eyebrow("Expected outcomes"));
            sp.Children.Add(Note("Add a line of text and say whether the pattern should match it. Each line shows pass or fail as you edit the pattern. They are saved with the pattern when you save it to the Library."));
            _caseRows = new StackPanel();
            sp.Children.Add(_caseRows);
            var add = new WrapPanel();
            add.Children.Add(Ui.Button("Add: should match", () => { _s.TestCases.Add(new RegexTestCase { ShouldMatch = true }); RebuildCases(); }, "Btn.Ghost"));
            add.Children.Add(Ui.Button("Add: should not match", () => { _s.TestCases.Add(new RegexTestCase { ShouldMatch = false }); RebuildCases(); }, "Btn.Ghost"));
            sp.Children.Add(add);
            _caseSummary = Ui.Mono("", Ui.Res("B.TextMute"), 11);
            AutomationProperties.SetLiveSetting(_caseSummary, AutomationLiveSetting.Polite);
            sp.Children.Add(_caseSummary);
            RebuildCases();
            return sp;
        }

        private void RebuildCases()
        {
            if (_caseRows == null) return;
            _caseRows.Children.Clear();
            _caseStatus.Clear();
            foreach (var c in _s.TestCases.ToList())
            {
                var item = c;
                var row = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
                var del = Ui.Button("Remove", () => { _s.TestCases.Remove(item); RebuildCases(); }, "Btn.Ghost");
                del.Margin = new Thickness(6, 0, 0, 0);
                DockPanel.SetDock(del, Dock.Right); row.Children.Add(del);
                var status = Ui.Mono("", Ui.Res("B.TextMute"), 12);
                status.Width = 62; status.VerticalAlignment = VerticalAlignment.Center; status.Margin = new Thickness(6, 0, 0, 0);
                DockPanel.SetDock(status, Dock.Right); row.Children.Add(status);
                var kind = Ui.Button(item.ShouldMatch ? "should match" : "should not match", () => { item.ShouldMatch = !item.ShouldMatch; RebuildCases(); }, "Btn.Chip");
                kind.ToolTip = "Switch between should match and should not match"; kind.Margin = new Thickness(6, 0, 0, 0);
                DockPanel.SetDock(kind, Dock.Right); row.Children.Add(kind);
                var tb = new TextBox { Style = Ui.Style("Input"), FontFamily = Ui.Font("F.Mono"), FontSize = 12, Text = item.Text ?? "" };
                AutomationProperties.SetName(tb, "Test text, " + (item.ShouldMatch ? "should match" : "should not match"));
                tb.TextChanged += (o, e) => { item.Text = tb.Text; UpdateCases(); };
                row.Children.Add(tb);
                _caseRows.Children.Add(row);
                _caseStatus.Add((item, status));
            }
            UpdateCases();
        }

        private void UpdateCases()
        {
            if (_caseSummary == null) return;
            var rx = _s.Regex;
            int pass = 0;
            foreach (var (c, status) in _caseStatus)
            {
                if (rx == null) { status.Text = "no pattern"; status.Foreground = Ui.Res("B.TextMute"); continue; }
                bool timedOut = false;
                bool hit = RegexKit.SafeIsMatch(rx, c.Text ?? "", ref timedOut);
                if (timedOut) { status.Text = "timed out"; status.Foreground = Ui.Res("B.Amber"); continue; }
                bool ok = hit == c.ShouldMatch;
                if (ok) pass++;
                status.Text = ok ? "pass" : "FAIL";
                status.Foreground = ok ? Ui.Res("B.Mint") : Ui.Res("B.Danger");
            }
            _caseSummary.Text = _caseStatus.Count == 0 ? "No expected outcomes yet." : rx == null ? "Type a valid pattern to run the expected outcomes." : $"{pass} of {_caseStatus.Count} pass.";
        }
    }
}
