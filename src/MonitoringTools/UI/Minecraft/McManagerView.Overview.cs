using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using MonitoringTools.Core;
using MonitoringTools.Minecraft;

namespace MonitoringTools.UI.Minecraft
{
    // Overview: live tiles, the last console lines, players online and what needs attention.
    internal sealed partial class McManagerView
    {
        private static readonly Dictionary<string, (McBuild Newer, string Unavailable)> UpdateChecks = new Dictionary<string, (McBuild, string)>();

        private FrameworkElement BuildOverview(ServerDef s)
        {
            var root = new StackPanel();
            var tiles = new UniformGrid { Columns = 5, Margin = new Thickness(0, 0, -12, 0) };
            AutomationProperties.SetName(tiles, "Live status");
            root.Children.Add(tiles);

            var two = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            two.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            two.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            var consoleCard = new StackPanel();
            var head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.Children.Add(McUi.Title("Recent console", 16));
            var open = Ui.Button("Open console", () => ShowSection("console"), "Btn.Ghost", Icons.Code);
            open.Margin = new Thickness(0);
            Grid.SetColumn(open, 1); head.Children.Add(open);
            consoleCard.Children.Add(head);
            var log = new StackPanel();
            AutomationProperties.SetName(log, "Last server log lines");
            var logBox = new Border { Background = Ui.Res("B.Ground"), CornerRadius = new CornerRadius(16), Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 10, 0, 0), Child = log, MinHeight = 120 };
            consoleCard.Children.Add(logBox);
            var c1 = McUi.Card(consoleCard); c1.Margin = new Thickness(0, 0, 12, 14);
            two.Children.Add(c1);
            var playersCard = new StackPanel();
            var players = new StackPanel();
            var phead = new Grid();
            phead.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            phead.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            phead.Children.Add(McUi.Title("Players online", 16));
            var pcount = new ContentControl { IsTabStop = false };
            Grid.SetColumn(pcount, 1); phead.Children.Add(pcount);
            playersCard.Children.Add(phead);
            playersCard.Children.Add(players);
            var c2 = McUi.Card(playersCard); Grid.SetColumn(c2, 1);
            two.Children.Add(c2);
            root.Children.Add(two);

            var attention = new StackPanel();
            var att = new StackPanel();
            att.Children.Add(McUi.Title("Needs your attention", 16));
            att.Children.Add(attention);
            root.Children.Add(McUi.Card(att));

            void Fill()
            {
                var st = McState.Cached(s).State;
                var live = McLive.For(s);
                bool up = st == McRunState.Running || st == McRunState.Starting;
                tiles.Children.Clear();
                tiles.Columns = ActualWidth > 0 && ActualWidth < 980 ? 3 : 5;
                tiles.Children.Add(Tile(Icons.Lines, "Players", up && live.PlayersOnline is int n ? $"{n} / {live.PlayersMax}" : "–",
                    !up ? "not running" : live.PlayersAt is DateTime pa ? "from the list command, " + McUi.Age(pa) : "waiting for the list command"));
                string tpsNote = !McSoftware.ReportsTps(s.Flavour) ? $"{s.Flavour} does not report it" : !up ? "not running" : live.Tps == null ? "waiting for the tps command" : "from the server, last minute";
                tiles.Children.Add(Tile(Icons.Bolt, "Ticks per second", up && live.Tps is double t ? t.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) : "Unavailable", tpsNote));
                tiles.Children.Add(Tile(Icons.Box, "Memory", up && live.MemoryBytes is long m ? $"{m / 1073741824.0:0.0} / {s.MemoryMb / 1024.0:0.#} GB" : $"– / {s.MemoryMb / 1024.0:0.#} GB", up ? (live.MemoryNote.Length > 0 ? live.MemoryNote : "reading…") : "the allowance; not running"));
                tiles.Children.Add(Tile(Icons.Clock, "Uptime", up && live.StartedAt is DateTime sa ? McUi.Span(DateTime.Now - sa) : "–", up ? (live.UptimeNote.Length > 0 ? live.UptimeNote : "reading…") : "not running"));
                tiles.Children.Add(Tile(Icons.Wifi, "Port", s.Port.ToString(), PortNote(s, up)));

                log.Children.Clear();
                var lines = live.Lines().TakeLast(6).ToList();
                if (lines.Count == 0) log.Children.Add(McUi.Body("No console lines yet. They appear here once the server has run.", "B.TextMute", 12.5));
                foreach (var l in lines) log.Children.Add(LogRow(l));

                players.Children.Clear();
                pcount.Content = McUi.Badge(up && live.PlayersOnline is int on ? $"{on} of {live.PlayersMax}" : "none", "B.Raised", "B.TextSoft");
                var names = up ? live.PlayerNames : Array.Empty<string>();
                if (names.Length == 0) players.Children.Add(McUi.Body(up ? "Nobody is online." : "The server is not running.", "B.TextMute", 12.5));
                var known = s.IsLocal ? McPlayers.Read(s.Folder) : new List<McPlayer>();
                foreach (var name in names.Take(8))
                {
                    var p = known.FirstOrDefault(k => string.Equals(k.Name, name, StringComparison.OrdinalIgnoreCase));
                    players.Children.Add(PlayerRow(s, name, p?.Operator == true ? "operator" : p?.Whitelisted == true ? "whitelisted" : "online", true));
                }

                attention.Children.Clear();
                foreach (var a in Attention(s, st, live)) attention.Children.Add(a);
                if (attention.Children.Count == 0) attention.Children.Add(McUi.Body("Nothing needs you right now.", "B.TextMute", 13));
                Core.Language.Refresh(root);
            }
            _tick = Fill;
            Fill();
            if (!McDemo.IsSample(s) && s.Flavour != Flavour.Vanilla && !string.IsNullOrEmpty(s.Build) && !UpdateChecks.ContainsKey(s.Id))
                _ = CheckUpdate(s, Fill);
            return root;
        }

        private static async Task CheckUpdate(ServerDef s, Action done)
        {
            UpdateChecks[s.Id] = (null, "Checking for a newer build…");
            try { UpdateChecks[s.Id] = await McSoftware.UpdateAsync(s); }
            catch (Exception ex) { UpdateChecks[s.Id] = (null, ex.Message); }
            done();
        }

        private static string PortNote(ServerDef s, bool up)
        {
            if (s.IsSsh) return "on the SSH host";
            if (up) return "open on this computer";
            if (ServerManager.Servers.Any(x => x != s && x.Port == s.Port && x.IsLocal)) return "shared with another server here";
            return ServerManager.PortFree(s.Port) ? "free on this computer" : "in use by another program";
        }

        private static FrameworkElement Tile(string icon, string label, string value, string note)
        {
            var sp = new StackPanel();
            var top = new StackPanel { Orientation = Orientation.Horizontal };
            top.Children.Add(new ContentControl { Content = Ui.Icon(icon, Ui.Res("B.Mint"), 14), Margin = new Thickness(0, 0, 8, 0), IsTabStop = false, VerticalAlignment = VerticalAlignment.Center });
            top.Children.Add(new TextBlock { Text = label, FontSize = 12.5, Foreground = Ui.Res("B.TextSoft"), FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap });
            sp.Children.Add(top);
            sp.Children.Add(new TextBlock { Text = value, FontSize = 24, Foreground = Ui.Res("B.Text"), FontFamily = Ui.Font("F.Display"), Margin = new Thickness(0, 6, 0, 4), TextWrapping = TextWrapping.Wrap });
            sp.Children.Add(new TextBlock { Text = note, FontSize = 11.5, Foreground = Ui.Res("B.TextMute"), FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap });
            var c = McUi.Card(sp, 16);
            Ui.Liftable(c);
            c.Margin = new Thickness(0, 0, 12, 12);
            AutomationProperties.SetName(c, label + ": " + value + ", " + note);
            return c;
        }

        internal static FrameworkElement LogRow(McLogLine l)
        {
            var t = new TextBlock { FontFamily = Ui.Font("F.Mono"), FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = Ui.Res("B.TextSoft"), Margin = new Thickness(0, 1, 0, 1) };
            Core.Language.SetLiteral(t, true);
            if (l.Time.Length > 0) t.Inlines.Add(new System.Windows.Documents.Run(l.Time + "  ") { Foreground = Ui.Res("B.TextMute") });
            var lv = McLogLine.LevelLabel(l.Level);
            t.Inlines.Add(new System.Windows.Documents.Run(lv.PadRight(5) + " ") { Foreground = Ui.Res(l.Level switch { McLogLevel.Warn => "B.Amber", McLogLevel.Chat => "B.Violet", McLogLevel.Command => "B.OnTertiaryContainer", _ => "B.Mint" }) });
            t.Inlines.Add(new System.Windows.Documents.Run(l.Message));
            return t;
        }

        private IEnumerable<FrameworkElement> Attention(ServerDef s, McRunState st, McLive live)
        {
            FrameworkElement Item(string icon, string text, string tone, string button, Action click)
            {
                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.Children.Add(new ContentControl { Content = Ui.Icon(icon, Ui.Res(tone == "warn" ? "B.Amber" : "B.Mint"), 18), Margin = new Thickness(0, 0, 14, 0), IsTabStop = false, VerticalAlignment = VerticalAlignment.Center });
                var tb = McUi.Body(text, tone == "warn" ? "B.Text" : "B.TextSoft", 13.5);
                tb.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(tb, 1); g.Children.Add(tb);
                if (button != null)
                {
                    var b = Ui.Button(button, click, tone == "warn" ? "Btn.Alert" : "Btn.Ghost");
                    b.Margin = new Thickness(12, 0, 0, 0);
                    Grid.SetColumn(b, 2); g.Children.Add(b);
                }
                return new Border { Background = Ui.Res(tone == "warn" ? "B.AmberWash" : "B.Raised"), CornerRadius = new CornerRadius(16), Padding = new Thickness(16, 10, 12, 10), Margin = new Thickness(0, 8, 0, 0), Child = g };
            }

            if (st == McRunState.HostOffline) yield return Item(Icons.WifiOff, McState.Cached(s).Detail, "warn", "Reconnect", () => _ = Act(s, "Reconnect", async () => { var n = await McState.ProbeAsync(s); if (n.State == McRunState.HostOffline) throw new InvalidOperationException(n.Detail); }));
            if (live.LastFailure != null) yield return Item(Icons.Bolt, "The last start failed: " + live.LastFailure, "warn", "Open console", () => ShowSection("console"));
            if (s.IsLocal && st == McRunState.Stopped && !ServerManager.PortFree(s.Port)) yield return Item(Icons.Wifi, $"Port {s.Port} is in use by another program, so the server cannot start until it is free or the port changes.", "warn", "Change the port", () => ShowSection("settings"));
            McBuild newer = null;
            lock (McScheduler.FoundUpdates) McScheduler.FoundUpdates.TryGetValue(s.Id, out newer);
            if (newer == null && UpdateChecks.TryGetValue(s.Id, out var chk)) newer = chk.Newer;
            if (newer != null) yield return Item(Icons.Restart, $"{s.Flavour} build {newer.Id} is out for {s.Version}. This server runs build {s.Build}. Take a backup first; the old build stays available to roll back to.", "warn", "Back up first", () => ShowSection("backups"));
            if (s.IsLocal)
            {
                var latest = McBackups.List(s.BackupFolder).FirstOrDefault();
                if (latest == null) yield return Item(Icons.Archive, "There is no backup of this world yet.", "info", "Back up now", () => ShowSection("backups"));
                else
                {
                    var next = s.Schedules.Where(j => j.Enabled && j.Kind == McScheduleKind.Backup).Select(j => j.Next(DateTime.Now)).Where(d => d != null).OrderBy(d => d).FirstOrDefault();
                    yield return Item(Icons.Archive, $"Last backup {McUi.Age(latest.When)}." + (next is DateTime nx ? $" The next scheduled backup runs at {nx:HH:mm}." : " No backup is scheduled."), "info", "Back up now", () => ShowSection("backups"));
                }
            }
        }
    }
}
