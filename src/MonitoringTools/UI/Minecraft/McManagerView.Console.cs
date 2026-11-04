using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using MonitoringTools.Core;
using MonitoringTools.Minecraft;
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI.Minecraft
{
    // Console: the live log with level filters, follow and search, and the way into the command builder with the
    // favourites and recent commands.
    internal sealed partial class McManagerView
    {
        internal static readonly HashSet<McLogLevel> HiddenLevels = new HashSet<McLogLevel>();
        internal static bool Follow = true;

        private FrameworkElement BuildConsole(ServerDef s)
        {
            var root = new StackPanel();
            var card = new StackPanel();
            var head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.Children.Add(McUi.Title("Console"));
            var chips = new WrapPanel();
            AutomationProperties.SetName(chips, "Show");
            Grid.SetColumn(chips, 1); head.Children.Add(chips);
            card.Children.Add(head);
            var search = new SearchField("minecraft:log", "Search the log", compact: true) { Margin = new Thickness(0, 10, 0, 8) };
            card.Children.Add(search);
            var lines = new StackPanel();
            var scroll = new ScrollViewer { Content = lines, Height = 340, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            AutomationProperties.SetName(scroll, "Server log");
            AutomationProperties.SetLiveSetting(scroll, AutomationLiveSetting.Polite);
            card.Children.Add(new Border { Background = Ui.Res("B.Ground"), CornerRadius = new CornerRadius(16), Padding = new Thickness(14, 10, 6, 10), Child = scroll });
            var count = McUi.Body("", "B.TextMute", 12);
            count.Margin = new Thickness(4, 6, 0, 0);
            card.Children.Add(count);
            root.Children.Add(McUi.Card(card));

            int shownFor = -1; string lastSig = null;
            void Lines(bool force = false)
            {
                var all = McLive.For(s).Lines();
                var sig = all.Count + "|" + (all.Count > 0 ? all[^1].Raw : "") + "|" + string.Join(",", HiddenLevels) + "|" + search.State.Query + search.State.IsRegex;
                if (!force && sig == lastSig) return;
                lastSig = sig;
                search.SampleSource = () => all.Select(l => l.Raw).TakeLast(200);
                var hits = all.Where(l => !HiddenLevels.Contains(l.Level) && search.State.Matches(l.Raw)).ToList();
                search.SetCount(hits.Count, all.Count, "lines");
                lines.Children.Clear();
                foreach (var l in hits.TakeLast(400)) lines.Children.Add(LogRow(l));
                if (hits.Count == 0) lines.Children.Add(McUi.Body(all.Count == 0 ? "No console lines yet. Start the server to see its log here." : "No lines match the filters.", "B.TextMute", 12.5));
                count.Text = all.Count == 0 ? "" : hits.Count > 400 ? $"Showing the last 400 of {hits.Count} lines." : $"{hits.Count} of {all.Count} lines";
                Core.Language.Refresh(count);
                if (Follow) scroll.ScrollToEnd();
                shownFor = all.Count;
            }
            void Chips()
            {
                chips.Children.Clear();
                foreach (var (lv, label) in new[] { (McLogLevel.Info, "Info"), (McLogLevel.Warn, "Warnings"), (McLogLevel.Chat, "Chat"), (McLogLevel.Command, "Commands") })
                {
                    var l = lv;
                    chips.Children.Add(McUi.Chip(label, !HiddenLevels.Contains(lv), () => { if (!HiddenLevels.Remove(l)) HiddenLevels.Add(l); Chips(); Lines(true); }, (HiddenLevels.Contains(lv) ? "Show " : "Hide ") + label.ToLowerInvariant()));
                }
                chips.Children.Add(McUi.Chip("Follow", Follow, () => { Follow = !Follow; Chips(); if (Follow) scroll.ScrollToEnd(); }, Follow ? "Stop following the newest line" : "Follow the newest line"));
                Core.Language.Refresh(chips);
            }
            search.Filter += () => Lines(true);
            Chips();
            Lines(true);
            _tick = () => Lines();

            root.Children.Add(s.IsBedrock ? BuildBedrockCommand(s) : BuildCommandBuilder(s));
            return root;
        }

        /// <summary>Bedrock has no command list to read from a jar, so its console takes a typed line, sent as it is.</summary>
        private FrameworkElement BuildBedrockCommand(ServerDef s)
        {
            var card = new StackPanel();
            card.Children.Add(McUi.Title("Run a command", 16));
            var where = McUi.Body("The Bedrock server has no command list to build from, so type the command as you would in its own console, for example list or allowlist add Ava.", "B.TextMute", 12.5);
            where.Margin = new Thickness(0, 2, 0, 10);
            card.Children.Add(where);
            var row = new WrapPanel();
            var box = McUi.Field("", "Command", 360, "A Bedrock server command, without the slash");
            box.FontFamily = Ui.Font("F.Mono");
            row.Children.Add(box);
            var answer = McUi.Body("", "B.TextMute", 12.5);
            AutomationProperties.SetLiveSetting(answer, AutomationLiveSetting.Polite);
            async Task Send()
            {
                var line = box.Text.Trim().TrimStart('/');
                if (line.Length == 0) return;
                answer.Text = "Sending…";
                string r = McDemo.IsSample(s) ? "Sample server: nothing was sent." : !IsUp(s) ? "The server is not running, so nothing was sent." : await ServerManager.CommandAsync(s, line);
                answer.Text = "/" + line + " · " + r;
                Core.Language.Refresh(answer);
            }
            var run = Ui.Button("Run", () => _ = Send(), "Btn.Accent", Icons.Play);
            run.Margin = new Thickness(8, 0, 0, 0);
            row.Children.Add(run);
            box.KeyDown += (o, e) => { if (e.Key == System.Windows.Input.Key.Enter) _ = Send(); };
            card.Children.Add(row);
            card.Children.Add(answer);
            return McUi.Card(card);
        }

        /// <summary>The console's way into the command builder, with the favourites and the recent commands, each one click to run.</summary>
        private FrameworkElement BuildCommandBuilder(ServerDef s)
        {
            var card = new StackPanel();
            var title = new StackPanel { Orientation = Orientation.Horizontal };
            title.Children.Add(new ContentControl { Content = Ui.Icon(Icons.Bolt, Ui.Res("B.Mint"), 16), Margin = new Thickness(0, 0, 8, 0), IsTabStop = false, VerticalAlignment = VerticalAlignment.Center });
            title.Children.Add(McUi.Title("Run a command", 16));
            card.Children.Add(title);
            var where = McUi.Body("Build any command with proper controls: players and selectors, items, blocks, effects and mobs from this version's own lists, numbers with their real limits and positions with relative axes. There is no free shell field.", "B.TextMute", 12.5);
            where.Margin = new Thickness(0, 2, 0, 10);
            card.Children.Add(where);
            var open = new WrapPanel();
            open.Children.Add(Ui.Button("Open the command builder", () => { CommandMode = "tasks"; ShowSection("commands"); }, "Btn.Accent", Icons.Bolt));
            var every = Ui.Button("Every command", () => { CommandMode = "tree"; ShowSection("commands"); }, "Btn.Ghost", Icons.Code);
            every.Margin = new Thickness(8, 0, 0, 0);
            open.Children.Add(every);
            card.Children.Add(open);
            var answer = McUi.Body("", "B.TextSoft", 12.5);
            AutomationProperties.SetLiveSetting(answer, AutomationLiveSetting.Polite);
            async Task Send(string line)
            {
                if (McCommandRun.NeedsConfirmation(line)) { OpenCommandLine(s, line); return; }   // stop, ban and operator changes confirm in the builder
                answer.Text = "Sending…";
                string r = McDemo.IsSample(s) ? "Sample server: nothing was sent." : !IsUp(s) ? "The server is not running, so nothing was sent." : await McCommandRun.SendAsync(s, line);
                answer.Text = line + " · " + r;
                Core.Language.Refresh(answer);
            }
            WrapPanel Row(string label, IEnumerable<string> lines, bool star)
            {
                var row = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
                AutomationProperties.SetName(row, label);
                row.Children.Add(new TextBlock { Text = label + ":", FontSize = 12, Foreground = Ui.Res("B.TextMute"), FontFamily = Ui.Font("F.Body"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 6) });
                foreach (var r in lines)
                {
                    var line = r;
                    var b = McUi.Chip((star ? "★ " : "") + r, false, () => _ = Send(line), (star ? "Run favourite: " : "Run again: ") + r);
                    Core.Language.SetLiteral(b, true);
                    row.Children.Add(b);
                }
                return row;
            }
            if (s.FavouriteCommands.Count > 0) card.Children.Add(Row("Favourites", s.FavouriteCommands.Take(8), true));
            if (s.RecentCommands.Count > 0) card.Children.Add(Row("Recent", s.RecentCommands.Take(8), false));
            card.Children.Add(answer);
            return McUi.Card(card);
        }

        /// <summary>Opens a line in the builder (Every command), where it is checked and confirmed before it runs.</summary>
        private void OpenCommandLine(ServerDef s, string line)
        {
            CommandMode = "tree";
            ShowSection("commands");
            int tries = 0;
            void Later() { if (_tree != null) OpenLine(s, line); else if (++tries < 400) Dispatcher.BeginInvoke(new Action(Later), System.Windows.Threading.DispatcherPriority.Background); }
            Dispatcher.BeginInvoke(new Action(Later), System.Windows.Threading.DispatcherPriority.Background);
        }

    }
}
