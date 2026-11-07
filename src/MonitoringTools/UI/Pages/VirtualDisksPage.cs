using System;
using System.Collections.Generic;
using System.Diagnostics;
using Activity = MonitoringTools.Core.Activity;
using ActivityKind = MonitoringTools.Core.ActivityKind;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using MonitoringTools.Core;
using MonitoringTools.Core.Disks;
using MonitoringTools.UI.Dialogs;
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI
{
    public partial class ControlCentreWindow
    {
        // -------------------------------------------------------------- state kept while the window is open

        private string _vdSelected;
        private string _vdAction;          // which action panel is open in the detail card
        private string _vdFilter = "all";
        private bool _vdBusy;
        private string _vdResult, _vdResultBrush;
        private static readonly Dictionary<string, (VDiskInfo Info, DateTime At)> VdInfo = new Dictionary<string, (VDiskInfo, DateTime)>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> VdVolumes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static Task _vdReading;

        /// <summary>What this computer offers. Hyper-V is null when its commands are there, otherwise the reason and the remedy.</summary>
        private sealed class VdRoutes { public bool Storage, DiskPart; public string HyperV; }
        private static VdRoutes _vdRoutes;
        private static Task _vdProbing;

        private FrameworkElement VirtualDisksPage()
        {
            var root = PageRoot("Virtual disks", "Virtual disks",
                "VHD, VHDX and VHD Set files, and ISO images. Every action says which way it ran and can copy the same steps as a script. Nothing here needs Hyper-V.");
            if (!OperatingSystem.IsWindows() || !VirtDisk.Available)
            {
                var sp = new StackPanel();
                sp.Children.Add(CardTitle("This needs Windows", "Virtual disks are managed through the Windows virtual disk service, which Windows 8 and later have on every edition. The feature article in Help and docs explains everything this page does."));
                root.Children.Add(Card(sp));
                return root;
            }
            root.Children.Add(VdRoutesRow());
            root.Children.Add(VdQuickPackCard());
            root.Children.Add(VdLibraryCard());
            if (_vdSelected != null) root.Children.Add(VdDetailCard(_vdSelected));
            root.Children.Add(VdCreateCard());
            return root;
        }

        private void VdRefresh() { if (_current == "vdisks") Navigate("vdisks", animate: false); }

        /// <summary>For the self-test and the captures: shows the page with one disk chosen and one action panel open (or none).</summary>
        internal void VdTestShow(string selected, string action)
        {
            _vdSelected = selected; _vdAction = action;
            Navigate("vdisks", animate: false);
        }

        /// <summary>For the self-test and the captures: true once each of these disks has been read.</summary>
        internal static bool VdTestRead(IEnumerable<string> paths) => paths.All(p => VdGet(p) != null);

        /// <summary>For the self-test and the captures: true once the volumes of this attached disk have been read.</summary>
        internal static bool VdTestVolumesRead(string path) { lock (VdVolumes) return VdVolumes.ContainsKey(path); }

        /// <summary>For the captures: starts the New virtual disk card from these settings.</summary>
        internal void VdTestNew(DiskJob job) { _vdNew = job; Navigate("vdisks", animate: false); }

        private void VdSay(string text, string brush) { _vdResult = text; _vdResultBrush = brush; }

        // -------------------------------------------------------------- the ways this computer can work with disks

        private FrameworkElement VdRoutesRow()
        {
            if (_vdRoutes == null && _vdProbing == null)
                _vdProbing = Task.Run(ProbeRoutes).ContinueWith(t => Dispatcher.BeginInvoke(new Action(VdRefresh)));
            var wrap = new WrapPanel { Margin = new Thickness(0, 0, 0, Appearance.Gap(10)) };
            AutomationProperties.SetName(wrap, "Ways this computer can work with virtual disks");
            void Chip(bool ok, string text, string tip = null)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                row.Children.Add(new ContentControl { Content = Ui.Icon(ok ? Icons.Check : Icons.Help, Ui.Res(ok ? "B.Mint" : "B.TextMute"), 14), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center, Focusable = false });
                row.Children.Add(new TextBlock { Text = text, FontSize = 12.5, FontFamily = Ui.Font("F.Body"), Foreground = Ui.Res("B.TextSoft"), TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 520 });
                var b = new Border { Child = row, CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 6, 12, 6), Margin = new Thickness(0, 0, 8, 8), BorderThickness = new Thickness(ok ? 0 : 1) };
                b.SetResourceReference(Border.BackgroundProperty, ok ? "B.Surface" : "B.Window");
                b.SetResourceReference(Border.BorderBrushProperty, "B.Line");
                if (tip != null) b.ToolTip = tip;
                wrap.Children.Add(b);
            }
            Chip(true, "Windows virtual disk service");
            if (_vdRoutes == null) { Chip(false, "Checking for the Storage commands, DiskPart and Hyper-V…"); return wrap; }
            Chip(_vdRoutes.Storage, _vdRoutes.Storage ? "Storage commands" : "Storage commands: not found. Volumes are prepared with DiskPart instead.");
            Chip(_vdRoutes.DiskPart, _vdRoutes.DiskPart ? "DiskPart" : "DiskPart: not found in the Windows folder.");
            Chip(_vdRoutes.HyperV == null, _vdRoutes.HyperV == null ? "Hyper-V commands" : "Hyper-V commands: " + _vdRoutes.HyperV);
            return wrap;
        }

        private static void ProbeRoutes()
        {
            var r = new VdRoutes { DiskPart = File.Exists(Path.Combine(Environment.SystemDirectory, "diskpart.exe")) };
            try
            {
                var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe")) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var a in new[] { "-NoProfile", "-NonInteractive", "-Command", "Get-Command Get-DiskImage, New-VHD -ErrorAction SilentlyContinue | ForEach-Object Name" }) psi.ArgumentList.Add(a);
                using var p = Process.Start(psi);
                var o = p.StandardOutput.ReadToEnd();
                p.WaitForExit(20000);
                r.Storage = o.Contains("Get-DiskImage");
                bool hv = o.Contains("New-VHD");
                if (!hv)
                {
                    string edition = null;
                    try { edition = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "EditionID", null) as string; } catch { }
                    r.HyperV = edition != null && edition.StartsWith("Core", StringComparison.OrdinalIgnoreCase)
                        ? "not part of Windows Home. Nothing here needs them."
                        : "turned off. Nothing here needs them; an administrator can turn on Hyper-V in Windows Features and restart.";
                }
            }
            catch { r.HyperV = "could not be checked. Nothing here needs them."; }
            _vdRoutes = r;
        }

        // -------------------------------------------------------------- the library

        private static List<string> VdEntries()
        {
            var s = Settings.Current;
            s.DiskLibrary ??= new List<string>();
            var all = new List<string>();
            foreach (var p in VirtDisk.Attached().Concat(s.DiskLibrary)) if (!all.Contains(p, StringComparer.OrdinalIgnoreCase)) all.Add(p);
            return all;
        }

        /// <summary>Adds disk files to the library, newest first, and reads them.</summary>
        internal void VdAdd(IEnumerable<string> files)
        {
            var s = Settings.Current;
            s.DiskLibrary ??= new List<string>();
            string last = null;
            foreach (var f in files.Where(DiskJobs.IsDiskFile))
            {
                s.DiskLibrary.RemoveAll(x => string.Equals(x, f, StringComparison.OrdinalIgnoreCase));
                s.DiskLibrary.Insert(0, f);
                VdInfo.Remove(f);
                last = f;
            }
            Settings.Save();
            if (last != null) _vdSelected = last;
        }

        private void VdReadAll(List<string> entries)
        {
            var stale = entries.Where(e => !VdInfo.TryGetValue(e, out var v) || (DateTime.Now - v.At).TotalSeconds > 15).ToList();
            if (stale.Count == 0 || (_vdReading != null && !_vdReading.IsCompleted)) return;
            _vdReading = Task.Run(() =>
            {
                foreach (var e in stale)
                {
                    var info = File.Exists(e) ? VirtDisk.Read(e) : new VDiskInfo { Path = e, Error = "The file is no longer there." };
                    lock (VdInfo) VdInfo[e] = (info, DateTime.Now);
                }
            }).ContinueWith(t => Dispatcher.BeginInvoke(new Action(VdRefresh)));
        }

        private static VDiskInfo VdGet(string path) { lock (VdInfo) return VdInfo.TryGetValue(path, out var v) ? v.Info : null; }

        private FrameworkElement VdLibraryCard()
        {
            var entries = VdEntries();
            VdReadAll(entries);
            var sp = new StackPanel();
            var head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition());
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.Children.Add(CardTitle("Disk library", entries.Count == 0
                ? "No virtual disks yet. Add files, scan a folder, drop a .vhd, .vhdx, .vhds, .avhdx or .iso file on this window, or create one below."
                : "Every virtual disk attached on this computer and every file you added. Choose one to see everything about it."));
            var tools = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(12, 0, 0, 0) };
            tools.Children.Add(Ui.Button("Add files…", () =>
            {
                var dlg = new OpenFileDialog { Title = "Add virtual disk files", Multiselect = true, Filter = "Virtual disks and images|*.vhd;*.vhdx;*.vhds;*.avhd;*.avhdx;*.iso|All files|*.*" };
                if (dlg.ShowDialog() == true) { VdAdd(dlg.FileNames); VdRefresh(); }
            }, "Btn.Ghost", Icons.Plus));
            tools.Children.Add(Ui.Button("Scan a folder…", async () =>
            {
                var dlg = new OpenFolderDialog { Title = "Scan a folder for virtual disks (three levels deep, at most 500 files)" };
                if (dlg.ShowDialog() != true) return;
                var found = await Task.Run(() => ScanForDisks(dlg.FolderName, 3, 500));
                VdAdd(found);
                VdSay(found.Count == 0 ? "No virtual disk files in " + dlg.FolderName + "." : $"Found {found.Count} virtual disk {(found.Count == 1 ? "file" : "files")} in {dlg.FolderName}.", found.Count == 0 ? "B.TextMute" : "B.Mint");
                VdRefresh();
            }, "Btn.Ghost", Icons.Search));
            Grid.SetColumn(tools, 1);
            head.Children.Add(tools);
            sp.Children.Add(head);
            if (_vdResult != null)
            {
                var said = new TextBlock { Text = _vdResult, Style = Ui.Style("T.Body"), FontSize = 13, Foreground = Ui.Res(_vdResultBrush ?? "B.TextSoft"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) };
                AutomationProperties.SetLiveSetting(said, AutomationLiveSetting.Polite);
                sp.Children.Add(said);
            }
            if (entries.Count == 0) return SearchScope.SetItem(Card(sp), "virtual disks library vhd vhdx iso");

            var search = new SearchField("vdisks", "Search the disk library") { Margin = new Thickness(0, 0, 0, 8) };
            search.SampleSource = () => entries.Select(Path.GetFileName);
            sp.Children.Add(search);
            var chips = new WrapPanel();
            var filters = new Collapsible("vdisks.filters", "Filters", chips, startOpen: true);
            sp.Children.Add(filters);
            var rows = new StackPanel();
            sp.Children.Add(rows);
            var kinds = new[] { ("all", "All"), ("attached", "Attached"), ("VHDX", "VHDX"), ("VHD", "VHD"), ("VHD Set", "VHD Set"), ("ISO", "ISO") };
            void Paint()
            {
                chips.Children.Clear();
                foreach (var (id, label) in kinds)
                {
                    var b = Ui.Button(label, null, "Btn.Chip");
                    bool on = _vdFilter == id;
                    b.Background = Ui.Res(on ? "B.SecondaryContainer" : "B.Raised");
                    b.Foreground = Ui.Res(on ? "B.OnSecondaryContainer" : "B.TextSoft");
                    var pick = id;
                    b.Click += (s, e) => { _vdFilter = pick; Paint(); };
                    chips.Children.Add(b);
                }
                filters.SetBadge(_vdFilter == "all" ? null : "1 filter on: " + kinds.First(k => k.Item1 == _vdFilter).Item2);
                rows.Children.Clear();
                var shown = entries.Where(e =>
                {
                    var i = VdGet(e);
                    bool kind = _vdFilter == "all" || (_vdFilter == "attached" ? i?.Attached == true : string.Equals(i?.Format ?? FormatOf(e), _vdFilter, StringComparison.OrdinalIgnoreCase));
                    return kind && search.State.Matches($"{Path.GetFileName(e)} {e} {i?.Format} {i?.Type}");
                }).ToList();
                search.SetCount(shown.Count, entries.Count, "disks");
                foreach (var e in shown) rows.Children.Add(VdRow(e));
                Core.Language.Refresh(rows);
            }
            search.Filter += Paint;
            Paint();
            return SearchScope.SetItem(Card(sp), "virtual disks library vhd vhdx iso attach detach");
        }

        private static string FormatOf(string path) => VirtDisk.DeviceOf(path) switch { VirtDisk.DeviceIso => "ISO", VirtDisk.DeviceVhd => "VHD", VirtDisk.DeviceVhdx => "VHDX", VirtDisk.DeviceVhdSet => "VHD Set", _ => "?" };

        private static List<string> ScanForDisks(string folder, int depth, int max)
        {
            var found = new List<string>();
            var opts = new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = depth, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System };
            try
            {
                foreach (var f in Directory.EnumerateFiles(folder, "*", opts))
                {
                    if (DiskJobs.IsDiskFile(f)) found.Add(f);
                    if (found.Count >= max) break;
                }
            }
            catch { }
            return found;
        }

        private FrameworkElement VdRow(string path)
        {
            var info = VdGet(path);
            bool selected = string.Equals(_vdSelected, path, StringComparison.OrdinalIgnoreCase);
            var g = new Grid { Margin = new Thickness(4, 2, 4, 2) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var fmt = new Border { Width = 52, Height = 40, CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center, Child = Ui.Mono((info?.Format ?? FormatOf(path)).Replace("VHD Set", "VHDS"), Ui.Res("B.TextSoft"), 11) };
            ((TextBlock)fmt.Child).HorizontalAlignment = HorizontalAlignment.Center; ((TextBlock)fmt.Child).VerticalAlignment = VerticalAlignment.Center;
            Core.Language.SetLiteral(fmt.Child, true);   // a format code such as VHDX, the same in every language
            fmt.SetResourceReference(Border.BackgroundProperty, "B.Raised");
            g.Children.Add(fmt);
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(new TextBlock { Text = Path.GetFileName(path), FontSize = 14.5, FontWeight = FontWeights.Medium, FontFamily = Ui.Font("F.Body"), Foreground = Ui.Res("B.Text"), TextWrapping = TextWrapping.Wrap });
            var where = Ui.Mono(PathDisplay.Shown(path), Ui.Res("B.TextMute"), 11.5);
            where.TextWrapping = TextWrapping.Wrap;
            Core.Language.SetLiteral(where, true);
            text.Children.Add(where);
            var meta = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            void M(string t) { var b = new Border { CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 2, 8, 3), Margin = new Thickness(0, 0, 6, 0), Child = new TextBlock { Text = t, FontSize = 12, Foreground = Ui.Res("B.TextSoft") } }; b.SetResourceReference(Border.BackgroundProperty, "B.Raised"); meta.Children.Add(b); }
            if (info == null) M("Reading…");
            else if (info.Error != null) M(info.Error);
            else
            {
                if (!string.IsNullOrEmpty(info.Type)) M(info.Type);
                M(Ui.Bytes((long)info.VirtualSize));
                if (info.Format != "ISO") M(Ui.Bytes((long)info.PhysicalSize) + " on disk");
            }
            text.Children.Add(meta);
            Grid.SetColumn(text, 1);
            g.Children.Add(text);
            var state = new TextBlock { Text = info?.Attached == true ? "Attached" + (info.DiskNumber is int n ? $", disk {n}" : "") : info == null ? "" : "Detached", FontSize = 12.5, Foreground = Ui.Res(info?.Attached == true ? "B.Mint" : "B.TextMute"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 4, 0), TextWrapping = TextWrapping.Wrap, MaxWidth = 140 };
            Grid.SetColumn(state, 2);
            g.Children.Add(state);
            var row = new Button { Style = Ui.Style("Btn.Row"), Content = g, Height = double.NaN, MinHeight = 56, Padding = new Thickness(8, 6, 8, 6), Margin = new Thickness(0, 0, 0, 4), HorizontalContentAlignment = HorizontalAlignment.Stretch };
            if (selected) row.SetResourceReference(Control.BackgroundProperty, "B.SecondaryContainer");
            AutomationProperties.SetName(row, Path.GetFileName(path) + (selected ? ", chosen" : ""));
            row.Click += (s, e) => { _vdSelected = path; _vdAction = null; VdRefresh(); };
            SearchScope.SetItem(row, $"{Path.GetFileName(path)} {info?.Format} {info?.Type}");
            return row;
        }

        // -------------------------------------------------------------- one disk

        private FrameworkElement VdDetailCard(string path)
        {
            var info = VdGet(path);
            var sp = new StackPanel();
            var title = new TextBlock { Text = Path.GetFileName(path), Style = Ui.Style("T.Title"), FontSize = 22, TextWrapping = TextWrapping.Wrap };
            sp.Children.Add(title);
            var where = Ui.Mono(PathDisplay.Shown(path), Ui.Res("B.TextMute"), 12);
            where.TextWrapping = TextWrapping.Wrap; where.Margin = new Thickness(0, 2, 0, 12);
            Core.Language.SetLiteral(where, true);
            sp.Children.Add(where);
            if (info == null) { sp.Children.Add(ToolsNote("Reading the disk…")); return Card(sp); }
            if (info.Error != null) { sp.Children.Add(ToolsNote(info.Error, "B.Amber")); sp.Children.Add(VdRemoveRow(path)); return Card(sp); }
            bool iso = info.Format == "ISO", vhdx = info.Format == "VHDX", diff = info.Type == "differencing";

            // actions
            var acts = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
            void A(string label, string action, string icon, bool enabled = true, string why = null, string style = "Btn.Ghost")
            {
                var b = Ui.Button(label, () => { _vdAction = _vdAction == action ? null : action; VdRefresh(); }, _vdAction == action ? "Btn.Accent" : style, icon);
                b.IsEnabled = enabled && !_vdBusy;
                if (!enabled && why != null) { b.ToolTip = why; AutomationProperties.SetHelpText(b, why); }
                acts.Children.Add(b);
            }
            A(info.Attached ? "Detach…" : "Attach…", info.Attached ? "detach" : "attach", Icons.Play);
            A("Grow…", "grow", Icons.Plus, !iso && info.Format != "VHD Set", iso ? "An ISO image cannot change size." : "Grow a VHD Set from Hyper-V, which manages its checkpoints.");
            A("Shrink…", "shrink", Icons.Lines, vhdx, "Only a VHDX can shrink.");
            A("Compact…", "compact", Icons.Archive, !iso && info.Type != "fixed", iso ? "An ISO image cannot change." : "A fixed disk has no unused blocks to return.");
            A("Convert…", "convert", Icons.Restart, !iso, "An ISO image cannot be converted here.");
            if (diff) A("Merge into parent…", "merge", Icons.Archive);
            if (diff) A("Reconnect parent…", "set-parent", Icons.Link);
            A("New identifier…", "reset-id", Icons.Shield, !iso, "An ISO image has no identifier to change.");
            acts.Children.Add(Ui.Button("Show in folder", () => ActionProvider.Reveal(path), "Btn.Ghost", Icons.Folder));
            sp.Children.Add(acts);
            if (_vdAction != null) sp.Children.Add(VdActionPanel(path, info));

            // figures
            var figs = new CardGrid { Columns = 3, MinColumnWidth = 160, Margin = new Thickness(0, 4, -12, 4) };
            void F(string label, string value) { var f = Stat(label, value); f.Margin = new Thickness(0, 0, 12, 12); figs.Children.Add(f); }
            F("Format", info.Format + (string.IsNullOrEmpty(info.Type) ? "" : ", " + info.Type));
            F("Virtual size", Ui.Bytes((long)info.VirtualSize));
            if (!iso) F("On disk", Ui.Bytes((long)info.PhysicalSize));
            if (info.SmallestSafeSize is ulong safe) F("Smallest safe size", Ui.Bytes((long)safe));
            if (info.BlockSize > 0) F("Block size", Ui.Bytes(info.BlockSize));
            if (info.LogicalSectorSize > 0) F("Sectors, logical and physical", info.LogicalSectorSize + (info.PhysicalSectorSize is uint ps ? " / " + ps : "") + " bytes");
            if (info.Is4KAligned is bool al) F("4K alignment", al ? "aligned" : "not aligned");
            if (info.FragmentationPercent is uint fr) F("Fragmentation", fr + " %");
            F("State", info.Attached ? (info.DiskNumber is int dn ? $"Attached, disk {dn}" : "Attached") : "Detached");
            if (info.ChangeTracking is bool ct) F("Change tracking", ct ? "on" + (info.ChangeTrackingId != null ? ", " + info.ChangeTrackingId : "") : "off");
            sp.Children.Add(figs);
            if (info.Identifier is Guid id || info.VirtualDiskId is Guid)
            {
                var ids = Ui.Mono("Disk identifier " + (info.Identifier?.ToString() ?? "none") + "\nVirtual disk ID " + (info.VirtualDiskId?.ToString() ?? "none"), Ui.Res("B.TextSoft"), 11.5);
                ids.TextWrapping = TextWrapping.Wrap; ids.Margin = new Thickness(0, 0, 0, 10);
                Core.Language.SetLiteral(ids, true);
                sp.Children.Add(ids);
            }
            if (diff)
            {
                var chain = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
                chain.Children.Add(new TextBlock { Text = info.ParentResolved == false ? "Parent chain: the parent is missing. Reconnect it to use this disk." : "Parent chain: the chain opens, so it is usable.", FontSize = 13, Foreground = Ui.Res(info.ParentResolved == false ? "B.Amber" : "B.Mint"), TextWrapping = TextWrapping.Wrap });
                foreach (var p in info.ParentPaths.Take(3))
                {
                    var t = Ui.Mono("↳ " + PathDisplay.Shown(p), Ui.Res("B.TextSoft"), 11.5);
                    t.TextWrapping = TextWrapping.Wrap; Core.Language.SetLiteral(t, true);
                    chain.Children.Add(t);
                }
                sp.Children.Add(chain);
            }
            if (info.Attached) sp.Children.Add(VdVolumesBlock(path));
            sp.Children.Add(VdRemoveRow(path));
            return SearchScope.SetItem(Card(sp), "virtual disk details attach grow shrink compact convert merge identifier");
        }

        private FrameworkElement VdRemoveRow(string path)
        {
            var w = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            if ((Settings.Current.DiskLibrary ?? new List<string>()).Contains(path, StringComparer.OrdinalIgnoreCase))
                w.Children.Add(Ui.Button("Remove from the list", () =>
                {
                    Settings.Current.DiskLibrary.RemoveAll(x => string.Equals(x, path, StringComparison.OrdinalIgnoreCase));
                    Settings.Save();
                    _vdSelected = null; VdSay("Removed from the list. The file itself was not touched.", "B.TextMute");
                    VdRefresh();
                }, "Btn.Ghost", Icons.Close));
            return w;
        }

        /// <summary>The partitions, volumes, letters, file systems and free space of an attached disk, read with the Storage commands.</summary>
        private FrameworkElement VdVolumesBlock(string path)
        {
            var host = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            host.Children.Add(new TextBlock { Text = "Inside", FontSize = 14, FontWeight = FontWeights.Medium, Foreground = Ui.Res("B.Text"), Margin = new Thickness(0, 0, 0, 6) });
            string known; lock (VdVolumes) VdVolumes.TryGetValue(path, out known);
            if (known == null)
            {
                host.Children.Add(ToolsNote("Reading its volumes…"));
                Task.Run(() =>
                {
                    string result;
                    try
                    {
                        var ps = $"$d = Get-DiskImage -ImagePath '{path.Replace("'", "''")}' | Get-Disk; Get-Partition -DiskNumber $d.Number | ForEach-Object {{ $v = $_ | Get-Volume -ErrorAction SilentlyContinue; [pscustomobject]@{{ N = $_.PartitionNumber; Size = $_.Size; Letter = [string]$_.DriveLetter; Paths = ($_.AccessPaths -join ';'); Fs = $v.FileSystem; Label = $v.FileSystemLabel; Free = $v.SizeRemaining; Type = [string]$_.Type }} }} | ConvertTo-Json -Compress";
                        var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe")) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                        foreach (var a in new[] { "-NoProfile", "-NonInteractive", "-Command", ps }) psi.ArgumentList.Add(a);
                        using var p = Process.Start(psi);
                        result = p.StandardOutput.ReadToEnd();
                        p.WaitForExit(20000);
                        if (string.IsNullOrWhiteSpace(result)) result = "[]";
                    }
                    catch (Exception ex) { result = "!" + ex.Message; }
                    lock (VdVolumes) VdVolumes[path] = result;
                }).ContinueWith(t => Dispatcher.BeginInvoke(new Action(VdRefresh)));
                return host;
            }
            if (known.StartsWith("!")) { host.Children.Add(ToolsNote("Its volumes could not be read: " + known.Substring(1), "B.Amber")); return host; }
            try
            {
                using var doc = JsonDocument.Parse(known.TrimStart().StartsWith("[") ? known : "[" + known + "]");
                int count = 0;
                foreach (var e in doc.RootElement.EnumerateArray())
                {
                    string S(string n) => e.TryGetProperty(n, out var v) && v.ValueKind != JsonValueKind.Null ? v.ToString() : "";
                    long L(string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : 0;
                    var type = S("Type");
                    if (type == "Reserved") continue;
                    count++;
                    var letter = S("Letter").Trim('\0', ' ');
                    var line = $"Partition {S("N")}: {Ui.Bytes(L("Size"))}" + (S("Fs").Length > 0 ? ", " + S("Fs") : "") + (S("Label").Length > 0 ? ", “" + S("Label") + "”" : "") +
                               (letter.Length > 0 ? ", " + letter + ":" : "") + (L("Free") > 0 ? $", {Ui.Bytes(L("Free"))} free" : "");
                    var row = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
                    var t = new TextBlock { Text = line, FontSize = 13, Foreground = Ui.Res("B.TextSoft"), TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
                    if (letter.Length > 0)
                    {
                        var open = Ui.Button("Open", () => ActionProvider.Shell(letter + @":\"), "Btn.Ghost");
                        DockPanel.SetDock(open, Dock.Right);
                        row.Children.Add(open);
                    }
                    row.Children.Add(t);
                    host.Children.Add(row);
                }
                if (count == 0) host.Children.Add(ToolsNote("No partitions. The disk is empty and can be prepared from Create below, or with Disk Management."));
            }
            catch { host.Children.Add(ToolsNote("Its volumes could not be read.", "B.Amber")); }
            return host;
        }

        // -------------------------------------------------------------- the action panel: options, the script and Run

        private FrameworkElement VdActionPanel(string path, VDiskInfo info)
        {
            var panel = new Border { CornerRadius = new CornerRadius(18), Padding = new Thickness(16), Margin = new Thickness(0, 0, 0, 12) };
            panel.SetResourceReference(Border.BackgroundProperty, "B.Raised");
            var sp = new StackPanel();
            panel.Child = sp;
            var job = new DiskJob { Op = _vdAction, File = path };
            bool danger = false;
            string confirmTitle = null, confirmBody = null;
            var scriptHost = new ContentControl();
            var check = new TextBlock { FontSize = 12.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 6) };
            AutomationProperties.SetLiveSetting(check, AutomationLiveSetting.Polite);
            Button run = null;
            void Update()
            {
                var problem = DiskJobs.Validate(job);
                check.Text = problem ?? "";
                check.Foreground = Ui.Res("B.Amber");
                check.Visibility = problem == null ? Visibility.Collapsed : Visibility.Visible;
                if (run != null) run.IsEnabled = problem == null && !_vdBusy;
                var (ps, dp) = DiskJobs.Scripts(job, _vdRoutes?.HyperV == null && _vdRoutes != null);
                scriptHost.Content = VdScripts(ps, dp);
                Core.Language.Refresh(check);
            }
            string runLabel;
            switch (_vdAction)
            {
                case "attach":
                    sp.Children.Add(CardTitle(info.Format == "ISO" ? "Attach the image" : "Attach", info.Format == "ISO" ? "An ISO image attaches read-only, with a drive letter." : "Read-write, or read-only so nothing on it can change. It stays attached until you detach it."));
                    if (info.Format != "ISO")
                    {
                        var ro = new Segmented("Access", new[] { new Choice("rw", "Read and write"), new Choice("ro", "Read-only") }, "rw") { Margin = new Thickness(0, 0, 0, 8) };
                        ro.Changed += v => { job.ReadOnly = v == "ro"; Update(); };
                        sp.Children.Add(ro);
                        var letters = new Segmented("Drive letters", new[] { new Choice("yes", "Give it drive letters"), new Choice("no", "No drive letters") }, "yes") { Margin = new Thickness(0, 0, 0, 8) };
                        letters.Changed += v => { job.NoLetters = v == "no"; Update(); };
                        sp.Children.Add(letters);
                    }
                    else job.ReadOnly = true;
                    runLabel = "Attach";
                    break;
                case "detach":
                    sp.Children.Add(CardTitle("Detach", "Close any files open on it first. Detaching never deletes the file or what is on it."));
                    runLabel = "Detach";
                    break;
                case "grow":
                    sp.Children.Add(CardTitle("Grow", $"Now {Ui.Bytes((long)info.VirtualSize)}. The new space goes to its last volume unless you turn that off. A VHD grows to 2,040 GB at most, a VHDX to 64 TB."));
                    double gbNow = Math.Ceiling(info.VirtualSize / (double)DiskJobs.Gb);
                    job.SizeBytes = (long)(gbNow + Math.Max(1, Math.Round(gbNow * 0.5))) * DiskJobs.Gb;
                    sp.Children.Add(new NumberField("New size", "In GB.", job.SizeBytes / (double)DiskJobs.Gb, Math.Max(1, gbNow), DiskJobs.MaxFor(path) / (double)DiskJobs.Gb, 1, v => { job.SizeBytes = (long)(v * DiskJobs.Gb); Update(); }, "GB"));
                    sp.Children.Add(Switch("Extend its last volume into the new space", "On by default. Off leaves the new space unallocated.", true, v => { job.Volume = v; Update(); }));
                    runLabel = "Grow";
                    break;
                case "shrink":
                    sp.Children.Add(CardTitle("Shrink to the smallest safe size", (info.SmallestSafeSize is ulong safe ? $"Its smallest safe size is now {Ui.Bytes((long)safe)}. " : "") + "Shrinking its volume first lets the disk get smaller. Make a copy first if what is on it matters."));
                    sp.Children.Add(Switch("Shrink its volume first", "On by default. The volume shrinks to what it holds plus 64 MB, then the disk shrinks to its smallest safe size.", true, v => { job.Volume = v; Update(); }));
                    danger = true; confirmTitle = "Shrink " + Path.GetFileName(path) + "?"; confirmBody = "The volume and the disk get smaller. Space past what the volume holds is given up. Windows refuses to cut into data, but a copy first is wise.";
                    runLabel = "Shrink";
                    break;
                case "compact":
                    sp.Children.Add(CardTitle("Compact the disk", "Returns unused space inside the disk to this drive. The disk is attached read-only while it runs and put back as it was. Quick reclaims unused blocks; Full scan also looks for zeroed blocks and takes longer."));
                    var mode = new Segmented("Mode", new[] { new Choice("quick", "Quick"), new Choice("full", "Full scan") }, "quick") { Margin = new Thickness(0, 0, 0, 8) };
                    mode.Changed += v => { job.Quick = v == "quick"; Update(); };
                    sp.Children.Add(mode);
                    runLabel = "Compact the disk";
                    break;
                case "convert":
                {
                    sp.Children.Add(CardTitle("Convert", "Writes a new file beside it in the format and type you choose. The original stays as it is."));
                    string fmt = info.Format == "VHD" ? ".vhdx" : ".vhd";
                    string Target() => Base64Files.Unique(Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + fmt));
                    job.Other = Target(); job.Type = info.Type == "fixed" ? "fixed" : "dynamic";
                    var f = new Segmented("New format", new[] { new Choice(".vhdx", "VHDX"), new Choice(".vhd", "VHD") }, fmt) { Margin = new Thickness(0, 0, 0, 8) };
                    f.Changed += v => { fmt = v; job.Other = Target(); Update(); };
                    sp.Children.Add(f);
                    var t = new Segmented("New type", new[] { new Choice("dynamic", "Grows as it fills"), new Choice("fixed", "Fixed size") }, job.Type) { Margin = new Thickness(0, 0, 0, 8) };
                    t.Changed += v => { job.Type = v; Update(); };
                    sp.Children.Add(t);
                    runLabel = "Convert";
                    break;
                }
                case "merge":
                    sp.Children.Add(CardTitle("Merge into its parent", "Its changes are written into " + Path.GetFileName(info.ParentPaths.FirstOrDefault() ?? "its parent") + ". Afterwards this child no longer works, and neither does any other child of that parent."));
                    job.Other = info.ParentPaths.FirstOrDefault();
                    danger = true; confirmTitle = "Merge " + Path.GetFileName(path) + " into its parent?"; confirmBody = "The parent changes for good. This child and every other child made from the same parent stop working. Make a copy of the parent first if you are not sure.";
                    runLabel = "Merge";
                    break;
                case "set-parent":
                    sp.Children.Add(CardTitle("Reconnect the parent", "Point this child at its parent's new place. The parent must be the same disk it was made from."));
                    sp.Children.Add(Ui.Button("Choose the parent…", () =>
                    {
                        var dlg = new OpenFileDialog { Title = "Choose the parent disk", Filter = "Virtual disks|*.vhd;*.vhdx;*.avhd;*.avhdx" };
                        if (dlg.ShowDialog() == true) { job.Other = dlg.FileName; Update(); }
                    }, "Btn.Ghost", Icons.Folder));
                    runLabel = "Reconnect the parent";
                    break;
                case "reset-id":
                    sp.Children.Add(CardTitle("Give it a new identifier", "Two copies of one disk share an identifier, so Windows will not attach both. A new one fixes that. Anything that found this disk by its old identifier will not find it any more."));
                    danger = true; confirmTitle = "Give " + Path.GetFileName(path) + " a new identifier?"; confirmBody = "Anything that found this disk by its old identifier, such as a virtual machine or a backup, will not find it any more.";
                    runLabel = "Set a new identifier";
                    break;
                default:
                    return panel;
            }
            sp.Children.Add(check);
            sp.Children.Add(scriptHost);
            var buttons = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            run = Ui.Button(runLabel, async () =>
            {
                if (danger)
                {
                    var c = new ConfirmWindow(confirmTitle, confirmBody, runLabel, danger: true);
                    c.Confirmed += async ok => { if (ok) await VdRun(job, info); };
                    c.Show();
                    return;
                }
                await VdRun(job, info);
            }, danger ? "Btn.Danger" : "Btn.Accent", Icons.Play);
            buttons.Children.Add(run);
            buttons.Children.Add(Ui.Button("Close", () => { _vdAction = null; VdRefresh(); }, "Btn.Ghost"));
            sp.Children.Add(buttons);
            Update();
            return panel;
        }

        /// <summary>The same steps as PowerShell and DiskPart, each with a copy button.</summary>
        private static FrameworkElement VdScripts(string ps, string dp)
        {
            var host = new StackPanel();
            var which = new Segmented("Script", new[] { new Choice("ps", "PowerShell"), new Choice("dp", "DiskPart") }, "ps") { Margin = new Thickness(0, 4, 0, 6), HorizontalAlignment = HorizontalAlignment.Left };
            var code = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, FontFamily = Ui.Font("F.Mono"), FontSize = 12, BorderThickness = new Thickness(0), Padding = new Thickness(12, 10, 12, 10), Text = ps };
            code.SetResourceReference(Control.BackgroundProperty, "B.Sunken");
            code.SetResourceReference(Control.ForegroundProperty, "B.Text");
            AutomationProperties.SetName(code, "The same steps as a script");
            Core.Language.SetLiteral(code, true);
            var copy = Ui.Button("Copy the script", () => { MonitoringTools.Clip.ClipboardWatcher.SetText(code.Text); }, "Btn.Ghost", Icons.Clipboard);
            which.Changed += v => code.Text = v == "ps" ? ps : dp;
            host.Children.Add(new TextBlock { Text = "The same steps as a script", FontSize = 13, FontWeight = FontWeights.Medium, Foreground = Ui.Res("B.Text"), Margin = new Thickness(0, 6, 0, 0) });
            host.Children.Add(which);
            host.Children.Add(new Border { CornerRadius = new CornerRadius(12), Child = code, ClipToBounds = true });
            host.Children.Add(copy);
            return host;
        }

        /// <summary>Runs one job behind the consent prompt and records what happened.</summary>
        private async Task VdRun(DiskJob job, VDiskInfo info)
        {
            if (_vdBusy) return;
            _vdBusy = true;
            VdSay("Windows asks for permission to change the disk. Waiting…", "B.TextSoft");
            VdRefresh();
            DiskJobStatus s;
            try { s = await DiskJobs.RunElevatedAsync(job); }
            catch (Exception ex) { s = new DiskJobStatus { Phase = "error", Error = ex.Message }; }
            _vdBusy = false;
            lock (VdInfo) { VdInfo.Remove(job.File); if (job.Other != null) VdInfo.Remove(job.Other); }
            lock (VdVolumes) VdVolumes.Remove(job.File);
            var name = Path.GetFileName(job.File);
            if (s.Phase == "done")
            {
                VdSay(name + ": " + s.Message + (s.Route != null ? " Done through the " + s.Route + "." : ""), "B.Mint");
                LocalHistory.Append("vdisk", VdVerb(job.Op) + " " + name, s.Message + (s.Route != null ? " Route: " + s.Route + "." : ""), job.Op == "convert" ? job.Other : job.File);
                Activity.Add(ActivityKind.Files, VdVerb(job.Op) + " " + name, job.File, true);
                if (job.Op == "convert" && job.Other != null) VdAdd(new[] { job.Other });
                _vdAction = null;
            }
            else
            {
                var why = s.Phase == "declined" ? s.Error : name + ": not done. " + s.Error;
                VdSay(why, "B.Amber");
                LocalHistory.Append("vdisk", "Could not " + VdTry(job.Op) + " " + name, s.Error, job.File);
                Activity.Add(ActivityKind.Files, "Could not " + VdTry(job.Op) + " " + name + ": " + s.Error, job.File, false);
            }
            VdRefresh();
        }

        private static string VdTry(string op) => op switch
        {
            "reset-id" => "give a new identifier to", "set-parent" => "reconnect the parent of", _ => op
        };

        private static string VdVerb(string op) => op switch
        {
            "attach" => "Attached", "detach" => "Detached", "grow" => "Grew", "shrink" => "Shrank", "compact" => "Compacted", "merge" => "Merged",
            "reset-id" => "Gave a new identifier to", "set-parent" => "Reconnected the parent of", "convert" => "Converted", "create" => "Created", _ => op
        };

        // -------------------------------------------------------------- create

        private DiskJob _vdNew;

        private FrameworkElement VdCreateCard()
        {
            var job = _vdNew ??= new DiskJob
            {
                Op = "create", Type = "dynamic", SizeBytes = 64 * DiskJobs.Gb, Init = "GPT", FileSystem = "NTFS", Label = "New disk",
                File = Base64Files.Unique(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "New disk.vhdx")),
            };
            var sp = new StackPanel();
            sp.Children.Add(CardTitle("New virtual disk", "Start from a preset or set every option. Nothing is written until you press Create."));
            var presets = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
            void Preset(string label, Action set)
            {
                var b = Ui.Button(label, () => { set(); VdRefresh(); }, "Btn.Chip");
                presets.Children.Add(b);
            }
            string Docs(string name) => Base64Files.Unique(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), name));
            Preset("Portable data disk", () => { job.File = Docs("Portable.vhdx"); job.Type = "dynamic"; job.SizeBytes = 64 * DiskJobs.Gb; job.Init = "GPT"; job.FileSystem = "exFAT"; job.Label = "Portable"; job.DevDrive = false; });
            Preset("Dev Drive", () => { job.File = Docs("DevDrive.vhdx"); job.Type = "dynamic"; job.SizeBytes = 80 * DiskJobs.Gb; job.Init = "GPT"; job.FileSystem = "ReFS"; job.Label = "Dev"; job.DevDrive = true; });
            Preset("Native boot disk", () => { job.File = Docs("Windows.vhdx"); job.Type = "fixed"; job.SizeBytes = 64 * DiskJobs.Gb; job.Init = "GPT"; job.FileSystem = "NTFS"; job.Label = "Windows"; job.DevDrive = false; });
            Preset("Scratch copy of a disk", () => { job.Type = "differencing"; job.Init = "none"; });
            sp.Children.Add(presets);

            var fmt = new Segmented("Format", new[] { new Choice(".vhdx", "VHDX"), new Choice(".vhd", "VHD"), new Choice(".vhds", "VHD Set") }, Path.GetExtension(job.File).ToLowerInvariant()) { Margin = new Thickness(0, 0, 0, 8), HorizontalAlignment = HorizontalAlignment.Left };
            fmt.Changed += v => { job.File = Path.ChangeExtension(job.File, v); VdRefresh(); };
            sp.Children.Add(DialogParts.Label("Format")); sp.Children.Add(fmt);
            var type = new Segmented("Type", new[] { new Choice("dynamic", "Grows as it fills"), new Choice("fixed", "Fixed size"), new Choice("differencing", "Child of a disk"), new Choice("copy", "Copy of a disk") }, job.Type) { Margin = new Thickness(0, 0, 0, 8), HorizontalAlignment = HorizontalAlignment.Left };
            type.Changed += v => { job.Type = v; if (v == "differencing" || v == "copy") job.Init = "none"; VdRefresh(); };
            sp.Children.Add(DialogParts.Label("Type")); sp.Children.Add(type);
            if (job.Type == "differencing" || job.Type == "copy")
            {
                var src = new WrapPanel();
                var shown = Ui.Mono(job.Other == null ? (job.Type == "differencing" ? "Choose the parent disk" : "Choose the disk to copy") : PathDisplay.Shown(job.Other), Ui.Res("B.TextSoft"), 12);
                shown.TextWrapping = TextWrapping.Wrap; shown.VerticalAlignment = VerticalAlignment.Center; shown.Margin = new Thickness(0, 0, 10, 8);
                src.Children.Add(shown);
                src.Children.Add(Ui.Button("Choose…", () =>
                {
                    var dlg = new OpenFileDialog { Title = job.Type == "differencing" ? "Choose the parent disk" : "Choose the disk to copy", Filter = "Virtual disks|*.vhd;*.vhdx;*.avhd;*.avhdx" };
                    if (dlg.ShowDialog() == true) { job.Other = dlg.FileName; VdRefresh(); }
                }, "Btn.Ghost", Icons.Folder));
                sp.Children.Add(src);
            }
            else
            {
                sp.Children.Add(new NumberField("Size", "In GB. 3 MB at least; a VHD up to 2,040 GB, a VHDX up to 64 TB.", Math.Round(job.SizeBytes / (double)DiskJobs.Gb, 2), 0.01, 65536, 1, v => { job.SizeBytes = (long)(v * DiskJobs.Gb); }, "GB", decimals: 2));
                if (Path.GetExtension(job.File).ToLowerInvariant() != ".vhd")
                {
                    var blocks = new Segmented("Block size", new[] { new Choice("0", "Default"), new Choice("1", "1 MB"), new Choice("4", "4 MB"), new Choice("32", "32 MB"), new Choice("128", "128 MB"), new Choice("256", "256 MB") }, job.BlockSizeMb.ToString()) { Margin = new Thickness(0, 0, 0, 8), HorizontalAlignment = HorizontalAlignment.Left };
                    blocks.Changed += v => job.BlockSizeMb = int.Parse(v);
                    sp.Children.Add(DialogParts.Label("Block size")); sp.Children.Add(blocks);
                    var sectors = new Segmented("Sectors", new[] { new Choice("0", "Default"), new Choice("512", "512 bytes"), new Choice("4096", "4096 bytes") }, job.PhysicalSector.ToString()) { Margin = new Thickness(0, 0, 0, 8), HorizontalAlignment = HorizontalAlignment.Left };
                    sectors.Changed += v => job.PhysicalSector = int.Parse(v);
                    sp.Children.Add(DialogParts.Label("Physical sector size")); sp.Children.Add(sectors);
                }
                var init = new Segmented("Prepare it", new[] { new Choice("GPT", "GPT"), new Choice("MBR", "MBR"), new Choice("none", "Leave it empty") }, job.Init) { Margin = new Thickness(0, 0, 0, 8), HorizontalAlignment = HorizontalAlignment.Left };
                init.Changed += v => { job.Init = v; VdRefresh(); };
                sp.Children.Add(DialogParts.Label("Prepare it")); sp.Children.Add(init);
                if (job.Init != "none")
                {
                    var fs = new Segmented("File system", new[] { new Choice("NTFS", "NTFS"), new Choice("ReFS", "ReFS"), new Choice("exFAT", "exFAT"), new Choice("FAT32", "FAT32") }, job.FileSystem) { Margin = new Thickness(0, 0, 0, 8), HorizontalAlignment = HorizontalAlignment.Left };
                    fs.Changed += v => { job.FileSystem = v; if (v != "ReFS") job.DevDrive = false; VdRefresh(); };
                    sp.Children.Add(DialogParts.Label("File system")); sp.Children.Add(fs);
                    var label = Input(job.Label, 260, "Volume label");
                    label.TextChanged += (s, e) => job.Label = label.Text;
                    sp.Children.Add(label);
                    if (job.FileSystem == "ReFS")
                        sp.Children.Add(Switch("Make it a Dev Drive", "Off by default. Needs Windows 11 build 10.0.22621.2338 or later and 50 GB at least. Trust and filters stay this computer's settings.", job.DevDrive, v => job.DevDrive = v));
                    sp.Children.Add(Switch("Leave it attached afterwards", "On by default, with a drive letter, ready to use.", job.KeepAttached, v => job.KeepAttached = v));
                }
            }
            var whereRow = new WrapPanel { Margin = new Thickness(0, 4, 0, 4) };
            var where = Ui.Mono(PathDisplay.Shown(job.File), Ui.Res("B.TextSoft"), 12);
            where.TextWrapping = TextWrapping.Wrap; where.VerticalAlignment = VerticalAlignment.Center; where.Margin = new Thickness(0, 0, 10, 0);
            Core.Language.SetLiteral(where, true);
            whereRow.Children.Add(where);
            whereRow.Children.Add(Ui.Button("Save as…", () =>
            {
                var dlg = new SaveFileDialog { Title = "Save the new disk as", FileName = Path.GetFileName(job.File), InitialDirectory = Path.GetDirectoryName(job.File), Filter = "VHDX|*.vhdx|VHD|*.vhd|VHD Set|*.vhds", OverwritePrompt = false };
                if (dlg.ShowDialog() == true) { job.File = dlg.FileName; VdRefresh(); }
            }, "Btn.Ghost", Icons.Folder));
            sp.Children.Add(whereRow);

            // the checks before anything is written
            var checks = new StackPanel { Margin = new Thickness(0, 6, 0, 6) };
            var problem = DiskJobs.Validate(job);
            void Line(bool ok, string text)
            {
                // the mark sits apart from the sentence, so the sentence is translated as a whole
                var row = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
                var mark = new TextBlock { Text = ok ? "✓" : "!", FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = Ui.Res(ok ? "B.Mint" : "B.Amber"), Margin = new Thickness(0, 0, 8, 0) };
                Core.Language.SetLiteral(mark, true);
                AutomationProperties.SetName(mark, ok ? "Checked" : "Needs attention");
                DockPanel.SetDock(mark, Dock.Left);
                row.Children.Add(mark);
                row.Children.Add(new TextBlock { Text = text, FontSize = 13, Foreground = Ui.Res(ok ? "B.Mint" : "B.Amber"), TextWrapping = TextWrapping.Wrap });
                checks.Children.Add(row);
            }
            if (problem != null) Line(false, problem);
            else
            {
                Line(true, Path.GetFileName(job.File) + " is a new name. Nothing is overwritten.");
                try
                {
                    var drive = new DriveInfo(Path.GetPathRoot(job.File));
                    if (job.Type == "fixed")
                        Line(job.SizeBytes < drive.AvailableFreeSpace, job.SizeBytes < drive.AvailableFreeSpace
                            ? $"Takes {Ui.Bytes(job.SizeBytes)} now. {drive.Name.TrimEnd('\\')} has {Ui.Bytes(drive.AvailableFreeSpace)} free."
                            : $"Takes {Ui.Bytes(job.SizeBytes)} now, and {drive.Name.TrimEnd('\\')} has {Ui.Bytes(drive.AvailableFreeSpace)} free: {Ui.Bytes(job.SizeBytes - drive.AvailableFreeSpace)} short.");
                    else if (job.Type == "dynamic")
                        Line(true, $"Starts small and can grow to {Ui.Bytes(job.SizeBytes)}. Windows does not set that space aside on {drive.Name.TrimEnd('\\')}, so keep it free.");
                }
                catch { }
                Line(true, "Windows asks once for administrator permission. Cancelling it changes nothing.");
            }
            sp.Children.Add(checks);
            var (ps, dp) = DiskJobs.Scripts(job, _vdRoutes != null && _vdRoutes.HyperV == null);
            sp.Children.Add(VdScripts(ps, dp));
            var create = Ui.Button(job.Init != "none" && job.Type != "differencing" && job.Type != "copy" ? "Create and prepare" : "Create", async () =>
            {
                var j = JsonSerializer.Deserialize<DiskJob>(JsonSerializer.Serialize(job));
                await VdRun(j, null);
                if (_vdResultBrush == "B.Mint") { VdAdd(new[] { j.File }); _vdNew = null; VdRefresh(); }
            }, "Btn.Accent", Icons.Plus);
            create.IsEnabled = problem == null && !_vdBusy;
            create.Margin = new Thickness(0, 8, 8, 0);
            sp.Children.Add(create);
            return SearchScope.SetItem(Card(sp), "new virtual disk create vhd vhdx vhd set dev drive native boot preset");
        }
    }
}
