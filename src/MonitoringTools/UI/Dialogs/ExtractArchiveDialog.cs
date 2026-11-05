using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using MonitoringTools.Core;
using MonitoringTools.Core.DocTools;
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI.Dialogs
{
    /// <summary>The states of the archive dialog.</summary>
    public enum ArchiveState { Ready, Password, Working, Done, CannotRead }

    /// <summary>
    /// "Extract an archive" (design/file-tools, board 7). Lists the contents with check marks, searches them, picks a destination
    /// and a rule for names that exist, and extracts on a worker thread with progress and Cancel. A path that would leave the
    /// destination is refused and counted. Zip, tar, tar.gz, tgz and gz open here; 7z and rar need 7-Zip, which is not bundled.
    /// </summary>
    public sealed class ExtractArchiveDialog : FileToolDialog
    {
        private readonly ArchiveInfo _info;
        private readonly CheckTree _tree;
        private readonly SearchField _search;
        private TextBox _dest;
        private int _ruleIndex = 2;                                  // Skip, Replace, Keep both
        private readonly TextBlock _selectedLine = Text("", 13);
        private Button _extract;
        private PasswordBox _pw; private TextBox _pwShown; private CheckBox _showPw; private TextBlock _pwError;
        private CancellationTokenSource _cts;
        private ExtractResult _result;
        private string _destText;
        private string _lastPassword;
        private TextBlock _bigPercent, _fileOf, _current; private ProgressBar _bar;

        public ArchiveState State { get; private set; }
        internal ArchiveInfo Info => _info;
        internal ExtractResult Result => _result;
        internal Task LastRun { get; private set; } = Task.CompletedTask;
        internal CheckTree Tree => _tree;

        private static readonly List<ExtractArchiveDialog> OpenList = new List<ExtractArchiveDialog>();
        internal static ExtractArchiveDialog Current => OpenList.LastOrDefault();

        public static ExtractArchiveDialog Open(string file)
        {
            if (!File.Exists(file)) { App.Toasts.ShowNotice("Nothing to extract", "The file is no longer there.", App.Toasts.Amber, 4, icon: Icons.Archive); return null; }
            var d = new ExtractArchiveDialog(file);
            d.Show(); d.Activate();
            return d;
        }

        private ExtractArchiveDialog(string file) : base("Extract an archive", file, Icons.Archive, 820)
        {
            _info = ArchiveTools.Open(file);
            _tree = new CheckTree("Archive contents", checkFolders: true);
            _search = new SearchField("extract-archive", "Search inside the archive") { Margin = new Thickness(0, 0, 0, 10) };
            _search.Filter += () => { _tree.SetFilter(_search.State.IsActive ? (p => _search.State.Matches(p)) : null); _search.SetCount(_tree.MatchCount, _tree.FileCount, "files"); };
            _tree.Changed += UpdateSelection;
            _destText = DefaultFolder(file);
            OpenList.Add(this);
            Closed += (s, e) => { OpenList.Remove(this); _cts?.Cancel(); };
            PreviewKeyDown += (s, e) =>
            {
                bool ctrlF = e.Key == System.Windows.Input.Key.F && System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control;
                bool slash = e.Key == System.Windows.Input.Key.OemQuestion && System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.None && !(System.Windows.Input.Keyboard.FocusedElement is TextBox);
                if ((ctrlF || slash) && State == ArchiveState.Ready) { _search.Box.Focus(); e.Handled = true; }
            };
            if (_info.Unsupported != null) ShowCannotRead(_info.Unsupported);
            else if (_info.NeedsPassword) ShowPassword(null);
            else ShowReady();
        }

        private static string DefaultFolder(string file)
        {
            var n = Path.GetFileName(file);
            foreach (var ext in new[] { ".tar.gz", ".tgz", ".zip", ".tar", ".gz", ".7z", ".rar" })
                if (n.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) { n = n[..^ext.Length]; break; }
            return Path.Combine(Path.GetDirectoryName(file), n.Length == 0 ? "extracted" : n);
        }

        protected override bool EnterRunsPrimary => State != ArchiveState.Working;

        protected override void OnEscape() { if (State == ArchiveState.Working) _cts?.Cancel(); Close(); }

        private string Summary()
        {
            var nm = _info.FileCount == 1 ? "1 file" : $"{_info.FileCount} files";
            return $"{nm} · {Ui.Bytes(_info.UnpackedBytes)} unpacked · {Ui.Bytes(_info.PackedBytes)} zipped";
        }

        // ------------------------------------------------------------ states

        private void ShowReady()
        {
            State = ArchiveState.Ready;
            SetSummary(Summary());
            _tree.SetItems(_info.Entries.Select(e => new TreeItem { Path = e.Path, IsDirectory = e.IsDirectory, Size = e.Size, Modified = e.Modified, Note = e.Unsafe && !e.IsDirectory ? "unsafe path, will be refused" : null, Tag = e }));
            _search.SetCount(_tree.FileCount, _tree.FileCount, "files");

            var chips = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var all = Ui.Button("Select all", () => _tree.SelectAll(), "Btn.Chip"); var none = Ui.Button("None", () => _tree.SelectNone(), "Btn.Chip");
            AutomationProperties.SetName(none, "Select none");
            all.Margin = new Thickness(0, 0, 6, 0); none.Margin = new Thickness(0);
            chips.Children.Add(all); chips.Children.Add(none);
            var selRow = new Grid { Margin = new Thickness(2, 4, 0, 2) };
            selRow.ColumnDefinitions.Add(new ColumnDefinition()); selRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _selectedLine.VerticalAlignment = VerticalAlignment.Center;
            AutomationProperties.SetLiveSetting(_selectedLine, AutomationLiveSetting.Polite);
            Grid.SetColumn(chips, 1); selRow.Children.Add(_selectedLine); selRow.Children.Add(chips);

            _dest = Field("Extract to", _destText);
            _dest.TextChanged += (s, e) => _destText = _dest.Text;
            var choose = Ui.Button("Choose folder", ChooseFolder, "Btn.Ghost", Icons.Folder);
            choose.Margin = new Thickness(8, 0, 0, 0);
            var destRow = new Grid();
            destRow.ColumnDefinitions.Add(new ColumnDefinition()); destRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(choose, 1); destRow.Children.Add(_dest); destRow.Children.Add(choose);

            var rule = Segmented("If a file already exists", new[] { "Skip", "Replace", "Keep both" }, _ruleIndex, i => { _ruleIndex = i; ShowReadyKeepState(); });

            var left = new StackPanel { Margin = new Thickness(0, 0, 16, 0) };
            left.Children.Add(FieldLabel("Extract to")); left.Children.Add(destRow); left.Children.Add(Hint("A new folder is made from the archive name."));
            var right = new StackPanel();
            right.Children.Add(FieldLabel("If a file already exists")); right.Children.Add(rule);
            var two = new Grid();
            two.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) }); two.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(right, 1); two.Children.Add(left); two.Children.Add(right);

            var notes = new List<UIElement>();
            if (_info.Format == "zip" && _info.Entries.Any(e => e.Unsafe && !e.IsDirectory))
                notes.Add(Banner(BannerKind.Warn, "Some paths in this archive are unsafe", "Entries that would land outside the destination folder are refused and counted when it finishes."));

            var cancel = Ui.Button("Cancel", Close, "Btn.Ghost");
            _extract = Ui.Button("Extract", StartExtract, "Btn.Accent", DocIcons.Down);
            SetBody(notes.Concat(new UIElement[] { _search, _tree, selRow, two }).ToArray());
            SetFooter("Files never leave the chosen folder.", null, new[] { cancel, _extract }, _extract);
            UpdateSelection();
        }

        // keeps the typed folder and selection when only the rule changes
        private void ShowReadyKeepState() { var keep = _tree.Selected.Select(i => i.Path).ToHashSet(); ShowReadyRebuild(keep); }
        private void ShowReadyRebuild(HashSet<string> keep) { ShowReady(); _tree.SelectWhere(i => keep.Contains(i.Path)); }

        private void UpdateSelection()
        {
            if (_extract == null) return;
            int n = _tree.SelectedCount;
            var total = _tree.FileCount;
            _selectedLine.Text = $"{n} of {total} selected · {Ui.Bytes(_tree.SelectedBytes)}";
            var nm = n == 1 ? "Extract 1 file" : $"Extract {n} files";
            SetExtractLabel(nm);
            _extract.IsEnabled = n > 0;
        }

        private void SetExtractLabel(string label)
        {
            _extract.Content = null;
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new ContentControl { Content = Ui.Icon(DocIcons.Down, Ui.Res("B.MintInk"), 14), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
            sp.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
            _extract.Content = sp;
            AutomationProperties.SetName(_extract, label);
        }

        private void ChooseFolder()
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Choose where the files go" };
            if (dlg.ShowDialog(this) == true) { _destText = dlg.FolderName; _dest.Text = _destText; }
        }

        private void ShowPassword(string error)
        {
            State = ArchiveState.Password;
            SetSummary(_info.FileCount == 1 ? "encrypted · 1 file" : $"encrypted · {_info.FileCount} files");
            var banner = Banner(BannerKind.Info, "This archive needs a password", "The file names are visible, the contents are locked. The password is used once and not stored.", DocIcons.Lock);
            _pw = new PasswordBox { Height = 42, FontSize = 14, Padding = new Thickness(10, 0, 10, 0), VerticalContentAlignment = VerticalAlignment.Center, Background = Ui.Res("B.Sunken"), Foreground = Ui.Res("B.Text"), CaretBrush = Ui.Res("B.Mint"), BorderBrush = error == null ? Ui.Res("B.LineStrong") : Ui.Res("B.Danger"), BorderThickness = new Thickness(1) };
            AutomationProperties.SetName(_pw, "Password");
            _pwShown = Field("Password", _lastPassword ?? ""); _pwShown.Visibility = Visibility.Collapsed;
            _pw.Password = _lastPassword ?? "";
            _pw.PasswordChanged += (s, e) => { _lastPassword = _pw.Password; _pwShown.Text = _pw.Password; };
            _pwShown.TextChanged += (s, e) => { if (_pwShown.IsVisible) { _lastPassword = _pwShown.Text; _pw.Password = _pwShown.Text; } };
            _pwError = new TextBlock { Text = error ?? "", FontSize = 12, Foreground = Ui.Res("B.Danger"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0), FontFamily = Ui.Font("F.Body"), Visibility = error == null ? Visibility.Collapsed : Visibility.Visible };
            AutomationProperties.SetLiveSetting(_pwError, AutomationLiveSetting.Assertive);
            _showPw = new CheckBox { Style = Ui.Style("Switch"), Content = new TextBlock { Text = "Show the password while typing", FontSize = 13, FontFamily = Ui.Font("F.Body"), Foreground = Ui.Res("B.Text") }, Margin = new Thickness(0, 12, 0, 0) };
            AutomationProperties.SetName(_showPw, "Show the password while typing");
            _showPw.Checked += (s, e) => { _pwShown.Text = _pw.Password; _pw.Visibility = Visibility.Collapsed; _pwShown.Visibility = Visibility.Visible; _pwShown.Focus(); };
            _showPw.Unchecked += (s, e) => { _pw.Password = _pwShown.Text; _pwShown.Visibility = Visibility.Collapsed; _pw.Visibility = Visibility.Visible; _pw.Focus(); };
            var cancel = Ui.Button("Cancel", Close, "Btn.Ghost");
            var unlock = Ui.Button("Unlock and extract", UnlockAndExtract, "Btn.Accent", DocIcons.Lock);
            SetBody(banner, FieldLabel("Password"), _pw, _pwShown, _pwError, _showPw);
            SetFooter("Try again or Cancel.", null, new[] { cancel, unlock }, unlock);
            if (error == null) SetStatus("Type the password, then Unlock and extract.");
            Loaded += (s, e) => _pw.Focus();
            _pw.Focus();
        }

        private void ShowCannotRead(string reason)
        {
            State = ArchiveState.CannotRead;
            SetSummary("could not read");
            var copy = Ui.Button("Copy details", () => { try { Clipboard.SetText(reason); } catch { } }, "Btn.Ghost", DocIcons.Copy);
            SetBody(Banner(BannerKind.Bad, "This archive cannot be opened here", reason),
                    Card(new StackPanel { Children = { Text("WHAT WAS NOT CHANGED", 12, Ui.Res("B.Mint"), FontWeights.Medium), Text("Nothing was extracted and the archive was not changed.", 13) } }));
            SetFooterClose("Zip, tar, tar.gz, tgz and gz archives open here.", copy);
        }

        // ------------------------------------------------------------ extraction

        private void UnlockAndExtract()
        {
            var pw = _lastPassword ?? "";
            var check = ArchiveTools.CheckPassword(_info, pw);
            if (check == "wrong") { ShowPassword("Wrong password for this archive. Nothing was extracted."); return; }
            if (check != null) { ShowCannotRead(check); return; }
            _tree.SetItems(_info.Entries.Select(e => new TreeItem { Path = e.Path, IsDirectory = e.IsDirectory, Size = e.Size, Modified = e.Modified, Tag = e }));
            Run(_info.Entries.Where(e => !e.IsDirectory).ToList(), pw);
        }

        private void StartExtract()
        {
            var picked = _tree.Selected.Select(i => (ArchiveEntry)i.Tag).ToList();
            if (picked.Count == 0) return;
            Run(picked, null);
        }

        /// <summary>Starts extracting the chosen entries (a click on the primary button does the same).</summary>
        internal void StartForTest(string password = null)
        {
            if (State == ArchiveState.Password) { _lastPassword = password; UnlockAndExtract(); return; }
            Run(_tree.Selected.Select(i => (ArchiveEntry)i.Tag).ToList(), password);
        }

        internal void SetDestinationForTest(string folder) { _destText = folder; if (_dest != null) _dest.Text = folder; }
        internal void SetRuleForTest(OverwriteRule r) { _ruleIndex = r == OverwriteRule.Skip ? 0 : r == OverwriteRule.Replace ? 1 : 2; }
        internal void SetPasswordForTest(string pw) { _lastPassword = pw; if (_pw != null) _pw.Password = pw; }

        private void Run(List<ArchiveEntry> picked, string password)
        {
            var dest = (_destText ?? "").Trim();
            if (dest.Length == 0) { SetStatus("Choose a folder to extract into."); return; }
            try { dest = Path.GetFullPath(dest); } catch { SetStatus("That folder name is not valid."); return; }
            var rule = _ruleIndex == 0 ? OverwriteRule.Skip : _ruleIndex == 1 ? OverwriteRule.Replace : OverwriteRule.KeepBoth;
            State = ArchiveState.Working;
            SetSummary("extracting");
            _cts = new CancellationTokenSource();
            _bigPercent = new TextBlock { Text = "0%", FontSize = 36, FontFamily = Ui.Font("F.Body"), Foreground = Ui.Res("B.Text"), VerticalAlignment = VerticalAlignment.Bottom };
            _fileOf = Text("", 13, Ui.Res("B.TextSoft")); _fileOf.VerticalAlignment = VerticalAlignment.Bottom; _fileOf.Margin = new Thickness(12, 0, 0, 6);
            _bar = Progress("Extraction progress", false);
            _current = Text("", 13, Ui.Res("B.TextSoft"), null, TextWrapping.NoWrap); _current.TextTrimming = TextTrimming.CharacterEllipsis;
            var top = new StackPanel { Orientation = Orientation.Horizontal }; top.Children.Add(_bigPercent); top.Children.Add(_fileOf);
            var card = new StackPanel(); card.Children.Add(top); card.Children.Add(_bar); card.Children.Add(_current);
            var cancel = Ui.Button("Cancel", () => _cts?.Cancel(), "Btn.Ghost");
            var disabled = Ui.Button("Extract", null, "Btn.Accent"); disabled.IsEnabled = false;
            SetBody(Card(card));
            SetFooter("Cancel stops now; files already written are kept.", null, new[] { cancel, disabled });
            var progress = new Progress<ExtractProgress>(p =>
            {
                if (State != ArchiveState.Working) return;
                double frac = p.TotalBytes > 0 ? (double)p.Bytes / p.TotalBytes : (p.Total > 0 ? (double)p.Done / p.Total : 0);
                _bigPercent.Text = $"{(int)Math.Min(100, frac * 100)}%";
                _bar.Value = Math.Min(100, frac * 100);
                _fileOf.Text = $"File {Math.Max(1, p.Done)} of {p.Total}";
                _current.Text = $"{p.Current} → {PathDisplay.Shown(dest)} · {Ui.Bytes(p.Bytes)} of {Ui.Bytes(p.TotalBytes)}";
            });
            var token = _cts.Token;
            LastRun = Task.Run(() => ArchiveTools.Extract(_info, picked, dest, rule, password, progress, token)).ContinueWith(t =>
            {
                Dispatcher.Invoke(() => ShowDone(t.IsFaulted ? new ExtractResult { Error = t.Exception?.GetBaseException().Message, Destination = dest } : t.Result));
            });
        }

        private void ShowDone(ExtractResult r)
        {
            _result = r; State = ArchiveState.Done;
            SetSummary(r.Canceled ? "stopped" : r.Error != null || r.WrongPassword ? "stopped" : "done");
            var parts = new List<UIElement>();
            var bytes = Ui.Bytes(r.Bytes);
            if (r.WrongPassword) parts.Add(Banner(BannerKind.Bad, "Wrong password for this archive", "Nothing more was extracted. Files already written are kept."));
            else if (r.Error != null) parts.Add(Banner(BannerKind.Bad, "Extraction stopped", r.Error + " Files already written are kept."));
            else if (r.Canceled) parts.Add(Banner(BannerKind.Warn, r.Extracted == 1 ? "Stopped after 1 file" : $"Stopped after {r.Extracted} files", "Files already written are kept; the one in progress was removed."));
            else
            {
                var title = r.Extracted == 1 ? $"Extracted 1 file, {bytes}" : $"Extracted {r.Extracted} files, {bytes}";
                var sub = r.Renamed.Count > 0 ? (r.Renamed.Count == 1 ? "1 file already existed, so Keep both saved it under a new name." : $"{r.Renamed.Count} files already existed, so Keep both saved them under new names.") : (r.Skipped > 0 ? (r.Skipped == 1 ? "1 file already existed and was skipped." : $"{r.Skipped} files already existed and were skipped.") : "Every selected file was written.");
                parts.Add(Banner(r.Refused.Count > 0 ? BannerKind.Warn : BannerKind.Ok, title, sub));
            }
            var rows = new List<(string, string)>
            {
                ("Renamed", r.Renamed.Count == 0 ? "None" : string.Join(", ", r.Renamed.Take(3)) + (r.Renamed.Count > 3 ? $" and {r.Renamed.Count - 3} more" : "")),
                ("Not selected", r.NotSelected == 0 ? "None" : (r.NotSelected == 1 ? "1 file" : $"{r.NotSelected} files")),
                ("Refused", r.Refused.Count == 0 ? "0 unsafe paths" : (r.Refused.Count == 1 ? "1 unsafe path" : $"{r.Refused.Count} unsafe paths") + ": " + string.Join(", ", r.Refused.Take(3)))
            };
            parts.Add(KeyValues(rows));
            var open = Ui.Button("Open folder", () => ActionProvider.Shell(r.Destination), "Btn.Ghost", Icons.Folder);
            var del = Ui.Button("Delete the archive", () => FileToolsWindow.ConfirmRecycle(new[] { FilePath }), "Btn.Danger", Icons.Trash);
            var actions = new StackPanel { Orientation = Orientation.Horizontal };
            actions.Children.Add(open); actions.Children.Add(del);
            parts.Add(actions);
            SetBody(parts.ToArray());
            SetFooterClose(r.Canceled ? "Cancel stopped the extraction." : r.Error != null ? "Nothing was extracted into a partial file." : "Open the folder to see the files.");
        }
    }
}
