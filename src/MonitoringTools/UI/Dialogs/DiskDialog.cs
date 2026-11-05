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
using MonitoringTools.Core.Disks;

namespace MonitoringTools.UI.Dialogs
{
    /// <summary>
    /// The "Put in a virtual disk…" window: a VHD or VHDX file, mounted and formatted, with the selected items copied or moved in.
    /// Creating and attaching needs administrator rights, so the app starts itself once, elevated, behind the Windows consent
    /// prompt (which only the person answers); that helper does the disk work and stays until told to detach or let go, so a
    /// Cancel or a Detach needs no second prompt. This window copies the files itself, as the normal user, and checks every one.
    /// </summary>
    public sealed class DiskDialog : ToolDialogWindow
    {
        private static DiskDialog _open;
        internal static DiskDialog Current => _open;

        private readonly string[] _items;
        private long _contentBytes, _largest; private int _contentFiles; private bool _measured;
        private FrameworkElement _form;
        private bool _building = true;
        private int _version;
        private VhdSession _session;
        private CancellationTokenSource _runCts;
        private bool _running;

        internal TextBox FolderBox, NameBox, LabelBox, MountFolderBox;
        internal NumberField SizeField;
        internal Dropdown SizeUnit, LetterBox;
        internal Segmented FormatBox, TypeBox, ContentsBox, FileSystemBox, MountBox, AfterBox;
        internal Button CreateButton;
        internal VhdSession Session => _session;
        internal CopyResult LastCopy { get; private set; }
        internal string FinalState { get; private set; }
        internal bool OriginalsDeleted { get; private set; }
        internal string ErrorText { get; private set; }

        private readonly TextBlock _line1 = new TextBlock { FontFamily = Ui.Font("F.Body"), FontSize = 13, FontWeight = FontWeights.Medium, Foreground = Ui.Res("B.Text"), TextWrapping = TextWrapping.Wrap };
        private readonly TextBlock _line2 = new TextBlock { FontFamily = Ui.Font("F.Body"), FontSize = 12, Foreground = Ui.Res("B.TextSoft"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
        internal string SummaryText => _line1.Text + "\n" + _line2.Text;
        private TextBlock _suffix, _freeHint, _typeHint, _formatHint, _labelHint, _contentsHint, _letterHint;
        private Border _fatNote;
        private FrameworkElement _letterRow, _folderRow;

        public static void Open(string[] items)
        {
            items = (items ?? Array.Empty<string>()).Where(p => File.Exists(p) || Directory.Exists(p.TrimEnd('\\'))).ToArray();
            if (items.Length == 0) { App.Toasts.ShowNotice("Nothing to put in a disk", "The files are no longer there.", App.Toasts.Amber, 4, icon: DialogParts.DiskIcon); return; }
            _open?.Close();
            _open = new DiskDialog(items);
            _open.Show();
            _open.Activate();
        }

        private DiskDialog(string[] items) : base("Create virtual disk")
        {
            _items = items;
            Closed += OnClosed;
            _form = BuildForm();
            ShowForm();
            _building = false;
            ApplyRules();
            ElementMenu.Attach(this, null, ElementMenu.Options.Copy);
            _ = MeasureAsync();
            Recalc();
        }

        private void OnClosed(object s, EventArgs e)
        {
            if (_open == this) _open = null;
            // Closing mid-job cleans up; closing a finished job lets go of the volume exactly as it is.
            if (_session != null && _session.HelperAlive)
            {
                if (_running) { _runCts?.Cancel(); _session.Send("detach-delete"); }
                else if (FinalState == "ready" || FinalState == "kept") _session.Send("release");
            }
        }

        protected override void OnEscape()
        {
            if (View == "progress") { CancelRun(); return; }
            Close();
        }

        protected override void OnPrimary() { if (View == "form" && CreateButton.IsEnabled) StartRun(); }

        private string ItemsText => _items.Length == 1 ? "the selected item" : $"the {_items.Length} selected items";

        private void ShowForm()
        {
            var sub = $"Copy or move {_items.Length} selected item{(_items.Length == 1 ? "" : "s")}" + (_measured ? " · " + Ui.Bytes(_contentBytes) : "");
            ShowView("form", _form, 840, "Create virtual disk", sub, DialogParts.DiskIcon);
        }

        // ------------------------------------------------------------ the form

        private FrameworkElement BuildForm()
        {
            var first = _items[0].TrimEnd('\\');
            var parent = Path.GetDirectoryName(first) ?? first;
            var baseName = Directory.Exists(first) ? Path.GetFileName(first) : Path.GetFileNameWithoutExtension(first);
            if (_items.Length > 1 && Path.GetFileName(parent.TrimEnd('\\')) is { Length: > 0 } pn) baseName = pn;
            if (string.IsNullOrWhiteSpace(baseName)) baseName = "Disk";

            var note = DialogParts.Banner(DialogParts.Warn, "Administrator rights are needed. Creating and mounting a virtual disk makes Windows ask for permission (consent prompt) when you press Create. The app never answers it.", "amber");

            // left: Disk file
            var left = new StackPanel();
            left.Children.Add(DialogParts.Section(DialogParts.DiskIcon, "Disk file"));
            left.Children.Add(DialogParts.Label("Save in"));
            FolderBox = new TextBox { Style = Ui.Style("Input"), Text = parent, Height = 48, FontSize = 13, Padding = new Thickness(0, 0, 44, 0) };
            AutomationProperties.SetName(FolderBox, "Save in");
            var browse = Ui.Button("", () => PickFolder(FolderBox), "Btn.Icon", Icons.Folder); browse.Margin = new Thickness(0);
            AutomationProperties.SetName(browse, "Browse for the folder the disk file is saved in"); browse.ToolTip = "Choose the folder for the disk file.";
            left.Children.Add(new Grid { Children = { FolderBox, new Border { Child = browse, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) } } });
            _freeHint = DialogParts.Hint(" "); left.Children.Add(_freeHint);

            left.Children.Add(DialogParts.Label("Name"));
            NameBox = new TextBox { Style = Ui.Style("Input"), Text = baseName, Height = 48, FontFamily = Ui.Font("F.Body"), FontSize = 15, Padding = new Thickness(0, 0, 60, 0), MaxLength = 100 };
            AutomationProperties.SetName(NameBox, "Name");
            _suffix = new TextBlock { Text = ".vhdx", Foreground = Ui.Res("B.TextSoft"), FontFamily = Ui.Font("F.Body"), FontSize = 14, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 14, 0), IsHitTestVisible = false };
            left.Children.Add(new Grid { Children = { NameBox, _suffix } });

            left.Children.Add(DialogParts.Label("Format"));
            FormatBox = new Segmented("Format", new[] { new Choice("vhdx", "VHDX"), new Choice("vhd", "VHD") }, "vhdx");
            left.Children.Add(FormatBox);
            _formatHint = DialogParts.Hint("VHDX: up to 64 TB, safer after a power cut. VHD: older tools, up to 2,040 GB.");
            left.Children.Add(_formatHint);

            var sizeRow = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            sizeRow.ColumnDefinitions.Add(new ColumnDefinition()); sizeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) }); sizeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            SizeField = new NumberField("Size", "Typed number, 1 to 65,536.", 64, 1, 65536, 1, v => { if (!_building) { ApplyRules(); Recalc(); } }, snap: true);
            var unit = new StackPanel(); unit.Children.Add(new TextBlock { Text = "Unit", FontSize = 14, Foreground = Ui.Res("B.Text"), FontFamily = Ui.Font("F.Body"), Margin = new Thickness(0, 4, 0, 2) });
            unit.Children.Add(new Border { Height = 0 });
            SizeUnit = new Dropdown("Size unit", new[] { new Choice("GB", "GB"), new Choice("TB", "TB"), new Choice("MB", "MB") }, "GB") { Margin = new Thickness(0, 6, 0, 0) };
            unit.Children.Add(SizeUnit);
            Grid.SetColumn(unit, 2); sizeRow.Children.Add(SizeField); sizeRow.Children.Add(unit);
            left.Children.Add(sizeRow);

            left.Children.Add(DialogParts.Label("Type"));
            TypeBox = new Segmented("Type", new[] { new Choice("dynamic", "Dynamically expanding"), new Choice("fixed", "Fixed size") }, "dynamic");
            left.Children.Add(TypeBox);
            _typeHint = DialogParts.Hint(" "); left.Children.Add(_typeHint);

            left.Children.Add(DialogParts.Section(Icons.Folder, "Contents"));
            left.Children.Add(DialogParts.Label($"Put {ItemsText} in"));
            ContentsBox = new Segmented("Contents", new[] { new Choice("copy", "Copy"), new Choice("move", "Move"), new Choice("empty", "Leave empty") }, "copy");
            left.Children.Add(ContentsBox);
            _contentsHint = DialogParts.Hint("Move deletes the originals only after every file copied and matched its size, after the two-key confirmation.");
            left.Children.Add(_contentsHint);

            // right: Volume
            var right = new StackPanel();
            right.Children.Add(DialogParts.Section(DialogParts.Volume, "Volume"));
            right.Children.Add(DialogParts.Label("File system"));
            FileSystemBox = new Segmented("File system", new[] { new Choice("NTFS", "NTFS"), new Choice("exFAT", "exFAT"), new Choice("FAT32", "FAT32") }, "NTFS");
            right.Children.Add(FileSystemBox);
            _fatNote = DialogParts.Banner(DialogParts.Info, "FAT32 is off above 32 GB: Windows formats FAT32 only up to 32 GB here, and no file on it may pass 4 GB. exFAT has no permissions. NTFS keeps permissions and compression.", "info");
            _fatNote.Margin = new Thickness(0, 12, 0, 0);
            right.Children.Add(_fatNote);

            right.Children.Add(DialogParts.Label("Volume label"));
            LabelBox = new TextBox { Style = Ui.Style("Input"), Text = baseName.Length > 32 ? baseName[..32] : baseName, Height = 48, FontFamily = Ui.Font("F.Body"), FontSize = 15, MaxLength = 32 };
            AutomationProperties.SetName(LabelBox, "Volume label");
            right.Children.Add(LabelBox);
            _labelHint = DialogParts.Hint("NTFS: up to 32 characters. FAT32 and exFAT: up to 11."); right.Children.Add(_labelHint);

            right.Children.Add(DialogParts.Label("Mount as"));
            MountBox = new Segmented("Mount as", new[] { new Choice("letter", "Drive letter"), new Choice("folder", "Folder") }, "letter");
            right.Children.Add(MountBox);
            var letters = VhdEngine.FreeLetters();
            _letterRow = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            var lg = (Grid)_letterRow; lg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) }); lg.ColumnDefinitions.Add(new ColumnDefinition());
            var lstack = new StackPanel(); lstack.Children.Add(new TextBlock { Text = "Letter", FontSize = 13, Foreground = Ui.Res("B.TextSoft"), FontFamily = Ui.Font("F.Body"), Margin = new Thickness(0, 0, 0, 6) });
            LetterBox = new Dropdown("Drive letter", letters.Select(l => new Choice(l, l + ":")), letters.FirstOrDefault() ?? "");
            lstack.Children.Add(LetterBox);
            _letterHint = DialogParts.Hint("Next free letter. Folder mode needs an empty NTFS folder, such as D:\\Mounts\\Work."); _letterHint.VerticalAlignment = VerticalAlignment.Center; _letterHint.Margin = new Thickness(14, 22, 0, 0);
            Grid.SetColumn(_letterHint, 1); lg.Children.Add(lstack); lg.Children.Add(_letterHint);
            right.Children.Add(_letterRow);
            _folderRow = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 12, 0, 0) };
            MountFolderBox = new TextBox { Style = Ui.Style("Input"), Height = 48, FontSize = 13, Padding = new Thickness(0, 0, 44, 0) };
            AutomationProperties.SetName(MountFolderBox, "Empty NTFS folder to mount into");
            var browse2 = Ui.Button("", () => PickFolder(MountFolderBox), "Btn.Icon", Icons.Folder); browse2.Margin = new Thickness(0);
            AutomationProperties.SetName(browse2, "Browse for the mount folder"); browse2.ToolTip = "Choose an empty folder on an NTFS drive.";
            ((StackPanel)_folderRow).Children.Add(new Grid { Children = { MountFolderBox, new Border { Child = browse2, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) } } });
            ((StackPanel)_folderRow).Children.Add(DialogParts.Hint("The folder must exist, be empty, and be on an NTFS drive."));
            right.Children.Add(_folderRow);

            right.Children.Add(DialogParts.Section(DialogParts.Power, "After"));
            right.Children.Add(DialogParts.Label("When it is done"));
            AfterBox = new Segmented("When it is done", new[] { new Choice("keep", "Keep mounted"), new Choice("detach", "Detach") }, "keep");
            right.Children.Add(AfterBox);

            var cols = new Grid();
            cols.ColumnDefinitions.Add(new ColumnDefinition()); cols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) }); cols.ColumnDefinitions.Add(new ColumnDefinition());
            Grid.SetColumn(right, 2); cols.Children.Add(left); cols.Children.Add(right);

            var summary = new Border { Background = Ui.Res("B.Sunken"), CornerRadius = new CornerRadius(14), Padding = new Thickness(14, 12, 14, 12), Margin = new Thickness(0, 16, 0, 0) };
            var sg = new Grid(); sg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); sg.ColumnDefinitions.Add(new ColumnDefinition());
            var sicon = Ui.Icon(DialogParts.Info, Ui.Res("B.TextSoft"), 18); sicon.Margin = new Thickness(0, 1, 12, 0); sicon.VerticalAlignment = VerticalAlignment.Top;
            var st = new StackPanel(); st.Children.Add(_line1); st.Children.Add(_line2);
            AutomationProperties.SetLiveSetting(st, AutomationLiveSetting.Polite);
            Grid.SetColumn(st, 1); sg.Children.Add(sicon); sg.Children.Add(st); summary.Child = sg;

            var foot = new Grid { Margin = new Thickness(0, 14, 0, 0) };
            foot.ColumnDefinitions.Add(new ColumnDefinition()); foot.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); foot.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var keys = new TextBlock { Text = "Esc cancels · Ctrl+Enter creates", FontSize = 12, Foreground = Ui.Res("B.TextSoft"), VerticalAlignment = VerticalAlignment.Center, FontFamily = Ui.Font("F.Body") };
            var cancel = Ui.Button("Cancel", Close, "Btn.Ghost"); cancel.Margin = new Thickness(0, 0, 10, 0);
            CreateButton = Ui.Button("Create disk", StartRun, "Btn.Accent", Icons.Shield); CreateButton.Margin = new Thickness(0);
            AutomationProperties.SetHelpText(CreateButton, "Windows asks for permission when you press this. The app never answers that prompt.");
            Grid.SetColumn(cancel, 1); Grid.SetColumn(CreateButton, 2);
            foot.Children.Add(keys); foot.Children.Add(cancel); foot.Children.Add(CreateButton);

            var root = new DockPanel();
            DockPanel.SetDock(note, Dock.Top); note.Margin = new Thickness(0, 0, 0, 4);
            DockPanel.SetDock(foot, Dock.Bottom); DockPanel.SetDock(summary, Dock.Bottom);
            root.Children.Add(note); root.Children.Add(foot); root.Children.Add(summary);
            root.Children.Add(new ScrollViewer { Content = cols, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(0, 0, 8, 0), MaxHeight = Math.Max(300, SystemParameters.WorkArea.Height - 360) });

            FolderBox.TextChanged += (s, e) => { if (!_building) { ApplyRules(); Recalc(); } };
            NameBox.TextChanged += (s, e) => { if (!_building) Recalc(); };
            LabelBox.TextChanged += (s, e) => { if (!_building) Recalc(); };
            MountFolderBox.TextChanged += (s, e) => { if (!_building) Recalc(); };
            FormatBox.Changed += id => { ApplyRules(); Recalc(); };
            SizeUnit.Changed += id => { ApplyRules(); Recalc(); };
            TypeBox.Changed += id => { ApplyRules(); Recalc(); };
            FileSystemBox.Changed += id => { ApplyRules(); Recalc(); };
            ContentsBox.Changed += id => { ApplyRules(); Recalc(); };
            MountBox.Changed += id => { ApplyRules(); Recalc(); };
            LetterBox.Changed += id => Recalc(); AfterBox.Changed += id => Recalc();
            return root;
        }

        private void PickFolder(TextBox target)
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Choose a folder", InitialDirectory = Directory.Exists(target.Text) ? target.Text : null };
            if (dlg.ShowDialog(this) == true) target.Text = dlg.FolderName;
        }

        private async Task MeasureAsync()
        {
            var (bytes, largest, files) = await Task.Run(() => VhdCopy.Measure(_items));
            _contentBytes = bytes; _largest = largest; _contentFiles = files; _measured = true;
            if (View == "form") SubText.Text = $"Copy or move {_items.Length} selected item{(_items.Length == 1 ? "" : "s")} · {Ui.Bytes(bytes)}";
            ApplyRules(); Recalc();
        }

        // ------------------------------------------------------------ rules

        private long SizeMb => VhdEngine.ToMb(SizeField.Value, SizeUnit.SelectedId);

        private void ApplyRules()
        {
            if (_building) return;
            _building = true;
            try
            {
                bool vhdx = FormatBox.SelectedId == "vhdx";
                _suffix.Text = vhdx ? ".vhdx" : ".vhd";
                long mb = SizeMb;
                // unit bounds: VHD stops at 2,040 GB
                bool fatOff = mb > VhdEngine.MaxFat32Mb;
                FileSystemBox.SetDisabled("FAT32", fatOff ? $"Off above 32 GB: Windows formats FAT32 only up to 32 GB here." : (_largest > VhdEngine.Fat32FileLimit ? "A file in your selection is over 4 GB, which FAT32 cannot hold." : null));
                bool fat = FileSystemBox.SelectedId == "FAT32";
                _fatNote.Visibility = FileSystemBox.SelectedId == "FAT32" || fatOff ? Visibility.Visible : Visibility.Collapsed;
                int max = VhdEngine.MaxLabel(FileSystemBox.SelectedId);
                LabelBox.MaxLength = max;
                if (LabelBox.Text.Length > max) LabelBox.Text = LabelBox.Text[..max];
                _labelHint.Text = $"NTFS: up to 32 characters. FAT32 and exFAT: up to 11. {FileSystemBox.SelectedId} allows {max}.";
                bool folder = MountBox.SelectedId == "folder";
                _letterRow.Visibility = folder ? Visibility.Collapsed : Visibility.Visible; _folderRow.Visibility = folder ? Visibility.Visible : Visibility.Collapsed;
                var drive = DriveName(FolderBox.Text);
                _typeHint.Text = TypeBox.SelectedId == "fixed"
                    ? $"Fixed takes the full {Ui.Bytes(mb * 1048576L)} on {drive} right away."
                    : $"Starts small and grows up to {Ui.Bytes(mb * 1048576L)} as you add files. Fixed takes the full size on {drive} right away.";
                _formatHint.Text = vhdx ? "VHDX: up to 64 TB, safer after a power cut. VHD: older tools, up to 2,040 GB." : "VHD: older tools, up to 2,040 GB. VHDX goes up to 64 TB and is safer after a power cut.";
                var free = FreeOn(FolderBox.Text);
                _freeHint.Text = free.HasValue ? $"{drive} has {Ui.Bytes(free.Value)} free." : "That drive's free space cannot be read.";
                bool empty = ContentsBox.SelectedId == "empty";
                _contentsHint.Text = empty ? "The disk starts empty. Nothing is copied or moved." : ContentsBox.SelectedId == "move"
                    ? "Move deletes the originals only after every file copied and matched its size, after the two-key confirmation. They go to the Recycle Bin."
                    : "Copy leaves the originals where they are.";
            }
            finally { _building = false; }
        }

        private static string DriveName(string path)
        {
            try { return Path.GetPathRoot(Path.GetFullPath(path)).TrimEnd('\\'); } catch { return "that drive"; }
        }

        private static long? FreeOn(string path)
        {
            try { var root = Path.GetPathRoot(Path.GetFullPath(path)); return root == null || root.StartsWith(@"\\") ? null : new DriveInfo(root).AvailableFreeSpace; } catch { return null; }
        }

        // ------------------------------------------------------------ the request

        internal VhdRequest ReadRequest()
        {
            bool vhdx = FormatBox.SelectedId == "vhdx";
            var name = NameBox.Text.Trim();
            var r = new VhdRequest
            {
                File = Path.Combine(FolderBox.Text.Trim().Length == 0 ? "." : FolderBox.Text.Trim(), name + (vhdx ? ".vhdx" : ".vhd")),
                SizeMb = SizeMb, Fixed = TypeBox.SelectedId == "fixed", FileSystem = FileSystemBox.SelectedId, Label = LabelBox.Text.Trim(),
                MountMode = MountBox.SelectedId, Letter = LetterBox.SelectedId, Folder = MountFolderBox.Text.Trim()
            };
            try { r.File = Path.GetFullPath(r.File); } catch { }
            return r;
        }

        private string Check(VhdRequest r)
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text)) return "Give the disk a name.";
            if (!Directory.Exists(FolderBox.Text.Trim())) return "The folder to save in does not exist.";
            if (File.Exists(r.File)) return "A file called " + Path.GetFileName(r.File) + " is already there. Choose another name.";
            bool withContent = ContentsBox.SelectedId != "empty";
            return VhdEngine.Validate(r, FreeOn(FolderBox.Text), withContent ? _contentBytes : 0, withContent ? _largest : 0);
        }

        private void Recalc()
        {
            if (_building) return;
            var r = ReadRequest();
            var err = Check(r);
            CreateButton.IsEnabled = err == null;
            CreateButton.ToolTip = err ?? "Asks Windows for permission, then creates the disk.";
            if (err != null) { _line1.Text = err; _line2.Text = "Fix this to create the disk."; _line1.Foreground = Ui.Res("B.Amber"); return; }
            _line1.Foreground = Ui.Res("B.Text");
            var fmt = r.Vhdx ? "VHDX" : "VHD";
            var mount = r.MountMode == "letter" ? r.Letter.ToUpperInvariant() + ":" : r.Folder;
            var copy = ContentsBox.SelectedId == "empty" ? "starts empty" : $"{(ContentsBox.SelectedId == "move" ? "moves" : "copies")} {_items.Length} item{(_items.Length == 1 ? "" : "s")} ({(_measured ? Ui.Bytes(_contentBytes) : "counting…")})";
            _line1.Text = $"{fmt} · {(r.Fixed ? "fixed size" : "dynamically expanding up to")} {Ui.Bytes(r.SizeMb * 1048576L)} · {r.FileSystem}{(r.Label.Length > 0 ? " “" + r.Label + "”" : "")} · mounts as {mount} · {copy}";
            _line2.Text = r.Fixed ? $"The file takes {Ui.Bytes(r.SizeMb * 1048576L)} on {DriveName(r.File)} at once. GPT partition, quick format." : $"The file starts at about 50 MB on {DriveName(r.File)}. GPT partition, quick format.";
        }

        // ------------------------------------------------------------ run

        private List<(string Id, FrameworkElement Host, string Text)> _steps;
        private string _stepNow = "permission";
        private TextBlock _copyBytes, _copyFile;
        private System.Windows.Controls.ProgressBar _bar;
        private TextBlock _stepFormat, _stepCopy;

        internal void PressCreate() => StartRun();

        private void StartRun()
        {
            if (!CreateButton.IsEnabled) return;
            var r = ReadRequest();
            var err = Check(r);
            if (err != null) { _line1.Text = err; return; }
            _running = true; FinalState = "running"; ErrorText = null;
            _runCts = new CancellationTokenSource();
            _session = new VhdSession(r);
            ShowProgress(r);
            _ = RunAsync(r, _session, _runCts);
        }

        private bool Cancelled => _runCts.IsCancellationRequested;

        private async Task RunAsync(VhdRequest r, VhdSession session, CancellationTokenSource cts)
        {
            var ct = cts.Token;
            try
            {
                if (!await session.StartAsync())
                {
                    ShowError(session.Declined ? "The Windows consent prompt was declined or timed out. No file was created and nothing was attached." : "The helper could not start: " + session.LaunchError,
                        $"exit code {session.ExitCode}" + (session.Declined ? " · cancelled by the user" : ""), r);
                    return;
                }
                SetStep("permission", true);
                var ready = await session.WaitForAsync(s => s.Phase == "ready" || s.Phase == "error", TimeSpan.FromMinutes(20), CancellationToken.None, s =>
                {
                    if (s.Phase == "attached") SetStep("created", true);
                    if (s.Phase == "formatted" || s.Phase == "ready") { SetStep("created", true); SetStep("format", true); }
                });
                if (ready == null || ready.Phase != "ready")
                {
                    var why = ready?.Error ?? "The helper did not finish.";
                    ShowError(why + " Nothing was left behind.", ready == null ? "no status" : $"exit code {ready.ExitCode}\n{ready.Log}", r);
                    return;
                }
                var mount = ready.Mount;
                if (Cancelled) { await CleanUpAsync(session); ShowCancelled(); return; }

                CopyResult copied = null;
                if (ContentsBox.SelectedId != "empty")
                {
                    SetStep("copy", false);
                    var progress = new Progress<CopyProgress>(p =>
                    {
                        if (View != "progress" || _bar == null) return;
                        _bar.Value = p.Total > 0 ? Math.Min(1000, 1000.0 * p.Done / p.Total) : 1000;
                        _copyBytes.Text = $"{Ui.Bytes(p.Done)} of {Ui.Bytes(p.Total)}" + (p.BytesPerSecond > 0 ? $" · {Ui.Bytes((long)p.BytesPerSecond)}/s" : "");
                        _copyFile.Text = p.Current;
                    });
                    copied = await VhdCopy.RunAsync(_items, mount, progress, ct);
                    LastCopy = copied;
                    if (Cancelled) { await CleanUpAsync(session); ShowCancelled(); return; }
                    if (!copied.Ok) { await CleanUpAsync(session); ShowError("Copying stopped: " + copied.Error + " The disk was detached and deleted.", "", r); return; }
                    SetStep("copy", true);
                }
                bool deletedOriginals = false;
                if (copied != null && ContentsBox.SelectedId == "move")
                    deletedOriginals = await ConfirmAndDeleteOriginalsAsync();
                OriginalsDeleted = deletedOriginals;

                SetStep("finish", false);
                string state = "Mounted";
                if (AfterBox.SelectedId == "detach")
                {
                    var d = await session.DetachAsync(false);
                    if (d != null && d.Phase == "detached") state = "Detached"; else { ShowError("The disk could not be detached: " + d?.Error, "", r); return; }
                    FinalState = "detached";
                }
                else FinalState = "ready";
                _running = false;
                ShowDone(r, mount, copied, state, deletedOriginals);
            }
            catch (Exception ex)
            {
                try { await CleanUpAsync(session); } catch { }
                ShowError(ex.Message + " Nothing was left behind.", ex.GetType().Name, r);
            }
        }

        private async Task CleanUpAsync(VhdSession s)
        {
            // Every failure cleans up after itself: detach, delete the file this run made.
            await s.DetachAsync(true);
        }

        private Task<bool> ConfirmAndDeleteOriginalsAsync()
        {
            var tcs = new TaskCompletionSource<bool>();
            var names = _items.Select(p => Path.GetFileName(p.TrimEnd('\\')) is { Length: > 0 } n ? n : p).ToList();
            var shown = names.Count <= 3 ? string.Join(", ", names.Take(names.Count - 1)) + (names.Count > 1 ? " and " : "") + names.Last() : string.Join(", ", names.Take(2)) + $" and {names.Count - 2} more";
            var ask = new ConfirmWindow($"Delete the {(_items.Length == 1 ? "original" : _items.Length + " originals")}?", $"Every file copied onto the disk and matched its size. {shown} move{(_items.Length == 1 ? "s" : "")} to the Recycle Bin. The copy on the disk stays.", "Delete originals", danger: true) { Owner = this };
            bool confirmed = false;
            ask.Confirmed += async _ =>
            {
                confirmed = true;
                var res = await FileOps.RecycleAsync(_items);
                tcs.TrySetResult(res.All(x => x.Ok));
            };
            ask.Closed += (s, e) => { if (!confirmed) tcs.TrySetResult(false); };
            ask.Show();
            return tcs.Task;
        }

        private void CancelRun()
        {
            if (!_running) { Close(); return; }
            _runCts?.Cancel();
            SubText.Text = "Cancelling… the disk is detached and deleted as soon as it is ready.";
        }
        internal void PressCancel() => CancelRun();

        // ------------------------------------------------------------ views

        private void ShowProgress(VhdRequest r)
        {
            var root = new StackPanel();
            _steps = new List<(string, FrameworkElement, string)>();
            string Mount() => r.MountMode == "letter" ? r.Letter.ToUpperInvariant() + ":" : r.Folder;
            var defs = new List<(string Id, string Text)>
            {
                ("permission", "Permission given"), ("created", "Created the file and attached it"), ("format", $"Formatted {r.FileSystem} as {Mount()}")
            };
            if (ContentsBox.SelectedId != "empty") defs.Add(("copy", $"Copying {_items.Length} item{(_items.Length == 1 ? "" : "s")}"));
            defs.Add(("finish", "Finish"));
            var list = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
            foreach (var (id, text) in defs)
            {
                var host = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 5) };
                list.Children.Add(host); _steps.Add((id, host, text));
            }
            root.Children.Add(list);
            _bar = DialogParts.Bar(); _bar.Margin = new Thickness(0, 12, 0, 0); _bar.Visibility = Visibility.Visible;
            AutomationProperties.SetName(_bar, "Copy progress");
            var nums = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            nums.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); nums.ColumnDefinitions.Add(new ColumnDefinition());
            _copyBytes = new TextBlock { FontSize = 13, Foreground = Ui.Res("B.Text"), FontFamily = Ui.Font("F.Body") };
            _copyFile = new TextBlock { FontSize = 12, Foreground = Ui.Res("B.TextSoft"), FontFamily = Ui.Font("F.Mono"), HorizontalAlignment = HorizontalAlignment.Right, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(16, 0, 0, 0) };
            Grid.SetColumn(_copyFile, 1); nums.Children.Add(_copyBytes); nums.Children.Add(_copyFile);
            if (ContentsBox.SelectedId != "empty") { root.Children.Add(_bar); root.Children.Add(nums); }
            _stepNow = "permission"; _done = new HashSet<string>(); PaintSteps();
            var cancel = Ui.Button("Cancel", CancelRun, "Btn.Ghost", Icons.Close); cancel.Margin = new Thickness(0); cancel.HorizontalAlignment = HorizontalAlignment.Right;
            AutomationProperties.SetName(cancel, "Cancel, detach and delete what was made");
            cancel.ToolTip = "Stops, detaches the disk and deletes the file that was made (Esc).";
            root.Children.Add(new Border { Margin = new Thickness(0, 18, 0, 0), BorderThickness = new Thickness(0, 1, 0, 0), BorderBrush = Ui.Res("B.Line"), Padding = new Thickness(0, 14, 0, 0), Child = cancel });
            ShowView("progress", root, 456, "Creating " + Path.GetFileName(r.File), PathDisplay.Shown(Path.GetDirectoryName(r.File)), DialogParts.DiskIcon);
            cancel.Focus();
        }

        private HashSet<string> _done = new HashSet<string>();

        private void SetStep(string id, bool done)
        {
            Dispatcher.Invoke(() =>
            {
                if (done) _done.Add(id);
                var idx = _steps?.FindIndex(s => s.Id == id) ?? -1;
                if (idx >= 0)
                {
                    // everything before a finished or running step is finished too
                    for (int i = 0; i < idx; i++) _done.Add(_steps[i].Id);
                    _stepNow = done ? (idx + 1 < _steps.Count ? _steps[idx + 1].Id : "") : id;
                }
                PaintSteps();
            });
        }

        private void PaintSteps()
        {
            if (_steps == null) return;
            foreach (var (id, host, text) in _steps)
            {
                var panel = (StackPanel)host; panel.Children.Clear();
                bool done = _done.Contains(id); bool current = !done && id == _stepNow;
                FrameworkElement glyph = done ? Ui.Icon(Icons.Check, Ui.Res("B.Mint"), 14, 2.4)
                    : current ? new System.Windows.Shapes.Ellipse { Width = 10, Height = 10, Fill = Ui.Res("B.Mint") }
                    : new System.Windows.Shapes.Ellipse { Width = 12, Height = 12, Stroke = Ui.Res("B.LineStrong"), StrokeThickness = 1.6 };
                glyph.VerticalAlignment = VerticalAlignment.Center; glyph.Margin = new Thickness(0, 0, 12, 0);
                if (glyph is System.Windows.Shapes.Ellipse e && current) { e.Margin = new Thickness(2, 0, 14, 0); }
                panel.Children.Add(glyph);
                panel.Children.Add(new TextBlock { Text = text, FontFamily = Ui.Font("F.Body"), FontSize = 14, FontWeight = current ? FontWeights.Medium : FontWeights.Normal, Foreground = done || current ? Ui.Res("B.Text") : Ui.Res("B.TextSoft") });
                AutomationProperties.SetName(panel, text + (done ? ", done" : current ? ", in progress" : ", waiting"));
            }
        }

        private void ShowCancelled()
        {
            _running = false; FinalState = "cancelled";
            ShowForm();
            _line1.Text = "Cancelled. The disk was detached and its file deleted."; _line2.Text = "Nothing was left behind. Your settings are still here."; _line1.Foreground = Ui.Res("B.Text");
        }

        private void ShowError(string message, string details, VhdRequest r)
        {
            _running = false; FinalState = "error"; ErrorText = message;
            var root = new StackPanel();
            root.Children.Add(DialogParts.Banner(DialogParts.Warn, message, "danger"));
            if (!string.IsNullOrWhiteSpace(details))
            {
                var box = new TextBlock { Text = details, FontFamily = Ui.Font("F.Mono"), FontSize = 12, Foreground = Ui.Res("B.TextSoft"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed };
                var toggle = new Button { Style = Ui.Style("Btn.Chip"), Margin = new Thickness(0, 12, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(10, 0, 12, 0), Height = 30, Content = new TextBlock { Text = "Details" } };
                toggle.Click += (s, e) => box.Visibility = box.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
                AutomationProperties.SetName(toggle, "Show or hide the details");
                root.Children.Add(toggle); root.Children.Add(box);
            }
            var close = Ui.Button("Close", Close, "Btn.Ghost"); close.Margin = new Thickness(0, 0, 10, 0);
            var again = Ui.Button("Try again", () => { ShowForm(); Recalc(); }, "Btn.Accent", Icons.Restart); again.Margin = new Thickness(0);
            root.Children.Add(new Border { Margin = new Thickness(0, 14, 0, 0), BorderThickness = new Thickness(0, 1, 0, 0), BorderBrush = Ui.Res("B.Line"), Padding = new Thickness(0, 14, 0, 0), Child = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { close, again } } });
            ShowView("error", root, 456, "Disk not created", "Nothing was left behind", Icons.Close, Ui.Res("B.Danger"), Ui.Res("B.DangerWash"));
            again.Focus();
        }

        private void ShowDone(VhdRequest r, string mount, CopyResult copied, string state, bool deletedOriginals)
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
            TextBlock T(string t, bool mono = false) => new TextBlock { Text = t, FontSize = mono ? 12.5 : 13, Foreground = Ui.Res("B.Text"), FontFamily = Ui.Font(mono ? "F.Mono" : "F.Body"), TextWrapping = TextWrapping.Wrap };
            Row("File", T(r.File, true));
            long onDisk = 0; try { onDisk = new FileInfo(r.File).Length; } catch { }
            Row("Size on " + DriveName(r.File), T(Ui.Bytes(onDisk) + (r.Fixed ? "" : ", grows as needed")));
            if (copied != null) Row("Copied", T($"{_items.Length} item{(_items.Length == 1 ? "" : "s")}, {Ui.Bytes(copied.Bytes)}" + (ContentsBox.SelectedId == "move" ? (deletedOriginals ? " · originals are in the Recycle Bin" : " · originals kept") : "")));
            Row("State", DialogParts.Pill(state, state == "Mounted" ? "mint" : "neutral"));
            root.Children.Add(g);
            if (state == "Detached") root.Children.Add(DialogParts.Hint("To use it again, double-click the file in Explorer: Windows asks for permission and mounts it."));
            var detach = new Button { Style = Ui.Style("Btn.Chip"), Content = "Detach", Height = 40, Padding = new Thickness(16, 0, 16, 0), BorderBrush = Brushes.Transparent, Foreground = Ui.Res("B.Mint"), Visibility = state == "Mounted" ? Visibility.Visible : Visibility.Collapsed };
            AutomationProperties.SetName(detach, "Detach the disk"); detach.ToolTip = "Detaches the disk. The file stays; double-click it to mount it again.";
            detach.Click += async (s, e) =>
            {
                detach.IsEnabled = false;
                var d = await _session.DetachAsync(false);
                bool ok = d != null && d.Phase == "detached";
                FinalState = ok ? "detached" : "error";
                if (ok) { ShowDone(r, mount, copied, "Detached", deletedOriginals); }
                else { detach.IsEnabled = true; App.Toasts?.ShowNotice("Not detached", d?.Error ?? "The helper is no longer running. Detach it in Disk Management.", App.Toasts.Amber, 6, icon: DialogParts.DiskIcon); }
            };
            var showFile = Ui.Button("Show file", () => { try { Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{r.File}\"") { UseShellExecute = true }); } catch { } }, "Btn.Ghost", Icons.Folder);
            var open = Ui.Button("Open " + (r.MountMode == "letter" ? r.Letter.ToUpperInvariant() + ":" : "the folder"), () => { try { Process.Start(new ProcessStartInfo("explorer.exe", $"\"{mount}\"") { UseShellExecute = true }); } catch { } }, "Btn.Accent", "M14 4h6v6M20 4l-9 9M18 14v5a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V7a1 1 0 0 1 1-1h5");
            open.IsEnabled = state == "Mounted";
            detach.Margin = new Thickness(0); showFile.Margin = new Thickness(0, 0, 10, 0); open.Margin = new Thickness(0);
            var foot = new Grid { Margin = new Thickness(0, 14, 0, 0) };
            foot.ColumnDefinitions.Add(new ColumnDefinition()); foot.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); foot.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(showFile, 1); Grid.SetColumn(open, 2);
            foot.Children.Add(detach); foot.Children.Add(showFile); foot.Children.Add(open);
            root.Children.Add(new Border { Margin = new Thickness(0, 14, 0, 0), BorderThickness = new Thickness(0, 1, 0, 0), BorderBrush = Ui.Res("B.Line"), Padding = new Thickness(0, 14, 0, 0), Child = foot });
            ShowView("done", root, 456, "Disk ready", $"{(r.MountMode == "letter" ? r.Letter.ToUpperInvariant() + ":" : PathDisplay.Shown(r.Folder))} · {Ui.Bytes(r.SizeMb * 1048576L)} · {r.FileSystem}" + (r.Label.Length > 0 ? " “" + r.Label + "”" : ""), Icons.Check);
            (state == "Mounted" ? open : showFile).Focus();
            FinalState = state == "Mounted" ? "ready" : "detached";
        }

        // ------------------------------------------------------------ test seams

        internal async Task<bool> WaitForViewAsync(string view, int ms)
        {
            var until = DateTime.UtcNow.AddMilliseconds(ms);
            while (DateTime.UtcNow < until) { if (View == view) return true; await Task.Delay(50); }
            return View == view;
        }
        internal async Task<bool> WaitForMeasureAsync(int ms)
        {
            var until = DateTime.UtcNow.AddMilliseconds(ms);
            while (DateTime.UtcNow < until) { if (_measured) return true; await Task.Delay(50); }
            return _measured;
        }
        internal bool CreateEnabled => CreateButton.IsEnabled;
        internal void ApplyForTest() { ApplyRules(); Recalc(); }
        // Capture-only views, filled with fixed values (see ArchiveDialog.PreviewProgress).
        internal void PreviewProgress()
        {
            var r = ReadRequest();
            ShowProgress(r);
            SetStep("permission", true); SetStep("created", true); SetStep("format", true); SetStep("copy", false);
            if (_bar != null) { _bar.Value = 340; _copyBytes.Text = "16.4 MB of 48.2 MB · 38 MB/s"; _copyFile.Text = "photos\\IMG_2041.jpg"; }
        }
        internal void PreviewDone()
        {
            var r = ReadRequest();
            ShowDone(r, VhdEngine.MountRoot(r.MountMode == "letter" && r.Letter != null ? r : new VhdRequest { MountMode = "letter", Letter = "V" }), new CopyResult { Ok = true, Bytes = 50_500_000, Files = 3 }, "Mounted", false);
        }
        internal void PreviewError() => ShowError("The Windows consent prompt was declined or timed out. No file was created and nothing was attached.", "exit code 1223 · cancelled by the user", ReadRequest());
        internal void PreviewForm() { ShowForm(); Recalc(); }
        internal void SetSizeForTest(double value, string unit) { SizeUnit.Select(unit); SizeField.SetValue(value); ApplyRules(); Recalc(); }
    }
}
