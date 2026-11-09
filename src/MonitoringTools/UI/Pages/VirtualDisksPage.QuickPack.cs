using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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
        /// <summary>One Quick pack, from the drop to the result. Kept with the window so the page can rebuild around it.</summary>
        private sealed class PackRun
        {
            public string[] Items;
            public QuickPackPlan Plan;
            public List<(string Source, string Relative, bool Folder, long Size)> Walk;
            public bool Fixed;
            public string FileSystem = "NTFS";
            public bool Permissions, Tight;
            public string Phase = "planning";   // planning, ready, packing, done, partial, stopped, cancelled
            public PackProgress Progress;
            public PackResult Result;
            public List<string> Log = new List<string>();
            public string Error;
            public long SizeBytes;
            public PackCopy Copy;
            public CancellationTokenSource Cts;
            public VhdSession Session;
        }

        private static PackRun _pack;

        /// <summary>
        /// For the self-test and the captures: the Quick pack card in one state, planned from real items. A real pack is over
        /// too fast to catch part way, so the progress and result states carry fixed numbers (the captions say "preview").
        /// </summary>
        internal async Task VdTestPackAsync(string phase, string[] items, string disk = null)
        {
            var p = new PackRun { Items = items };
            p.Walk = await Task.Run(() => PackCopy.Walk(items));
            p.Plan = QuickPackPlan.For(items, entries: p.Walk.Select(w => (w.Size, w.Folder)));
            if (disk != null) p.Plan.File = disk;
            var name = Path.GetFileName(p.Plan.File);
            var files = p.Walk.Where(w => !w.Folder).ToList();
            long total = files.Sum(w => w.Size);
            const long G = 1L << 30;
            p.Phase = phase;
            switch (phase)
            {
                case "packing":
                    p.Copy = new PackCopy();
                    p.Progress = new PackProgress { Done = total * 62 / 100, Total = total, FilesDone = files.Count * 6 / 10, FilesTotal = files.Count, Current = files.Count > 0 ? files[files.Count - 1].Relative : null, BytesPerSecond = 182 * 1048576.0, Left = TimeSpan.FromSeconds(95) };
                    p.Result = new PackResult();
                    p.Result.Growths.Add((G, 2 * G));
                    p.Log.Add("Asking Windows for permission to make and attach the disk.");
                    p.Log.Add($"Made {name}: 1 GB, grows as it fills, GPT, NTFS, at P:.");
                    p.Log.Add($"Ran out of room at {p.Progress.Current}. Growing the disk from 1 GB to 2 GB.");
                    p.Log.Add("Grew from 1 GB to 2 GB, extended the volume, and carried on, checking that file again.");
                    break;
                case "done":
                    p.Result = new PackResult { Ok = true, Files = files.Count, Verified = files.Count, Bytes = total };
                    p.Result.Growths.Add((G, 2 * G));
                    p.Log.Add("Ran out of room at " + (files.Count > 0 ? files[files.Count - 1].Relative : "") + ". Growing the disk from 1 GB to 2 GB.");
                    p.Log.Add("Grew from 1 GB to 2 GB, extended the volume, and carried on, checking that file again.");
                    break;
                case "partial":
                    p.Result = new PackResult { Partial = true, Files = files.Count, Verified = Math.Max(0, files.Count - 2), Bytes = total };
                    foreach (var w in files.Take(2)) p.Result.Skipped.Add((w.Source, p.Result.Skipped.Count == 0 ? "open in another app, which locks it" : "Windows denied read access"));
                    break;
                case "cancelled":
                    p.Error = $"Cancelled. Detached and removed {name}, the only file this run made. Your files were not touched.";
                    break;
                case "stopped":
                    p.Error = "D: is 3.2 GB short of the room the disk needs to grow. The new disk was detached and removed; your files were not touched.";
                    break;
            }
            _pack = p;
            Navigate("vdisks", animate: false);
        }

        /// <summary>For the self-test and the captures: clears the Quick pack card back to its drop zone.</summary>
        internal void VdTestPackClear() { _pack = null; Navigate("vdisks", animate: false); }

        private FrameworkElement VdQuickPackCard()
        {
            var sp = new StackPanel();
            sp.Children.Add(CardTitle("Quick pack", "Drop files and folders here, or choose them. One new VHDX is made in the folder they came from and filled with everything you dropped. It grows by itself if it runs short, every file is checked against its original by SHA-256, and your originals are never moved or changed."));
            var p = _pack;
            if (p == null || p.Phase == "done" || p.Phase == "cancelled" || p.Phase == "partial" || p.Phase == "stopped")
            {
                if (p != null) sp.Children.Add(PackResultBlock(p));
                sp.Children.Add(PackDropZone());
                return SearchScope.SetItem(Card(sp), "quick pack vhdx drop folder files pack verify");
            }
            if (p.Phase == "planning") { sp.Children.Add(ToolsNote("Counting what you dropped…")); return Card(sp); }
            if (p.Phase == "ready") { sp.Children.Add(PackPlanBlock(p)); return Card(sp); }
            sp.Children.Add(PackProgressBlock(p));
            return Card(sp);
        }

        private FrameworkElement PackDropZone()
        {
            var zone = new Border { CornerRadius = new CornerRadius(18), Padding = new Thickness(18, 14, 14, 14), Margin = new Thickness(0, 6, 0, 0), BorderThickness = new Thickness(1.5), AllowDrop = true, Focusable = true, Cursor = Cursors.Hand, FocusVisualStyle = null };
            zone.SetResourceReference(Border.BackgroundProperty, "B.MintWash");
            zone.SetResourceReference(Border.BorderBrushProperty, "B.Line");
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var chip = Ui.IconChip(DialogParts.DiskIcon, Ui.Res("B.Mint"), Ui.Res("B.Sunken"), 40, 20);
            chip.Margin = new Thickness(0, 0, 14, 0); chip.VerticalAlignment = VerticalAlignment.Center;
            g.Children.Add(chip);
            var t = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            t.Children.Add(new TextBlock { Text = "Drop files and folders to pack them", Style = Ui.Style("T.Title"), FontSize = 15, TextWrapping = TextWrapping.Wrap });
            t.Children.Add(new TextBlock { Text = "Or choose them. Windows asks once for administrator permission, to make and attach the disk.", Style = Ui.Style("T.Body"), FontSize = 12.5, Foreground = Ui.Res("B.TextSoft"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
            Grid.SetColumn(t, 1);
            g.Children.Add(t);
            var pickFiles = Ui.Button("Choose files…", () =>
            {
                var dlg = new OpenFileDialog { Title = "Choose files to pack", Multiselect = true };
                if (dlg.ShowDialog() == true) StartPack(dlg.FileNames);
            }, "Btn.Ghost", Icons.File);
            var pickFolder = Ui.Button("Choose a folder…", () =>
            {
                var dlg = new OpenFolderDialog { Title = "Choose a folder to pack" };
                if (dlg.ShowDialog() == true) StartPack(new[] { dlg.FolderName });
            }, "Btn.Ghost", Icons.Folder);
            var picks = new WrapPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            picks.Children.Add(pickFiles); picks.Children.Add(pickFolder);
            Grid.SetColumn(picks, 2);
            g.Children.Add(picks);
            zone.Child = g;
            AutomationProperties.SetName(zone, "Drop files and folders here to pack them into a new VHDX");
            void Lit(bool on) { zone.SetResourceReference(Border.BorderBrushProperty, on ? "B.Mint" : "B.Line"); zone.SetResourceReference(Border.BackgroundProperty, on ? "B.SecondaryContainer" : "B.MintWash"); }
            zone.DragEnter += (s, e) => { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; Lit(true); e.Handled = true; };
            zone.DragOver += (s, e) => { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
            zone.DragLeave += (s, e) => Lit(false);
            zone.Drop += (s, e) => { Lit(false); if (e.Data.GetData(DataFormats.FileDrop) is string[] items) StartPack(items); e.Handled = true; };
            zone.KeyDown += (s, e) => { if (e.Key == Key.Enter || e.Key == Key.Space) { pickFolder.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); e.Handled = true; } };
            zone.GotKeyboardFocus += (s, e) => Lit(true);
            zone.LostKeyboardFocus += (s, e) => Lit(false);
            return zone;
        }

        /// <summary>Plans a Quick pack of these items on the page, as a drop on the Quick pack card does.</summary>
        internal void VdStartPack(string[] items) => StartPack(items);

        private void StartPack(string[] items)
        {
            items = items.Where(i => File.Exists(i) || Directory.Exists(i.TrimEnd('\\'))).ToArray();
            if (items.Length == 0) { VdSay("Nothing to pack: the items are no longer there.", "B.Amber"); VdRefresh(); return; }
            var p = new PackRun { Items = items };
            _pack = p;
            VdRefresh();
            Task.Run(() =>
            {
                p.Walk = PackCopy.Walk(items);
                p.Plan = QuickPackPlan.For(items, entries: p.Walk.Select(w => (w.Size, w.Folder)));
                p.SizeBytes = p.Plan.StartBytes;
            }).ContinueWith(t => Dispatcher.BeginInvoke(new Action(() =>
            {
                if (t.IsFaulted) { p.Phase = "stopped"; p.Error = t.Exception?.GetBaseException().Message; }
                else p.Phase = "ready";
                VdRefresh();
            })));
        }

        private FrameworkElement PackPlanBlock(PackRun p)
        {
            var sp = new StackPanel();
            var plan = p.Plan;
            void Field(string label, string value, bool literal = true)
            {
                var row = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
                var l = new TextBlock { Text = label, Width = 120, FontSize = 13.5, Foreground = Ui.Res("B.TextSoft"), VerticalAlignment = VerticalAlignment.Top, TextWrapping = TextWrapping.Wrap };
                DockPanel.SetDock(l, Dock.Left);
                row.Children.Add(l);
                var v = Ui.Mono(value, Ui.Res("B.Text"), 13);
                v.TextWrapping = TextWrapping.Wrap;
                if (literal) Core.Language.SetLiteral(v, true);
                row.Children.Add(v);
                sp.Children.Add(row);
            }
            Field("Name", Path.GetFileName(plan.File));
            if (plan.Numbered) sp.Children.Add(ToolsNote(plan.Name + ".vhdx is already in that folder, so the new one is numbered. Nothing is ever overwritten."));
            Field("Saved in", PathDisplay.Shown(plan.Folder));
            if (plan.MixedFolders) sp.Children.Add(ToolsNote("The items came from more than one folder, so the disk goes in the first item's folder.", "B.Amber"));
            Field("Contents", $"{plan.Files:N0} {(plan.Files == 1 ? "file" : "files")} in {plan.Folders:N0} {(plan.Folders == 1 ? "folder" : "folders")}, {Ui.Bytes(plan.ContentBytes)}", literal: false);

            var type = new Segmented("Type", new[] { new Choice("dynamic", "Grows as it fills"), new Choice("fixed", "Fixed size") }, p.Fixed ? "fixed" : "dynamic") { Margin = new Thickness(0, 4, 0, 8), HorizontalAlignment = HorizontalAlignment.Left };
            type.Changed += v => p.Fixed = v == "fixed";
            sp.Children.Add(DialogParts.Label("Type")); sp.Children.Add(type);
            var fs = new Segmented("File system", new[] { new Choice("NTFS", "NTFS"), new Choice("exFAT", "exFAT"), new Choice("ReFS", "ReFS") }, p.FileSystem) { Margin = new Thickness(0, 0, 0, 8), HorizontalAlignment = HorizontalAlignment.Left };
            fs.Changed += v => p.FileSystem = v;
            sp.Children.Add(DialogParts.Label("File system")); sp.Children.Add(fs);
            sp.Children.Add(Switch("Copy permissions too", "Off by default. Keeps who can open each file.", p.Permissions, v => p.Permissions = v));
            sp.Children.Add(Switch("Fit it tightly when done", "Off by default. Shrinks the disk to its smallest safe size plus a small margin.", p.Tight, v => p.Tight = v));

            var sizes = new StackPanel { Margin = new Thickness(0, 6, 0, 8) };
            sizes.Children.Add(new TextBlock { Text = "How the size was worked out", FontSize = 13.5, FontWeight = FontWeights.Medium, Foreground = Ui.Res("B.Text"), Margin = new Thickness(0, 0, 0, 4) });
            void S(string label, long bytes, bool sum = false)
            {
                var row = new DockPanel();
                var v = Ui.Mono(Ui.Bytes(bytes), Ui.Res(sum ? "B.Mint" : "B.TextSoft"), 12.5);
                DockPanel.SetDock(v, Dock.Right);
                row.Children.Add(v);
                row.Children.Add(new TextBlock { Text = label, FontSize = 13, Foreground = Ui.Res(sum ? "B.Text" : "B.TextSoft"), TextWrapping = TextWrapping.Wrap });
                sizes.Children.Add(row);
            }
            S("Files and folders", plan.ContentBytes);
            S("Rounding every file up to whole clusters", plan.RoundingBytes);
            S("File system records for every file and folder", plan.RecordBytes);
            S("Partition tables and reserved space", plan.TableBytes);
            S("Headroom", plan.HeadroomBytes);
            S("Starting size", plan.StartBytes, sum: true);
            sp.Children.Add(sizes);
            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(plan.File));
                long need = p.Fixed ? plan.StartBytes : plan.ContentBytes + plan.RecordBytes + 64 * QuickPackPlan.Mb;
                sp.Children.Add(ToolsNote(need < drive.AvailableFreeSpace
                    ? $"{drive.Name.TrimEnd('\\')} has {Ui.Bytes(drive.AvailableFreeSpace)} free. The pack needs about {Ui.Bytes(need)} there."
                    : $"{drive.Name.TrimEnd('\\')} has {Ui.Bytes(drive.AvailableFreeSpace)} free, and the pack needs about {Ui.Bytes(need)}: {Ui.Bytes(need - drive.AvailableFreeSpace)} short.", need < drive.AvailableFreeSpace ? "B.TextMute" : "B.Amber"));
            }
            catch { }
            var buttons = new WrapPanel();
            buttons.Children.Add(Ui.Button($"Pack {Ui.Bytes(plan.ContentBytes)}", () => _ = RunPack(p), "Btn.Accent", Icons.Play));
            buttons.Children.Add(Ui.Button("Cancel", () => { _pack = null; VdRefresh(); }, "Btn.Ghost"));
            sp.Children.Add(buttons);
            return sp;
        }

        private async Task RunPack(PackRun p)
        {
            p.Phase = "packing";
            p.Cts = new CancellationTokenSource();
            p.Log.Add("Asking Windows for permission to make and attach the disk.");
            VdRefresh();
            var letters = VhdEngine.FreeLetters();
            if (letters.Count == 0) { p.Phase = "stopped"; p.Error = "Every drive letter from D to Z is in use, so the disk has nowhere to appear."; VdRefresh(); return; }
            var req = new VhdRequest
            {
                File = p.Plan.File, SizeMb = Math.Max(VhdEngine.MinMb, p.Plan.StartBytes / QuickPackPlan.Mb), Fixed = p.Fixed, FileSystem = p.FileSystem,
                Label = QuickPackPlan.LabelFor(p.Plan.Name), MountMode = "letter", Letter = letters[0]
            };
            var problem = VhdEngine.Validate(req, checkFolderState: false);
            if (problem != null) { p.Phase = "stopped"; p.Error = problem; VdRefresh(); return; }
            var session = new VhdSession(req);
            p.Session = session;
            if (!await session.StartAsync())
            {
                p.Phase = "cancelled"; p.Error = session.Declined ? "Windows asked for permission and it was not given, so nothing was made." : session.LaunchError;
                VdRefresh(); return;
            }
            var ready = await session.WaitForAsync(s => s.Phase == "ready" || s.Phase == "error", TimeSpan.FromMinutes(10), CancellationToken.None, s => { p.Log.Add(s.Message); VdRefreshSoon(); });
            if (ready?.Phase != "ready") { p.Phase = "stopped"; p.Error = ready?.Error ?? "The disk was not made."; VdRefresh(); return; }
            p.Log.Add($"Made {Path.GetFileName(req.File)}: {Ui.Bytes(req.SizeMb * QuickPackPlan.Mb)}, {(p.Fixed ? "fixed size" : "grows as it fills")}, GPT, {p.FileSystem}, at {req.Letter}:.");
            var copy = new PackCopy { CopyPermissions = p.Permissions };
            p.Copy = copy;
            long current = req.SizeMb * QuickPackPlan.Mb;
            copy.Grow = async left =>
            {
                long to = QuickPackPlan.GrowTo(current, left);
                if (to <= current) return "The disk is at the largest size a VHDX allows.";
                p.Log.Add($"Ran out of room at {p.Progress?.Current}. Growing the disk from {Ui.Bytes(current)} to {Ui.Bytes(to)}.");
                await Dispatcher.InvokeAsync(VdRefresh);
                session.Send("grow " + to / QuickPackPlan.Mb);
                var g = await session.WaitForAsync(s => s.Phase == "grown" || s.Phase == "grow-failed" || s.Phase == "error", TimeSpan.FromMinutes(10), CancellationToken.None);
                if (g?.Phase != "grown") return g?.Error ?? "The disk could not grow.";
                (p.Result ??= new PackResult()).Growths.Add((current, to));
                p.Log.Add($"Grew from {Ui.Bytes(current)} to {Ui.Bytes(to)}, extended the volume, and carried on, checking that file again.");
                current = to;
                return null;
            };
            var progress = new Progress<PackProgress>(pr => { p.Progress = pr; VdRefreshSoon(); });
            var growths = new List<(long, long)>();
            var r = await copy.RunAsync(p.Walk, VhdEngine.MountRoot(req), progress, p.Cts.Token);
            if (p.Result != null) r.Growths.AddRange(p.Result.Growths);
            p.Result = r;
            if (r.Cancelled)
            {
                await session.DetachAsync(delete: true);
                p.Phase = "cancelled";
                p.Error = $"Cancelled. Detached and removed {Path.GetFileName(req.File)}, the only file this run made. Your files were not touched.";
                LocalHistory.Append("vdisk", "Quick pack cancelled: " + Path.GetFileName(req.File) + " removed", null, req.File);
                VdRefresh(); return;
            }
            if (r.Error != null)
            {
                // stopped (for example, the host drive cannot hold the growth): keep nothing half-made
                await session.DetachAsync(delete: true);
                p.Phase = "stopped";
                p.Error = r.Error + " The new disk was detached and removed; your files were not touched.";
                LocalHistory.Append("vdisk", "Quick pack stopped: " + Path.GetFileName(req.File), r.Error, req.File);
                VdRefresh(); return;
            }
            if (p.Tight)
            {
                session.Send("fit");
                var f = await session.WaitForAsync(s => s.Phase == "fitted" || s.Phase == "fit-failed" || s.Phase == "error", TimeSpan.FromMinutes(10), CancellationToken.None);
                p.Log.Add(f?.Phase == "fitted" ? f.Message : "Could not fit it tightly: " + (f?.Error ?? "no answer") + ". It keeps its size.");
            }
            await session.DetachAsync(delete: false);
            var info = VirtDisk.Read(req.File);
            p.Phase = r.Partial ? "partial" : "done";
            p.Log.Add($"Detached. {Path.GetFileName(req.File)} sits beside the originals.");
            LocalHistory.Append("vdisk", (r.Partial ? "Quick pack, partial: " : "Quick pack: ") + Path.GetFileName(req.File),
                $"{r.Verified:N0} of {r.Files:N0} files match by SHA-256. Virtual size {Ui.Bytes((long)info.VirtualSize)}, on disk {Ui.Bytes((long)info.PhysicalSize)}." + (r.Growths.Count > 0 ? $" Grew {r.Growths.Count} time{(r.Growths.Count == 1 ? "" : "s")}." : ""), req.File);
            Activity.Add(ActivityKind.Files, (r.Partial ? "Packed with skipped files into " : "Packed into ") + Path.GetFileName(req.File), req.File, !r.Partial);
            VdAdd(new[] { req.File });
            VdRefresh();
        }

        private DateTime _vdLastSoon;
        /// <summary>Redraws the page at most a few times a second while a pack runs.</summary>
        private void VdRefreshSoon()
        {
            if ((DateTime.Now - _vdLastSoon).TotalMilliseconds < 400) return;
            _vdLastSoon = DateTime.Now;
            Dispatcher.BeginInvoke(new Action(VdRefresh));
        }

        private FrameworkElement PackProgressBlock(PackRun p)
        {
            var sp = new StackPanel();
            var pr = p.Progress;
            double frac = pr == null || pr.Total == 0 ? 0 : pr.Done / (double)pr.Total;
            var big = new WrapPanel();
            big.Children.Add(new TextBlock { Text = $"{frac * 100:0}%", FontSize = 36, FontFamily = Ui.Font("F.Display"), Foreground = Ui.Res("B.Text"), Margin = new Thickness(0, 0, 12, 0) });
            big.Children.Add(new TextBlock { Text = pr == null ? "Starting…" : $"{Ui.Bytes(pr.Done)} of {Ui.Bytes(pr.Total)}", FontSize = 14, Foreground = Ui.Res("B.TextSoft"), VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 8) });
            sp.Children.Add(big);
            var track = new Grid { Height = 10, Margin = new Thickness(0, 4, 0, 10) };
            track.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(frac, 0.0001), GridUnitType.Star) });
            track.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(1 - frac, 0.0001), GridUnitType.Star) });
            var bg = new Border { CornerRadius = new CornerRadius(5) }; bg.SetResourceReference(Border.BackgroundProperty, "B.Sunken"); Grid.SetColumnSpan(bg, 2);
            var fill = new Border { CornerRadius = new CornerRadius(5) }; fill.SetResourceReference(Border.BackgroundProperty, "B.Mint");
            track.Children.Add(bg); track.Children.Add(fill);
            AutomationProperties.SetName(track, $"Packed {frac * 100:0} percent");
            sp.Children.Add(track);
            if (pr != null)
            {
                var figs = new CardGrid { Columns = 4, MinColumnWidth = 130, Margin = new Thickness(0, 0, -12, 4) };
                void F(string l, string v) { var f = Stat(l, v); f.Margin = new Thickness(0, 0, 12, 10); figs.Children.Add(f); }
                F("Files", $"{pr.FilesDone:N0} of {pr.FilesTotal:N0}");
                F("Speed", pr.Paused ? "paused" : Ui.Bytes((long)pr.BytesPerSecond) + "/s");
                F("Time left", pr.Paused ? "paused" : pr.Left is TimeSpan t ? (t.TotalMinutes >= 1 ? $"about {Math.Ceiling(t.TotalMinutes)} min" : "under a minute") : "working it out");
                F("Grew by itself", (p.Result?.Growths.Count ?? 0) == 0 ? "not yet" : $"{p.Result.Growths.Count} time{(p.Result.Growths.Count == 1 ? "" : "s")}");
                sp.Children.Add(figs);
                if (pr.Current != null)
                {
                    var cur = Ui.Mono(pr.Current, Ui.Res("B.TextSoft"), 12);
                    cur.TextWrapping = TextWrapping.Wrap; cur.Margin = new Thickness(0, 0, 0, 8);
                    Core.Language.SetLiteral(cur, true);
                    AutomationProperties.SetName(cur, "Now copying " + pr.Current);
                    sp.Children.Add(cur);
                }
            }
            foreach (var line in p.Log.Skip(Math.Max(0, p.Log.Count - 4))) sp.Children.Add(ToolsNote(line, "B.TextSoft"));
            var buttons = new WrapPanel();
            if (p.Copy != null)
            {
                bool paused = !p.Copy.Running.IsSet;
                buttons.Children.Add(Ui.Button(paused ? "Resume" : "Pause", () => { if (p.Copy.Running.IsSet) p.Copy.Running.Reset(); else p.Copy.Running.Set(); VdRefresh(); }, "Btn.Ghost", paused ? Icons.Play : Icons.Pause));
            }
            buttons.Children.Add(Ui.Button("Cancel", () => { p.Copy?.Running.Set(); p.Cts?.Cancel(); }, "Btn.Ghost", Icons.Close));
            sp.Children.Add(buttons);
            return sp;
        }

        private FrameworkElement PackResultBlock(PackRun p)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 4, 0, 10) };
            var r = p.Result;
            string head, brush, body;
            switch (p.Phase)
            {
                case "done":
                    head = "Packed and checked"; brush = "B.Mint";
                    body = $"All {r.Verified:N0} files match their originals by SHA-256. The disk is detached and sits beside what you dropped.";
                    break;
                case "partial":
                    head = $"Partial: {r.Skipped.Count} file{(r.Skipped.Count == 1 ? " was" : "s were")} skipped"; brush = "B.Amber";
                    body = $"{r.Verified:N0} of {r.Files:N0} files match their originals. The pack is not complete until the ones below are in.";
                    break;
                case "cancelled": head = "Cancelled"; brush = "B.TextSoft"; body = p.Error; break;
                default: head = "Stopped"; brush = "B.Amber"; body = p.Error; break;
            }
            var box = new Border { CornerRadius = new CornerRadius(16), Padding = new Thickness(14, 10, 14, 12) };
            box.SetResourceReference(Border.BackgroundProperty, p.Phase == "done" ? "B.SecondaryContainer" : p.Phase == "cancelled" ? "B.Raised" : "B.AmberWash");
            var inner = new StackPanel();
            inner.Children.Add(new TextBlock { Text = head, FontSize = 16, FontWeight = FontWeights.Medium, Foreground = Ui.Res(brush), TextWrapping = TextWrapping.Wrap });
            if (!string.IsNullOrEmpty(body)) inner.Children.Add(new TextBlock { Text = body, FontSize = 13, Foreground = Ui.Res("B.TextSoft"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
            box.Child = inner;
            AutomationProperties.SetLiveSetting(box, AutomationLiveSetting.Polite);
            sp.Children.Add(box);
            if (r != null && (p.Phase == "done" || p.Phase == "partial"))
            {
                var file = p.Plan.File;
                var info = VirtDisk.Read(file);
                var path = Ui.Mono(PathDisplay.Shown(file), Ui.Res("B.Text"), 12.5);
                path.TextWrapping = TextWrapping.Wrap; path.Margin = new Thickness(0, 8, 0, 6);
                Core.Language.SetLiteral(path, true);
                sp.Children.Add(path);
                var figs = new CardGrid { Columns = 4, MinColumnWidth = 130, Margin = new Thickness(0, 0, -12, 4) };
                void F(string l, string v) { var f = Stat(l, v); f.Margin = new Thickness(0, 0, 12, 10); figs.Children.Add(f); }
                F("Virtual size", Ui.Bytes((long)info.VirtualSize));
                F("On disk", Ui.Bytes((long)info.PhysicalSize));
                F("Files", r.Files.ToString("N0"));
                F("Grew by itself", r.Growths.Count == 0 ? "not needed" : string.Join(", ", r.Growths.Select(g => Ui.Bytes(g.From) + " to " + Ui.Bytes(g.To))));
                sp.Children.Add(figs);
                foreach (var (src, why) in r.Skipped.Take(10))
                {
                    // the path stays as it is; the reason is a sentence of its own, so it is translated
                    var item = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };
                    var where = Ui.Mono(PathDisplay.Shown(src), Ui.Res("B.Text"), 12);
                    where.TextWrapping = TextWrapping.Wrap;
                    Core.Language.SetLiteral(where, true);
                    item.Children.Add(where);
                    item.Children.Add(new TextBlock { Text = char.ToUpperInvariant(why[0]) + why.Substring(1) + ".", FontSize = 12.5, Foreground = Ui.Res("B.Amber"), TextWrapping = TextWrapping.Wrap });
                    sp.Children.Add(item);
                }
                var acts = new WrapPanel();
                acts.Children.Add(Ui.Button("Show in folder", () => ActionProvider.Reveal(file), "Btn.Accent", Icons.Folder));
                acts.Children.Add(Ui.Button("Open in the disk library", () => { _vdSelected = file; _pack = null; VdRefresh(); }, "Btn.Ghost"));
                acts.Children.Add(Ui.Button("Attach now", async () => { _vdSelected = file; await VdRun(new DiskJob { Op = "attach", File = file }, info); }, "Btn.Ghost", Icons.Play));
                if (p.Phase == "partial") acts.Children.Add(Ui.Button("Pack again", () => StartPack(p.Items), "Btn.Ghost", Icons.Restart));
                sp.Children.Add(acts);
            }
            foreach (var line in p.Log.Where(l => l.StartsWith("Ran out") || l.StartsWith("Grew") || l.StartsWith("Could not fit") || l.StartsWith("Fitted"))) sp.Children.Add(ToolsNote(line, "B.TextSoft"));
            return sp;
        }
    }
}
