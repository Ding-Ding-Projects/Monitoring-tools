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

namespace MonitoringTools.UI.Dialogs
{
    public enum DiscState { Ready, Mounting, Mounted, CouldNotMount, Extracting, Extracted }

    /// <summary>
    /// "Disc image" (design/file-tools, board 15). Browses an .iso with the app's own reader, mounts it read-only as a drive for this
    /// user (no elevation), extracts chosen files without mounting, and ejects. Closing the dialog never ejects. A mount that Windows
    /// refuses shows the reason and still offers browsing and extracting.
    /// </summary>
    public sealed class DiscImageDialog : FileToolDialog
    {
        private DiscImageTools.IsoVolume _volume;
        private string _browseError;
        private readonly CheckTree _tree;
        private string _drive;
        private string _destText;
        private TextBox _dest;
        private string _mountError;
        private CancellationTokenSource _cts;
        private ExtractResult _result;

        public DiscState State { get; private set; }
        public string Drive => _drive;
        internal CheckTree Tree => _tree;
        internal ExtractResult Result => _result;
        internal Task LastRun { get; private set; } = Task.CompletedTask;
        internal bool Browsable => _volume != null;

        private static readonly List<DiscImageDialog> OpenList = new List<DiscImageDialog>();
        internal static DiscImageDialog Current => OpenList.LastOrDefault();

        public static DiscImageDialog Open(string file)
        {
            if (!File.Exists(file)) { App.Toasts.ShowNotice("Nothing to open", "The file is no longer there.", App.Toasts.Amber, 4, icon: DocIcons.Disc); return null; }
            var d = new DiscImageDialog(file);
            d.Show(); d.Activate();
            return d;
        }

        private DiscImageDialog(string file) : base("Disc image", file, DocIcons.Disc, 780)
        {
            OpenList.Add(this);
            Closed += (s, e) => { OpenList.Remove(this); _cts?.Cancel(); };
            _tree = new CheckTree("Disc image contents", checkFolders: false, maxHeight: 240);
            _destText = Path.Combine(Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file));
            if (DiscImageTools.CanBrowse(file))
            {
                try
                {
                    _volume = DiscImageTools.ReadIso(file);
                    _tree.SetItems(_volume.Entries.Select(e => new TreeItem { Path = e.Path, IsDirectory = e.IsDirectory, Size = e.Size, Modified = e.Modified, Tag = e }), selectAll: false);
                }
                catch (Exception ex) { _browseError = ex.Message; }
            }
            _tree.Changed += UpdateButtons;
            ShowReady();
            // an image that is already attached (mounted earlier, here or elsewhere) shows as mounted
            var path = file;
            Task.Run(() => DiscImageTools.AttachedDrive(path)).ContinueWith(t =>
            {
                try { Dispatcher.Invoke(() => { if (State == DiscState.Ready && t.Result != null) { _drive = t.Result; ShowMounted(); } }); } catch { }
            });
        }

        protected override bool EnterRunsPrimary => State != DiscState.Mounting && State != DiscState.Extracting;

        protected override void OnEscape() { if (State == DiscState.Mounting || State == DiscState.Extracting) _cts?.Cancel(); Close(); }

        private string Kind => Path.GetExtension(FilePath).TrimStart('.').ToUpperInvariant();

        private string SummaryLine()
        {
            var size = Ui.Bytes(new FileInfo(FilePath).Length);
            if (_volume != null) return $"ISO 9660 · {(string.IsNullOrWhiteSpace(_volume.Label) ? "no label" : _volume.Label)} · {size}";
            return $"{Kind} · {size}";
        }

        // ------------------------------------------------------------ pieces

        private UIElement StateBadge(string text, bool on)
        {
            var b = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(9, 2, 9, 3), Background = on ? Ui.Res("B.MintWash") : Ui.Res("B.TertiaryContainer"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0),
                Child = new TextBlock { Text = text, FontSize = 12, FontFamily = Ui.Font("F.Body"), Foreground = on ? Ui.Res("B.OnPrimaryContainer") : Ui.Res("B.OnTertiaryContainer") } };
            AutomationProperties.SetName(b, text);
            return b;
        }

        private UIElement ContentArea()
        {
            if (_volume != null) return _tree;
            var why = _browseError != null ? "The file list could not be read here: " + _browseError + " You can still try Mount as a drive."
                : "The contents of a " + Kind + " image cannot be listed without mounting it. Mount it as a drive to see them.";
            return Banner(BannerKind.Info, "No file list for this image", why);
        }

        private UIElement DestinationRow()
        {
            _dest = Field("Extract to", _destText);
            _dest.TextChanged += (s, e) => _destText = _dest.Text;
            var choose = Ui.Button("Choose folder", () =>
            {
                var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Choose where the files go" };
                if (dlg.ShowDialog(this) == true) { _destText = dlg.FolderName; _dest.Text = _destText; }
            }, "Btn.Ghost", Icons.Folder);
            choose.Margin = new Thickness(8, 0, 0, 0);
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(choose, 1); g.Children.Add(_dest); g.Children.Add(choose);
            var host = new StackPanel(); host.Children.Add(FieldLabel("Extract to")); host.Children.Add(g);
            return host;
        }

        private Button _extractBtn;
        private Button ExtractButton()
        {
            _extractBtn = Ui.Button("Extract selected", StartExtract, "Btn.Chip", DocIcons.Down);
            _extractBtn.Height = 40; _extractBtn.Padding = new Thickness(16, 0, 16, 0);
            _extractBtn.Margin = new Thickness(0, 0, 8, 0);
            _extractBtn.IsEnabled = false;
            return _extractBtn;
        }

        private void UpdateButtons()
        {
            if (_extractBtn == null) return;
            _extractBtn.IsEnabled = _tree.SelectedCount > 0 && (State == DiscState.Ready || State == DiscState.Mounted || State == DiscState.CouldNotMount);
            if (State == DiscState.Ready || State == DiscState.Mounted)
                SetStatus(State == DiscState.Mounted ? "Leaving this window open does not unmount it." : _tree.SelectedCount == 0 ? "Pick files to extract, or mount the image as a drive." : (_tree.SelectedCount == 1 ? "1 file selected" : $"{_tree.SelectedCount} files selected") + " · " + Ui.Bytes(_tree.SelectedBytes));
        }

        private UIElement ButtonRow(params Button[] buttons)
        {
            var p = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            foreach (var b in buttons) { b.Height = 40; p.Children.Add(b); }
            return p;
        }

        // ------------------------------------------------------------ states

        private void ShowReady()
        {
            State = DiscState.Ready;
            SetSummary(SummaryLine());
            var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            head.Children.Add(StateBadge("Not mounted", false));
            head.Children.Add(Text("Mounting adds a read-only drive. The image file is never changed.", 13, Ui.Res("B.TextSoft")));
            var mount = Ui.Button("Mount as a drive", StartMount, "Btn.Accent", DocIcons.Disc);
            SetBody(head, ContentArea(), ButtonRow(mount, ExtractButton()), DestinationRow());
            var close = Ui.Button("Close", Close, "Btn.Ghost");
            SetFooter("", null, new[] { close }, null);
            UpdateButtons();
        }

        private void StartMount()
        {
            State = DiscState.Mounting;
            SetSummary("mounting");
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            var top = new StackPanel { Orientation = Orientation.Horizontal };
            top.Children.Add(new TextBlock { Text = "Mounting", FontSize = 22, FontFamily = Ui.Font("F.Body"), Foreground = Ui.Res("B.Text"), VerticalAlignment = VerticalAlignment.Bottom });
            var sub = Text("Attaching the image as a drive", 13, Ui.Res("B.TextSoft")); sub.Margin = new Thickness(12, 0, 0, 3); sub.VerticalAlignment = VerticalAlignment.Bottom; top.Children.Add(sub);
            var card = new StackPanel(); card.Children.Add(top); card.Children.Add(Progress("Mounting the image", true));
            card.Children.Add(Text("Asking Windows to attach the image. This usually takes a second or two.", 13, Ui.Res("B.TextSoft")));
            var cancel = Ui.Button("Cancel", () => _cts?.Cancel(), "Btn.Ghost");
            SetBody(Card(card));
            SetFooter("", null, new[] { cancel }, null);
            var path = FilePath;
            LastRun = Task.Run(() => DiscImageTools.Mount(path, token)).ContinueWith(t =>
            {
                try
                {
                    Dispatcher.Invoke(() =>
                    {
                        var r = t.IsFaulted ? new MountResult { Error = t.Exception?.GetBaseException().Message } : t.Result;
                        if (r.Canceled) { ShowReady(); SetStatus("Mounting was canceled. Nothing is attached."); }
                        else if (r.Ok) { _drive = r.Drive; ShowMounted(); }
                        else { _mountError = r.Error; ShowCouldNotMount(); }
                    });
                }
                catch { }
            });
        }

        private void ShowMounted()
        {
            State = DiscState.Mounted;
            SetSummary("mounted as " + _drive);
            var open = Ui.Button($"Open drive {_drive}", () => ActionProvider.Shell(_drive + "\\"), "Btn.Accent", Icons.Folder);
            var eject = Ui.Button("Eject", Eject, "Btn.Chip", DocIcons.Eject); eject.Height = 40; eject.Padding = new Thickness(16, 0, 16, 0);
            var banner = Banner(BannerKind.Ok, $"Mounted as drive {_drive}", "Read-only. Eject when you are done so the image file can be moved or deleted.");
            SetBody(banner, ContentArea(), ButtonRow(open, ExtractButton(), eject), DestinationRow());
            var close = Ui.Button("Close", Close, "Btn.Accent");
            SetFooter("Leaving this window open does not unmount it.", null, new[] { close }, close);
            UpdateButtons();
        }

        private void ShowCouldNotMount()
        {
            State = DiscState.CouldNotMount;
            SetSummary("could not mount");
            var reason = _mountError ?? "Windows did not say why.";
            var browse = Ui.Button("Browse without mounting", ShowReady, "Btn.Accent", Icons.Folder);
            browse.IsEnabled = true;
            var copy = Ui.Button("Copy details", () => { try { Clipboard.SetText(reason); } catch { } }, "Btn.Ghost", DocIcons.Copy);
            var card = new StackPanel();
            card.Children.Add(Text("WHAT WAS NOT CHANGED", 12, Ui.Res("B.Mint"), FontWeights.Medium));
            card.Children.Add(Text("Nothing was changed. You can still browse and extract the contents without mounting, or copy the file to a local disk first.", 13));
            var row = new StackPanel { Orientation = Orientation.Horizontal }; row.Children.Add(browse); row.Children.Add(copy);
            SetBody(Banner(BannerKind.Bad, "Windows could not mount this image", reason + " The file may be on a network share that does not allow mounting, or it may not be a valid disc image."), Card(card), row);
            SetFooterClose("Nothing is attached.");
        }

        private void Eject()
        {
            var err = DiscImageTools.Dismount(FilePath);
            if (err != null) { SetStatus("Could not eject: " + err); return; }
            _drive = null;
            ShowReady();
            SetStatus("Ejected. The drive is gone and the image file can be moved or deleted.");
        }

        // ------------------------------------------------------------ extraction

        private void StartExtract()
        {
            var picked = _tree.Selected.Select(i => (IsoEntry)i.Tag).ToList();
            if (picked.Count == 0) return;
            Run(picked);
        }

        internal void ExtractForTest(string folder) { _destText = folder; if (_dest != null) _dest.Text = folder; Run(_tree.Selected.Select(i => (IsoEntry)i.Tag).ToList()); }
        internal void MountForTest() => StartMount();
        internal void EjectForTest() => Eject();

        private void Run(List<IsoEntry> picked)
        {
            var dest = (_destText ?? "").Trim();
            if (dest.Length == 0) { SetStatus("Choose a folder to extract into."); return; }
            try { dest = Path.GetFullPath(dest); } catch { SetStatus("That folder name is not valid."); return; }
            var before = State;
            State = DiscState.Extracting;
            SetSummary("extracting");
            _cts = new CancellationTokenSource();
            var big = new TextBlock { Text = "0%", FontSize = 36, FontFamily = Ui.Font("F.Body"), Foreground = Ui.Res("B.Text") };
            var fileOf = Text("", 13, Ui.Res("B.TextSoft")); fileOf.Margin = new Thickness(12, 0, 0, 6); fileOf.VerticalAlignment = VerticalAlignment.Bottom;
            var bar = Progress("Extraction progress", false);
            var cur = Text("", 13, Ui.Res("B.TextSoft"), null, TextWrapping.NoWrap); cur.TextTrimming = TextTrimming.CharacterEllipsis;
            var top = new StackPanel { Orientation = Orientation.Horizontal }; top.Children.Add(big); top.Children.Add(fileOf);
            var card = new StackPanel(); card.Children.Add(top); card.Children.Add(bar); card.Children.Add(cur);
            var cancel = Ui.Button("Cancel", () => _cts?.Cancel(), "Btn.Ghost");
            SetBody(Card(card));
            SetFooter("Cancel stops now; files already copied are kept.", null, new[] { cancel }, null);
            var progress = new Progress<ExtractProgress>(p =>
            {
                if (State != DiscState.Extracting) return;
                double f = p.TotalBytes > 0 ? (double)p.Bytes / p.TotalBytes : 0;
                big.Text = $"{(int)Math.Min(100, f * 100)}%"; bar.Value = Math.Min(100, f * 100);
                fileOf.Text = $"File {Math.Max(1, p.Done)} of {p.Total}";
                cur.Text = $"{p.Current} → {PathDisplay.Shown(dest)} · {Ui.Bytes(p.Bytes)} of {Ui.Bytes(p.TotalBytes)}";
            });
            var token = _cts.Token; var file = FilePath;
            LastRun = Task.Run(() => DiscImageTools.Extract(file, picked, dest, progress, token)).ContinueWith(t =>
            {
                try { Dispatcher.Invoke(() => ShowExtracted(t.IsFaulted ? new ExtractResult { Error = t.Exception?.GetBaseException().Message, Destination = dest } : t.Result)); } catch { }
            });
        }

        private void ShowExtracted(ExtractResult r)
        {
            _result = r; State = DiscState.Extracted;
            SetSummary(r.Canceled || r.Error != null ? "stopped" : "done");
            var parts = new List<UIElement>();
            if (r.Error != null) parts.Add(Banner(BannerKind.Bad, "Extraction stopped", r.Error + " Files already copied are kept."));
            else if (r.Canceled) parts.Add(Banner(BannerKind.Warn, r.Extracted == 1 ? "Stopped after 1 file" : $"Stopped after {r.Extracted} files", "Files already copied are kept; the one in progress was removed."));
            else parts.Add(Banner(BannerKind.Ok, r.Extracted == 1 ? $"Extracted 1 file, {Ui.Bytes(r.Bytes)}" : $"Extracted {r.Extracted} files, {Ui.Bytes(r.Bytes)}", r.Renamed.Count > 0 ? "A name that already existed got a number instead of being overwritten." : "Every selected file was copied out of the image."));
            var open = Ui.Button("Open folder", () => ActionProvider.Shell(r.Destination), "Btn.Ghost", Icons.Folder);
            var back = Ui.Button("Back to the image", () => { if (_drive != null) ShowMounted(); else ShowReady(); }, "Btn.Ghost");
            var row = new StackPanel { Orientation = Orientation.Horizontal }; row.Children.Add(open); row.Children.Add(back);
            parts.Add(row);
            SetBody(parts.ToArray());
            SetFooterClose("The image file was not changed.");
        }
    }
}
