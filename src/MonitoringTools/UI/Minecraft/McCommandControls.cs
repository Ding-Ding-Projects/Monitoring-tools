using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// <summary>One field of the command builder: what is shown, and the control a click on its part of the preview focuses.</summary>
    internal sealed class McFieldView
    {
        public FrameworkElement View;
        public FrameworkElement Focus;
    }

    /// <summary>
    /// The controls of the command builder, one per kind of argument the server's command tree names: players and selectors,
    /// searchable id lists from the version's registries, typed numbers with the parser's bounds, positions with a relative
    /// toggle per axis, times with a unit, choices and true or false. Each control writes the exact text the argument takes.
    /// </summary>
    internal static class McCommandControls
    {
        /// <summary>Reads a player's position as the reply of data get entity; null when it cannot be read, with the reason.</summary>
        internal delegate Task<(double[] Pos, string Problem)> PositionReader(string player);

        private static readonly Dictionary<string, string> Plural = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["minecraft:item"] = "items", ["minecraft:block"] = "blocks", ["minecraft:mob_effect"] = "effects", ["minecraft:entity_type"] = "entities",
            ["minecraft:enchantment"] = "enchantments", ["minecraft:dimension"] = "dimensions", ["minecraft:particle_type"] = "particles",
            ["minecraft:sound_event"] = "sounds", ["minecraft:worldgen/biome"] = "biomes", ["minecraft:attribute"] = "attributes",
        };
        internal static string PluralOf(string registry) => registry != null && Plural.TryGetValue(registry, out var p) ? p : "entries";

        /// <summary>Why each choice exists and what its options do, so every choice explains itself.</summary>
        internal static readonly Dictionary<string, string> ChoiceHelp = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["change"] = "Add puts more on top of what they have; set replaces it.",
            ["unit"] = "Levels are the number above the hotbar; points fill the bar between levels.",
            ["operator"] = "Op makes them an operator, who can run every command; deop takes that away.",
            ["listchange"] = "Add lets them join while the whitelist is on; remove takes them off it.",
            ["when"] = "Day is 1000, noon 6000, night 13000 and midnight 18000. An exact time is in ticks: 20 ticks are a second and a whole day is 24000.",
            ["weather"] = "Without a length, the weather lasts a random while and then changes on its own.",
            ["placing"] = "Replace overwrites what is there; keep only places into air; destroy breaks the old block as a player would, so it drops.",
            ["filling"] = "Replace fills every block; hollow and outline keep only the shell; keep only fills air; destroy drops what it breaks.",
            ["difficulty"] = "Peaceful has no hostile mobs and refills hunger; hard makes mobs stronger and lets hunger take all your health.",
            ["titlekind"] = "A title is large in the middle of the screen, a subtitle sits under it, and the action bar is a line above the hotbar. A subtitle shows with the next title.",
            ["gamemode"] = "Survival is the normal game; creative has unlimited blocks and flight; adventure stops block breaking; spectator flies through everything unseen.",
            ["bool"] = "True turns it on, false turns it off.",
            ["time-unit"] = "Ticks are the game's own unit: 20 ticks are one second, and a game day is 24000 ticks.",
            ["sort"] = "Which players come first when there are more than the limit.",
            ["only-mode"] = "Only counts players in this game mode.",
        };

        private static TextBlock Label(string text, bool optional)
        {
            var t = new TextBlock { Text = text, FontSize = 13, FontWeight = FontWeights.Medium, Foreground = Ui.Res("B.TextSoft"), FontFamily = Ui.Font("F.Body"), Margin = new Thickness(0, 0, 0, 6), TextWrapping = TextWrapping.Wrap };
            if (optional) t.Text = text + " (optional)";
            return t;
        }

        private static TextBlock Hint(string text) => new TextBlock { Text = text, FontSize = 11.5, Foreground = Ui.Res("B.TextMute"), FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 2, 0, 0) };

        /// <summary>The control for an argument node, chosen by its parser.</summary>
        internal static McFieldView For(ServerDef s, McCommandNode n, string label, bool optional, string value, Action<string> set, McRegistries reg, PositionReader readPos, bool literalNames = false)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
            var labelText = Label(label, optional);
            // a field named by a server (a plugin command) keeps the server's own word
            if (literalNames) Core.Language.SetLiteral(labelText, true);
            sp.Children.Add(labelText);
            FrameworkElement body, focus;
            var p = n.Parser ?? "";
            if (p == "brigadier:integer" || p == "brigadier:long") (body, focus) = Whole(n, label, optional, value, set);
            else if (p == "brigadier:double" || p == "brigadier:float") (body, focus) = Decimal(n, label, value, set);
            else if (p == "brigadier:bool") { var c = Bool(label, optional, value, set); body = focus = c; }
            else if (p == "minecraft:time") (body, focus) = Time(n, label, optional, value, set);
            else if (p == "minecraft:gamemode") { var c = Enum("gamemode", label, McCommandTree.GameModes, optional, value, set, false); body = focus = c; }
            else if (McCommandTree.IsTarget(p)) (body, focus) = Who(s, n, label, value, set);
            else if (McCommandTree.Axes(p) > 0) (body, focus) = Position(s, n, label, optional, value, set, readPos);
            else if (p == "minecraft:nbt_compound_tag" || p == "minecraft:nbt_tag") { var ed = new McNbtEditor(label, value, set); body = ed; focus = Logical<Button>(ed).FirstOrDefault() ?? (FrameworkElement)ed; }
            else if (p == "minecraft:item_stack" && McRegistries.RegistryOf(n) is string itemRegistry) (body, focus) = ItemStack(s, n, itemRegistry, label, value, set, reg);
            else if (McRegistries.RegistryOf(n) is string registry) (body, focus) = Pick(n, registry, label, value, set, reg);
            else if (p == "minecraft:component") { var box = Text(McCommandTasks.Uncomponent(value), label, 420, n.Hint); box.TextChanged += (o, e) => set(box.Text.Length == 0 ? "" : McCommandTasks.Component(box.Text)); body = focus = box; }
            else
            {
                bool wide = p == "minecraft:message" || n.StringKind == "greedy" || n.StringKind == "phrase";
                var box = Text(value, label, wide ? 420 : 240, n.Hint);
                box.TextChanged += (o, e) => set(box.Text.Replace("\r", "").Replace("\n", " "));
                body = focus = box;
            }
            if (literalNames) foreach (var named in Logical<FrameworkElement>(body).Where(e => e is TextBox || e is McNumberBox)) Core.Language.SetLiteral(named, true);
            sp.Children.Add(body);
            var hint = p.StartsWith("brigadier:", StringComparison.Ordinal) || p.Length == 0 ? n.Hint : n.Hint + " · " + p;
            var h = Hint(hint);
            sp.Children.Add(h);
            return new McFieldView { View = sp, Focus = focus };
        }

        internal static TextBox Text(string value, string name, double width, string hint)
        {
            var box = McUi.Field(value, name, width, hint);
            box.FontFamily = Ui.Font("F.Mono");
            Core.Language.SetLiteral(box, true);
            return box;
        }

        private static (FrameworkElement, FrameworkElement) Whole(McCommandNode n, string label, bool optional, string value, Action<string> set)
        {
            long min = n.Min is double a ? (long)a : int.MinValue, max = n.Max is double b ? (long)b : int.MaxValue;
            bool empty = string.IsNullOrWhiteSpace(value);
            long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var v);
            var start = empty ? Math.Clamp(0, min, max) : v;
            var box = new McNumberBox(label, start, min, max) { AllowEmpty = optional };
            if (empty && optional) box.Box.Text = "";
            else if (empty) set(start.ToString(CultureInfo.InvariantCulture));
            else box.Box.Text = value.Trim();   // shown as typed, even out of range, so the problem is visible
            box.Box.TextChanged += (o, e) => set(box.Box.Text.Trim());
            return (box, box.Box);
        }

        private static (FrameworkElement, FrameworkElement) Decimal(McCommandNode n, string label, string value, Action<string> set)
        {
            var row = new WrapPanel();
            var box = Text(value, label, 120, n.Hint);
            box.Margin = new Thickness(0, 0, 4, 0);
            void Step(int dir)
            {
                double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d);
                d += dir;
                if (n.Min is double mn) d = Math.Max(mn, d);
                if (n.Max is double mx) d = Math.Min(mx, d);
                box.Text = d.ToString(CultureInfo.InvariantCulture);
            }
            row.Children.Add(McUi.IconButton("M6 12h12", "Decrease " + label, () => Step(-1)));
            row.Children.Add(box);
            row.Children.Add(McUi.IconButton(Icons.Plus, "Increase " + label, () => Step(1)));
            box.TextChanged += (o, e) => set(box.Text.Trim());
            box.PreviewKeyDown += (o, e) => { if (e.Key == System.Windows.Input.Key.Up) { Step(1); e.Handled = true; } else if (e.Key == System.Windows.Input.Key.Down) { Step(-1); e.Handled = true; } };
            return (row, box);
        }

        private static FrameworkElement Bool(string label, bool optional, string value, Action<string> set)
        {
            var opts = new List<(string, string)>();
            if (optional) opts.Add(("Leave out", ""));
            opts.Add(("True", "true")); opts.Add(("False", "false"));
            var c = McUi.Choice(label, ChoiceHelp["bool"], opts, value ?? "", v => set(v));
            c.Children.RemoveAt(0);   // the field already has its label above
            return c;
        }

        internal static StackPanel Enum(string helpKey, string label, IEnumerable<string> options, bool optional, string value, Action<string> set, bool literal)
        {
            var opts = new List<(string, string)>();
            if (optional) opts.Add(("Leave out", ""));
            foreach (var o in options) opts.Add((o.StartsWith("{", StringComparison.Ordinal) ? McCommandTasks.LabelOf(o.Trim('{', '}')) : literal ? o : McRegistries.Readable(o), o));
            var c = McUi.Choice(label, ChoiceHelp.TryGetValue(helpKey, out var h) ? h : "Choose one of the words this command takes here.", opts, value ?? "", v => set(v), literal);
            c.Children.RemoveAt(0);
            return c;
        }

        private static (FrameworkElement, FrameworkElement) Time(McCommandNode n, string label, bool optional, string value, Action<string> set)
        {
            var sp = new StackPanel();
            var row = new WrapPanel();
            string unit = "";
            string number = value ?? "";
            if (number.EndsWith("s") || number.EndsWith("d") || number.EndsWith("t")) { unit = number[^1..] == "t" ? "" : number[^1..]; number = number[..^1]; }
            long min = n.Min is double a ? (long)a : 0;
            bool empty = string.IsNullOrWhiteSpace(number);
            long.TryParse(number, out var v);
            var box = new McNumberBox(label, empty ? Math.Max(min, 1) : v, min, int.MaxValue) { AllowEmpty = optional, Margin = new Thickness(0, 0, 10, 0) };
            if (empty && optional) box.Box.Text = "";
            void Write() { var t = box.Box.Text.Trim(); set(t.Length == 0 ? "" : t + unit); }
            row.Children.Add(box);
            var units = McUi.Choice("Unit", ChoiceHelp["time-unit"], new[] { ("Ticks", ""), ("Seconds", "s"), ("Days", "d") }, unit, u => { unit = u; Write(); });
            units.Children.RemoveAt(0);
            row.Children.Add(units);
            sp.Children.Add(row);
            box.Box.TextChanged += (o, e) => Write();
            if (empty && !optional) Write();
            return (sp, box.Box);
        }

        // ------------------------------------------------------------------ who

        private static (FrameworkElement, FrameworkElement) Who(ServerDef s, McCommandNode n, string label, string value, Action<string> set)
        {
            var sp = new StackPanel();
            var online = McLive.For(s).PlayerNames ?? Array.Empty<string>();
            var chips = new WrapPanel();
            AutomationProperties.SetName(chips, label);
            var explain = Hint("");
            var filters = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
            var nameBox = Text(value != null && !value.StartsWith("@", StringComparison.Ordinal) && !online.Contains(value) ? value : "", "Another player's name", 220, "A player who is not online now; letters, digits and _, up to 16");
            var bases = new List<string> { "@p", "@a", "@r", "@s" };
            bool entities = n.Parser == "minecraft:score_holder" || (n.Parser == "minecraft:entity" && n.EntityKind != "players");
            if (entities) bases.Add("@e");
            string current = value ?? "";
            var sel = McSelector.Parse(current) ?? new McSelector { Base = "@a" };
            bool showFilters = McSelector.Parse(current) is McSelector ps && (ps.MaxDistance != null || ps.Limit != null || ps.Sort.Length > 0 || ps.GameMode.Length > 0);
            Button filterChip = null;

            void Paint()
            {
                chips.Children.Clear();
                foreach (var name in online)
                {
                    var nm = name;
                    var c = McUi.Chip(name, current == name, () => { current = nm; nameBox.Text = ""; set(nm); Paint(); }, "Player " + name);
                    Core.Language.SetLiteral(c, true);
                    chips.Children.Add(c);
                }
                foreach (var b in bases)
                {
                    var bb = b;
                    bool on = current.StartsWith(b, StringComparison.Ordinal);
                    var c = McUi.Chip(b, on, () => { sel.Base = bb; current = bb == "@s" || !showFilters ? bb : sel.ToString(); if (bb == "@s") sel = new McSelector { Base = "@s" }; nameBox.Text = ""; set(current); Paint(); }, b + ": " + McSelector.Explain(b));
                    Core.Language.SetLiteral(c, true);
                    c.ToolTip = McSelector.Explain(b);
                    chips.Children.Add(c);
                }
                filterChip = McUi.Chip("Filters", showFilters, () => { showFilters = !showFilters; if (!showFilters) { sel = new McSelector { Base = sel.Base }; if (current.StartsWith("@", StringComparison.Ordinal)) { current = sel.Base; set(current); } } Paint(); }, showFilters ? "Hide the selector filters" : "Filter the selector by distance, number, order and game mode");
                filterChip.Visibility = current.StartsWith("@", StringComparison.Ordinal) && !current.StartsWith("@s", StringComparison.Ordinal) ? Visibility.Visible : Visibility.Collapsed;
                chips.Children.Add(filterChip);
                explain.Text = current.Length == 0 ? (online.Length > 0 ? "Choose a player who is online, a selector, or type a name." : "No one is online now. Choose a selector, or type a name.")
                    : current.StartsWith("@", StringComparison.Ordinal) ? McSelector.Explain(current.Substring(0, 2))
                    : online.Contains(current) ? current + " is online now."
                    : n.Parser == "minecraft:game_profile" ? current + " is not online now; that is fine for this command."
                    : current + " is not online now, so the command finds no one unless they join first.";
                Core.Language.Refresh(explain);
                filters.Children.Clear();
                filters.Visibility = showFilters && filterChip.Visibility == Visibility.Visible ? Visibility.Visible : Visibility.Collapsed;
                if (filters.Visibility == Visibility.Visible) BuildFilters();
                Core.Language.Refresh(chips);
                Core.Language.Refresh(filters);
            }

            void Apply() { current = sel.ToString(); set(current); explain.Text = McSelector.Explain(sel.Base); Core.Language.Refresh(explain); }

            void BuildFilters()
            {
                var row = new WrapPanel();
                var near = new StackPanel { Margin = new Thickness(0, 0, 18, 8) };
                near.Children.Add(Hint("Within this many blocks"));
                var dist = new McNumberBox("Within this many blocks", sel.MaxDistance ?? 10, 1, 100000, 1, "blocks") { AllowEmpty = true };
                if (sel.MaxDistance == null) dist.Box.Text = "";
                dist.Box.TextChanged += (o, e) => { sel.MaxDistance = int.TryParse(dist.Box.Text.Trim(), out var d) && d >= 1 ? d : null; Apply(); };
                near.Children.Add(dist);
                row.Children.Add(near);
                var lim = new StackPanel { Margin = new Thickness(0, 0, 18, 8) };
                lim.Children.Add(Hint("At most this many"));
                var limit = new McNumberBox("At most this many", sel.Limit ?? 1, 1, 1000) { AllowEmpty = true };
                if (sel.Limit == null) limit.Box.Text = "";
                limit.Box.TextChanged += (o, e) => { sel.Limit = int.TryParse(limit.Box.Text.Trim(), out var l) && l >= 1 ? l : null; Apply(); };
                lim.Children.Add(limit);
                row.Children.Add(lim);
                filters.Children.Add(row);
                filters.Children.Add(McUi.Choice("Order", ChoiceHelp["sort"], new[] { ("Any", ""), ("Nearest", "nearest"), ("Furthest", "furthest"), ("Random", "random"), ("Arbitrary", "arbitrary") }, sel.Sort, v => { sel.Sort = v; Apply(); }));
                filters.Children.Add(McUi.Choice("Only players in", ChoiceHelp["only-mode"], new[] { ("Any game mode", ""), ("Survival", "survival"), ("Creative", "creative"), ("Adventure", "adventure"), ("Spectator", "spectator") }, sel.GameMode, v => { sel.GameMode = v; Apply(); }));
            }

            nameBox.TextChanged += (o, e) => { var t = nameBox.Text.Trim(); if (t.Length == 0) return; current = t; set(t); Paint(); };
            sp.Children.Add(chips);
            sp.Children.Add(explain);
            sp.Children.Add(filters);
            var nameRow = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            var or = new TextBlock { Text = "Or type a name", FontSize = 12.5, Foreground = Ui.Res("B.TextMute"), FontFamily = Ui.Font("F.Body"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 8) };
            nameRow.Children.Add(or);
            nameRow.Children.Add(nameBox);
            sp.Children.Add(nameRow);
            Paint();
            return (sp, (FrameworkElement)chips.Children.OfType<Button>().FirstOrDefault() ?? nameBox);
        }

        // ------------------------------------------------------------------ positions

        private static (FrameworkElement, FrameworkElement) Position(ServerDef s, McCommandNode n, string label, bool optional, string value, Action<string> set, PositionReader readPos)
        {
            int count = McCommandTree.Axes(n.Parser);
            var names = count == 3 ? new[] { "X", "Y", "Z" } : n.Parser == "minecraft:rotation" ? new[] { "Yaw", "Pitch" } : new[] { "X", "Z" };
            var parts = (value ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            bool empty = parts.Length != count;
            var rel = new bool[count]; var boxes = new TextBox[count]; var toggles = new Button[count];
            bool whole = n.Parser == "minecraft:block_pos" || n.Parser == "minecraft:column_pos";
            var sp = new StackPanel();
            var row = new WrapPanel();
            bool loading = true;
            void Write()
            {
                if (loading) return;
                if (optional && boxes.All(b => b.Text.Trim().Length == 0) && rel.All(r => !r)) { set(""); return; }
                set(McPosition.Join(Enumerable.Range(0, count).Select(i => (rel[i], boxes[i].Text))));
            }
            for (int i = 0; i < count; i++)
            {
                int at = i;
                var p = empty ? (optional ? "" : "~") : parts[i];
                rel[i] = p.StartsWith("~", StringComparison.Ordinal);
                var num = rel[i] ? p.Substring(1) : p;
                var cell = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 14, 8) };
                var axis = new TextBlock { Text = names[i], FontFamily = Ui.Font("F.Mono"), FontSize = 13, Foreground = Ui.Res("B.TextSoft"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
                Core.Language.SetLiteral(axis, true);
                cell.Children.Add(axis);
                Button t = null;
                t = McUi.Chip("~", rel[i], () => { rel[at] = !rel[at]; McUi.Paint(t, rel[at]); AutomationProperties.SetName(t, names[at] + (rel[at] ? " is relative to the target" : " is exact")); Write(); }, names[i] + (rel[i] ? " is relative to the target" : " is exact"));
                Core.Language.SetLiteral(t, true);
                t.ToolTip = "Relative: ~ means where the target is, ~5 means five blocks further along this axis.";
                t.Margin = new Thickness(0, 0, 6, 0);
                toggles[i] = t;
                cell.Children.Add(t);
                var box = Text(num, names[i], 96, whole ? "A whole number, or an offset when ~ is on" : "A number, or an offset when ~ is on");
                box.Margin = new Thickness(0);
                boxes[i] = box;
                box.TextChanged += (o, e) => Write();
                cell.Children.Add(box);
                row.Children.Add(cell);
            }
            loading = false;
            sp.Children.Add(row);
            var online = McLive.For(s).PlayerNames ?? Array.Empty<string>();
            var answer = Hint("");
            AutomationProperties.SetLiveSetting(answer, AutomationLiveSetting.Polite);
            if (readPos != null)
            {
                var use = new WrapPanel();
                AutomationProperties.SetName(use, "Use a player's position");
                foreach (var pl in online.Take(8))
                {
                    var who = pl;
                    var b = Ui.Button("Use " + pl + "'s position", async () =>
                    {
                        answer.Text = "Reading " + who + "'s position…"; Core.Language.Refresh(answer);
                        var (pos, problem) = await readPos(who);
                        if (pos == null) { answer.Text = problem; Core.Language.Refresh(answer); return; }
                        loading = true;
                        for (int i = 0; i < count && i < 3; i++)
                        {
                            int src = count == 2 && i == 1 ? 2 : i;   // two axes are X and Z
                            rel[i] = false; McUi.Paint(toggles[i], false);
                            boxes[i].Text = whole ? Math.Floor(pos[src]).ToString(CultureInfo.InvariantCulture) : Math.Round(pos[src], 2).ToString(CultureInfo.InvariantCulture);
                        }
                        loading = false; Write();
                        answer.Text = who + " is at " + string.Join(" ", pos.Select(x => Math.Round(x, 1).ToString(CultureInfo.InvariantCulture))) + ".";
                        Core.Language.Refresh(answer);
                    }, "Btn.Ghost", Icons.Globe);
                    Core.Language.SetLiteral(b, false);
                    b.Margin = new Thickness(0, 0, 8, 6);
                    use.Children.Add(b);
                }
                if (online.Length == 0) answer.Text = "No one is online, so there is no player's position to use.";
                sp.Children.Add(use);
                sp.Children.Add(answer);
            }
            if (empty && !optional) Write();
            return (sp, toggles[0]);
        }

        // ------------------------------------------------------------------ ids from the registries

        internal static (FrameworkElement, FrameworkElement) Pick(McCommandNode n, string registry, string label, string value, Action<string> set, McRegistries reg)
        {
            var ids = reg?.Get(registry);
            var sp = new StackPanel();
            if (ids == null || ids.Count == 0)
            {
                var box = Text(value, label, 280, n.Hint);
                box.TextChanged += (o, e) => set(box.Text.Trim());
                sp.Children.Add(box);
                sp.Children.Add(Hint(reg == null ? "This version's lists have not been read, so type the id." : $"The {McRegistries.Readable(registry).ToLowerInvariant()} list is not in this version's reports, so type the id."));
                return (sp, box);
            }
            var plural = PluralOf(registry);
            var search = new SearchField("minecraft:pick:" + registry, "Search " + plural, compact: true) { Margin = new Thickness(0, 0, 0, 6) };
            search.SampleSource = () => ids.Take(200);
            var count = Hint($"{ids.Count.ToString("N0", CultureInfo.InvariantCulture)} {plural} in {reg.Version}");
            var grid = new WrapPanel();
            AutomationProperties.SetName(grid, label);
            string current = value ?? "";
            var buttons = new List<(Button B, string Id)>();
            void Paint()
            {
                grid.Children.Clear(); buttons.Clear();
                var hits = ids.Where(id => search.State.Matches(id) || search.State.Matches(McRegistries.Readable(id))).ToList();
                // the chosen id stays first, so it is visible whatever the search
                if (current.Length > 0 && hits.Remove(current)) hits.Insert(0, current);
                search.SetCount(hits.Count, ids.Count, plural);
                foreach (var id in hits.Take(24))
                {
                    var i = id;
                    var stack = new StackPanel { Width = 168, HorizontalAlignment = HorizontalAlignment.Left };
                    stack.Children.Add(new TextBlock { Text = McRegistries.Readable(id), FontSize = 13, FontFamily = Ui.Font("F.Body"), TextTrimming = TextTrimming.CharacterEllipsis });
                    stack.Children.Add(new TextBlock { Text = id, FontSize = 11, FontFamily = Ui.Font("F.Mono"), Opacity = 0.75, TextTrimming = TextTrimming.CharacterEllipsis });
                    var b = new Button { Style = Ui.Style("Btn.Chip"), Content = stack, Height = double.NaN, Width = 196, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 6, 6), ToolTip = id };
                    McUi.Paint(b, id == current);
                    AutomationProperties.SetName(b, McRegistries.Readable(id) + ", " + id);
                    Core.Language.SetLiteral(b, true);
                    b.Click += (o, e) => { current = i; set(i); foreach (var (bb, ii) in buttons) McUi.Paint(bb, ii == current); };
                    buttons.Add((b, id));
                    grid.Children.Add(b);
                }
                if (hits.Count > 24) grid.Children.Add(Hint($"{hits.Count - 24} more; narrow the search."));
                Core.Language.Refresh(grid);
            }
            search.Filter += Paint;
            sp.Children.Add(count);
            sp.Children.Add(search);
            sp.Children.Add(grid);
            Paint();
            return (sp, search.Box);
        }

        private static IEnumerable<T> Logical<T>(DependencyObject d) where T : DependencyObject
        {
            foreach (var c in LogicalTreeHelper.GetChildren(d).OfType<DependencyObject>())
            {
                if (c is T t) yield return t;
                foreach (var x in Logical<T>(c)) yield return x;
            }
        }

        /// <summary>
        /// An item argument: the id from the registry, and under it the item's data. Java 1.20.5 and newer take components
        /// (a custom name, enchantments, damage and the rest) and earlier versions take NBT, each built with controls.
        /// </summary>
        private static (FrameworkElement, FrameworkElement) ItemStack(ServerDef s, McCommandNode n, string registry, string label, string value, Action<string> set, McRegistries reg)
        {
            var (id, rest) = McItemComponents.SplitValue(value);
            string curId = id, curRest = rest;
            void Push() => set(curId + curRest);
            var (pick, focus) = Pick(n, registry, label, curId, v => { curId = v; Push(); }, reg);
            var sp = new StackPanel();
            sp.Children.Add(pick);
            bool components = McItemComponents.Supports(s.Version);
            var host = new ContentControl { IsTabStop = false, Margin = new Thickness(0, 6, 0, 0) };
            var open = curRest.Length > 0;
            Button toggle = null;
            void Paint()
            {
                McUi.Paint(toggle, open);
                host.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
                if (open && host.Content == null)
                {
                    if (components) host.Content = new McItemEditor(s.Version, reg, curRest, r => { curRest = r; Push(); });
                    else host.Content = new McNbtEditor("Item data (NBT)", curRest, r => { curRest = r; Push(); });
                    Core.Language.Refresh(host);
                }
            }
            string name = components ? "Item components" : "Item data (NBT)";
            toggle = McUi.Chip(name, open, () => { open = !open; Paint(); }, name);
            toggle.ToolTip = components ? "Custom name, lore, enchantments, damage and other components of the item" : "Item data written as NBT";
            toggle.Margin = new Thickness(0, 8, 0, 0);
            sp.Children.Add(toggle);
            sp.Children.Add(host);
            Paint();
            return (sp, focus);
        }

        /// <summary>A long choice (game rules) as a searchable list of literal words.</summary>
        internal static FrameworkElement LongChoice(string id, string label, IReadOnlyList<string> options, string value, Action<string> set, string version)
        {
            var sp = new StackPanel();
            var search = new SearchField("minecraft:choice:" + id, "Search " + label.ToLowerInvariant() + "s", compact: true) { Margin = new Thickness(0, 0, 0, 6) };
            search.SampleSource = () => options;
            var wrap = new WrapPanel();
            AutomationProperties.SetName(wrap, label);
            Core.Language.SetLiteral(wrap, true);
            string current = value ?? "";
            void Paint()
            {
                wrap.Children.Clear();
                var hits = options.Where(o => search.State.Matches(o)).ToList();
                search.SetCount(hits.Count, options.Count, "rules");
                foreach (var o in hits)
                {
                    var oo = o;
                    var b = McUi.Chip(o, o == current, () => { current = oo; set(oo); Paint(); }, label + " " + o);
                    wrap.Children.Add(b);
                }
            }
            search.Filter += Paint;
            sp.Children.Add(Hint($"{options.Count} rules in {version}, from its own command list."));
            sp.Children.Add(search);
            sp.Children.Add(wrap);
            Paint();
            return sp;
        }
    }
}
