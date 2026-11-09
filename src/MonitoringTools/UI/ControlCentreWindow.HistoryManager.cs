using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using MonitoringTools.Clip;
using MonitoringTools.Core;
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI
{
    public partial class ControlCentreWindow
    {
        // -------------------------------------------------------------- the history manager over the clipboard history repository

        private string _repoKind = "All";
        private string _repoRange = "All time";
        private bool _repoPinnedOnly;
        private bool _repoOldestFirst;
        private int _repoShown = 200;
        private bool _repoSelecting;
        private readonly HashSet<string> _repoSelected = new HashSet<string>();
        private string _repoOpen;
        private SearchField _repoSearch;
        private TextBlock _repoStatus;
        private bool _repoHooked;

        private static readonly (string Label, string[] Kinds)[] RepoKinds =
        {
            ("All", null),
            ("Text", new[] { "text", "markdown" }),
            ("Links", new[] { "url", "email" }),
            ("Code and JSON", new[] { "json", "command", "install" }),
            ("Colours", new[] { "color" }),
            ("Files", new[] { "files" }),
            ("Images and sound", new[] { "image", "audio" }),
        };

        private static readonly string[] RepoRanges = { "Today", "7 days", "30 days", "All time" };

        private FrameworkElement RepoHistoryPage()
        {
            if (!_repoHooked)
            {
                _repoHooked = true;
                ClipRepo.Changed += () => Dispatcher.BeginInvoke(new Action(PaintRepoStatus));
            }
            var all = ClipRepo.All();
            var root = PageRoot("Clipboard", "History", "Every copy, kept in a Git repository on this computer. Each copy, edit, pin and removal is a commit, and there is no limit.");
            root.Children.Add(RepoStatusCard(all.Count));
            root.Children.Add(RepoManagerCard(all));
            root.Children.Add(RepoTimelineCard());
            return root;
        }

        // ---- status: where it lives, how much is in it, and the controls for the repository itself

        private FrameworkElement RepoStatusCard(int count)
        {
            var s = Settings.Current;
            var sp = new StackPanel();
            var head = new StackPanel { Orientation = Orientation.Horizontal };
            var (dot, setDot) = Ui.LiveDot(Ui.Res(ClipRepo.Recording ? "B.Mint" : "B.Amber"), ClipRepo.Recording, 10);
            head.Children.Add(dot);
            head.Children.Add(new TextBlock { Text = ClipRepo.Recording ? "Recording every copy" : "Paused: new copies are not committed", Style = Ui.Style("T.Title"), FontSize = 18, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) });
            sp.Children.Add(head);
            var path = Ui.Mono(ClipRepo.Dir, Ui.Res("B.TextSoft"), 11.5);
            path.Margin = new Thickness(0, 8, 0, 0);
            Core.Language.SetLiteral(path, true);
            sp.Children.Add(path);
            _repoStatus = new TextBlock { Style = Ui.Style("T.Body"), Foreground = Ui.Res("B.TextMute"), Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap };
            AutomationProperties.SetLiveSetting(_repoStatus, AutomationLiveSetting.Polite);
            sp.Children.Add(_repoStatus);
            PaintRepoStatus();

            var acts = new WrapPanel { Margin = new Thickness(0, 14, 0, 0) };
            Button B(string label, Action a, string style = "Btn.Ghost", string icon = null)
            {
                var b = Ui.Button(label, a, style, icon);
                b.Margin = new Thickness(0, 0, 8, 8);
                acts.Children.Add(b);
                return b;
            }
            B(s.ClipRepoPaused ? "Resume recording" : "Pause recording", () => { s.ClipRepoPaused = !s.ClipRepoPaused; Settings.Save(); Navigate("history", animate: false); }, s.ClipRepoPaused ? "Btn.Accent" : "Btn.Ghost", s.ClipRepoPaused ? Icons.Play : Icons.Pause);
            B("Open folder", () => { System.IO.Directory.CreateDirectory(ClipRepo.Dir); ActionProvider.Shell(ClipRepo.Dir); }, "Btn.Ghost", Icons.Folder);
            B("Copy path", () => ClipboardWatcher.SetText(ClipRepo.Dir), "Btn.Ghost", Icons.Clipboard);
            B("Change folder…", () =>
            {
                var d = new Microsoft.Win32.OpenFolderDialog { Title = "Choose a folder for the clipboard history repository" };
                if (d.ShowDialog(this) != true) return;
                ClipRepo.Drain();
                s.ClipRepoPath = d.FolderName; Settings.Save();
                ClipRepo.Reload();
                Navigate("history", animate: false);
            }, "Btn.Ghost", Icons.Git);
            B("Forget removed copies for good…", () =>
            {
                var ok = new ConfirmWindow("Rewrite the history repository?",
                    "Removed copies stay in earlier commits until this runs. It replaces every commit with one commit of what is kept today, so the versions of edited copies go too. This cannot be undone.",
                    "Rewrite", danger: true);
                ok.Confirmed += _ =>
                {
                    var err = ClipRepo.ForgetRemoved();
                    if (err != null) App.Toasts.ShowNotice("History not rewritten", err, ((SolidColorBrush)Ui.Res("B.Amber")).Color);
                    Navigate("history", animate: false);
                };
                ok.Show();
            }, "Btn.Danger", Icons.Trash);
            sp.Children.Add(acts);

            sp.Children.Add(Switch("Keep secrets too", "Also keeps copies marked as passwords, keys or tokens. Off by default, so they never reach the repository.", s.ClipRepoKeepSecrets, v => s.ClipRepoKeepSecrets = v));
            sp.Children.Add(Ui.Text(HistoryVault.Encrypts
                ? "New copies are written encrypted, using the history protection in Safety."
                : "Copies are written as plain files. Turn on history protection in Safety to encrypt new ones.", "T.Body"));
            return Card(sp);
        }

        private void PaintRepoStatus()
        {
            if (_repoStatus == null) return;
            var n = ClipRepo.All().Count;
            var parts = new List<string> { n == 1 ? "1 copy" : $"{n:N0} copies", ClipRepo.Commits == 1 ? "1 commit" : $"{ClipRepo.Commits:N0} commits", Ui.Bytes(ClipRepo.DiskBytes()) + " on disk" };
            if (ClipRepo.LastCommit is DateTime t) parts.Add("last commit " + t.ToString("HH:mm:ss"));
            if (ClipRepo.Pending > 0) parts.Add($"committing {ClipRepo.Pending}…");
            _repoStatus.Text = string.Join(" · ", parts);
            if (ClipRepo.LastError != null) { _repoStatus.Text += "\n" + ClipRepo.LastError; _repoStatus.Foreground = Ui.Res("B.Amber"); }
            else _repoStatus.Foreground = Ui.Res("B.TextMute");
        }

        // ---- the manager: search, filters, sort, paging, select, and one row per copy

        private FrameworkElement RepoManagerCard(IReadOnlyList<ClipRepo.Entry> all)
        {
            var sp = new StackPanel();
            sp.Children.Add(CardTitle("All copies", "Search the whole history. Filters and search reach every copy, not only the ones shown."));

            _repoSearch = new SearchField("history:repo", "Search every copy, for example an address or a word") { Margin = new Thickness(0, 0, 0, 10) };
            _repoSearch.SampleSource = () => all.Take(200).Select(e => e.ShownPreview);
            sp.Children.Add(_repoSearch);

            var kinds = new WrapPanel();
            foreach (var (label, ks) in RepoKinds)
            {
                int n = ks == null ? all.Count : all.Count(e => ks.Contains(e.Kind));
                if (n == 0 && ks != null) continue;
                kinds.Children.Add(FilterChip($"{label} {n:N0}", _repoKind == label, () => { _repoKind = label; _repoShown = 200; Navigate("history", animate: false); }));
            }
            sp.Children.Add(kinds);
            var more = new WrapPanel { Margin = new Thickness(0, 2, 0, 8) };
            foreach (var r in RepoRanges) more.Children.Add(FilterChip(r, _repoRange == r, () => { _repoRange = r; _repoShown = 200; Navigate("history", animate: false); }));
            more.Children.Add(FilterChip("Pinned only", _repoPinnedOnly, () => { _repoPinnedOnly = !_repoPinnedOnly; Navigate("history", animate: false); }));
            more.Children.Add(FilterChip(_repoOldestFirst ? "Oldest first" : "Newest first", false, () => { _repoOldestFirst = !_repoOldestFirst; Navigate("history", animate: false); }));
            sp.Children.Add(more);

            var bar = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
            sp.Children.Add(bar);
            var countLine = new TextBlock { Style = Ui.Style("T.Body"), Foreground = Ui.Res("B.TextMute"), Margin = new Thickness(0, 0, 0, 6) };
            sp.Children.Add(countLine);
            var rows = new StackPanel();
            sp.Children.Add(rows);
            var moreHost = new StackPanel();
            sp.Children.Add(moreHost);

            List<ClipRepo.Entry> Matching()
            {
                var ks = RepoKinds.First(k => k.Label == _repoKind).Kinds;
                DateTime from = _repoRange switch { "Today" => DateTime.Today, "7 days" => DateTime.Today.AddDays(-6), "30 days" => DateTime.Today.AddDays(-29), _ => DateTime.MinValue };
                IEnumerable<ClipRepo.Entry> q = all.Where(e =>
                    (ks == null || ks.Contains(e.Kind)) && e.At >= from && (!_repoPinnedOnly || e.Pinned) &&
                    _repoSearch.State.Matches(e.ShownPreview + " " + e.Kind + " " + e.Source));
                // pinned copies lead, then time
                q = _repoOldestFirst ? q.OrderByDescending(e => e.Pinned).ThenBy(e => e.At) : q.OrderByDescending(e => e.Pinned).ThenByDescending(e => e.At);
                return q.ToList();
            }

            List<ClipRepo.Entry> hits = null;
            void Paint()
            {
                hits = Matching();
                rows.Children.Clear();
                foreach (var e in hits.Take(_repoShown)) rows.Children.Add(RepoRow(e));
                if (hits.Count == 0)
                    rows.Children.Add(new TextBlock { Text = all.Count == 0 ? "Nothing yet. Copy something and it is committed here." : "No copy matches. Try fewer words or another filter.", Style = Ui.Style("T.Body"), Foreground = Ui.Res("B.TextMute"), Margin = new Thickness(4, 8, 0, 8) });
                countLine.Text = hits.Count == all.Count ? $"{all.Count:N0} copies" : $"{hits.Count:N0} of {all.Count:N0} copies";
                _repoSearch.SetCount(hits.Count, all.Count, "copies");
                moreHost.Children.Clear();
                if (hits.Count > _repoShown)
                {
                    var m = Ui.Button($"Show {Math.Min(200, hits.Count - _repoShown)} more ({hits.Count - _repoShown:N0} left)", () => { _repoShown += 200; Paint(); }, "Btn.Ghost");
                    m.Margin = new Thickness(0, 10, 0, 0);
                    moreHost.Children.Add(m);
                }
                PaintBar();
            }

            void PaintBar()
            {
                bar.Children.Clear();
                _repoSelected.IntersectWith(all.Select(e => e.Id));
                Button Act(string label, Action a, string style = "Btn.Ghost", bool needsSelection = true)
                {
                    var b = Ui.Button(label, a, style);
                    b.Margin = new Thickness(0, 0, 8, 0);
                    b.IsEnabled = !needsSelection || _repoSelected.Count > 0;
                    bar.Children.Add(b);
                    return b;
                }
                Act(_repoSelecting ? "Done selecting" : "Select", () => { _repoSelecting = !_repoSelecting; if (!_repoSelecting) _repoSelected.Clear(); Paint(); }, _repoSelecting ? "Btn.Accent" : "Btn.Ghost", false);
                if (!_repoSelecting) return;
                var count = new TextBlock { Text = $"{_repoSelected.Count} selected", Style = Ui.Style("T.Body"), Foreground = Ui.Res("B.Text"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 12, 0) };
                AutomationProperties.SetLiveSetting(count, AutomationLiveSetting.Polite);
                bar.Children.Add(count);
                Act("Select all shown", () => { foreach (var e in hits.Take(_repoShown)) _repoSelected.Add(e.Id); Paint(); }, needsSelection: false);
                Act($"Select all {hits.Count:N0} matching", () => { foreach (var e in hits) _repoSelected.Add(e.Id); Paint(); }, needsSelection: false);
                Act("Select none", () => { _repoSelected.Clear(); Paint(); }, needsSelection: false);
                IEnumerable<ClipRepo.Entry> Chosen() => all.Where(e => _repoSelected.Contains(e.Id)).OrderBy(e => e.At);
                Act("Copy", () => ClipboardWatcher.SetText(string.Join(Environment.NewLine, Chosen().Select(e => ClipRepo.ReadText(e) ?? ""))));
                Act("Pin", () => { ClipRepo.SetPinned(Chosen(), true); Paint(); });
                Act("Unpin", () => { ClipRepo.SetPinned(Chosen(), false); Paint(); });
                Act("Export…", () =>
                {
                    var d = new Microsoft.Win32.SaveFileDialog { FileName = "clipboard-history.txt", Filter = "Text|*.txt|JSON lines|*.jsonl" };
                    if (d.ShowDialog() != true) return;
                    var chosen = Chosen().ToList();
                    if (d.FileName.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase))
                        File.WriteAllLines(d.FileName, chosen.Select(e => System.Text.Json.JsonSerializer.Serialize(new { e.Id, e.At, e.Kind, e.Source, e.Pinned, Text = ClipRepo.ReadText(e) })));
                    else
                        File.WriteAllText(d.FileName, string.Join(Environment.NewLine + Environment.NewLine, chosen.Select(e => $"[{e.At:yyyy-MM-dd HH:mm:ss}] {e.Kind}" + Environment.NewLine + (ClipRepo.ReadText(e) ?? ""))));
                });
                Act("Remove…", () =>
                {
                    var n = _repoSelected.Count;
                    var ok = new ConfirmWindow($"Remove {n:N0} {(n == 1 ? "copy" : "copies")} from history?",
                        "They leave the history in one commit. Git keeps them in earlier commits until you choose Forget removed copies for good.", "Remove", danger: true);
                    ok.Confirmed += _ => { ClipRepo.Remove(Chosen().ToList()); _repoSelected.Clear(); Navigate("history", animate: false); };
                    ok.Show();
                }, "Btn.Danger");
            }

            _repoSearch.Filter += Paint;
            Paint();
            return Card(sp, pad: 14);
        }

        private Button FilterChip(string label, bool on, Action pick)
        {
            var b = Ui.Button(label, pick, "Btn.Chip");
            b.Height = 32; b.Margin = new Thickness(0, 0, 6, 6);
            b.Background = Ui.Res(on ? "B.SecondaryContainer" : "B.Raised");
            b.Foreground = Ui.Res(on ? "B.OnSecondaryContainer" : "B.TextSoft");
            AutomationProperties.SetName(b, label + (on ? ", chosen" : ""));
            return b;
        }

        private FrameworkElement RepoRow(ClipRepo.Entry e)
        {
            var outer = new StackPanel();
            var g = new Grid { Margin = new Thickness(6, 6, 6, 6) };
            if (_repoSelecting)
            {
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var cb = new CheckBox { IsChecked = _repoSelected.Contains(e.Id), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 12, 0) };
                AutomationProperties.SetName(cb, Core.Language.T("Select") + " " + Short(e.ShownPreview, 60)); Core.Language.SetLiteral(cb, true);
                cb.Checked += (s, ev) => { _repoSelected.Add(e.Id); Navigate("history", animate: false); };
                cb.Unchecked += (s, ev) => { _repoSelected.Remove(e.Id); Navigate("history", animate: false); };
                g.Children.Add(cb);
            }
            int c0 = g.ColumnDefinitions.Count;
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var icon = e.Kind switch
            {
                "url" => Icons.Link, "files" => Icons.File, "image" => Icons.Image, "audio" => Icons.Audio, "json" => Icons.Braces,
                "color" => Icons.Palette, "email" => Icons.Mail, "command" => Icons.Bolt, _ => Icons.Text
            };
            var chip = Ui.IconChip(icon, Ui.Res(e.Pinned ? "B.OnPrimaryContainer" : "B.TextSoft"), Ui.Res(e.Pinned ? "B.MintWash" : "B.Raised"));
            Grid.SetColumn(chip, c0); g.Children.Add(chip);

            var mid = new StackPanel { Margin = new Thickness(12, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
            var prev = Core.PathDisplay.Shown(e.ShownPreview ?? "").Replace("\r", " ").Replace("\n", " ");
            mid.Children.Add(LiteralText(new TextBlock { Text = Short(prev, 160), Foreground = Ui.Res("B.Text"), FontSize = 12.5, FontFamily = e.Kind == "files" ? Ui.Font("F.Mono") : Ui.Font("F.Body"), TextTrimming = TextTrimming.CharacterEllipsis }));
            var meta = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            meta.Children.Add(Ui.Pill(e.Kind));
            meta.Children.Add(Ui.Pill(e.At.Date == DateTime.Today ? e.At.ToString("HH:mm:ss") : e.At.ToString("yyyy-MM-dd HH:mm"), mono: true));
            if (!string.IsNullOrEmpty(e.Source)) { var src = Ui.Pill(e.Source); Core.Language.SetLiteral(src, true); meta.Children.Add(src); }
            if (e.Chars > 0 && e.Kind != "files") meta.Children.Add(Ui.Pill($"{e.Chars:N0} characters"));
            if (e.Pinned) meta.Children.Add(Ui.Pill("pinned", Ui.Res("B.MintWash"), Ui.Res("B.Mint")));
            if (e.Edits > 0) meta.Children.Add(Ui.Pill(e.Edits == 1 ? "edited once" : $"edited {e.Edits} times", Ui.Res("B.AmberWash"), Ui.Res("B.Amber")));
            mid.Children.Add(meta);
            Grid.SetColumn(mid, c0 + 1); g.Children.Add(mid);

            var btns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var again = Ui.Button("Copy again", () => RepoCopyAgain(e), "Btn.Ghost");
            again.Margin = new Thickness(0, 0, 6, 0);
            var pin = Ui.Button(e.Pinned ? "Unpin" : "Pin", () => { ClipRepo.SetPinned(new[] { e }, !e.Pinned); Navigate("history", animate: false); }, "Btn.Ghost");
            pin.Margin = new Thickness(0, 0, 6, 0);
            var open = Ui.Button(_repoOpen == e.Id ? "Less" : "More", () => { _repoOpen = _repoOpen == e.Id ? null : e.Id; Navigate("history", animate: false); }, "Btn.Ghost");
            open.Margin = new Thickness(0);
            AutomationProperties.SetName(open, (_repoOpen == e.Id ? "Hide details of " : "Show details of ") + Short(prev, 40));
            btns.Children.Add(again); btns.Children.Add(pin); btns.Children.Add(open);
            Grid.SetColumn(btns, c0 + 2); g.Children.Add(btns);

            var tint = new SolidColorBrush(Colors.Transparent);
            var row = new Border { Background = tint, CornerRadius = new CornerRadius(16), Child = g };
            row.MouseEnter += (s, ev) => Ui.ColorTo(tint, ((SolidColorBrush)Ui.Res("B.Raised")).Color, Ui.Reduced ? 1 : 180);
            row.MouseLeave += (s, ev) => Ui.ColorTo(tint, Colors.Transparent, Ui.Reduced ? 1 : 240);
            SearchScope.SetItem(row, "copy " + e.Kind + " " + prev);
            outer.Children.Add(row);
            if (_repoOpen == e.Id) { var d = RepoDetail(e); outer.Children.Add(d); Ui.Expand(d); }
            return outer;
        }

        /// <summary>The open row: the whole copy, its colours, its versions, and remove.</summary>
        private FrameworkElement RepoDetail(ClipRepo.Entry e)
        {
            var sp = new StackPanel { Margin = new Thickness(52, 0, 12, 12) };
            var img = ClipRepo.ReadImage(e);
            if (img != null) sp.Children.Add(new Image { Source = img, MaxHeight = 220, HorizontalAlignment = HorizontalAlignment.Left, Stretch = Stretch.Uniform });
            else
            {
                var text = ClipRepo.ReadText(e) ?? "This copy cannot be read right now (the file is missing, or the history is locked).";
                var box = new TextBox { Text = text.Length > 20000 ? text.Substring(0, 20000) + "\n…" : text, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, FontFamily = Ui.Font("F.Mono"), FontSize = 12, MaxHeight = 260, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Ui.Res("B.Window"), Foreground = Ui.Res("B.Text"), BorderThickness = new Thickness(0), Padding = new Thickness(12) };
                AutomationProperties.SetName(box, "Copied text");
                Core.Language.SetLiteral(box, true);
                sp.Children.Add(new Border { CornerRadius = new CornerRadius(12), Background = Ui.Res("B.Window"), Child = box, ClipToBounds = true });
                var colours = ColorPreview.StripFor(text);
                if (colours != null) sp.Children.Add(colours);
            }
            var file = Ui.Mono(e.File, Ui.Res("B.TextMute"), 11);
            file.Margin = new Thickness(0, 8, 0, 0);
            Core.Language.SetLiteral(file, true);
            sp.Children.Add(file);

            var versions = ClipRepo.Versions(e);
            if (versions.Count > 1)
            {
                sp.Children.Add(new TextBlock { Text = $"Versions · {versions.Count}", Style = Ui.Style("T.Eyebrow"), Margin = new Thickness(0, 10, 0, 4) });
                foreach (var (sha, at, msg) in versions)
                {
                    var line = new WrapPanel();
                    var shaText = Ui.Mono(sha, Ui.Res("B.Mint"), 11.5); Core.Language.SetLiteral(shaText, true);
                    shaText.Margin = new Thickness(0, 0, 10, 0); shaText.VerticalAlignment = VerticalAlignment.Center;
                    line.Children.Add(shaText);
                    line.Children.Add(new TextBlock { Text = at.ToString("yyyy-MM-dd HH:mm:ss") + " · " + msg, Style = Ui.Style("T.Body"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
                    var s1 = sha;
                    var copyV = Ui.Button("Copy this version", () => { var t = ClipRepo.TextAt(e, s1); if (t != null) ClipboardWatcher.SetText(t); }, "Btn.Ghost");
                    copyV.Height = 30; copyV.Margin = new Thickness(0, 2, 0, 2);
                    line.Children.Add(copyV);
                    sp.Children.Add(line);
                }
            }
            var remove = Ui.Button("Remove from history…", () =>
            {
                var ok = new ConfirmWindow("Remove this copy from history?", "It leaves the history in one commit. Git keeps it in earlier commits until you choose Forget removed copies for good.", "Remove", danger: true);
                ok.Confirmed += _ => { ClipRepo.Remove(new[] { e }); _repoOpen = null; Navigate("history", animate: false); };
                ok.Show();
            }, "Btn.Danger", Icons.Trash);
            remove.Margin = new Thickness(0, 10, 0, 0); remove.HorizontalAlignment = HorizontalAlignment.Left;
            sp.Children.Add(remove);
            return sp;
        }

        private static void RepoCopyAgain(ClipRepo.Entry e)
        {
            if (e.Kind == "image") { var img = ClipRepo.ReadImage(e); if (img != null) ClipboardWatcher.SetImage(img); return; }
            var text = ClipRepo.ReadText(e);
            if (text == null) return;
            if (e.Kind == "files") { ClipboardWatcher.SetFiles(text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(f => f.Trim()).ToArray()); return; }
            ClipboardWatcher.SetText(text);
        }

        // ---- the newest commits, as a timeline

        private FrameworkElement RepoTimelineCard()
        {
            var sp = new StackPanel();
            sp.Children.Add(CardTitle("Latest commits", "What changed most recently in the history repository."));
            var recent = ClipRepo.Recent(10);
            if (recent.Count == 0) sp.Children.Add(Ui.Text("No commits yet. The first copy starts the repository."));
            foreach (var (sha, at, msg) in recent)
            {
                var line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                var shaText = Ui.Mono(sha, Ui.Res("B.Mint"), 11.5); Core.Language.SetLiteral(shaText, true);
                shaText.Width = 70;
                line.Children.Add(shaText);
                line.Children.Add(Ui.Mono(at.ToString("MM-dd HH:mm:ss"), Ui.Res("B.TextMute"), 11.5));
                line.Children.Add(new TextBlock { Text = "  " + msg, Style = Ui.Style("T.Body"), VerticalAlignment = VerticalAlignment.Center });
                sp.Children.Add(line);
            }
            return Card(sp);
        }

        private static string Short(string s, int n) => string.IsNullOrEmpty(s) ? "" : s.Length > n ? s.Substring(0, n) + "…" : s;
    }
}
