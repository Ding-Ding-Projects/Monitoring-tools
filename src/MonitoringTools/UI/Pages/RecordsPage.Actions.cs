using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using MonitoringTools.Clip;
using MonitoringTools.Core;
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI
{
    public partial class ControlCentreWindow
    {
        /// <summary>
        /// The append-only record of actions: discarded unsaved changes (with the text, to copy back), Base64 files
        /// (with Show and Undo) and virtual disk operations. Undo and copy-back add new lines; nothing is rewritten.
        /// </summary>
        private FrameworkElement RecordsActions()
        {
            var sp = new StackPanel();
            sp.Children.Add(CardTitle("Actions and discarded changes",
                "Every action this app records, newest first: unsaved changes you discarded, Base64 files it wrote and virtual disk operations. Discarded text is kept so you can copy it back. The record only grows: an undo or a copy-back is added as a new line. It stays on this computer."));
            var status = SafetyUi.Live("Actions status");
            var list = new StackPanel();
            void Build()
            {
                list.Children.Clear();
                var all = LocalHistory.Recent(400);
                var reversed = all.Where(a => a.Undoes != null).Select(a => a.Undoes).ToHashSet();
                var shown = all.Where(a => a.Kind != "base64-undo" && a.Kind != "restore").Take(30).ToList();
                if (shown.Count == 0)
                {
                    list.Children.Add(ToolsNote("Nothing recorded yet. Discarding unsaved changes, writing Base64 beside a file or changing a virtual disk adds a line here."));
                    return;
                }
                foreach (var a in shown)
                {
                    var row = new Border { CornerRadius = new CornerRadius(14), Padding = new Thickness(14, 10, 8, 10), Margin = new Thickness(0, 0, 0, 6) };
                    row.SetResourceReference(Border.BackgroundProperty, "B.Sunken");
                    var g = new Grid();
                    g.ColumnDefinitions.Add(new ColumnDefinition());
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                    text.Children.Add(new TextBlock { Text = a.Title, Style = Ui.Style("T.Body"), FontSize = 13.5, Foreground = Ui.Res("B.Text"), TextWrapping = TextWrapping.Wrap });
                    var when = a.At.ToString("d MMM HH:mm") + (reversed.Contains(a.Id) ? (a.Kind == "discard" ? ", copied back" : ", undone") : "");
                    var meta = new TextBlock { Text = when + (string.IsNullOrEmpty(a.Detail) ? "" : ". " + a.Detail), Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
                    text.Children.Add(meta);
                    if (a.Path != null)
                    {
                        var path = Ui.Mono(PathDisplay.Shown(a.Path), Ui.Res("B.TextSoft"), 11.5);
                        path.TextWrapping = TextWrapping.Wrap; path.Margin = new Thickness(0, 3, 0, 0);
                        Core.Language.SetLiteral(path, true);
                        text.Children.Add(path);
                    }
                    g.Children.Add(text);
                    var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
                    var entry = a;
                    if (a.Kind == "discard")
                    {
                        var copy = Ui.Button("Copy it back", () =>
                        {
                            var kept = LocalHistory.KeptText(entry);
                            if (kept == null) { status.Text = HistoryVault.IsLocked ? "Unlock the protected history above to read this text." : "The text was not kept for this one."; return; }
                            ClipboardWatcher.SetText(kept);
                            LocalHistory.Append("restore", "Copied back the text discarded in " + entry.Title.Replace("Discarded unsaved changes in ", ""), null, undoes: entry.Id);
                            status.Text = "Copied. The discarded text is on the clipboard.";
                            Build();
                        }, "Btn.Ghost", Icons.Clipboard);
                        copy.IsEnabled = entry.Kept != null;
                        if (entry.Kept == null) copy.ToolTip = "The text was not kept for this one.";
                        buttons.Children.Add(copy);
                    }
                    else if (a.Path != null && File.Exists(a.Path))
                    {
                        buttons.Children.Add(Ui.Button("Show", () => ActionProvider.Reveal(entry.Path), "Btn.Ghost"));
                        if (a.Kind == "base64" && a.Sha256 != null && !reversed.Contains(a.Id))
                            buttons.Children.Add(Ui.Button("Undo", async () =>
                            {
                                string now = null;
                                try { now = await Task.Run(() => Base64Files.Sha256File(entry.Path)); } catch { }
                                if (!string.Equals(now, entry.Sha256, StringComparison.OrdinalIgnoreCase)) { status.Text = Path.GetFileName(entry.Path) + " has changed since it was written, so it was left alone."; return; }
                                var r = await FileOps.RecycleAsync(new[] { entry.Path });
                                if (r.All(x => x.Ok))
                                {
                                    LocalHistory.Append("base64-undo", "Undid " + Path.GetFileName(entry.Path) + ": moved it to the Recycle Bin", null, entry.Path, undoes: entry.Id);
                                    status.Text = "Undone. " + Path.GetFileName(entry.Path) + " is in the Recycle Bin, and the file it came from was not touched.";
                                }
                                else status.Text = "Could not undo: " + r.First(x => !x.Ok).Error;
                                Build();
                            }, "Btn.Ghost", Icons.Restart));
                    }
                    Grid.SetColumn(buttons, 1);
                    g.Children.Add(buttons);
                    row.Child = g;
                    list.Children.Add(row);
                }
                Core.Language.Refresh(list);
            }
            Build();
            sp.Children.Add(status);
            sp.Children.Add(list);
            var foot = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            foot.Children.Add(Ui.Button("Open the record's folder", () => ActionProvider.Shell(Path.GetDirectoryName(LocalHistory.FilePath)), "Btn.Ghost", Icons.Folder));
            sp.Children.Add(foot);
            return SearchScope.SetItem(Card(sp), "actions discarded unsaved changes undo restore base64 virtual disk history record");
        }
    }
}
