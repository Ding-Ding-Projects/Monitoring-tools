using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MonitoringTools.Core;
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI
{
    public partial class ControlCentreWindow
    {
        // The records page. Its content is built in this file only.
        private FrameworkElement RecordsPage()
        {
            var root = PageRoot("Records", "Records and exports", "Notifications, settings history, exports and the changelog.");
            root.Children.Add(RecordsNotifications());
            root.Children.Add(RecordsSettingsHistory());
            root.Children.Add(RecordsLocalHistory());
            root.Children.Add(RecordsExports());
            root.Children.Add(RecordsExportEverything());
            root.Children.Add(RecordsChangelog());
            return root;
        }

        // -------------------------------------------------------------- small helpers

        private static string RecordsAgo(DateTime t)
        {
            var d = DateTime.Now - t;
            if (d.TotalMinutes < 1) return "just now";
            if (d.TotalHours < 1) return $"{(int)d.TotalMinutes} min ago";
            if (d.TotalDays < 1) return $"{(int)d.TotalHours} h ago";
            return $"{(int)d.TotalDays} d ago";
        }

        private static void RunOnUi(Action a) => Application.Current?.Dispatcher.BeginInvoke(a);

        /// <summary>A row of exclusive chips that does not touch the saved settings.</summary>
        private static FrameworkElement Pick<T>(string label, IEnumerable<(string Label, T Value)> options, T current, Action<T> set, string tip)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 4, 0, 6) };
            sp.Children.Add(new TextBlock { Text = label, Foreground = Ui.Res("B.Text"), FontSize = 14, FontFamily = Ui.Font("F.Body"), Margin = new Thickness(0, 0, 0, 8) });
            var wrap = new WrapPanel();
            var buttons = new List<(Button B, T V)>();
            void Paint()
            {
                foreach (var (b, v) in buttons)
                {
                    bool on = EqualityComparer<T>.Default.Equals(v, current);
                    b.Background = on ? Ui.Res("B.Mint") : Ui.Res("B.Raised");
                    b.Foreground = on ? Ui.Res("B.MintInk") : Ui.Res("B.TextSoft");
                    b.BorderBrush = on ? Ui.Res("B.Mint") : Ui.Res("B.LineStrong");
                }
            }
            foreach (var (l, v) in options)
            {
                var val = v;
                var b = Ui.Button(l, null, "Btn.Chip");
                b.ToolTip = tip + ": " + l;
                b.Click += (s, e) => { current = val; set(val); Paint(); };
                buttons.Add((b, v));
                wrap.Children.Add(b);
            }
            Paint();
            sp.Children.Add(wrap);
            return sp;
        }

        // -------------------------------------------------------------- notification centre

        private FrameworkElement RecordsNotifications()
        {
            var box = new StackPanel();
            box.Children.Add(CardTitle("Notification centre", $"Every toast, notice, offline alert and IP change since the app started, newest first. Kept in memory only, up to {NotificationLog.Cap}."));

            var search = new SearchField("records:notifications", "Search notifications") { Margin = new Thickness(0, 0, 0, 10) };
            box.Children.Add(search);

            string kind = null;
            var chips = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
            var chipButtons = new List<(Button B, string K)>();
            void PaintChips()
            {
                foreach (var (b, k) in chipButtons)
                {
                    bool on = k == kind;
                    b.Background = on ? Ui.Res("B.Mint") : Ui.Res("B.Raised");
                    b.Foreground = on ? Ui.Res("B.MintInk") : Ui.Res("B.TextSoft");
                    b.BorderBrush = on ? Ui.Res("B.Mint") : Ui.Res("B.LineStrong");
                }
            }

            var list = new StackPanel();
            const int MaxRows = 200;

            // Select mode: the same reusable helper as the other lists. The list rebuilds itself, so it supplies its own refresh.
            var barHost = new ContentControl { IsTabStop = false };
            BulkSelect<NotificationEntry> bulk = null;
            Action rebuildAll = null;
            bulk = new BulkSelect<NotificationEntry>(() => rebuildAll(), "notifications");
            string Noun(int n) => n == 1 ? "notification" : "notifications";
            void RebuildBar() => barHost.Content = bulk.Bar(new[]
            {
                new BulkAction<NotificationEntry> { Label = "Copy", Hint = "Copy the chosen notifications to the clipboard as plain text.",
                    Run = items => BulkSelect<NotificationEntry>.CopyText(string.Join(Environment.NewLine, items.Select(ListActions.NotificationLine)), items.Count, Noun(items.Count)) },
                new BulkAction<NotificationEntry> { Label = "Export as text…", Hint = "Save the chosen notifications to a text file.",
                    Run = items => BulkSelect<NotificationEntry>.SaveText("notifications.txt", "Text|*.txt", string.Join(Environment.NewLine, items.Select(ListActions.NotificationLine)), items.Count, Noun(items.Count)) },
                new BulkAction<NotificationEntry> { Label = "Remove", Danger = true, Hint = "Remove only the chosen notifications from the centre, after you confirm. Others stay.",
                    Run = items =>
                    {
                        var chosen = items.ToList();
                        bulk.ConfirmThen($"Remove {chosen.Count} {Noun(chosen.Count)}?", "They leave the notification centre for good, because they are only kept in memory. Other notifications are not touched.", "Remove", () => NotificationLog.Remove(chosen));
                    } },
            });

            void Rebuild()
            {
                var all = NotificationLog.All();
                bulk.Prune(all);
                bulk.Begin();
                var byKind = all.Where(n => kind == null || n.Kind == kind).ToList();
                var hits = byKind.Where(n => search.State.Matches(n.Kind + " " + n.Title + " " + n.Detail)).ToList();
                list.Children.Clear();
                if (all.Count == 0)
                    list.Children.Add(new TextBlock { Text = "Nothing has been shown yet. Toasts and alerts will appear here as they happen.", Style = Ui.Style("T.Body"), Foreground = Ui.Res("B.TextMute"), TextWrapping = TextWrapping.Wrap });
                foreach (var n in hits.Take(MaxRows))
                {
                    var g = new Grid { Margin = new Thickness(0, 0, 0, 8) };
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    var when = Ui.Mono(n.At.ToString("HH:mm:ss"), Ui.Res("B.TextMute")); when.ToolTip = n.At.ToString("yyyy-MM-dd HH:mm:ss");
                    g.Children.Add(when);
                    var k = Ui.Mono(n.Kind, n.Kind == "Offline" ? Ui.Res("B.Amber") : n.Kind == "IP changed" ? Ui.Res("B.Violet") : Ui.Res("B.Mint")); Grid.SetColumn(k, 1);
                    g.Children.Add(k);
                    var text = new StackPanel(); Grid.SetColumn(text, 2);
                    text.Children.Add(new TextBlock { Text = n.Title, Foreground = Ui.Res("B.Text"), FontSize = 13, FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap });
                    if (!string.IsNullOrWhiteSpace(n.Detail))
                        text.Children.Add(new TextBlock { Text = n.Detail, Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute"), TextWrapping = TextWrapping.Wrap });
                    g.Children.Add(text);
                    SearchScope.SetItem(g, "notification " + n.Kind);
                    list.Children.Add(bulk.Wrap(n, g, n.Title));
                }
                if (hits.Count > MaxRows)
                    list.Children.Add(new TextBlock { Text = $"Showing the newest {MaxRows} of {hits.Count}. Narrow the search or an export to see the rest.", Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute") });
                search.SetCount(hits.Count, byKind.Count, "notifications");
            }

            void AddChip(string label, string k)
            {
                var b = Ui.Button(label, () => { kind = k; PaintChips(); Rebuild(); }, "Btn.Chip");
                b.ToolTip = k == null ? "Show every kind of notification" : "Show only " + label + " notifications";
                chipButtons.Add((b, k));
                chips.Children.Add(b);
            }
            AddChip("All", null);
            foreach (var k in NotificationLog.Kinds) AddChip(k, k);
            box.Children.Add(chips);
            PaintChips();

            box.Children.Add(barHost);
            box.Children.Add(list);

            var clear = Ui.Button("Clear all", () =>
            {
                var dlg = new ConfirmWindow("Clear all notifications?", "This empties the notification centre. The notifications themselves are gone for good, because they are only kept in memory.", "Clear all", danger: true);
                dlg.Confirmed += _ => NotificationLog.Clear();
                dlg.Show();
            }, "Btn.Danger", Icons.Trash);
            clear.ToolTip = "Remove every entry from the notification centre, after you confirm";
            clear.Margin = new Thickness(0, 10, 8, 0);
            box.Children.Add(clear);

            rebuildAll = () => { Rebuild(); RebuildBar(); };
            RebuildBar();
            search.Filter += Rebuild;
            Action changed = () => RunOnUi(Rebuild);
            box.Loaded += (s, e) => { NotificationLog.Changed -= changed; NotificationLog.Changed += changed; Rebuild(); };
            box.Unloaded += (s, e) => NotificationLog.Changed -= changed;
            Rebuild();
            return SearchScope.SetItem(Card(box), "notification centre toasts alerts log");
        }

        // -------------------------------------------------------------- settings history

        // Release notes are written on GitHub in English; the language layer leaves their text as published.
        private static T LiteralText<T>(T element) where T : DependencyObject { Core.Language.SetLiteral(element, true); return element; }

        private FrameworkElement RecordsSettingsHistory()
        {
            var box = new StackPanel();
            box.Children.Add(CardTitle("Settings history", $"Each save that changes a setting keeps a snapshot, up to {SettingsHistory.Cap}. Restore puts a snapshot back after you confirm."));
            var status = new TextBlock { Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8), Visibility = Visibility.Collapsed };
            box.Children.Add(status);
            var list = new StackPanel();
            const int MaxRows = 50;

            // Select mode for the snapshot list: copy, export or delete the chosen ones. The newest snapshot is the current state and is never deleted.
            var barHost = new ContentControl { IsTabStop = false };
            BulkSelect<string> bulk = null;
            Action rebuildList = null;
            bulk = new BulkSelect<string>(() => rebuildList(), "snapshots");
            var shownSnaps = new Dictionary<string, SettingsSnapshot>();
            string Lines(IEnumerable<string> files) => string.Join(Environment.NewLine, files.Where(shownSnaps.ContainsKey).Select(f => shownSnaps[f].At.ToString("yyyy-MM-dd HH:mm:ss") + "  " + shownSnaps[f].Summary));
            string Noun(int n) => n == 1 ? "snapshot" : "snapshots";
            void RebuildBar() => barHost.Content = bulk.Bar(new[]
            {
                new BulkAction<string> { Label = "Copy", Hint = "Copy the time and summary of each chosen snapshot.",
                    Run = items => BulkSelect<string>.CopyText(Lines(items), items.Count, Noun(items.Count)) },
                new BulkAction<string> { Label = "Export as JSON...", Hint = "Save the chosen snapshots, with their full settings, to one JSON file.",
                    Run = items => BulkSelect<string>.SaveText("settings-snapshots.json", "JSON|*.json", SettingsHistory.ExportJson(items), items.Count, Noun(items.Count)) },
                new BulkAction<string> { Label = "Delete", Danger = true, Hint = "Delete the chosen snapshots, after you confirm. The newest one is the current state and is kept.",
                    Run = items =>
                    {
                        var chosen = items.ToList();
                        bulk.ConfirmThen($"Delete {chosen.Count} {Noun(chosen.Count)}?", "They are removed from the history for good. The newest snapshot is kept, and your current settings are not changed.", "Delete", () => { SettingsHistory.Delete(chosen); });
                    } },
            });
            box.Children.Add(barHost);
            box.Children.Add(list);

            void Rebuild()
            {
                list.Children.Clear();
                var snaps = SettingsHistory.List();
                bulk.Prune(snaps.Select(x => x.File));
                bulk.Begin();
                shownSnaps.Clear();
                RebuildBar();
                if (snaps.Count == 0)
                {
                    list.Children.Add(new TextBlock { Text = "No snapshots yet. The first one appears the next time a setting changes.", Style = Ui.Style("T.Body"), Foreground = Ui.Res("B.TextMute") });
                    return;
                }
                for (int i = 0; i < Math.Min(MaxRows, snaps.Count); i++)
                {
                    var (file, snap) = snaps[i];
                    var older = i + 1 < snaps.Count ? snaps[i + 1].Snap : null;
                    var row = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
                    var head = new Grid();
                    head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    var text = new StackPanel();
                    Core.Language.SetLiteral(text, true);   // the summary names internal setting keys, so it stays as recorded
                    var when = Ui.Mono(snap.At.ToString("yyyy-MM-dd HH:mm:ss") + (i == 0 ? "  ·  current" : ""), Ui.Res("B.TextMute"));
                    text.Children.Add(when);
                    text.Children.Add(new TextBlock { Text = snap.Summary, Foreground = Ui.Res("B.Text"), FontSize = 13, FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 12, 0) });
                    head.Children.Add(text);

                    var detail = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 6, 0, 0) };
                    var acts = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
                    Grid.SetColumn(acts, 1);
                    var view = Ui.Button("View diff", () =>
                    {
                        if (detail.Visibility == Visibility.Visible) { detail.Visibility = Visibility.Collapsed; return; }
                        detail.Children.Clear();
                        if (older == null)
                            detail.Children.Add(new TextBlock { Text = "This is the oldest snapshot kept, so there is nothing earlier to compare it with.", Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute") });
                        else
                        {
                            var changes = SettingsHistory.Diff(older.Settings, snap.Settings);
                            if (changes.Count == 0) detail.Children.Add(new TextBlock { Text = "No differences from the previous snapshot.", Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute") });
                            foreach (var c in changes)
                                detail.Children.Add(Ui.Mono($"{c.Label}: {c.Old} → {c.New}", Ui.Res("B.TextSoft")));
                        }
                        detail.Visibility = Visibility.Visible;
                    }, "Btn.Ghost", Icons.Lines);
                    view.ToolTip = "Show what this save changed compared with the snapshot before it";
                    acts.Children.Add(view);
                    if (i > 0)
                    {
                        var restore = Ui.Button("Restore", () =>
                        {
                            var dlg = new ConfirmWindow("Restore these settings?", $"Every setting goes back to how it was on {snap.At:yyyy-MM-dd HH:mm:ss}. Your current settings stay in the history, so you can undo this.", "Restore");
                            dlg.Confirmed += _ =>
                            {
                                var restored = SettingsHistory.Restore(snap);
                                if (restored == null) { status.Text = "That snapshot could not be read, so nothing was changed."; status.Visibility = Visibility.Visible; return; }
                                Settings.ReplaceCurrent(restored);
                                App.Tray?.Refresh();
                                Navigate("records", animate: false);
                            };
                            dlg.Show();
                        }, "Btn.Accent", Icons.Restart);
                        restore.ToolTip = "Replace the current settings with this snapshot, after you confirm";
                        acts.Children.Add(restore);
                    }
                    head.Children.Add(acts);
                    row.Children.Add(head);
                    row.Children.Add(detail);
                    SearchScope.SetItem(row, "settings history snapshot");
                    shownSnaps[file] = snap;
                    list.Children.Add(i == 0 ? row : bulk.Wrap(file, row, snap.At.ToString("yyyy-MM-dd HH:mm:ss")));
                }
                if (snaps.Count > MaxRows)
                    list.Children.Add(new TextBlock { Text = $"Showing the newest {MaxRows} of {snaps.Count} snapshots.", Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute") });
            }

            rebuildList = Rebuild;
            Action changed = () => RunOnUi(Rebuild);
            box.Loaded += (s, e) => { SettingsHistory.Changed -= changed; SettingsHistory.Changed += changed; Rebuild(); };
            box.Unloaded += (s, e) => SettingsHistory.Changed -= changed;
            Rebuild();
            return SearchScope.SetItem(Card(box), "settings history restore diff snapshots");
        }

        // -------------------------------------------------------------- exports

        private FrameworkElement RecordsExports()
        {
            var box = new StackPanel();
            box.Children.Add(CardTitle("Exports", "Pick what to export and how. Clipboard history exports text entries only, never images, files or audio."));
            string dataset = Exporter.Datasets[0].Id;
            var format = ExportFormat.Json;
            box.Children.Add(Pick("What to export", Exporter.Datasets.Select(d => (d.Label, d.Id)), dataset, v => dataset = v, "Export"));
            box.Children.Add(Pick("Format", Exporter.Formats.Select(f => (f.Label, f.Format)), format, v => format = v, "Write as"));

            var bar = new ProgressBar { Height = 6, Minimum = 0, Maximum = 100, Foreground = Ui.Res("B.Mint"), Background = Ui.Res("B.Sunken"), BorderThickness = new Thickness(0), Visibility = Visibility.Collapsed, Margin = new Thickness(0, 6, 0, 6), MaxWidth = 420, HorizontalAlignment = HorizontalAlignment.Left };
            var status = new TextBlock { Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
            bool busy = false;

            async void Run(bool save)
            {
                if (busy) return;
                busy = true;
                try
                {
                    var table = Exporter.Snapshot(dataset);
                    if (table.Rows.Count == 0) { status.Text = $"{table.Name} is empty, so there is nothing to export."; return; }
                    string path = null;
                    if (save)
                    {
                        var dlg = new Microsoft.Win32.SaveFileDialog
                        {
                            FileName = table.Name.ToLowerInvariant().Replace(' ', '-') + Exporter.Extension(format),
                            Filter = Exporter.Formats.First(f => f.Format == format).Label + "|*" + Exporter.Extension(format) + "|All files|*.*",
                            Title = "Export " + table.Name
                        };
                        if (dlg.ShowDialog(this) != true) { status.Text = "Export cancelled."; return; }
                        path = dlg.FileName;
                    }
                    bar.Value = 0; bar.Visibility = Visibility.Visible;
                    status.Text = $"Preparing {table.Rows.Count:N0} rows…";
                    var text = await Exporter.FormatAsync(table, format, new Progress<double>(v => bar.Value = v * 100));
                    if (save)
                    {
                        var enc = new UTF8Encoding(format == ExportFormat.Csv);
                        await Task.Run(() => File.WriteAllText(path, text, enc));
                        status.Text = $"Saved {table.Rows.Count:N0} rows ({Ui.Bytes(enc.GetByteCount(text))}) to {path}";
                    }
                    else
                    {
                        MonitoringTools.Clip.ClipboardWatcher.SetText(text);
                        status.Text = $"Copied {table.Rows.Count:N0} rows ({text.Length:N0} characters) to the clipboard.";
                    }
                }
                catch (Exception ex) { status.Text = "The export failed: " + ex.Message; Log.Warn("Export failed: " + ex.Message); }
                finally { busy = false; bar.Visibility = Visibility.Collapsed; }
            }

            var row = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            var save1 = Ui.Button("Save to file…", () => Run(true), "Btn.Accent", Icons.File);
            save1.ToolTip = "Choose a file and write the selected data in the selected format";
            var copy = Ui.Button("Copy to clipboard", () => Run(false), "Btn.Ghost", Icons.Clipboard);
            copy.ToolTip = "Put the selected data on the clipboard as text in the selected format";
            row.Children.Add(save1); row.Children.Add(copy);
            box.Children.Add(row);
            box.Children.Add(bar);
            box.Children.Add(status);
            return SearchScope.SetItem(Card(box), "exports json csv markdown html text save copy");
        }

        // -------------------------------------------------------------- changelog

        private FrameworkElement RecordsChangelog()
        {
            var box = new StackPanel();
            box.Children.Add(CardTitle("Changelog", $"Release notes from GitHub. You are running v{Changelog.RunningVersion}."));
            var status = new TextBlock { Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
            var search = new SearchField("records:changelog", "Search the changelog") { Margin = new Thickness(0, 0, 0, 10) };
            var list = new StackPanel();
            var refresh = Ui.Button("Refresh", null, "Btn.Ghost", Icons.Restart);
            refresh.ToolTip = "Fetch the release notes from GitHub again";
            box.Children.Add(refresh);
            box.Children.Add(status);
            box.Children.Add(search);

            ChangelogResult data = null;
            bool loading = false;

            // Select mode: releases come from GitHub and cannot be changed here, so the actions copy and export their notes only.
            var barHost = new ContentControl { IsTabStop = false };
            BulkSelect<ReleaseNote> bulk = null;
            Action render = null;
            bulk = new BulkSelect<ReleaseNote>(() => render(), "releases");
            string Noun(int n) => n == 1 ? "release" : "releases";
            void RebuildBar() => barHost.Content = bulk.Bar(new[]
            {
                new BulkAction<ReleaseNote> { Label = "Copy notes", Hint = "Copy the chosen releases and their notes to the clipboard as Markdown.",
                    Run = items => BulkSelect<ReleaseNote>.CopyText(ListActions.ReleaseNotesMarkdown(items), items.Count, Noun(items.Count)) },
                new BulkAction<ReleaseNote> { Label = "Export as Markdown...", Hint = "Save the chosen releases and their notes to a Markdown file.",
                    Run = items => BulkSelect<ReleaseNote>.SaveText("release-notes.md", "Markdown|*.md", ListActions.ReleaseNotesMarkdown(items), items.Count, Noun(items.Count)) },
            });
            box.Children.Add(barHost);
            box.Children.Add(list);

            void Render()
            {
                list.Children.Clear();
                if (data == null) return;
                bulk.Prune(data.Releases);
                bulk.Begin();
                RebuildBar();
                var hits = data.Releases.Where(r => search.State.Matches(r.Name + " " + r.Tag + " " + r.Body)).ToList();
                foreach (var r in hits)
                {
                    var running = Changelog.IsRunning(r);
                    var inner = new StackPanel();
                    var head = new WrapPanel();
                    head.Children.Add(new TextBlock { Text = r.Name, Style = Ui.Style("T.Title"), FontSize = 16, Margin = new Thickness(0, 0, 10, 0) });
                    if (r.Name != r.Tag) head.Children.Add(Ui.Mono(r.Tag, Ui.Res("B.TextMute")));
                    if (running) head.Children.Add(new TextBlock { Text = "  ·  RUNNING NOW", Foreground = Ui.Res("B.Mint"), FontSize = 11, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
                    inner.Children.Add(head);
                    if (r.Published != null) inner.Children.Add(Ui.Mono(r.Published.Value.ToString("yyyy-MM-dd HH:mm"), Ui.Res("B.TextMute")));
                    inner.Children.Add(string.IsNullOrWhiteSpace(r.Body)
                        ? new TextBlock { Text = "No notes were written for this release.", Style = Ui.Style("T.Body"), Foreground = Ui.Res("B.TextMute"), Margin = new Thickness(0, 8, 0, 0) }
                        : (UIElement)LiteralText(new ContentControl { Content = Markdown.Render(r.Body, 40), Margin = new Thickness(0, 8, 0, 0) }));
                    var card = Card(inner, running ? Ui.C("C.Mint").ToString() : null, 14);
                    card.Background = Ui.Res("B.Raised");
                    SearchScope.SetItem(card, "release " + r.Tag);
                    list.Children.Add(bulk.Wrap(r, card, r.Name));
                }
                search.SetCount(hits.Count, data.Releases.Count, "releases");
            }

            render = Render;
            async void Load()
            {
                if (loading) return;
                loading = true;
                refresh.IsEnabled = false;
                status.Text = "Fetching release notes…";
                try
                {
                    data = await Changelog.LoadAsync();
                    if (data.Error == null) status.Text = $"{data.Releases.Count} releases fetched from GitHub just now.";
                    else if (data.FromCache) status.Text = $"GitHub could not be reached ({data.Error}). Showing the copy saved {RecordsAgo(data.CachedAt ?? DateTime.Now)} ({data.CachedAt:yyyy-MM-dd HH:mm}).";
                    else status.Text = $"The changelog is unavailable: GitHub could not be reached ({data.Error}) and no saved copy exists yet.";
                    Render();
                }
                finally { loading = false; refresh.IsEnabled = true; }
            }

            refresh.Click += (s, e) => Load();
            search.Filter += Render;
            box.Loaded += (s, e) => { if (data == null) Load(); };
            return SearchScope.SetItem(Card(box), "changelog release notes version");
        }
    }
}
