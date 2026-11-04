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
    // Console: the live log with level filters, follow and search, and the typed command builder.
    internal sealed partial class McManagerView
    {
        internal static readonly HashSet<McLogLevel> HiddenLevels = new HashSet<McLogLevel>();
        internal static bool Follow = true;
        private static readonly Dictionary<string, List<McCommandTree.Step>> Drafts = new Dictionary<string, List<McCommandTree.Step>>();
        private McCommandNode _tree;
        private TextBlock _preview;
        internal McCommandNode CommandTreeForTest => _tree;
        internal string PreviewLine => _preview?.Text;
        internal List<McCommandTree.Step> BuilderSteps => Current == null ? null : Drafts.TryGetValue(Current.Id, out var d) ? d : null;

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

        private FrameworkElement BuildCommandBuilder(ServerDef s)
        {
            var card = new StackPanel();
            var title = new StackPanel { Orientation = Orientation.Horizontal };
            title.Children.Add(new ContentControl { Content = Ui.Icon(Icons.Bolt, Ui.Res("B.Mint"), 16), Margin = new Thickness(0, 0, 8, 0), IsTabStop = false, VerticalAlignment = VerticalAlignment.Center });
            title.Children.Add(McUi.Title("Run a command", 16));
            card.Children.Add(title);
            var where = McUi.Body(s.Flavour == Flavour.Vanilla
                ? $"Commands and their arguments come from this server's own command list ({s.Version})."
                : $"Commands and their arguments come from the {s.Version} game's own command list. Commands added by plugins or mods are not in it.", "B.TextMute", 12.5);
            where.Margin = new Thickness(0, 2, 0, 10);
            card.Children.Add(where);
            var host = new StackPanel();
            card.Children.Add(host);
            var outer = McUi.Card(card);

            async void Load(bool download)
            {
                host.Children.Clear();
                var note = McUi.Body(download ? "Reading the command list from the server jar…" : "Loading the command list…", "B.TextMute", 12.5);
                host.Children.Add(note);
                var (tree, unavailable) = await McCommandTree.LoadAsync(s.Version, t => Dispatcher.BeginInvoke(new Action(() => { note.Text = t; Core.Language.Refresh(note); })), download);
                host.Children.Clear();
                if (tree == null)
                {
                    host.Children.Add(McUi.Body(unavailable + " Reading it downloads the official server jar for this version once and asks it for its commands.", "B.TextSoft", 13));
                    var b = Ui.Button("Read this version's command list", () => Load(true), "Btn.Ghost", Icons.Code);
                    b.HorizontalAlignment = HorizontalAlignment.Left; b.Margin = new Thickness(0, 8, 0, 0);
                    host.Children.Add(b);
                    Core.Language.Refresh(host);
                    return;
                }
                _tree = tree;
                host.Children.Add(BuilderBody(s, tree));
                Core.Language.Refresh(host);
            }
            Load(false);
            return outer;
        }

        private FrameworkElement BuilderBody(ServerDef s, McCommandNode tree)
        {
            if (!Drafts.TryGetValue(s.Id, out var steps)) Drafts[s.Id] = steps = new List<McCommandTree.Step>();
            var sp = new StackPanel();
            var picker = new SearchField("minecraft:commands", "Find a command", compact: true) { Margin = new Thickness(0, 0, 0, 6) };
            var roots = tree.Children.Where(c => c.IsLiteral).ToList();
            picker.SampleSource = () => roots.Select(r => r.Name);
            var rootChips = new WrapPanel();
            Core.Language.SetLiteral(rootChips, true);
            AutomationProperties.SetName(rootChips, "Commands");
            var chain = new WrapPanel { Margin = new Thickness(0, 8, 0, 4) };
            AutomationProperties.SetName(chain, "Command so far");
            var next = new WrapPanel();
            AutomationProperties.SetName(next, "What can come next");
            var nextLabel = McUi.Body("", "B.TextMute", 12);
            _preview = new TextBlock { FontFamily = Ui.Font("F.Mono"), FontSize = 14, Foreground = Ui.Res("B.OnSecondaryContainer"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 4) };
            Core.Language.SetLiteral(_preview, true);
            AutomationProperties.SetName(_preview, "The exact command line");
            var problem = McUi.Body("", "B.Amber", 12.5);
            var answer = McUi.Body("", "B.TextSoft", 12.5);
            AutomationProperties.SetLiveSetting(answer, AutomationLiveSetting.Polite);
            var recent = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };

            void Roots()
            {
                rootChips.Children.Clear();
                var hits = roots.Where(r => picker.State.Matches(r.Name)).ToList();
                picker.SetCount(hits.Count, roots.Count, "commands");
                foreach (var r in hits.Take(36))
                {
                    var rr = r;
                    rootChips.Children.Add(McUi.Chip(r.Name, steps.Count > 0 && steps[0].Node == r, () => { steps.Clear(); steps.Add(new McCommandTree.Step { Node = rr }); Paint(); Roots(); }, "Command " + r.Name));
                }
                if (hits.Count > 36) rootChips.Children.Add(McUi.Body($"{hits.Count - 36} more; narrow the search.", "B.TextMute", 12));
            }

            void Preview()
            {
                _preview.Text = steps.Count == 0 ? "/" : McCommandTree.Line(steps);
                var bad = McCommandTree.Validate(steps);
                problem.Text = bad ?? "";
                problem.Visibility = bad == null ? Visibility.Collapsed : Visibility.Visible;
                Core.Language.Refresh(problem);
            }

            void Paint()
            {
                chain.Children.Clear();
                for (int i = 0; i < steps.Count; i++)
                {
                    var step = steps[i];
                    int at = i;
                    if (step.Node.IsLiteral)
                    {
                        var chip = McUi.Chip(step.Node.Name, true, () => { steps.RemoveRange(at, steps.Count - at); Paint(); Roots(); }, $"Remove {step.Node.Name} and what follows");
                        Core.Language.SetLiteral(chip, true);
                        chain.Children.Add(chip);
                    }
                    else chain.Children.Add(ArgumentInput(s, step, Preview));
                }
                next.Children.Clear();
                var node = steps.Count == 0 ? null : steps[^1].Node;
                var kids = node?.Children ?? new List<McCommandNode>();
                foreach (var k in kids)
                {
                    var kk = k;
                    var label = k.IsLiteral ? k.Name : "<" + k.Name + ">";
                    var b = McUi.Chip(label, false, () => { steps.Add(new McCommandTree.Step { Node = kk, Value = kk.Parser == "brigadier:bool" ? "true" : kk.Min is double m && (kk.Parser == "brigadier:integer" || kk.Parser == "brigadier:long") ? ((long)m).ToString() : "" }); Paint(); }, k.IsLiteral ? "Add " + k.Name : $"Add {k.Name}, {k.Hint}");
                    Core.Language.SetLiteral(b, true);
                    b.ToolTip = k.IsLiteral ? null : k.Hint;
                    next.Children.Add(b);
                }
                nextLabel.Text = node == null ? "Choose a command above." : kids.Count == 0 ? "This command is complete." : node.Executable ? "It can run as it is, or take more:" : "Next:";
                Core.Language.Refresh(nextLabel);
                Preview();
            }

            void Recent()
            {
                recent.Children.Clear();
                if (s.RecentCommands.Count == 0) return;
                recent.Children.Add(new TextBlock { Text = "Recent:", FontSize = 12, Foreground = Ui.Res("B.TextMute"), FontFamily = Ui.Font("F.Body"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 6) });
                foreach (var r in s.RecentCommands.Take(8))
                {
                    var line = r;
                    var b = McUi.Chip(r, false, () => _ = Send(line), "Run again: " + r);
                    Core.Language.SetLiteral(b, true);
                    recent.Children.Add(b);
                }
                Core.Language.Refresh(recent);
            }

            async Task Send(string line)
            {
                answer.Text = "Sending…";
                string r;
                if (McDemo.IsSample(s)) r = "Sample server: nothing was sent.";
                else if (!IsUp(s)) r = "The server is not running, so nothing was sent.";
                else r = await ServerManager.CommandAsync(s, line);
                answer.Text = line + " · " + r;
                Core.Language.Refresh(answer);
                if (!McDemo.IsSample(s) && IsUp(s))
                {
                    s.RecentCommands.Remove(line);
                    s.RecentCommands.Insert(0, line);
                    if (s.RecentCommands.Count > 8) s.RecentCommands.RemoveRange(8, s.RecentCommands.Count - 8);
                    try { ServerManager.Save(); } catch { }
                    Recent();
                }
            }

            picker.Filter += Roots;
            sp.Children.Add(picker);
            sp.Children.Add(rootChips);
            sp.Children.Add(chain);
            sp.Children.Add(nextLabel);
            sp.Children.Add(next);
            var runRow = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            runRow.Children.Add(_preview);
            sp.Children.Add(runRow);
            sp.Children.Add(problem);
            var buttons = new WrapPanel();
            buttons.Children.Add(Ui.Button("Run", () => { var bad = McCommandTree.Validate(steps); if (bad != null) { problem.Text = bad; problem.Visibility = Visibility.Visible; Core.Language.Refresh(problem); return; } _ = Send(McCommandTree.Line(steps)); }, "Btn.Accent", Icons.Play));
            buttons.Children.Add(Ui.Button("Clear", () => { steps.Clear(); Paint(); Roots(); }, "Btn.Ghost", Icons.Close));
            sp.Children.Add(buttons);
            sp.Children.Add(answer);
            sp.Children.Add(recent);
            Roots();
            Paint();
            Recent();
            return sp;
        }

        /// <summary>The control for one argument, chosen by its parser: a number box with the jar's bounds, true or false, or a text field.</summary>
        private FrameworkElement ArgumentInput(ServerDef s, McCommandTree.Step step, Action changed)
        {
            var n = step.Node;
            var label = new TextBlock { Text = n.Name, FontSize = 11.5, Foreground = Ui.Res("B.TextMute"), FontFamily = Ui.Font("F.Mono"), Margin = new Thickness(2, 0, 0, 2) };
            Core.Language.SetLiteral(label, true);
            var sp = new StackPanel { Margin = new Thickness(0, 0, 8, 6) };
            sp.Children.Add(label);
            if (n.Parser == "brigadier:integer" || n.Parser == "brigadier:long")
            {
                long min = n.Min is double a ? (long)a : int.MinValue, max = n.Max is double b ? (long)b : int.MaxValue;
                long.TryParse(step.Value, out var v);
                var box = new McNumberBox(n.Name, Math.Clamp(v, min, max), min, max);
                step.Value = box.Value.ToString();
                box.ValueChanged += x => { step.Value = x.ToString(); changed(); };
                box.Box.TextChanged += (o, e) => { step.Value = box.Box.Text.Trim(); changed(); };
                sp.Children.Add(box);
            }
            else if (n.Parser == "brigadier:bool")
            {
                var wrap = new WrapPanel();
                Button t = null, f = null;
                t = McUi.Chip("true", step.Value == "true", () => { step.Value = "true"; McUi.Paint(t, true); McUi.Paint(f, false); changed(); });
                f = McUi.Chip("false", step.Value == "false", () => { step.Value = "false"; McUi.Paint(t, false); McUi.Paint(f, true); changed(); });
                Core.Language.SetLiteral(wrap, true);
                wrap.Children.Add(t); wrap.Children.Add(f);
                sp.Children.Add(wrap);
            }
            else
            {
                var box = McUi.Field(step.Value, n.Name, n.Parser == "minecraft:message" || n.StringKind == "greedy" ? 320 : 200, n.Hint);
                box.TextChanged += (o, e) => { step.Value = box.Text.Replace("\r", "").Replace("\n", " "); changed(); };
                sp.Children.Add(box);
                if ((n.Parser == "minecraft:entity" || n.Parser == "minecraft:game_profile") && McLive.For(s).PlayerNames.Length > 0)
                {
                    var names = new WrapPanel();
                    Core.Language.SetLiteral(names, true);
                    foreach (var pn in McLive.For(s).PlayerNames.Take(6)) { var x = pn; names.Children.Add(McUi.Chip(pn, false, () => box.Text = x, "Use " + pn)); }
                    sp.Children.Add(names);
                }
            }
            var hint = new TextBlock { Text = n.Hint, FontSize = 11, Foreground = Ui.Res("B.TextMute"), FontFamily = Ui.Font("F.Body"), Margin = new Thickness(2, 0, 0, 0) };
            sp.Children.Add(hint);
            return sp;
        }
    }
}
