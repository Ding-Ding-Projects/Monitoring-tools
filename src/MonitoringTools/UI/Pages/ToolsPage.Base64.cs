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
using System.Windows.Media;
using Microsoft.Win32;
using MonitoringTools.Core;
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI
{
    public partial class ControlCentreWindow
    {
        /// <summary>Results from this session, newest first, so they survive moving between pages.</summary>
        private static readonly List<(Base64Files.Result Result, LocalAction Entry)> Base64Done = new List<(Base64Files.Result, LocalAction)>();
        private static CancellationTokenSource _base64Cancel;

        /// <summary>The Base64 drop: a file gets its Base64 text beside it, and that text file gets its original back beside it.</summary>
        private FrameworkElement ToolsBase64Card()
        {
            var sp = new StackPanel();
            sp.Children.Add(CardTitle("Base64 beside the file",
                "Drop any file here and its Base64 text is written next to it as name.ext.base64.txt. Drop that text file and the original comes back next to it, with its extension. Nothing is overwritten: a name that is taken gets a number. Every result is checked by a SHA-256 round trip before it counts, and each one can be undone."));

            var status = ToolsNote("");
            AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite);
            var track = new Grid { Height = 6, Margin = new Thickness(0, 2, 0, 8), Visibility = Visibility.Collapsed };
            track.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0, GridUnitType.Star) });
            track.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var trackBg = new Border { CornerRadius = new CornerRadius(3) };
            trackBg.SetResourceReference(Border.BackgroundProperty, "B.Sunken");
            Grid.SetColumnSpan(trackBg, 2);
            var fill = new Border { CornerRadius = new CornerRadius(3) };
            fill.SetResourceReference(Border.BackgroundProperty, "B.Mint");
            track.Children.Add(trackBg);
            track.Children.Add(fill);
            AutomationProperties.SetName(track, "Base64 progress");
            var cancel = Ui.Button("Cancel", () => _base64Cancel?.Cancel(), "Btn.Ghost", Icons.Close);
            cancel.Visibility = Visibility.Collapsed;
            var list = new StackPanel();

            void Progress(double p)
            {
                p = Math.Max(0, Math.Min(1, p));
                track.ColumnDefinitions[0].Width = new GridLength(p, GridUnitType.Star);
                track.ColumnDefinitions[1].Width = new GridLength(1 - p, GridUnitType.Star);
            }

            async Task Run(string[] paths)
            {
                var files = paths.Where(File.Exists).ToArray();
                if (files.Length == 0) { B64Say(status, "Only files can be converted. Folders are left as they are.", "B.Amber"); return; }
                if (_base64Cancel != null) { B64Say(status, "A conversion is still running. Wait for it, or cancel it first.", "B.Amber"); return; }
                _base64Cancel = new CancellationTokenSource();
                var ct = _base64Cancel.Token;
                track.Visibility = Visibility.Visible; cancel.Visibility = Visibility.Visible; Progress(0);
                int ok = 0, bad = 0;
                try
                {
                    for (int i = 0; i < files.Length && !ct.IsCancellationRequested; i++)
                    {
                        var f = files[i];
                        int index = i;
                        B64Say(status, (Base64Files.IsEncoded(f) ? "Decoding " : "Encoding ") + Path.GetFileName(f) + (files.Length > 1 ? $" ({i + 1} of {files.Length})" : ""), "B.TextSoft");
                        var progress = new Progress<double>(p => Progress((index + p) / files.Length));
                        var r = await Task.Run(() => Base64Files.Run(f, ct, progress));
                        LocalAction entry = null;
                        if (r.Ok)
                        {
                            ok++;
                            var what = Base64Files.IsEncoded(f) ? "Decoded " + Path.GetFileName(f) + " to " + Path.GetFileName(r.Output) : "Wrote " + Path.GetFileName(r.Output) + " beside " + Path.GetFileName(f);
                            entry = LocalHistory.Append("base64", what, "SHA-256 round trip matches.", r.Output, r.WrittenSha256);
                            Activity.Add(ActivityKind.Files, what, r.Output, true);
                        }
                        else if (!r.Cancelled)
                        {
                            bad++;
                            Activity.Add(ActivityKind.Files, "Base64 stopped for " + Path.GetFileName(f) + ": " + r.Error, f, false);
                        }
                        if (!r.Cancelled) Base64Done.Insert(0, (r, entry));
                    }
                }
                finally
                {
                    bool cancelled = ct.IsCancellationRequested;
                    _base64Cancel.Dispose(); _base64Cancel = null;
                    track.Visibility = Visibility.Collapsed; cancel.Visibility = Visibility.Collapsed;
                    while (Base64Done.Count > 12) Base64Done.RemoveAt(Base64Done.Count - 1);
                    B64Fill(list, status);
                    if (cancelled) B64Say(status, "Cancelled. The file that was being written was removed, and the files you dropped were not touched.", "B.Amber");
                    else if (bad == 0) B64Say(status, ok == 1 ? "Done. One file written beside its original and checked." : $"Done. {ok} files written beside their originals and checked.", "B.Mint");
                    else B64Say(status, $"{ok} written, {bad} stopped. Each stopped file says why below.", "B.Amber");
                }
            }

            var zone = new Border { CornerRadius = new CornerRadius(18), Padding = new Thickness(18, 14, 14, 14), Margin = new Thickness(0, 4, 0, 8), BorderThickness = new Thickness(1.5), AllowDrop = true, Focusable = true, Cursor = Cursors.Hand, FocusVisualStyle = null };
            zone.SetResourceReference(Border.BackgroundProperty, "B.MintWash");
            zone.SetResourceReference(Border.BorderBrushProperty, "B.Line");
            var zg = new Grid();
            zg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            zg.ColumnDefinitions.Add(new ColumnDefinition());
            zg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var chip = Ui.IconChip(Icons.File, Ui.Res("B.Mint"), Ui.Res("B.Sunken"), 40, 20);
            chip.Margin = new Thickness(0, 0, 14, 0); chip.VerticalAlignment = VerticalAlignment.Center;
            zg.Children.Add(chip);
            var zt = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            zt.Children.Add(new TextBlock { Text = "Drop files here", Style = Ui.Style("T.Title"), FontSize = 15, TextWrapping = TextWrapping.Wrap });
            zt.Children.Add(new TextBlock { Text = "Or choose them. A .base64.txt file turns back into its original.", Style = Ui.Style("T.Body"), FontSize = 12.5, Foreground = Ui.Res("B.TextSoft"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
            Grid.SetColumn(zt, 1);
            zg.Children.Add(zt);
            void Pick()
            {
                var dlg = new OpenFileDialog { Title = "Choose files to encode, or .base64.txt files to decode", Multiselect = true, Filter = "All files|*.*|Base64 text|*.base64.txt;*.b64;*.base64" };
                if (dlg.ShowDialog() == true) _ = Run(dlg.FileNames);
            }
            var choose = Ui.Button("Choose files…", Pick, "Btn.Ghost", Icons.Folder);
            choose.VerticalAlignment = VerticalAlignment.Center; choose.Margin = new Thickness(12, 0, 0, 0);
            Grid.SetColumn(choose, 2);
            zg.Children.Add(choose);
            zone.Child = zg;
            AutomationProperties.SetName(zone, "Drop files here to write Base64 beside them, or press Enter to choose files");
            void Lit(bool on)
            {
                zone.SetResourceReference(Border.BorderBrushProperty, on ? "B.Mint" : "B.Line");
                zone.SetResourceReference(Border.BackgroundProperty, on ? "B.SecondaryContainer" : "B.MintWash");
            }
            zone.DragEnter += (s, e) => { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; Lit(true); e.Handled = true; };
            zone.DragOver += (s, e) => { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
            zone.DragLeave += (s, e) => Lit(false);
            zone.Drop += (s, e) => { Lit(false); if (e.Data.GetData(DataFormats.FileDrop) is string[] dropped) _ = Run(dropped); e.Handled = true; };
            zone.MouseLeftButtonUp += (s, e) => { if (e.OriginalSource is DependencyObject d && IsWithinElement(d, choose)) return; Pick(); };
            zone.KeyDown += (s, e) => { if (e.Key == Key.Enter || e.Key == Key.Space) { Pick(); e.Handled = true; } };
            zone.GotKeyboardFocus += (s, e) => Lit(true);
            zone.LostKeyboardFocus += (s, e) => Lit(false);

            sp.Children.Add(zone);
            var progressRow = new DockPanel();
            DockPanel.SetDock(cancel, Dock.Right);
            progressRow.Children.Add(cancel);
            progressRow.Children.Add(track);
            sp.Children.Add(progressRow);
            sp.Children.Add(status);
            sp.Children.Add(list);
            B64Fill(list, status);
            return SearchScope.SetItem(Card(sp), "base64 encode decode drop file beside text b64 data url");
        }

        private static bool IsWithinElement(DependencyObject d, DependencyObject ancestor)
        {
            for (var x = d; x != null; x = x is Visual || x is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(x) : LogicalTreeHelper.GetParent(x))
                if (x == ancestor) return true;
            return false;
        }

        private static void B64Say(TextBlock t, string text, string brush)
        {
            t.Text = text;
            t.Foreground = Ui.Res(brush);
            Core.Language.Refresh(t);
            try { System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(t)?.RaiseAutomationEvent(System.Windows.Automation.Peers.AutomationEvents.LiveRegionChanged); } catch { }
        }

        /// <summary>The session's results, each with Show and Undo.</summary>
        private static void B64Fill(StackPanel list, TextBlock status)
        {
            list.Children.Clear();
            foreach (var (r, entry) in Base64Done)
            {
                var row = new Border { CornerRadius = new CornerRadius(14), Padding = new Thickness(14, 10, 8, 10), Margin = new Thickness(0, 0, 0, 6) };
                row.SetResourceReference(Border.BackgroundProperty, "B.Sunken");
                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition());
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                var shownPath = PathDisplay.Shown(r.Ok ? r.Output : r.Source);
                var path = Ui.Mono(shownPath, Ui.Res(r.Ok ? "B.Text" : "B.Amber"), 12);
                path.TextWrapping = TextWrapping.Wrap;
                Core.Language.SetLiteral(path, true);
                text.Children.Add(path);
                string note;
                if (!r.Ok) note = "Stopped: " + r.Error;
                else if (Base64Files.IsEncoded(r.Source))
                    note = "Decoded from " + Path.GetFileName(r.Source) + ", " + Ui.Bytes(r.OutBytes) + (r.DetectedType != null ? ", looks like a " + r.DetectedType : "") + ". The round trip matches.";
                else note = "From " + Path.GetFileName(r.Source) + ", " + Ui.Bytes(r.InBytes) + " to " + Ui.Bytes(r.OutBytes) + ". The round trip matches.";
                if (r.Ok && !string.Equals(Path.GetFileName(r.Output), Path.GetFileName(Base64Files.IsEncoded(r.Source) ? Base64Files.DecodedName(r.Source) : Base64Files.EncodedName(r.Source)), StringComparison.OrdinalIgnoreCase))
                    note += " The usual name was taken, so this one is numbered.";
                text.Children.Add(new TextBlock { Text = note, Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) });
                g.Children.Add(text);
                var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
                if (r.Ok && File.Exists(r.Output))
                {
                    buttons.Children.Add(Ui.Button("Show", () => ActionProvider.Reveal(r.Output), "Btn.Ghost"));
                    var res = r; var ent = entry;
                    buttons.Children.Add(Ui.Button("Undo", async () =>
                    {
                        string current = null;
                        try { current = await Task.Run(() => Base64Files.Sha256File(res.Output)); } catch { }
                        if (current == null || !string.Equals(current, res.WrittenSha256, StringComparison.OrdinalIgnoreCase))
                        { B64Say(status, Path.GetFileName(res.Output) + " has changed since it was written, so it was left alone.", "B.Amber"); return; }
                        var done = await FileOps.RecycleAsync(new[] { res.Output });
                        if (done.All(x => x.Ok))
                        {
                            LocalHistory.Append("base64-undo", "Undid " + Path.GetFileName(res.Output) + ": moved it to the Recycle Bin", null, res.Output, null, undoes: ent?.Id);
                            Activity.Add(ActivityKind.Files, "Moved " + Path.GetFileName(res.Output) + " to the Recycle Bin", res.Output, true);
                            Base64Done.RemoveAll(x => x.Result == res);
                            B64Fill(list, status);
                            B64Say(status, "Undone. " + Path.GetFileName(res.Output) + " is in the Recycle Bin, and the file it came from was not touched.", "B.Mint");
                        }
                        else B64Say(status, "Could not undo: " + done.First(x => !x.Ok).Error, "B.Amber");
                    }, "Btn.Ghost", Icons.Restart));
                }
                Grid.SetColumn(buttons, 1);
                g.Children.Add(buttons);
                row.Child = g;
                list.Children.Add(row);
            }
        }
    }
}
