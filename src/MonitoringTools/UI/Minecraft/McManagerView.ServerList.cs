using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
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
            var note = McUi.Body("Servers are listed here when this app created them. Their files stay in their own folders, and removing one from the list never deletes its world.", "B.TextMute", 12);
            note.Margin = new Thickness(4, 12, 4, 0);
            sp.Children.Add(note);
            RebuildList();
            return sp;
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
