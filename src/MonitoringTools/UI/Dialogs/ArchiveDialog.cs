using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MonitoringTools.Clip;
using MonitoringTools.Core;
using MonitoringTools.Core.Archives;
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI.Dialogs
{
    /// <summary>
    /// The "Archive…" window: zip, 7z or tar.gz for the files that were copied, with every option the formats have, a live
    /// estimate, a progress view that can cancel (and removes the partial files), a test step that reads the archive back,
    /// and an optional delete of the originals behind the app's two-key confirmation.
    /// </summary>
    public sealed class ArchiveDialog : ToolDialogWindow
    {
        private static ArchiveDialog _open;
        internal static ArchiveDialog Current => _open;

        private readonly string[] _items;
        private ArchiveScan _scan;
        private readonly CancellationTokenSource _scanCts = new CancellationTokenSource();
        private CancellationTokenSource _runCts;
        private int _version;
        private bool _building = true;
        private ArchiveOptions _lastOptions;
        private FrameworkElement _form;

        // The controls, internal so the self-test drives the real window.
        internal TextBox NameBox, FolderBox, ExcludeBox, PasswordPlain;
        internal PasswordBox PasswordBox;
        internal Segmented FormatBox, ExcludeMode;
        internal Slider LevelSlider;
        internal Dropdown MethodBox, DictionaryBox, SolidBox, VolumeUnit;
        internal NumberField ThreadsField, VolumeField;
        internal CheckBox EncryptNames, SplitSwitch, IncludeParent, TestSwitch, DeleteSwitch;
        internal Button CreateButton;
        internal SearchField ExcludeRegex;
        internal ArchiveResult LastResult;
        internal string SummaryText => _summaryLine1.Text + "\n" + _summaryLine2.Text;
        internal bool Scanned => _scan != null;
        internal List<ExcludeRule> Excludes { get; } = new List<ExcludeRule>();

        private readonly TextBlock _summaryLine1 = new TextBlock { FontFamily = Ui.Font("F.Body"), FontSize = 13, FontWeight = FontWeights.Medium, Foreground = Ui.Res("B.Text"), TextWrapping = TextWrapping.Wrap };
        private readonly TextBlock _summaryLine2 = new TextBlock { FontFamily = Ui.Font("F.Body"), FontSize = 12, Foreground = Ui.Res("B.TextSoft"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
        private readonly TextBlock _strengthText = new TextBlock { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Right, FontFamily = Ui.Font("F.Body") };
        private System.Windows.Controls.ProgressBar _strengthBar;
        private WrapPanel _chipRow, _excludeChips, _presets;
        private FrameworkElement _sevenRows, _passwordRows, _splitRows, _methodRow, _regexHost, _tarNote;
        private Border _volumeBox;
        private TextBlock _suffix;
        private TextBlock _folderHint;
        private ToggleButtonShim _eye;
        private sealed class ToggleButtonShim { public System.Windows.Controls.Primitives.ToggleButton Button; }

        public static void Open(string[] items)
        {
            items = (items ?? Array.Empty<string>()).Where(p => File.Exists(p) || Directory.Exists(p.TrimEnd('\\'))).ToArray();
            if (items.Length == 0) { App.Toasts.ShowNotice("Nothing to archive", "The files are no longer there.", App.Toasts.Amber, 4, icon: Icons.Archive); return; }
            _open?.Close();
            _open = new ArchiveDialog(items);
            _open.Show();
            _open.Activate();
        }

        private ArchiveDialog(string[] items) : base("Create archive")
        {
            _items = items;
            Closed += (s, e) => { _scanCts.Cancel(); _runCts?.Cancel(); if (_open == this) _open = null; };
            _form = BuildForm();
            ShowForm();
            _building = false;
            ApplyRules();
            ElementMenu.Attach(this, null, ElementMenu.Options.Copy);
            _ = ScanAsync();
        }

        protected override void OnEscape()
        {
            if (View == "progress") { _runCts?.Cancel(); return; }
            Close();
        }

        protected override void OnPrimary() { if (View == "form" && CreateButton.IsEnabled) StartRun(); }

        private void ShowForm()
        {
            var sub = _scan == null ? $"{_items.Length} item{(_items.Length == 1 ? "" : "s")} · counting…" : SourceLine();
            ShowView("form", _form, 840, "Create archive", sub, Icons.Archive);
        }

        private string SourceLine()
        {
            var folder = _scan.Root;
            var total = _scan.Entries.Sum(e => e.Size);
            var from = _items.Length == 1 && Directory.Exists(_items[0].TrimEnd('\\')) ? Path.GetDirectoryName(_items[0].TrimEnd('\\')) ?? folder : folder;
            return $"{_items.Length} item{(_items.Length == 1 ? "" : "s")} from {PathDisplay.Shown(from)} · {Ui.Bytes(total)}" + (_scan.Truncated ? " · only the first 250,000 files counted" : "");
        }

        // ------------------------------------------------------------ the form

        private FrameworkElement BuildForm()
        {
            var o = new ArchiveOptions();
            var first = _items[0].TrimEnd('\\');
            var parentOfFirst = Path.GetDirectoryName(first) ?? first;
            o.OutFolder = parentOfFirst;
            o.Name = _items.Length == 1 ? (Directory.Exists(first) ? Path.GetFileName(first) : Path.GetFileNameWithoutExtension(first)) : (Path.GetFileName(parentOfFirst.TrimEnd('\\')) is { Length: > 0 } n ? n : "Archive");
            if (string.IsNullOrWhiteSpace(o.Name)) o.Name = "Archive";

            // chips with the selected items
            _chipRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
            int shown = 0;
            foreach (var item in _items)
            {
                if (shown >= 6) break;
                _chipRow.Children.Add(ItemChip(item)); shown++;
            }
            if (_items.Length > shown) _chipRow.Children.Add(Ui.Pill($"+{_items.Length - shown} more", Ui.Res("B.Sunken"), Ui.Res("B.TextSoft")));

            // left column: Archive
            var left = new StackPanel();
            left.Children.Add(DialogParts.Section(Icons.Archive, "Archive"));
            left.Children.Add(DialogParts.Label("Name"));
            NameBox = new TextBox { Style = Ui.Style("Input"), Text = o.Name, FontFamily = Ui.Font("F.Body"), FontSize = 15, Height = 48, MaxLength = 120 };
            AutomationProperties.SetName(NameBox, "Name");
            AutomationProperties.SetHelpText(NameBox, "The archive's name. The format adds its own ending.");
            _suffix = new TextBlock { Text = o.Suffix, Foreground = Ui.Res("B.TextSoft"), FontFamily = Ui.Font("F.Body"), FontSize = 14, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 14, 0), IsHitTestVisible = false };
            NameBox.Padding = new Thickness(0, 0, 56, 0);
            left.Children.Add(new Grid { Children = { NameBox, _suffix } });

            left.Children.Add(DialogParts.Label("Save in"));
            FolderBox = new TextBox { Style = Ui.Style("Input"), Text = o.OutFolder, Height = 48, FontSize = 13 };
            AutomationProperties.SetName(FolderBox, "Save in");
            var browse = Ui.Button("", Browse, "Btn.Icon", "M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z");
            browse.Margin = new Thickness(0); browse.HorizontalAlignment = HorizontalAlignment.Right; browse.VerticalAlignment = VerticalAlignment.Center;
            AutomationProperties.SetName(browse, "Browse for a folder");
            browse.ToolTip = "Choose the folder the archive is saved in.";
            FolderBox.Padding = new Thickness(0, 0, 44, 0);
            left.Children.Add(new Grid { Children = { FolderBox, new Border { Child = browse, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 4, 0) } } });
            _folderHint = DialogParts.Hint("Same folder as the first item.");
            left.Children.Add(_folderHint);

            left.Children.Add(DialogParts.Label("Format"));
            FormatBox = new Segmented("Format", new[] { new Choice("zip", "Zip"), new Choice("7z", "7z"), new Choice("tgz", "tar.gz") }, "zip");
            left.Children.Add(FormatBox);

            left.Children.Add(DialogParts.Label("Compression level"));
            left.Children.Add(DialogParts.StopSlider("Compression level", ArchiveOptions.LevelNames, 3, v => { if (!_building) Recalc(); }, out LevelSlider));

            _methodRow = new StackPanel();
            ((StackPanel)_methodRow).Children.Add(DialogParts.Label("Method"));
            MethodBox = new Dropdown("Method", ArchiveOptions.ZipMethods.Select(m => new Choice(m, m)), "Deflate");
            ((StackPanel)_methodRow).Children.Add(MethodBox);
            left.Children.Add(_methodRow);

            _sevenRows = new StackPanel();
            var two = new Grid { Margin = new Thickness(0, 0, 0, 0) };
            two.ColumnDefinitions.Add(new ColumnDefinition()); two.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) }); two.ColumnDefinitions.Add(new ColumnDefinition());
            var dict = new StackPanel(); dict.Children.Add(DialogParts.Label("Dictionary size"));
            DictionaryBox = new Dropdown("Dictionary size", ArchiveOptions.Dictionaries.Select(d => new Choice(d, DictLabel(d))), "auto");
            dict.Children.Add(DictionaryBox);
            var solid = new StackPanel(); solid.Children.Add(DialogParts.Label("Solid block"));
            SolidBox = new Dropdown("Solid block", ArchiveOptions.SolidBlocks.Select(d => new Choice(d, SolidLabel(d))), "auto");
            solid.Children.Add(SolidBox);
            Grid.SetColumn(solid, 2); two.Children.Add(dict); two.Children.Add(solid);
            ((StackPanel)_sevenRows).Children.Add(two);
            var threadsRow = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            threadsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); threadsRow.ColumnDefinitions.Add(new ColumnDefinition());
            int auto = Math.Clamp(Environment.ProcessorCount, 1, 16);
            ThreadsField = new NumberField("CPU threads", $"Typed number, 1 to 16. Default: {auto}, one for each core.", auto, 1, 16, 1, v => { if (!_building) Recalc(); }, snap: true);
            threadsRow.Children.Add(ThreadsField);
            var threadNote = DialogParts.Hint("Dictionary, solid block and threads are 7z options with LZMA or LZMA2. They are hidden for Zip.");
            threadNote.VerticalAlignment = VerticalAlignment.Center; threadNote.Margin = new Thickness(18, 20, 0, 0);
            Grid.SetColumn(threadNote, 1); threadsRow.Children.Add(threadNote);
            ((StackPanel)_sevenRows).Children.Add(threadsRow);
            left.Children.Add(_sevenRows);

            // right column: Protection, Split, Contents
            var right = new StackPanel();
            right.Children.Add(DialogParts.Section(DialogParts.Lock, "Protection"));
            _passwordRows = new StackPanel();
            var pr = (StackPanel)_passwordRows;
            pr.Children.Add(DialogParts.Label("Password"));
            PasswordBox = new PasswordBox { Height = 48, Background = Ui.Res("B.Sunken"), Foreground = Ui.Res("B.Text"), CaretBrush = Ui.Res("B.Mint"), BorderThickness = new Thickness(2), BorderBrush = Brushes.Transparent, Padding = new Thickness(12, 0, 48, 0), VerticalContentAlignment = VerticalAlignment.Center, FontSize = 14, MaxLength = 128, Template = PasswordTemplate() };
            PasswordPlain = new TextBox { Style = Ui.Style("Input"), Height = 48, Visibility = Visibility.Collapsed, FontFamily = Ui.Font("F.Mono"), MaxLength = 128, Padding = new Thickness(0, 0, 44, 0) };
            AutomationProperties.SetName(PasswordBox, "Password"); AutomationProperties.SetName(PasswordPlain, "Password (shown)");
            var eye = new System.Windows.Controls.Primitives.ToggleButton { Style = null, Width = 36, Height = 36, Cursor = Cursors.Hand, Background = Brushes.Transparent, BorderThickness = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0), Content = Ui.Icon(DialogParts.Eye, Ui.Res("B.TextSoft"), 18), Focusable = true };
            eye.Template = FlatToggleTemplate();
            AutomationProperties.SetName(eye, "Show the password"); eye.ToolTip = "Shows what you typed.";
            _eye = new ToggleButtonShim { Button = eye };
            bool syncing = false;
            PasswordBox.PasswordChanged += (s, e) => { if (syncing) return; syncing = true; PasswordPlain.Text = PasswordBox.Password; syncing = false; PasswordChangedNow(); };
            PasswordPlain.TextChanged += (s, e) => { if (syncing) return; syncing = true; PasswordBox.Password = PasswordPlain.Text; syncing = false; PasswordChangedNow(); };
            eye.Click += (s, e) =>
            {
                bool show = eye.IsChecked == true;
                PasswordPlain.Visibility = show ? Visibility.Visible : Visibility.Collapsed; PasswordBox.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
                (show ? (Control)PasswordPlain : PasswordBox).Focus();
            };
            pr.Children.Add(new Grid { Children = { PasswordBox, PasswordPlain, eye } });
            pr.Children.Add(DialogParts.Hint("AES-256. Leave empty for no password. The password is handed to 7-Zip on its command line for the moments it runs, and is never saved."));
            var strength = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            strength.ColumnDefinitions.Add(new ColumnDefinition()); strength.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _strengthBar = DialogParts.Bar(); _strengthBar.Height = 6; _strengthBar.VerticalAlignment = VerticalAlignment.Center;
            AutomationProperties.SetName(_strengthBar, "Password strength");
            strength.Children.Add(_strengthBar); _strengthText.Margin = new Thickness(12, 0, 0, 0); Grid.SetColumn(_strengthText, 1); strength.Children.Add(_strengthText);
            pr.Children.Add(strength);
            EncryptNames = DialogParts.SwitchRow("Encrypt file names", "Hides names and sizes too. 7z only.", false, out var encRow);
            pr.Children.Add(encRow);
            right.Children.Add(_passwordRows);

            _splitRows = new StackPanel();
            var sr = (StackPanel)_splitRows;
            sr.Children.Add(DialogParts.Section(DialogParts.Split, "Split"));
            SplitSwitch = DialogParts.SwitchRow("Split into volumes", "Names end in .7z.001, .7z.002 and so on.", false, out var splitRow);
            sr.Children.Add(splitRow);
            _volumeBox = new Border { Margin = new Thickness(0, 4, 0, 0) };
            var vg = new Grid();
            vg.ColumnDefinitions.Add(new ColumnDefinition()); vg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) }); vg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            VolumeField = new NumberField("Volume size", null, 700, 1, 1000000, 1, v => { if (!_building) Recalc(); }, snap: true);
            var unit = new StackPanel(); unit.Children.Add(new TextBlock { Text = "Unit", FontSize = 14, Foreground = Ui.Res("B.Text"), FontFamily = Ui.Font("F.Body"), Margin = new Thickness(0, 4, 0, 8) });
            VolumeUnit = new Dropdown("Volume unit", new[] { new Choice("MB", "MB"), new Choice("GB", "GB") }, "MB");
            unit.Children.Add(VolumeUnit);
            Grid.SetColumn(unit, 2); vg.Children.Add(VolumeField); vg.Children.Add(unit);
            _volumeBox.Child = vg;
            sr.Children.Add(_volumeBox);
            _presets = new WrapPanel { Margin = new Thickness(0, 0, 0, 0) };
            foreach (var (label, mb, tip) in new[] { ("700 MB", 700, "A CD."), ("FAT32 4 GB", 4095, "FAT32 files stop at 4 GiB minus one byte, so each volume is 4,095 MB."), ("DVD 4.4 GB", 4480, "A single-layer DVD.") })
            {
                var m = mb;
                var chip = Ui.Button(label, () => { VolumeUnit.Select("MB", false); VolumeField.SetValue(m); Recalc(); }, "Btn.Chip");
                chip.Margin = new Thickness(0, 0, 8, 8); chip.ToolTip = tip;
                AutomationProperties.SetName(chip, "Volume size " + label);
                _presets.Children.Add(chip);
            }
            sr.Children.Add(_presets);
            right.Children.Add(_splitRows);

            right.Children.Add(DialogParts.Section(Icons.Folder, "Contents"));
            IncludeParent = DialogParts.SwitchRow("Include the parent folder", "Keeps the folder the items are in as the top folder inside.", true, out var parentRow);
            right.Children.Add(parentRow);
            right.Children.Add(DialogParts.Label("Exclude patterns"));
            _excludeChips = new WrapPanel();
            ExcludeBox = new TextBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Ui.Res("B.Text"), CaretBrush = Ui.Res("B.Mint"), FontFamily = Ui.Font("F.Mono"), FontSize = 13, MinWidth = 150, Height = 30, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 2, 0, 2), FocusVisualStyle = null };
            AutomationProperties.SetName(ExcludeBox, "Add an exclude pattern");
            ExcludeBox.ToolTip = "Type a pattern and press Enter. Glob: node_modules, *.tmp, build/**. Regex: tested against the relative path.";
            var hint = new TextBlock { Text = "add pattern…", Foreground = Ui.Res("B.TextMute"), FontFamily = Ui.Font("F.Body"), FontSize = 13, IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
            ExcludeBox.TextChanged += (s, e) => hint.Visibility = ExcludeBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            ExcludeBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) { e.Handled = true; AddExclude(ExcludeBox.Text, false); } else if (e.Key == Key.Back && ExcludeBox.Text.Length == 0 && Excludes.Count > 0) RemoveExclude(Excludes[^1]); };
            var inputHost = new Grid { MinWidth = 160, Margin = new Thickness(0, 0, 0, 0) };
            inputHost.Children.Add(hint); inputHost.Children.Add(ExcludeBox);
            _excludeChips.Children.Add(inputHost);
            right.Children.Add(new Border { Background = Ui.Res("B.Sunken"), CornerRadius = new CornerRadius(14), Padding = new Thickness(8, 8, 8, 6), MinHeight = 72, Child = _excludeChips });
            var modeRow = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            modeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); modeRow.ColumnDefinitions.Add(new ColumnDefinition()); modeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            ExcludeMode = new Segmented("Exclude pattern type", new[] { new Choice("glob", "Glob"), new Choice("regex", "Regex") }, "glob");
            var builder = Ui.Button("Builder", () => { ExcludeMode.Select("regex"); ExcludeRegex.OpenBuilder(); }, "Btn.Chip", Icons.Wand);
            builder.Margin = new Thickness(0); builder.Height = 40; builder.Padding = new Thickness(16, 0, 16, 0);
            AutomationProperties.SetName(builder, "Open the regex builder for the exclude patterns");
            builder.ToolTip = "Opens the app's full regex builder. Press Enter in its box to add the pattern.";
            Grid.SetColumn(builder, 2); modeRow.Children.Add(ExcludeMode); modeRow.Children.Add(builder);
            right.Children.Add(modeRow);
            ExcludeRegex = new SearchField("archive-exclude", "Exclude regex", compact: true) { Margin = new Thickness(0, 10, 0, 0) };
            ExcludeRegex.Box.KeyDown += (s, e) => { if (e.Key == Key.Enter) { e.Handled = true; var q = ExcludeRegex.State.Query; if (ExcludeRegex.State.Error != null) return; AddExclude(q, true); ExcludeRegex.Box.Clear(); } };
            _regexHost = new StackPanel { Visibility = Visibility.Collapsed };
            ((StackPanel)_regexHost).Children.Add(ExcludeRegex);
            ((StackPanel)_regexHost).Children.Add(DialogParts.Hint("Matched against each file's path from the top folder, with / between names. Press Enter to add the pattern as a chip."));
            right.Children.Add(_regexHost);

            // after
            var after = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            after.ColumnDefinitions.Add(new ColumnDefinition()); after.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) }); after.ColumnDefinitions.Add(new ColumnDefinition());
            TestSwitch = DialogParts.SwitchRow("Test the archive after creating", "Reads it back and checks every file.", true, out var testRow);
            DeleteSwitch = DialogParts.SwitchRow("Delete the originals", "Recycle Bin, after the test passes. Needs the two-key confirmation.", false, out var delRow);
            Grid.SetColumn(delRow, 2); after.Children.Add(testRow); after.Children.Add(delRow);

            var cols = new Grid();
            cols.ColumnDefinitions.Add(new ColumnDefinition()); cols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) }); cols.ColumnDefinitions.Add(new ColumnDefinition());
            Grid.SetColumn(right, 2); cols.Children.Add(left); cols.Children.Add(right);
            var scroll = new StackPanel(); scroll.Children.Add(cols); scroll.Children.Add(after);

            var summary = new Border { Background = Ui.Res("B.Sunken"), CornerRadius = new CornerRadius(14), Padding = new Thickness(14, 12, 14, 12), Margin = new Thickness(0, 16, 0, 0) };
            var sg = new Grid(); sg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); sg.ColumnDefinitions.Add(new ColumnDefinition());
            var sicon = Ui.Icon(DialogParts.Info, Ui.Res("B.TextSoft"), 18); sicon.Margin = new Thickness(0, 1, 12, 0); sicon.VerticalAlignment = VerticalAlignment.Top;
            var st = new StackPanel(); st.Children.Add(_summaryLine1); st.Children.Add(_summaryLine2);
            AutomationProperties.SetLiveSetting(st, AutomationLiveSetting.Polite);
            Grid.SetColumn(st, 1); sg.Children.Add(sicon); sg.Children.Add(st); summary.Child = sg;

            var foot = new Grid { Margin = new Thickness(0, 14, 0, 0) };
            foot.ColumnDefinitions.Add(new ColumnDefinition()); foot.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); foot.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var keys = new TextBlock { Text = "Esc cancels · Ctrl+Enter creates", FontSize = 12, Foreground = Ui.Res("B.TextSoft"), VerticalAlignment = VerticalAlignment.Center, FontFamily = Ui.Font("F.Body") };
            var cancel = Ui.Button("Cancel", Close, "Btn.Ghost"); cancel.Margin = new Thickness(0, 0, 10, 0);
            CreateButton = Ui.Button("Create archive", StartRun, "Btn.Accent", Icons.Archive); CreateButton.Margin = new Thickness(0);
            CreateButton.IsDefault = false;
            Grid.SetColumn(cancel, 1); Grid.SetColumn(CreateButton, 2);
            foot.Children.Add(keys); foot.Children.Add(cancel); foot.Children.Add(CreateButton);

            var root = new DockPanel();
            var chipHost = new Border { Child = _chipRow };
            DockPanel.SetDock(chipHost, Dock.Top);
            DockPanel.SetDock(foot, Dock.Bottom); DockPanel.SetDock(summary, Dock.Bottom);
            root.Children.Add(chipHost); root.Children.Add(foot); root.Children.Add(summary);
            root.Children.Add(new ScrollViewer { Content = scroll, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(0, 0, 8, 0), MaxHeight = Math.Max(300, SystemParameters.WorkArea.Height - 330) });

            // wiring
            NameBox.TextChanged += (s, e) => { if (!_building) Recalc(); };
            FolderBox.TextChanged += (s, e) => { if (!_building) { _folderHint.Text = Path.GetFullPath(FolderBox.Text.Trim().Length == 0 ? "." : FolderBox.Text.Trim()).TrimEnd('\\') == parentOfFirst.TrimEnd('\\') ? "Same folder as the first item." : " "; Recalc(); } };
            FormatBox.Changed += id => { ApplyRules(); Recalc(); };
            MethodBox.Changed += id => { ApplyRules(); Recalc(); };
            DictionaryBox.Changed += id => Recalc(); SolidBox.Changed += id => Recalc(); VolumeUnit.Changed += id => Recalc();
            void OnToggle(CheckBox c, Action a) { c.Checked += (s, e) => a(); c.Unchecked += (s, e) => a(); }
            OnToggle(EncryptNames, Recalc);
            OnToggle(SplitSwitch, () => { ApplyRules(); Recalc(); });
            OnToggle(IncludeParent, Recalc);
            OnToggle(TestSwitch, () => { ApplyRules(); Recalc(); });
            OnToggle(DeleteSwitch, () => { ApplyRules(); Recalc(); });
            ExcludeMode.Changed += id => { _regexHost.Visibility = id == "regex" ? Visibility.Visible : Visibility.Collapsed; ExcludeBox.IsEnabled = id == "glob"; };
            return root;
        }

        private static string DictLabel(string d) => d == "auto" ? "Automatic" : d.ToUpperInvariant().Replace("M", " MB").Replace("G", " GB");
        private static string SolidLabel(string d) => d switch { "auto" => "Automatic", "off" => "Off", "on" => "One block", _ => d.ToUpperInvariant().Replace("M", " MB").Replace("G", " GB") };

        private FrameworkElement ItemChip(string path)
        {
            var p = path.TrimEnd('\\');
            bool dir = Directory.Exists(p);
            long size = 0;
            try { if (!dir) size = new FileInfo(p).Length; } catch { }
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new ContentControl { Content = Ui.Icon(dir ? Icons.Folder : Icons.File, Ui.Res("B.TextSoft"), 16), Margin = new Thickness(0, 0, 8, 0), Focusable = false, VerticalAlignment = VerticalAlignment.Center });
            sp.Children.Add(new TextBlock { Text = Path.GetFileName(p).Length == 0 ? p : Path.GetFileName(p), FontFamily = Ui.Font("F.Body"), FontSize = 13, FontWeight = FontWeights.Medium, Foreground = Ui.Res("B.Text"), VerticalAlignment = VerticalAlignment.Center, MaxWidth = 220, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = p });
            if (!dir) sp.Children.Add(new TextBlock { Text = Ui.Bytes(size), FontFamily = Ui.Font("F.Body"), FontSize = 12, Foreground = Ui.Res("B.TextSoft"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) });
            return new Border { CornerRadius = new CornerRadius(18), BorderBrush = Ui.Res("B.LineStrong"), BorderThickness = new Thickness(1), Padding = new Thickness(12, 6, 14, 6), Margin = new Thickness(0, 0, 8, 8), Child = sp };
        }

        private static ControlTemplate PasswordTemplate() => (ControlTemplate)System.Windows.Markup.XamlReader.Parse(
            "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='PasswordBox'><Border x:Name='Bd' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' CornerRadius='12' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' Padding='{TemplateBinding Padding}'><ScrollViewer x:Name='PART_ContentHost' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Bd' Property='BorderBrush' Value='{DynamicResource B.Mint}'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");

        private static ControlTemplate FlatToggleTemplate() => (ControlTemplate)System.Windows.Markup.XamlReader.Parse(
            "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ToggleButton'><Grid Background='Transparent'><Border x:Name='Bd' CornerRadius='18' Background='Transparent'/><Border x:Name='Fc' CornerRadius='18' BorderBrush='{DynamicResource B.Mint}' BorderThickness='2' Opacity='0'/><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Grid><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='Bd' Property='Background' Value='{DynamicResource B.SecondaryContainer}'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Fc' Property='Opacity' Value='1'/></Trigger><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Bd' Property='Background' Value='{DynamicResource B.Line}'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");

        private void Browse()
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Choose where the archive is saved", InitialDirectory = Directory.Exists(FolderBox.Text) ? FolderBox.Text : null };
            if (dlg.ShowDialog(this) == true) FolderBox.Text = dlg.FolderName;
        }

        // ------------------------------------------------------------ rules between the controls

        private static int Strength(string pw)
        {
            if (string.IsNullOrEmpty(pw)) return 0;
            int classes = new[] { pw.Any(char.IsLower), pw.Any(char.IsUpper), pw.Any(char.IsDigit), pw.Any(c => !char.IsLetterOrDigit(c)) }.Count(x => x);
            if (pw.Length >= 14 && classes >= 3) return 4;
            if (pw.Length >= 10 && classes >= 3) return 3;
            if (pw.Length >= 8 && classes >= 2) return 2;
            return 1;
        }

        private void PasswordChangedNow()
        {
            var level = Strength(PasswordBox.Password);
            _strengthBar.Value = level * 250;
            _strengthBar.Foreground = Ui.Res(level >= 3 ? "B.Mint" : "B.Amber");
            _strengthText.Text = level switch { 0 => "", 1 => "Weak", 2 => "Fair", 3 => "Good", _ => "Strong" };
            _strengthText.Foreground = Ui.Res(level >= 3 ? "B.Mint" : "B.Amber");
            AutomationProperties.SetItemStatus(_strengthBar, _strengthText.Text);
            if (!_building) { ApplyRules(); Recalc(); }
        }

        internal string PasswordText => PasswordBox.Password;
        internal void SetPassword(string pw) { PasswordBox.Password = pw; }

        private void ApplyRules()
        {
            if (_building) return;
            _building = true;
            try
            {
                bool seven = ArchiveEngine.SevenZip != null;
                const string needs7z = "Needs 7-Zip, which this build does not carry.";
                FormatBox.SetDisabled("7z", seven ? null : needs7z);
                var fmt = FormatBox.SelectedId;
                bool zip = fmt == "zip", sevenZ = fmt == "7z", tgz = fmt == "tgz";
                _suffix.Text = zip ? ".zip" : sevenZ ? ".7z" : ".tar.gz";
                // methods
                var methods = zip ? ArchiveOptions.ZipMethods : ArchiveOptions.SevenZipMethods;
                MethodBox.SetChoices(methods.Select(m => new Choice(m, m, zip && !seven && !(m == "Deflate" || m == "Store") ? needs7z : null)), MethodBox.SelectedId);
                _methodRow.Visibility = tgz ? Visibility.Collapsed : Visibility.Visible;
                var method = MethodBox.SelectedId;
                _sevenRows.Visibility = sevenZ ? Visibility.Visible : Visibility.Collapsed;
                bool lz = sevenZ && (method == "LZMA2" || method == "LZMA");
                DictionaryBox.IsEnabled = lz; SolidBox.IsEnabled = lz;
                DictionaryBox.ToolTip = lz ? null : "Only LZMA and LZMA2 use a dictionary size.";
                SolidBox.ToolTip = lz ? null : "Only LZMA and LZMA2 build solid blocks.";
                // protection and volumes
                _passwordRows.Visibility = tgz ? Visibility.Collapsed : Visibility.Visible;
                _splitRows.Visibility = tgz ? Visibility.Collapsed : Visibility.Visible;
                PasswordBox.IsEnabled = PasswordPlain.IsEnabled = seven;
                PasswordBox.ToolTip = seven ? null : needs7z;
                bool hasPw = !string.IsNullOrEmpty(PasswordBox.Password);
                EncryptNames.IsEnabled = sevenZ && hasPw;
                if (!EncryptNames.IsEnabled) EncryptNames.IsChecked = false;
                DialogParts.SetSub(EncryptNames, !sevenZ ? "Hides names and sizes too. 7z only: Zip cannot hide its file names." : !hasPw ? "Hides names and sizes too. Type a password first." : "Hides names and sizes too. 7z only.");
                SplitSwitch.IsEnabled = seven;
                if (!seven) SplitSwitch.IsChecked = false;
                DialogParts.SetSub(SplitSwitch, seven ? $"Names end in {_suffix.Text}.001, {_suffix.Text}.002 and so on." : needs7z);
                bool split = SplitSwitch.IsChecked == true;
                _volumeBox.Visibility = split ? Visibility.Visible : Visibility.Collapsed; _presets.Visibility = split ? Visibility.Visible : Visibility.Collapsed;
                // delete originals needs the test
                if (DeleteSwitch.IsChecked == true) { TestSwitch.IsChecked = true; TestSwitch.IsEnabled = false; DialogParts.SetSub(TestSwitch, "On while the originals are deleted: they go only after the test passes."); }
                else { TestSwitch.IsEnabled = true; DialogParts.SetSub(TestSwitch, "Reads it back and checks every file."); }
            }
            finally { _building = false; }
        }

        // ------------------------------------------------------------ exclude chips

        internal bool AddExclude(string pattern, bool regex)
        {
            pattern = (pattern ?? "").Trim();
            if (pattern.Length == 0) return false;
            var rule = new ExcludeRule(pattern, regex);
            var bad = rule.Validate();
            if (bad != null) { App.Toasts?.ShowNotice("Pattern not added", bad, App.Toasts.Amber, 4, icon: Icons.Archive); return false; }
            if (Excludes.Any(r => r.Pattern == pattern && r.Regex == regex)) return false;
            Excludes.Add(rule);
            ExcludeBox.Clear();
            RebuildExcludeChips();
            Recalc();
            return true;
        }

        private void RemoveExclude(ExcludeRule r) { Excludes.Remove(r); RebuildExcludeChips(); Recalc(); }

        private void RebuildExcludeChips()
        {
            var input = _excludeChips.Children[_excludeChips.Children.Count - 1];
            _excludeChips.Children.Clear();
            foreach (var r in Excludes)
            {
                var rule = r;
                var b = new Button { Style = Ui.Style("Btn.Chip"), Height = 30, Margin = new Thickness(0, 0, 8, 6), Padding = new Thickness(10, 0, 8, 0) };
                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                sp.Children.Add(new TextBlock { Text = (r.Regex ? ".* " : "") + r.Pattern, FontFamily = Ui.Font("F.Mono"), FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center, Foreground = Ui.Res("B.Text") });
                sp.Children.Add(new ContentControl { Content = Ui.Icon(Icons.Close, Ui.Res("B.TextSoft"), 11), Margin = new Thickness(8, 0, 0, 0), Focusable = false, VerticalAlignment = VerticalAlignment.Center });
                b.Content = sp;
                AutomationProperties.SetName(b, "Remove the exclude pattern " + r.Pattern);
                b.ToolTip = "Removes this pattern.";
                b.Click += (s, e) => RemoveExclude(rule);
                _excludeChips.Children.Add(b);
            }
            _excludeChips.Children.Add(input);
        }

        // ------------------------------------------------------------ reading the form

        internal ArchiveOptions ReadOptions()
        {
            var o = new ArchiveOptions
            {
                Items = _items, Name = NameBox.Text.Trim(), OutFolder = FolderBox.Text.Trim(),
                Format = FormatBox.SelectedId == "7z" ? ArchiveFormat.SevenZip : FormatBox.SelectedId == "tgz" ? ArchiveFormat.TarGz : ArchiveFormat.Zip,
                Level = (int)Math.Round(LevelSlider.Value), Method = MethodBox.SelectedId,
                Dictionary = DictionaryBox.SelectedId, Solid = SolidBox.SelectedId, Threads = (int)ThreadsField.Value,
                Password = PasswordBox.Password, EncryptNames = EncryptNames.IsChecked == true,
                Split = SplitSwitch.IsChecked == true && FormatBox.SelectedId != "tgz",
                IncludeParent = IncludeParent.IsChecked == true, TestAfter = TestSwitch.IsChecked == true,
                Excludes = Excludes.Select(r => new ExcludeRule(r.Pattern, r.Regex)).ToList()
            };
            if (o.Format == ArchiveFormat.TarGz) { o.Password = null; o.Method = null; }
            if (o.Format != ArchiveFormat.SevenZip) o.Threads = 0;
            o.VolumeBytes = (long)(VolumeField.Value * (VolumeUnit.SelectedId == "GB" ? 1073741824.0 : 1048576.0));
            return o;
        }

        private async void Recalc()
        {
            if (_building || _scan == null) { if (_scan == null) SetSummary("Counting the files…", "The estimate appears when the count is done.", false); return; }
            int ver = ++_version;
            var o = ReadOptions(); var scan = _scan; var seven = ArchiveEngine.SevenZip != null;
            var (plan, err) = await Task.Run(() => (ArchiveEngine.Estimate(scan, o), ArchiveEngine.Validate(scan, o, seven)));
            if (ver != _version || !IsLoaded && !IsVisible) return;
            if (err == null && plan.FreeOnTarget.HasValue && plan.FreeNeeded > plan.FreeOnTarget.Value) err = $"There is not enough free space: about {Ui.Bytes(plan.FreeNeeded)} is needed and {Ui.Bytes(plan.FreeOnTarget.Value)} is free on {Path.GetPathRoot(Path.GetFullPath(o.OutFolder))}.";
            if (err != null) { SetSummary(err, "Fix this to create the archive.", true); CreateButton.IsEnabled = false; CreateButton.ToolTip = err; return; }
            CreateButton.IsEnabled = true; CreateButton.ToolTip = "Creates the archive. Nothing already there is overwritten.";
            var fmt = o.Format == ArchiveFormat.Zip ? "Zip" : o.Format == ArchiveFormat.SevenZip ? "7z" : "tar.gz";
            var parts = new List<string> { fmt };
            if (o.Format != ArchiveFormat.TarGz) parts.Add(o.EffectiveMethod);
            parts.Add(ArchiveOptions.LevelNames[o.Level]);
            if (o.HasPassword) parts.Add("AES-256" + (o.EncryptNames ? " with names" : ""));
            var items = plan.Files + plan.Folders;
            var line1 = $"{plan.Files} file{(plan.Files == 1 ? "" : "s")}, {Ui.Bytes(plan.SourceBytes)} → about {Ui.Bytes(plan.EstimatedBytes)} · {string.Join(" · ", parts)}" + (o.Split ? $" · {plan.Volumes} volume{(plan.Volumes == 1 ? "" : "s")} of {Ui.Bytes(o.VolumeBytes)}" : "");
            var drive = Path.GetPathRoot(Path.GetFullPath(o.OutFolder)).TrimEnd('\\');
            var extra = new List<string>();
            if (plan.Excluded > 0) extra.Add($"{plan.Excluded} excluded");
            if (o.Format == ArchiveFormat.Zip && o.HasPassword) extra.Add("Windows Explorer cannot open an AES-256 zip, but 7-Zip can");
            if (o.Format == ArchiveFormat.Zip && !o.NeedsSevenZip && o.Level == 2) extra.Add("Fast and Fastest are the same level for a plain zip");
            if (o.Format == ArchiveFormat.Zip && !o.NeedsSevenZip && o.Level == 5) extra.Add("Ultra is the same as Maximum for a plain zip");
            SetSummary(line1, $"An estimate from the file types. Needs about {Ui.Bytes(plan.FreeNeeded)} free on {drive}. About {DialogParts.Duration(plan.EstimatedTime)}." + (extra.Count > 0 ? " " + string.Join(". ", extra) + "." : ""), false);
        }

        private void SetSummary(string line1, string line2, bool problem)
        {
            _summaryLine1.Text = line1; _summaryLine2.Text = line2;
            _summaryLine1.Foreground = problem ? Ui.Res("B.Amber") : Ui.Res("B.Text");
        }

        private async Task ScanAsync()
        {
            SetSummary("Counting the files…", "The estimate appears when the count is done.", false);
            CreateButton.IsEnabled = false;
            try
            {
                var scan = await Task.Run(() => ArchiveScan.Run(_items, _scanCts.Token));
                _scan = scan;
                if (View == "form") SubText.Text = SourceLine();
                Recalc();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { SetSummary("The files could not be counted: " + ex.Message, "", true); }
        }

        // ------------------------------------------------------------ run

        private TextBlock _pBytes, _pRate, _pFile;
        private System.Windows.Controls.ProgressBar _pBar;
        private List<(string Id, string Label, FrameworkElement Host)> _pSteps;
        private string _pStep = "Scan";

        private void StartRun()
        {
            if (_scan == null || !CreateButton.IsEnabled) return;
            var o = ReadOptions();
            var err = ArchiveEngine.Validate(_scan, o, ArchiveEngine.SevenZip != null);
            if (err != null) { SetSummary(err, "Fix this to create the archive.", true); return; }
            _lastOptions = o;
            _runCts = new CancellationTokenSource();
            ShowProgress(o);
            var progress = new Progress<ArchiveProgress>(OnProgress);
            var cts = _runCts;
            Task.Run(() => ArchiveEngine.RunAsync(_scan, o, progress, cts.Token)).ContinueWith(t => Dispatcher.Invoke(() => Finished(o, t)));
        }

        private void ShowProgress(ArchiveOptions o)
        {
            var root = new StackPanel();
            _pBar = DialogParts.Bar(); _pBar.Margin = new Thickness(0, 6, 0, 0);
            AutomationProperties.SetName(_pBar, "Archive progress");
            root.Children.Add(_pBar);
            var nums = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            nums.ColumnDefinitions.Add(new ColumnDefinition()); nums.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _pBytes = new TextBlock { FontSize = 13, Foreground = Ui.Res("B.Text"), FontFamily = Ui.Font("F.Body"), Text = "Starting…" };
            _pRate = new TextBlock { FontSize = 13, Foreground = Ui.Res("B.TextSoft"), FontFamily = Ui.Font("F.Body") };
            Grid.SetColumn(_pRate, 1); nums.Children.Add(_pBytes); nums.Children.Add(_pRate);
            root.Children.Add(nums);
            _pFile = new TextBlock { FontSize = 12, Foreground = Ui.Res("B.TextSoft"), FontFamily = Ui.Font("F.Mono"), TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 8, 0, 0), Height = 18 };
            AutomationProperties.SetLiveSetting(_pFile, AutomationLiveSetting.Off);
            root.Children.Add(_pFile);
            var steps = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };
            _pSteps = new List<(string, string, FrameworkElement)>();
            foreach (var (id, label) in new[] { ("Scan", "Scan"), ("Compress", "Compress"), ("Test", "Test"), ("Finish", "Finish") })
            {
                if (id == "Test" && !o.TestAfter) continue;
                var host = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 18, 0) };
                steps.Children.Add(host);
                _pSteps.Add((id, label, host));
            }
            root.Children.Add(steps);
            _pStep = "Scan"; PaintSteps();
            var cancel = Ui.Button("Cancel", () => _runCts?.Cancel(), "Btn.Ghost", Icons.Close);
            cancel.Margin = new Thickness(0); cancel.HorizontalAlignment = HorizontalAlignment.Right;
            AutomationProperties.SetName(cancel, "Cancel and remove the partial archive");
            cancel.ToolTip = "Stops and removes the partly written archive (Esc).";
            var foot = new Border { Margin = new Thickness(0, 18, 0, 0), BorderThickness = new Thickness(0, 1, 0, 0), BorderBrush = Ui.Res("B.Line"), Padding = new Thickness(0, 14, 0, 0), Child = cancel };
            root.Children.Add(foot);
            ShowView("progress", root, 456, "Creating archive", Path.GetFileName(ArchiveEngine.UniqueOutput(o.OutFolder, o.Name, o.Suffix)), Icons.Archive);
            cancel.Focus();
        }

        private void PaintSteps()
        {
            bool before = true;
            foreach (var (id, label, host) in _pSteps)
            {
                var panel = (StackPanel)host; panel.Children.Clear();
                bool current = id == _pStep; bool done = before && !current;
                if (current) before = false;
                FrameworkElement glyph = done ? Ui.Icon(Icons.Check, Ui.Res("B.Mint"), 14, 2.4)
                    : current ? new System.Windows.Shapes.Ellipse { Width = 10, Height = 10, Fill = Ui.Res("B.Mint") }
                    : new System.Windows.Shapes.Ellipse { Width = 12, Height = 12, Stroke = Ui.Res("B.LineStrong"), StrokeThickness = 1.6 };
                glyph.VerticalAlignment = VerticalAlignment.Center; glyph.Margin = new Thickness(0, 0, 8, 0);
                panel.Children.Add(glyph);
                panel.Children.Add(new TextBlock { Text = label, FontFamily = Ui.Font("F.Body"), FontSize = 13, FontWeight = current ? FontWeights.Medium : FontWeights.Normal, Foreground = current ? Ui.Res("B.Text") : Ui.Res("B.TextSoft") });
                AutomationProperties.SetName(panel, label + (done ? ", done" : current ? ", in progress" : ", waiting"));
            }
        }

        private void OnProgress(ArchiveProgress p)
        {
            if (View != "progress" || _pBar == null) return;
            if (p.Step != _pStep) { _pStep = p.Step; PaintSteps(); }
            _pBar.Value = p.Total > 0 ? Math.Min(1000, 1000.0 * p.Done / p.Total) : 0;
            _pBytes.Text = $"{Ui.Bytes(p.Done)} of {Ui.Bytes(p.Total)}";
            var bits = new List<string>();
            if (p.BytesPerSecond > 0) bits.Add(Ui.Bytes((long)p.BytesPerSecond) + "/s");
            if (p.Remaining.HasValue) bits.Add("about " + DialogParts.Duration(p.Remaining.Value) + " left");
            _pRate.Text = string.Join(" · ", bits);
            if (!string.IsNullOrEmpty(p.CurrentFile)) _pFile.Text = p.CurrentFile;
        }

        private void Finished(ArchiveOptions o, Task<ArchiveResult> t)
        {
            var r = t.IsCompletedSuccessfully ? t.Result : new ArchiveResult { Error = t.Exception?.GetBaseException().Message ?? "The archive could not be made." };
            LastResult = r;
            if (r.Cancelled)
            {
                ShowForm();
                SetSummary("Cancelled. The partly written archive was removed.", "Nothing was left behind. Your settings are still here.", false);
                return;
            }
            if (!r.Ok) { ShowError(o, r); return; }
            ShowDone(o, r);
            if (o.TestAfter && r.TestOk && DeleteSwitch.IsChecked == true) AskDeleteOriginals(o);
        }

        // ------------------------------------------------------------ done, error, delete

        private TextBlock _originals;

        private void ShowDone(ArchiveOptions o, ArchiveResult r)
        {
            var root = new StackPanel();
            var g = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) }); g.ColumnDefinitions.Add(new ColumnDefinition());
            int row = 0;
            void Row(string label, UIElement value)
            {
                g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var l = new TextBlock { Text = label, FontSize = 13, Foreground = Ui.Res("B.TextSoft"), FontFamily = Ui.Font("F.Body"), Margin = new Thickness(0, 7, 0, 7), VerticalAlignment = VerticalAlignment.Center };
                Grid.SetRow(l, row); Grid.SetRow(value, row); Grid.SetColumn(value, 1);
                if (value is FrameworkElement fe) fe.Margin = new Thickness(0, 7, 0, 7);
                g.Children.Add(l); g.Children.Add(value); row++;
            }
            long pct = r.SourceBytes > 0 ? 100 * r.ArchiveBytes / r.SourceBytes : 0;
            Row("Size", new TextBlock { Text = $"{Ui.Bytes(r.ArchiveBytes)} from {Ui.Bytes(r.SourceBytes)} ({pct}%)", FontSize = 13, Foreground = Ui.Res("B.Text"), FontFamily = Ui.Font("F.Body") });
            Row("Test", r.Tested ? DialogParts.Pill(r.TestOk ? "No errors" : "Failed", r.TestOk ? "mint" : "danger") : DialogParts.Pill("Not tested", "neutral"));
            _originals = new TextBlock { Text = "Kept", FontSize = 13, Foreground = Ui.Res("B.Text"), FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap };
            Row("Originals", _originals);
            Row("Saved in", new TextBlock { Text = Path.GetDirectoryName(r.Primary), FontSize = 12.5, Foreground = Ui.Res("B.Text"), FontFamily = Ui.Font("F.Mono"), TextWrapping = TextWrapping.Wrap });
            root.Children.Add(g);
            if (r.Tested && !string.IsNullOrEmpty(r.TestDetail)) root.Children.Add(DialogParts.Hint(r.TestDetail + " (" + r.Backend + ")"));
            var copy = Ui.Button("Copy path", () => { ClipboardWatcher.SetText(r.Primary); App.Toasts?.ShowNotice("Copied", r.Primary, App.Toasts.Mint, 3, icon: Icons.Clipboard); }, "Btn.Ghost", DialogParts.Copy);
            var show = Ui.Button("Show in folder", () => { try { Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{r.Primary}\"") { UseShellExecute = true }); } catch { } Close(); }, "Btn.Accent", Icons.Folder);
            copy.Margin = new Thickness(0, 0, 10, 0); show.Margin = new Thickness(0);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { copy, show } };
            root.Children.Add(new Border { Margin = new Thickness(0, 14, 0, 0), BorderThickness = new Thickness(0, 1, 0, 0), BorderBrush = Ui.Res("B.Line"), Padding = new Thickness(0, 14, 0, 0), Child = buttons });
            var names = r.Outputs.Count > 1 ? $"{Path.GetFileName(r.Outputs[0])} and {r.Outputs.Count - 1} more volume{(r.Outputs.Count == 2 ? "" : "s")}" : Path.GetFileName(r.Primary);
            ShowView("done", root, 456, "Archive ready", names, Icons.Check);
            show.Focus();
        }

        internal bool OriginalsDeleted { get; private set; }

        private void AskDeleteOriginals(ArchiveOptions o)
        {
            var list = _items;
            var names = list.Select(p => Path.GetFileName(p.TrimEnd('\\')) is { Length: > 0 } n ? n : p).ToList();
            var shown = names.Count <= 3 ? string.Join(", ", names.Take(names.Count - 1)) + (names.Count > 1 ? " and " : "") + names.Last() : string.Join(", ", names.Take(2)) + $" and {names.Count - 2} more";
            var ask = new ConfirmWindow($"Delete the {(list.Length == 1 ? "original" : list.Length + " originals")}?", $"{shown} move{(list.Length == 1 ? "s" : "")} to the Recycle Bin. The archive stays. Only after the archive tested with no errors.", "Delete originals", danger: true) { Owner = this };
            bool confirmed = false;
            ask.Confirmed += async _ =>
            {
                confirmed = true;
                _originals.Text = "Moving to the Recycle Bin…";
                var res = await FileOps.RecycleAsync(list);
                var failed = res.Where(x => !x.Ok).ToList();
                OriginalsDeleted = failed.Count == 0;
                _originals.Text = failed.Count == 0 ? "Deleted: they are in the Recycle Bin" : $"{res.Count - failed.Count} deleted, {failed.Count} could not be: {failed[0].Error}";
            };
            ask.Closed += (s, e) => { if (!confirmed && _originals != null) _originals.Text = "Kept: the deletion was not confirmed"; };
            ask.Show();
        }

        private void ShowError(ArchiveOptions o, ArchiveResult r)
        {
            var root = new StackPanel();
            root.Children.Add(DialogParts.Banner(DialogParts.Warn, r.Error, "danger"));
            root.Children.Add(DialogParts.Hint("Nothing was left behind: the partly written archive was removed, and your files were not touched."));
            var close = Ui.Button("Close", Close, "Btn.Ghost"); close.Margin = new Thickness(0, 0, 10, 0);
            var again = Ui.Button("Try again", () => { ShowForm(); SetSummary("Your settings are still here.", "Change what is needed and create the archive again.", false); }, "Btn.Accent", Icons.Restart); again.Margin = new Thickness(0);
            root.Children.Add(new Border { Margin = new Thickness(0, 14, 0, 0), BorderThickness = new Thickness(0, 1, 0, 0), BorderBrush = Ui.Res("B.Line"), Padding = new Thickness(0, 14, 0, 0), Child = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { close, again } } });
            ShowView("error", root, 456, "Archive not created", "Nothing was left behind", Icons.Close, Ui.Res("B.Danger"), Ui.Res("B.DangerWash"));
            again.Focus();
        }

        // ------------------------------------------------------------ test seams

        /// <summary>Starts the run the way the primary button does.</summary>
        internal void PressCreate() => StartRun();
        internal void CancelRun() => _runCts?.Cancel();
        internal async Task<bool> WaitForViewAsync(string view, int ms)
        {
            var until = DateTime.UtcNow.AddMilliseconds(ms);
            while (DateTime.UtcNow < until) { if (View == view) return true; await Task.Delay(50); }
            return View == view;
        }
        internal async Task<bool> WaitForScanAsync(int ms)
        {
            var until = DateTime.UtcNow.AddMilliseconds(ms);
            while (DateTime.UtcNow < until) { if (_scan != null && CreateButton.IsEnabled) return true; await Task.Delay(50); }
            return _scan != null;
        }
        internal bool CreateEnabled => CreateButton.IsEnabled;

        // Capture-only views: the real progress, done and error views filled with fixed numbers, so a screenshot can show them
        // without racing a real run. They are labelled as such in the capture list and never used as proof of a run.
        internal void PreviewProgress()
        {
            var o = ReadOptions();
            ShowProgress(o);
            _pStep = "Compress"; PaintSteps();
            OnProgress(new ArchiveProgress { Step = "Compress", Done = 19_300_000, Total = 50_500_000, BytesPerSecond = 6_400_000, Remaining = TimeSpan.FromSeconds(5), CurrentFile = "photos\\IMG_2041.jpg" });
        }
        internal void PreviewDone(bool deleted = false)
        {
            var o = ReadOptions();
            var r = new ArchiveResult { Ok = true, SourceBytes = 50_500_000, ArchiveBytes = 22_300_000, Tested = true, TestOk = true, TestDetail = "7-Zip tested every file: everything is Ok", Backend = "7-Zip", Files = 3 };
            r.Outputs.Add(Path.Combine(o.OutFolder, o.Name + o.Suffix + (o.Split ? ".001" : "")));
            if (o.Split) r.Outputs.Add(Path.Combine(o.OutFolder, o.Name + o.Suffix + ".002"));
            ShowDone(o, r);
            if (deleted) _originals.Text = "Deleted: they are in the Recycle Bin";
        }
        internal void PreviewError() => ShowError(ReadOptions(), new ArchiveResult { Error = "7-Zip stopped: Cannot open file 'locked.db' (it is used by another process)." });
        internal void PreviewForm() { ShowForm(); }
    }
}
