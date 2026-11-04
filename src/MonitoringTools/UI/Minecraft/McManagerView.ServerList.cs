using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using MonitoringTools.Core;
using System.Windows.Automation;
using System.Windows.Controls;
using MonitoringTools.Minecraft;
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI.Minecraft
{
    // The server list pane: New server, a search with the regex builder, state filters with counts, one row per server.
    internal sealed partial class McManagerView
    {
        internal static string ListFilter = "all";
        private SearchField _listSearch;
        private StackPanel _rows, _filters;
        private readonly Dictionary<string, (Button Row, Border Dot, TextBlock Sub)> _rowParts = new Dictionary<string, (Button, Border, TextBlock)>();

        internal SearchField ListSearch => _listSearch;
        internal IEnumerable<string> VisibleServerNames => _rows.Children.OfType<Button>().Where(b => b.Visibility == Visibility.Visible).Select(b => AutomationProperties.GetName(b));

        private FrameworkElement BuildList()
        {
            var sp = new StackPanel();
            var add = Ui.Button("New server", OpenCreate, "Btn.Accent", Icons.Plus);
            add.Height = 52; add.HorizontalAlignment = HorizontalAlignment.Stretch; add.Margin = new Thickness(0, 0, 0, 12);
            AutomationProperties.SetHelpText(add, "Opens the guided create flow.");
            sp.Children.Add(add);

            _listSearch = new SearchField("minecraft:servers", "Search servers", compact: true) { Margin = new Thickness(0, 0, 0, 8) };
            _listSearch.SampleSource = () => ServerManager.Servers.Select(RowText);
            _listSearch.Filter += () => FilterRows();
            sp.Children.Add(_listSearch);

            _filters = new StackPanel();
            AutomationProperties.SetName(_filters, "Filter by state");
            sp.Children.Add(_filters);
            _rows = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
            AutomationProperties.SetName(_rows, "Server list");
            sp.Children.Add(_rows);
            var note = McUi.Body("Servers are listed here when this app created or adopted them. Their files stay in their own folders, and removing one from the list never deletes its world.", "B.TextMute", 12);
            note.Margin = new Thickness(4, 12, 4, 0);
            sp.Children.Add(note);
            sp.Children.Add(BuildDiscovered());
            RebuildList();
            return sp;
        }

        // ---- servers found on this computer that the app does not manage yet

        private StackPanel _found;
        private TextBlock _foundNote;
        internal static List<McFound> Found = new List<McFound>();
        private static bool _lookedOnce;
        internal IEnumerable<string> FoundTitles => Found.Select(f => f.Title);

        private FrameworkElement BuildDiscovered()
        {
            var sp = new StackPanel { Margin = new Thickness(0, 18, 0, 0) };
            AutomationProperties.SetName(sp, "Found on this computer");
            var head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var t = McUi.Title("Found on this computer", 15);
            t.VerticalAlignment = VerticalAlignment.Center; t.Margin = new Thickness(4, 0, 0, 0);
            head.Children.Add(t);
            var look = McUi.IconButton(Icons.Search, "Look for servers", () => _ = Discover());
            look.ToolTip = "Look for servers";
            Grid.SetColumn(look, 1); head.Children.Add(look);
            sp.Children.Add(head);
            _foundNote = McUi.Body("Java and Bedrock servers that run here, and Minecraft containers in Docker, that this app did not make. Nothing is touched until you adopt one.", "B.TextMute", 12);
            _foundNote.Margin = new Thickness(4, 4, 4, 4);
            sp.Children.Add(_foundNote);
            _found = new StackPanel();
            AutomationProperties.SetName(_found, "Servers found");
            sp.Children.Add(_found);
            PaintFound();
            if (!_lookedOnce) { _lookedOnce = true; Dispatcher.BeginInvoke(new Action(() => _ = Discover()), System.Windows.Threading.DispatcherPriority.Background); }
            return sp;
        }

        /// <summary>Looks again: running Java and Bedrock processes and Minecraft containers. Only reads.</summary>
        internal async Task Discover()
        {
            if (_foundNote != null) { _foundNote.Text = "Looking…"; Core.Language.Refresh(_foundNote); }
            try { Found = await McDiscovery.FindAsync(); }
            catch (Exception ex) { Found = new List<McFound>(); Core.Log.Error("Minecraft discovery", ex); }
            if (_foundNote != null) { _foundNote.Text = "Java and Bedrock servers that run here, and Minecraft containers in Docker, that this app did not make. Nothing is touched until you adopt one."; Core.Language.Refresh(_foundNote); }
            PaintFound();
        }

        private void PaintFound()
        {
            if (_found == null) return;
            _found.Children.Clear();
            if (Found.Count == 0) _found.Children.Add(new TextBlock { Text = "None found.", FontSize = 12.5, Foreground = Ui.Res("B.TextMute"), FontFamily = Ui.Font("F.Body"), Margin = new Thickness(4, 2, 0, 0) });
            foreach (var f in Found)
            {
                var g = new Grid { Margin = new Thickness(0, 6, 0, 0) };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var text = new StackPanel { Margin = new Thickness(4, 0, 8, 0) };
                var name = McUi.Literal(f.Title, "B.Text", 13.5);
                name.FontWeight = FontWeights.Medium; name.TextWrapping = TextWrapping.NoWrap; name.TextTrimming = TextTrimming.CharacterEllipsis;
                text.Children.Add(name);
                var kind = new TextBlock { Text = (f.Kind == "docker" ? "Docker container" : f.Flavour == Flavour.Bedrock ? "Bedrock process" : "Java process") + " · " + f.Flavour + " " + f.Version + (f.Running ? " · running" : " · not running"), FontSize = 12, Foreground = Ui.Res("B.TextSoft"), FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap };
                text.Children.Add(kind);
                var where = McUi.Literal(f.Kind == "docker" ? f.Container + " · " + f.Image : PathDisplay.Shown(f.Folder), "B.TextMute", 11.5, mono: true);
                where.TextWrapping = TextWrapping.NoWrap; where.TextTrimming = TextTrimming.CharacterEllipsis;
                where.ToolTip = f.Kind == "docker" ? f.Container : f.Folder;
                text.Children.Add(where);
                g.Children.Add(text);
                var ff = f;
                var adopt = Ui.Button("Adopt", () => ConfirmAdopt(ff), "Btn.Ghost", Icons.Plus);
                AutomationProperties.SetName(adopt, "Adopt " + f.Title);
                adopt.VerticalAlignment = VerticalAlignment.Top;
                Grid.SetColumn(adopt, 1); g.Children.Add(adopt);
                _found.Children.Add(new Border { Background = Ui.Res("B.Raised"), CornerRadius = new CornerRadius(16), Padding = new Thickness(10, 8, 8, 8), Margin = new Thickness(0, 0, 0, 4), Child = g });
            }
            Core.Language.Refresh(_found);
        }

        private void ConfirmAdopt(McFound f)
        {
            var what = f.Kind == "docker" ? $"the container {f.Container}" : $"the folder {PathDisplay.Shown(f.Folder)}";
            var dlg = new ConfirmWindow($"Adopt {f.Title}?",
                $"The server is added to this list and points at {what}. Nothing is stopped, moved or changed. " +
                (f.Kind == "docker" ? "Start, stop and commands then go through Docker." : "A server started elsewhere takes commands, and a graceful stop, only over RCON; once stopped it can be started from here."), "Adopt", danger: false);
            dlg.Confirmed += _ => AdoptNow(f);
            dlg.Show();
        }

        internal ServerDef AdoptNow(McFound f)
        {
            var s = McDiscovery.Adopt(f);
            Found.Remove(f);
            PaintFound();
            Select(s.Id);
            return s;
        }

        /// <summary>The text a server row is searched by: its name, version, software, target and host.</summary>
        internal static string RowText(ServerDef s) => $"{s.Name} {s.Version} {s.Flavour} {McUi.TargetLabel(s.Target)} {s.SshHost}";

        internal static bool InFilter(ServerDef s, string filter)
        {
            var st = McState.Cached(s).State;
            return filter switch
            {
                "running" => st == McRunState.Running || st == McRunState.Starting,
                "stopped" => st == McRunState.Stopped || st == McRunState.Missing,
                "offline" => st == McRunState.HostOffline,
                _ => true,
            };
        }

        private void RebuildList()
        {
            if (_rows == null) return;
            _rows.Children.Clear();
            _rowParts.Clear();
            foreach (var s in ServerManager.Servers.ToList())
            {
                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var dot = new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(5), Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
                var text = new StackPanel();
                var name = McUi.Literal(s.Name, "B.Text", 14.5);
                name.FontWeight = FontWeights.Medium; name.TextWrapping = TextWrapping.NoWrap; name.TextTrimming = TextTrimming.CharacterEllipsis;
                text.Children.Add(name);
                var sub = new TextBlock { FontSize = 12, Foreground = Ui.Res("B.TextSoft"), FontFamily = Ui.Font("F.Body"), TextTrimming = TextTrimming.CharacterEllipsis };
                text.Children.Add(sub);
                Grid.SetColumn(text, 1);
                var badge = new Border
                {
                    BorderBrush = Ui.Res("B.LineStrong"), BorderThickness = new System.Windows.Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(6, 2, 8, 3), Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
                    Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { new ContentControl { Content = Ui.Icon(McUi.TargetIcon(s.Target), Ui.Res("B.TextSoft"), 12), Margin = new Thickness(0, 0, 4, 0), IsTabStop = false, VerticalAlignment = VerticalAlignment.Center }, new TextBlock { Text = McUi.TargetLabel(s.Target), FontSize = 11.5, Foreground = Ui.Res("B.TextSoft"), FontFamily = Ui.Font("F.Body") } } }
                };
                Grid.SetColumn(badge, 2);
                g.Children.Add(dot); g.Children.Add(text); g.Children.Add(badge);
                var row = McUi.Row(g, s.Name);
                var id = s.Id;
                row.Click += (o, e) => Select(id);
                _rows.Children.Add(row);
                _rowParts[s.Id] = (row, dot, sub);
            }
            Ui.RiseAll(_rows.Children);
            if (ServerManager.Servers.Count == 0) _rows.Children.Add(McUi.Body("No servers yet. New server walks you through making one.", "B.TextMute", 12.5));
            RepaintList();
        }

        /// <summary>Updates the dots, sub lines, selection and filter counts without rebuilding the rows.</summary>
        private void RepaintList()
        {
            if (_rows == null) return;
            foreach (var s in ServerManager.Servers.ToList())
            {
                if (!_rowParts.TryGetValue(s.Id, out var p)) continue;
                var st = McState.Cached(s);
                p.Dot.Background = Ui.Res(McUi.StateTone(st.State).Dot);
                var live = McLive.For(s);
                string tail = st.State switch
                {
                    McRunState.Running => live.PlayersOnline is int n && live.PlayersMax is int m ? $"{n} of {m} players" : "running",
                    McRunState.HostOffline => "offline",
                    McRunState.Starting => "starting",
                    McRunState.Missing => "missing",
                    McRunState.Checking => "checking",
                    _ => "stopped",
                };
                p.Sub.Text = $"{s.Flavour} {s.Version} · {tail}";
                bool sel = !Creating && s.Id == (Current?.Id);
                p.Row.Background = (sel ? Ui.Res("B.SecondaryContainer") : System.Windows.Media.Brushes.Transparent);
                p.Row.Foreground = Ui.Res(sel ? "B.OnSecondaryContainer" : "B.Text");
                AutomationProperties.SetItemStatus(p.Row, (sel ? "selected, " : "") + McState.Label(st.State));
                var shape = McUi.Shape(p.Row);
                if (shape != null && Math.Abs(shape.CornerRadius.TopLeft - (sel ? 28 : 16)) > 0.5) McUi.Morph(shape, sel ? 28 : 16);
            }
            PaintFilters();
            FilterRows();
            Core.Language.Refresh(_rows);
        }

        private readonly List<(Button B, string Id, string Label)> _filterChips = new List<(Button, string, string)>();

        /// <summary>Updates the filter chips in place, so keyboard focus stays on a chip while the counts change.</summary>
        private void PaintFilters()
        {
            int Count(string f) => ServerManager.Servers.Count(s => InFilter(s, f));
            if (_filterChips.Count == 0)
            {
                var wrap = new WrapPanel();
                foreach (var (id, label) in new[] { ("all", "All"), ("running", "Running"), ("stopped", "Stopped"), ("offline", "Offline host") })
                {
                    var fid = id;
                    var b = McUi.Chip(label, false, () => { ListFilter = fid; RepaintList(); });
                    _filterChips.Add((b, id, label));
                    wrap.Children.Add(b);
                }
                _filters.Children.Add(wrap);
            }
            foreach (var (b, id, label) in _filterChips)
            {
                var text = $"{label} {Count(id)}";
                if (!Equals(b.Content, text)) b.Content = text;
                McUi.Paint(b, ListFilter == id);
                AutomationProperties.SetName(b, $"{label}: {Count(id)} servers");
            }
        }

        private void FilterRows()
        {
            if (_listSearch == null) return;
            int shown = 0;
            foreach (var s in ServerManager.Servers.ToList())
            {
                if (!_rowParts.TryGetValue(s.Id, out var p)) continue;
                bool ok = InFilter(s, ListFilter) && _listSearch.State.Matches(RowText(s));
                p.Row.Visibility = ok ? Visibility.Visible : Visibility.Collapsed;
                if (ok) shown++;
            }
            _listSearch.SetCount(shown, ServerManager.Servers.Count, "servers");
        }
    }
}
