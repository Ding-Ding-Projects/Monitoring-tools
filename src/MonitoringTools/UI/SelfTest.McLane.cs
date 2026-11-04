using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using MonitoringTools.Core;
using MonitoringTools.Minecraft;
using MonitoringTools.UI.Minecraft;

namespace MonitoringTools.UI
{
    // Checks for item components and NBT built with controls, plugin and mod commands read from a server's help, the
    // peak-players history, the Worlds buttons (import a folder and a ZIP, replace, reset) and pause for pre-generation.
    public static partial class SelfTest
    {
        private static void Fill(TextBox box, string text) { box.Text = text; }

        private static void MakeWorldFolder(string dir, string marker)
        {
            Directory.CreateDirectory(Path.Combine(dir, "region"));
            File.WriteAllBytes(Path.Combine(dir, "level.dat"), new byte[] { 1, 2, 3, 4 });
            File.WriteAllText(Path.Combine(dir, "region", "r.0.0.mca"), marker);
        }

        private static async Task McLaneChecksAsync(ControlCentreWindow cc)
        {
            var mc = cc.MinecraftManager;

            Check("minecraft commands: SNBT is written and read back for every tag type, nested compounds and lists, and wrong values are refused", () =>
            {
                var root = new SnbtNode { Type = SnbtType.Compound };
                root.Items.Add(new SnbtNode { Name = "Invulnerable", Type = SnbtType.Byte, Value = "1" });
                root.Items.Add(new SnbtNode { Name = "Age", Type = SnbtType.Short, Value = "-5" });
                root.Items.Add(new SnbtNode { Name = "Score", Type = SnbtType.Int, Value = "42" });
                root.Items.Add(new SnbtNode { Name = "Big", Type = SnbtType.Long, Value = "9000000000" });
                root.Items.Add(new SnbtNode { Name = "Speed", Type = SnbtType.Float, Value = "1.5" });
                root.Items.Add(new SnbtNode { Name = "Scale", Type = SnbtType.Double, Value = "2" });
                root.Items.Add(new SnbtNode { Name = "CustomName", Type = SnbtType.String, Value = "Bessie \"the cow\"" });
                var tags = new SnbtNode { Name = "Tags", Type = SnbtType.List };
                tags.Items.Add(new SnbtNode { Type = SnbtType.String, Value = "pet" });
                tags.Items.Add(new SnbtNode { Type = SnbtType.String, Value = "it's" });
                root.Items.Add(tags);
                var inner = new SnbtNode { Name = "Brain", Type = SnbtType.Compound };
                inner.Items.Add(new SnbtNode { Name = "memories", Type = SnbtType.Compound });
                root.Items.Add(inner);
                var text = McSnbt.Render(root);
                var want = "{Invulnerable:1b,Age:-5s,Score:42,Big:9000000000L,Speed:1.5f,Scale:2.0d,CustomName:'Bessie \"the cow\"',Tags:[\"pet\",\"it's\"],Brain:{memories:{}}}";
                if (text != want) return "rendered " + text;
                var back = McSnbt.Parse(text);
                if (back == null || McSnbt.Render(back) != text) return "did not read back: " + (back == null ? "null" : McSnbt.Render(back));
                if (McSnbt.Validate(root) != null) return "a valid tree was refused: " + McSnbt.Validate(root);
                var bad = new (SnbtType T, string V)[] { (SnbtType.Byte, "200"), (SnbtType.Short, "40000"), (SnbtType.Int, "1.5"), (SnbtType.Long, "x"), (SnbtType.Float, "abc") };
                foreach (var (t, v) in bad) if (McSnbt.CheckValue(t, v) == null) return $"{t} accepted {v}";
                var mixed = new SnbtNode { Type = SnbtType.List };
                mixed.Items.Add(new SnbtNode { Type = SnbtType.Int, Value = "1" });
                mixed.Items.Add(new SnbtNode { Type = SnbtType.String, Value = "a" });
                if (McSnbt.Validate(new SnbtNode { Type = SnbtType.Compound, Items = { new SnbtNode { Name = "L", Type = SnbtType.List, Items = mixed.Items } } }) == null) return "a list of two types was accepted";
                var dup = new SnbtNode { Type = SnbtType.Compound, Items = { new SnbtNode { Name = "a", Type = SnbtType.Int, Value = "1" }, new SnbtNode { Name = "a", Type = SnbtType.Int, Value = "2" } } };
                if (McSnbt.Validate(dup) == null) return "a key used twice was accepted";
                if (McSnbt.Parse("{a:[B;1b,2b]}") != null) return "a typed array was read as if the tree could hold it";
                return McSnbt.LooksValid("{a:[B;1b,2b]}") && !McSnbt.LooksValid("{a:1") ? null : "typed arrays or an unclosed brace were judged wrongly";
            });

            Check("minecraft commands: item components are written in the form each version takes, and read back", () =>
            {
                var (tree, reg) = SampleReports();
                reg.AddData(McSampleReports.DataComponents);
                var levels = new[] { new KeyValuePair<string, int>("minecraft:sharpness", 5) };
                if (McItemComponents.Enchantments("1.21.4", levels) != "{levels:{\"minecraft:sharpness\":5}}") return "1.21.4 enchantments: " + McItemComponents.Enchantments("1.21.4", levels);
                if (McItemComponents.Enchantments("1.21.5", levels) != "{\"minecraft:sharpness\":5}") return "1.21.5 enchantments: " + McItemComponents.Enchantments("1.21.5", levels);
                if (McItemComponents.Text("1.21.4", "Hi \"you\"") != "'\"Hi \\\\\"you\\\\\"\"'") return "1.21.4 name: " + McItemComponents.Text("1.21.4", "Hi \"you\"");
                if (McItemComponents.Text("1.21.5", "Hi") != "\"Hi\"") return "1.21.5 name: " + McItemComponents.Text("1.21.5", "Hi");
                if (McItemComponents.ModelData("1.20.6", 7) != "7" || McItemComponents.ModelData("1.21.4", 7) != "{floats:[7.0f]}") return "model data came out as " + McItemComponents.ModelData("1.20.6", 7) + " and " + McItemComponents.ModelData("1.21.4", 7);
                foreach (var v in new[] { "1.21.4", "1.21.5" })
                {
                    if (McItemComponents.UnText(v, McItemComponents.Text(v, "Big \"one\"")) != "Big \"one\"") return v + ": the name did not read back";
                    if (!McItemComponents.UnLore(v, McItemComponents.Lore(v, new[] { "a", "b c" })).SequenceEqual(new[] { "a", "b c" })) return v + ": the lore did not read back";
                    var e = McItemComponents.UnEnchantments(v, McItemComponents.Enchantments(v, levels));
                    if (e == null || e.Count != 1 || e[0].Key != "minecraft:sharpness" || e[0].Value != 5) return v + ": the enchantments did not read back";
                    if (McItemComponents.UnModelData(v, McItemComponents.ModelData(v, 12)) != 12) return v + ": the model data did not read back";
                }
                if (McItemComponents.Supports("1.20.4") || !McItemComponents.Supports("1.20.5") || !McItemComponents.Supports("1.21.4")) return "the component cut-off is wrong";
                var good = "[minecraft:custom_name='\"Hi there\"',minecraft:damage=3,minecraft:unbreakable={}]";
                if (McItemComponents.Check("1.21.4", good, reg) != null) return "good components were refused: " + McItemComponents.Check("1.21.4", good, reg);
                var cases = new (string Suffix, string Want)[]
                {
                    ("[minecraft:not_a_component=1]", "minecraft:not_a_component is not a component in this version's list."),
                    ("[minecraft:damage=3,minecraft:damage=4]", "minecraft:damage is listed twice."),
                    ("[minecraft:damage=]", "The value of minecraft:damage is not valid SNBT."),
                    ("[minecraft:damage={]", "Item components go in square brackets, like [minecraft:damage=3]."),
                    ("{Damage:3}", "This version takes item data as components in square brackets, not NBT."),
                };
                foreach (var (rest, want) in cases) { var got = McItemComponents.Check("1.21.4", rest, reg); if (got != want) return $"{rest} said \"{got}\", expected \"{want}\""; }
                if (McItemComponents.Check("1.20.4", "{Damage:3}", reg) != null || McItemComponents.Check("1.20.4", "[a=1]", reg) == null) return "NBT item data on an older version was judged wrongly";
                // the whole give line goes through the tree walker as one word, spaces in the name included
                var line = "give Ava minecraft:diamond_sword[minecraft:custom_name='\"Big sword\"',minecraft:damage=3]";
                var steps = McCommandTree.ParseLine(tree, line);
                if (steps == null) return "the walker could not read a give with components";
                if (McCommandTree.Validate(steps, reg) != null) return "the walker refused it: " + McCommandTree.Validate(steps, reg);
                return McCommandTree.Line(steps) == "/" + line ? null : "the walker rebuilt " + McCommandTree.Line(steps);
            });

            Check("minecraft commands: commands the running server reports are read from its help and become forms", () =>
            {
                var (tree, _) = SampleReports();
                var cmds = McLiveCommands.Parse(McSampleReports.LiveHelp.Split('\n'), tree);
                var names = string.Join(",", cmds.Select(c => c.Name));
                if (names != "home,mute,spark,warp") return "found " + names + " (time is the jar's own and minecraft: ones are not new)";
                var mute = cmds.First(c => c.Name == "mute");
                if (mute.Description != "Stops a player from chatting" || mute.Usage != "<player> [minutes] [reason]") return "mute read as " + mute.Description + " | " + mute.Usage;
                if (cmds.First(c => c.Name == "warp").Source != "essentials") return "the namespace of warp was lost";
                var task = McLiveCommands.ToTask(mute);
                if (!task.Live || task.Template != "mute {player} {minutes?} {reason?}") return "template: " + task.Template;
                var b = McCommandTasks.Build(task, task.Synthetic, new Dictionary<string, string> { ["player"] = "Ava", ["minutes"] = "10", ["reason"] = "spam" }, null);
                if (b.Problem != null || b.Line != "/mute Ava 10 spam") return "the form built " + b.Line + " · " + b.Problem;
                var two = McCommandTasks.Build(task, task.Synthetic, new Dictionary<string, string> { ["player"] = "Ava" }, null);
                if (two.Problem != null || two.Line != "/mute Ava") return "the optional fields were not optional: " + two.Line + " · " + two.Problem;
                var noPlayer = McCommandTasks.Build(task, task.Synthetic, new Dictionary<string, string>(), null);
                if (noPlayer.Problem != "Fill in player.") return "a missing player said: " + noPlayer.Problem;
                var badMinutes = McCommandTasks.Build(task, task.Synthetic, new Dictionary<string, string> { ["player"] = "Ava", ["minutes"] = "soon" }, null);
                if (badMinutes.Problem != "minutes must be a whole number.") return "minutes soon said: " + badMinutes.Problem;
                var spark = McCommandTasks.Build(McLiveCommands.ToTask(cmds.First(c => c.Name == "spark")), McLiveCommands.ToTask(cmds.First(c => c.Name == "spark")).Synthetic, new Dictionary<string, string> { ["choice"] = "stop" }, null);
                if (spark.Line != "/spark stop") return "a choice came out as " + spark.Line;
                // RCON returns vanilla's usage lines glued into one run of text; they are told apart again
                var glued = McLiveCommands.Parse(new[] { "/time set (day|night)/spark <player> [seconds]/give <targets> <item>/profiler (start|stop)/essentials:warp <name>" }, tree);
                if (string.Join(",", glued.Select(c => c.Name)) != "profiler,spark,warp" || glued.First(c => c.Name == "spark").Usage != "<player> [seconds]") return "glued help read as " + string.Join(",", glued.Select(c => c.Name + ":" + c.Usage));
                var described = McLiveCommands.Parse(new[] { "/home: Use /help home to see more", "/warp: Goes to a warp" }, tree);
                if (described.First(c => c.Name == "home").Description != "Use /help home to see more" || described.Count != 2) return "a description naming another command was split";
                // Paper's own help index and a colour-coded line, as the console prints them
                var paper = McLiveCommands.Parse(new[] { "[10:00:00 INFO]: --------- Help: Index (1/3) ---------------------", "[10:00:00 INFO]: /bukkit:version: Gets the version of this server", "[10:00:00 INFO]: /§6paper§r: Paper related commands", "[10:00:00 INFO]: /minecraft:help", "[10:00:00 INFO]: /give <targets> <item>" }, tree);
                return string.Join(",", paper.Select(c => c.Name + "@" + c.Source)) == "paper@server,version@bukkit" ? null : "paper help read as " + string.Join(",", paper.Select(c => c.Name + "@" + c.Source));
            });

            Check("minecraft overview: the peak per day is kept per server, only goes up within a day, is saved to disk and prunes after 30 days", () =>
            {
                File.Delete(McPeaks.FilePath); McPeaks.Reload();
                var today = new DateTime(2026, 11, 4, 15, 0, 0);
                McPeaks.Record("lane-a", 3, today.AddDays(-2));
                McPeaks.Record("lane-a", 7, today.AddDays(-2));
                McPeaks.Record("lane-a", 2, today.AddDays(-2));
                McPeaks.Record("lane-a", 5, today);
                McPeaks.Record("lane-b", 11, today);
                McPeaks.Record("lane-a", 1, today.AddDays(-40));
                if (McPeaks.PeakToday("lane-a", today) != 5 || McPeaks.PeakToday("lane-b", today) != 11) return "today's peaks are " + McPeaks.PeakToday("lane-a", today) + " and " + McPeaks.PeakToday("lane-b", today);
                if (McPeaks.Peak7Days("lane-a", today) != 7) return "the 7 day peak is " + McPeaks.Peak7Days("lane-a", today);
                var days = McPeaks.Last("lane-a", 7, today);
                if (days.Count != 7 || days[4].Peak != 7 || days[6].Peak != 5 || days[5].Peak != null) return "the days are " + string.Join(",", days.Select(d => d.Peak?.ToString() ?? "-"));
                McPeaks.Reload();
                if (McPeaks.Peak7Days("lane-a", today) != 7) return "the history did not come back from the file";
                var json = File.ReadAllText(McPeaks.FilePath);
                if (!json.Contains("\"2026-11-02\":7") || !json.Contains("lane-b") || json.Contains("2026-09-25")) return "the file holds " + json;
                return McPeaks.PeakToday("nobody", today) == null && McPeaks.Peak7Days("nobody", today) == null ? null : "an unknown server has a peak";
            });

            Check("minecraft worlds: pre-generation batches cover the square exactly, never exceed 16 x 16 chunks and are never thin strips", () =>
            {
                foreach (var (radius, centre) in new[] { (1, 0), (2, -3), (8, 0), (16, 0), (17, 0), (23, 5), (31, 0), (50, 0), (100, 0) })
                {
                    var tiles = WorldGenerator.Tiles(centre - radius, radius * 2);
                    if (tiles.Count != WorldGenerator.BatchCount(radius) && tiles.Count * tiles.Count != WorldGenerator.BatchCount(radius)) return $"radius {radius}: {tiles.Count} tiles per side but BatchCount says {WorldGenerator.BatchCount(radius)}";
                    int at = centre - radius;
                    foreach (var (first, last) in tiles)
                    {
                        if (first != at) return $"radius {radius}: a gap or an overlap at chunk {first}";
                        int width = last - first + 1;
                        if (width > WorldGenerator.ChunksPerBatchSide) return $"radius {radius}: a batch {width} chunks wide";
                        if (radius * 2 >= 16 && width < 8) return $"radius {radius}: a thin batch of {width} chunks";
                        at = last + 1;
                    }
                    if (at != centre + radius) return $"radius {radius}: the tiles end at {at - 1}, not {centre + radius - 1}";
                }
                var t17 = WorldGenerator.Tiles(-17, 34);
                return string.Join(",", t17.Select(t => t.Last - t.First + 1)) == "12,11,11" ? null : "17 chunks each side split as " + string.Join(",", t17.Select(t => t.Last - t.First + 1));
            });

            Check("minecraft worlds: pausing holds pre-generation at a batch boundary and resuming carries on; cancelling works while paused", () =>
            {
                var p = new GenPause();
                var order = new List<string>();
                using var cts = new CancellationTokenSource();
                p.Paused = true;
                var run = Task.Run(async () => { order.Add("before"); await p.WaitAsync(cts.Token, () => order.Add("announced")); order.Add("after"); });
                Thread.Sleep(450);
                if (run.IsCompleted || order.Contains("after")) return "the run went past a pause";
                if (p.Waits != 1 || !order.Contains("announced")) return "the pause was not announced";
                p.Paused = false;
                if (!run.Wait(3000)) return "resume did not release the run";
                var q = new GenPause { Paused = true };
                var stuck = Task.Run(() => q.WaitAsync(cts.Token, null));
                Thread.Sleep(200);
                cts.Cancel();
                try { stuck.Wait(3000); return "cancelling a paused run did not stop it"; }
                catch (AggregateException ex) when (ex.InnerException is OperationCanceledException) { }
                return new GenPause().Paused ? "a new run starts paused" : null;
            });

            await CheckAsync("minecraft commands: the item component editor builds a give line with a name, an enchantment, damage and unbreakable", async () =>
            {
                mc.Select("sample-friends");
                var s = McManagerView.Current;
                McManagerView.ValuesFor(s, "give").Clear();
                mc.OpenCommandTask("give");
                await Wave6Flows.Settle(cc, 900);
                if (!Press(mc, "Ava")) return "no Ava";
                await Task.Delay(150);
                if (Named(mc, "Diamond sword, minecraft:diamond_sword") is not Button sword) return "no diamond sword in the list";
                sword.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                await Task.Delay(150);
                if (Named(mc, "Item components") is not Button open) return "no Item components control under the item list";
                open.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                await Wave6Flows.Settle(cc, 400);
                if (FindBox(mc, "Custom name") is not TextBox name) return "the editor has no Custom name box";
                Fill(name, "Big sword");
                await Task.Delay(100);
                if (mc.PreviewLine != "/give Ava minecraft:diamond_sword[minecraft:custom_name='\"Big sword\"']") return "after the name the line is " + mc.PreviewLine;
                if (Descend<TextBox>(mc).FirstOrDefault(t => (AutomationProperties.GetName(t) ?? "").StartsWith("Search enchantments")) is TextBox find) { find.Text = "sharp"; await Task.Delay(200); }
                if (Named(mc, "Sharpness, minecraft:sharpness") is not Button sharp) return "the editor lists no Sharpness from the enchantment registry";
                sharp.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Fill(FindBox(mc, "Enchantment level"), "4");
                await Task.Delay(100);
                if (!Press(mc, "Add enchantment")) return "no Add enchantment button";
                await Task.Delay(150);
                Fill(FindBox(mc, "Damage"), "3");
                await Task.Delay(100);
                if (Named(mc, "Unbreakable") is not Button un) return "no Unbreakable switch";
                un.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                await Task.Delay(150);
                var want = "/give Ava minecraft:diamond_sword[minecraft:custom_name='\"Big sword\"',minecraft:enchantments={levels:{\"minecraft:sharpness\":4}},minecraft:damage=3,minecraft:unbreakable={}]";
                if (mc.PreviewLine != want) return "the line is " + mc.PreviewLine;
                if (!mc.RunEnabled || !mc.CommandStatus.StartsWith("Complete and valid for 1.21.4")) return "the status says: " + mc.CommandStatus;
                // a generic component, validated against the version's component registry
                if (Named(mc, "Max stack size, minecraft:max_stack_size") is not Button stack) return "the generic component list lacks max_stack_size";
                stack.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Fill(FindBox(mc, "Component value"), "{");
                if (!Press(mc, "Add component")) return "no Add component button";
                await Task.Delay(120);
                if (mc.PreviewLine != want) return "a bad component value changed the line: " + mc.PreviewLine;
                Fill(FindBox(mc, "Component value"), "16");
                Press(mc, "Add component");
                await Task.Delay(150);
                if (mc.PreviewLine != want.Replace(",minecraft:unbreakable={}]", ",minecraft:unbreakable={},minecraft:max_stack_size=16]")) return "the generic component was not added: " + mc.PreviewLine;
                // removing the enchantment chip takes it out of the line again
                if (Named(mc, "Remove Sharpness 4") is not Button rm) return "no chip to remove the enchantment";
                rm.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                await Task.Delay(150);
                return mc.PreviewLine.Contains("enchantments") ? "the enchantment stayed in the line: " + mc.PreviewLine : null;
            });

            await CheckAsync("minecraft commands: the NBT editor builds a summon with typed keys, a list and a nested compound, and refuses a value out of range", async () =>
            {
                McManagerView.CommandMode = "tree";
                mc.Select("sample-friends");
                var s = McManagerView.Current;
                mc.ShowSection("commands");
                await Wave6Flows.Settle(cc, 700);
                Press(mc, "Clear"); await Task.Delay(120);
                if (!Press(mc, "summon")) return "no summon command";
                await Task.Delay(150);
                if (!Press(mc, "<entity>")) return "no <entity> after summon";
                await Task.Delay(200);
                if (Named(mc, "Cow, minecraft:cow") is not Button cow) return "no cow in the entity list";
                cow.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                await Task.Delay(120);
                if (!Press(mc, "<pos>")) return "no <pos> after the entity";
                await Task.Delay(200);
                if (!Press(mc, "<nbt>")) return "no <nbt> after the position";
                await Wave6Flows.Settle(cc, 400);
                async Task<Button> Wait(string name) { for (int i = 0; i < 10; i++) { if (Named(mc, name) is Button b) return b; await Task.Delay(80); } return null; }
                async Task Click(string name) { var b = await Wait(name); if (b == null) throw new InvalidOperationException("no control named " + name); b.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); await Task.Delay(120); }
                await Click("Add key in root");
                Fill(FindBox(mc, "Key of root/1"), "Invulnerable");
                await Click("Type byte for root/1");
                Fill(FindBox(mc, "Value of root/1"), "1");
                await Click("Add key in root");
                Fill(FindBox(mc, "Key of root/2"), "CustomName");
                await Click("Type string for root/2");
                Fill(FindBox(mc, "Value of root/2"), "Bessie");
                await Click("Add key in root");
                Fill(FindBox(mc, "Key of root/3"), "Tags");
                await Click("Type list for root/3");
                await Click("Add item in root/3");
                await Click("Type string for root/3/1");
                Fill(FindBox(mc, "Value of root/3/1"), "pet");
                await Click("Add key in root");
                Fill(FindBox(mc, "Key of root/4"), "Brain");
                await Click("Type compound for root/4");
                await Click("Add key in root/4");
                Fill(FindBox(mc, "Key of root/4/1"), "angry");
                await Click("Type byte for root/4/1");
                await Task.Delay(120);
                var want = "/summon minecraft:cow ~ ~ ~ {Invulnerable:1b,CustomName:\"Bessie\",Tags:[\"pet\"],Brain:{angry:0b}}";
                if (mc.PreviewLine != want || mc.CommandStatus?.StartsWith("Complete") != true) return "the summon is " + mc.PreviewLine + " · " + mc.CommandStatus;
                // a byte out of range is shown as a problem and the line stops being valid
                Fill(FindBox(mc, "Value of root/1"), "300");
                await Task.Delay(150);
                if (mc.RunEnabled) return "Run stayed on with a byte of 300";
                if (mc.CommandStatus != "Invulnerable: A byte is from -128 to 127.") return "the status line said: " + mc.CommandStatus;
                var texts = string.Join(" | ", Descend<TextBlock>(mc).Where(t => t.IsVisible).Select(t => t.Text));
                if (!texts.Contains("A byte is from -128 to 127.")) return "the byte problem was not shown";
                Fill(FindBox(mc, "Value of root/1"), "1");
                await Task.Delay(120);
                // a command saved with the NBT opens in the editor again, key by key
                var steps = McCommandTree.ParseLine(mc.CommandTreeForTest, want.TrimStart('/'));
                if (steps == null || McCommandTree.Validate(steps, mc.RegistriesForTest) != null) return "the walker did not accept the finished summon";
                var again = McSnbt.Parse(steps[^1].Value);
                McManagerView.CommandMode = "tasks";
                return again != null && again.Items.Count == 4 && again.Items[3].Items[0].Name == "angry" ? null : "the NBT did not read back into a tree";
            });

            await CheckAsync("minecraft commands: plugin and mod commands from the live server get their own group, labelled, with typed forms", async () =>
            {
                mc.Select("sample-friends");
                var s = McManagerView.Current;
                McLiveCommands.Forget(s.Id);
                McManagerView.CommandMode = "tasks";
                mc.OpenCommandTask("give");
                await Wave6Flows.Settle(cc, 900);
                for (int i = 0; i < 20 && McCommandTasks.LiveFor(s.Id).Count == 0; i++) await Task.Delay(100);
                await Wave6Flows.Settle(cc, 300);
                var live = McCommandTasks.LiveFor(s.Id).Select(t => t.Label).ToList();
                if (!live.SequenceEqual(new[] { "/home", "/mute", "/spark", "/warp" })) return "the live group holds " + string.Join(", ", live);
                var texts = string.Join(" | ", Descend<TextBlock>(mc).Where(t => t.IsVisible).Select(t => t.Text));
                if (!texts.Contains("Plugins and mods") || !texts.Contains("Sample commands, shown to illustrate the form. Nothing was asked of a real server.")) return "the group or its sample label is not shown";
                if (Named(mc, "/mute") is not Button row) return "no /mute row in the gallery";
                row.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                await Wave6Flows.Settle(cc, 600);
                texts = string.Join(" | ", Descend<TextBlock>(mc).Where(t => t.IsVisible).Select(t => t.Text));
                if (!texts.Contains("From the live server. The fields follow the usage text the server printed.") || !texts.Contains("/mute <player> [minutes] [reason]")) return "the form does not say it came from the live server";
                if (!Press(mc, "Ava")) return "no Ava for the player field";
                await Task.Delay(120);
                Fill(FindBox(mc, "minutes"), "10");
                Fill(FindBox(mc, "Reason"), "spam");
                await Task.Delay(150);
                if (mc.PreviewLine != "/mute Ava 10 spam" || !mc.RunEnabled) return "the form built " + mc.PreviewLine + " · " + mc.CommandStatus;
                Fill(FindBox(mc, "minutes"), "soon");
                await Task.Delay(120);
                if (mc.RunEnabled || mc.CommandStatus != "minutes must be a whole number.") return "a word for minutes said: " + mc.CommandStatus;
                Fill(FindBox(mc, "minutes"), "");
                Fill(FindBox(mc, "Reason"), "");
                await Task.Delay(120);
                McCommandTasks.SetLive(s.Id, new List<McTask>());
                McLiveCommands.Forget(s.Id);
                mc.OpenCommandTask("give");
                return McManagerView.CommandTaskId == "give" ? null : "the builder did not return to the give task";
            });

            await CheckAsync("minecraft commands: Use a player's position fills the three axes from the server's reply and builds the teleport line", async () =>
            {
                mc.Select("sample-friends");
                var s = McManagerView.Current;
                McCommandRun.PositionHook = (srv, who) => Task.FromResult((who == "Ava" ? new[] { 12.5, 64.0, -3.25 } : null, who == "Ava" ? null : "no such player"));
                try
                {
                    var v = McManagerView.ValuesFor(s, "tp"); v.Clear(); v["targets"] = "Ben";
                    mc.OpenCommandTask("tp");
                    await Wave6Flows.Settle(cc, 800);
                    var btn = Descend<Button>(mc).FirstOrDefault(b => AutomationProperties.GetName(b) == "Use Ava's position" || ButtonLabel(b) == "Use Ava's position");
                    if (btn == null) return "no Use Ava's position button";
                    btn.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                    await Task.Delay(300);
                    if (mc.PreviewLine != "/tp Ben 12.5 64 -3.25") return "the teleport is " + mc.PreviewLine;
                    var said = string.Join(" | ", Descend<TextBlock>(mc).Select(t => t.Text));
                    return said.Contains("Ava is at 12.5 64 -3.2") ? null : "the answer line is missing: " + said.Substring(0, Math.Min(200, said.Length));
                }
                finally { McCommandRun.PositionHook = null; McManagerView.ValuesFor(s, "tp").Clear(); }
            });

            await CheckAsync("minecraft overview: the peak tiles and the seven-day bars show a sample server's history and a real server's recorded peaks", async () =>
            {
                mc.Select("sample-friends");
                mc.ShowSection("overview");
                await Wave6Flows.Settle(cc, 700);
                var sample = string.Join(" | ", Descend<FrameworkElement>(mc).Select(e => AutomationProperties.GetName(e)).Where(n => !string.IsNullOrEmpty(n) && (n.StartsWith("Peak") || n.Contains(": peak ") || n.Contains("no reading"))));
                if (!sample.Contains("Peak today: 8") || !sample.Contains("Peak in 7 days: 8") || !sample.Contains(": no reading")) return "the sample overview says: " + sample;
                var lane = LaneServer();
                File.Delete(McPeaks.FilePath); McPeaks.Reload();
                var now = DateTime.Now;
                McPeaks.Record(lane.Id, 2, now.AddDays(-3)); McPeaks.Record(lane.Id, 9, now.AddDays(-1)); McPeaks.Record(lane.Id, 4, now);
                mc.Select(lane.Id);
                mc.ShowSection("overview");
                await Wave6Flows.Settle(cc, 700);
                var names = Descend<FrameworkElement>(mc).Select(e => AutomationProperties.GetName(e)).Where(n => !string.IsNullOrEmpty(n)).ToList();
                if (!names.Contains("Peak today: 4") || !names.Contains("Peak in 7 days: 9")) return "the real server shows " + string.Join(" | ", names.Where(n => n.StartsWith("Peak")));
                if (names.Count(n => n.EndsWith("no reading") && n.Contains(":")) < 4) return "days with no reading are not shown as such";
                McPeaks.Record(lane.Id, 12, now);
                await Task.Delay(4500);   // the poll redraws when the peak changes
                names = Descend<FrameworkElement>(mc).Select(e => AutomationProperties.GetName(e)).Where(n => !string.IsNullOrEmpty(n)).ToList();
                return names.Contains("Peak today: 12") ? null : "a new peak did not reach the overview: " + string.Join(" | ", names.Where(n => n.StartsWith("Peak")));
            });

            await CheckAsync("minecraft worlds: the real buttons import a folder and a ZIP, replace after a backup, and reset", async () =>
            {
                var lane = LaneServer();
                var dir = McTemp("worlds");
                var folder = Path.Combine(dir, "folder-world"); MakeWorldFolder(folder, "from-folder");
                var zipSrc = Path.Combine(dir, "zip-src", "my world"); MakeWorldFolder(zipSrc, "from-zip");
                var zip = Path.Combine(dir, "world.zip");
                ZipFile.CreateFromDirectory(Path.Combine(dir, "zip-src"), zip);
                var notWorld = Path.Combine(dir, "empty"); Directory.CreateDirectory(notWorld);
                var world = Path.Combine(lane.Folder, "world");
                if (Directory.Exists(world)) Directory.Delete(world, true);
                foreach (var f in Directory.Exists(lane.BackupFolder) ? Directory.GetFiles(lane.BackupFolder, "*", SearchOption.AllDirectories) : Array.Empty<string>()) File.Delete(f);
                mc.Select(lane.Id);
                mc.ShowSection("worlds");
                await Wave6Flows.Settle(cc, 700);
                string Marker() => File.Exists(Path.Combine(world, "region", "r.0.0.mca")) ? File.ReadAllText(Path.Combine(world, "region", "r.0.0.mca")) : null;
                string Said() => string.Join(" | ", Descend<TextBlock>(mc).Where(t => t.IsVisible).Select(t => t.Text));
                try
                {
                    McManagerView.WorldSourceForTest = () => notWorld;
                    if (!Press(mc, "Import a world…")) return "no Import a world button";
                    await Task.Delay(200);
                    if (!Said().Contains("That folder has no level.dat, so it is not a Minecraft world.") || Directory.Exists(world)) return "a folder without level.dat was not refused";
                    McManagerView.WorldSourceForTest = () => folder;
                    Press(mc, "Import a world…");
                    await Wave6Flows.Settle(cc, 500);
                    if (Marker() != "from-folder" || !File.Exists(Path.Combine(world, "level.dat"))) return "the folder was not imported (marker " + Marker() + ")";
                    // importing a ZIP onto an existing world asks first; no answer, nothing changes
                    McManagerView.WorldSourceForTest = () => zip;
                    McManagerView.WorldConfirmForTest = () => false;
                    Press(mc, "Import a world…");
                    await Task.Delay(300);
                    if (Marker() != "from-folder") return "the world changed without a confirmation";
                    if (!Said().Contains("Not changed: it was not confirmed.")) return "declining did not say so";
                    McManagerView.WorldConfirmForTest = () => true;
                    Press(mc, "Import a world…");
                    await McManagerView.WorldActionTask;
                    await Wave6Flows.Settle(cc, 500);
                    if (Marker() != "from-zip") return "the ZIP did not replace the world (marker " + Marker() + ")";
                    var backups = McBackups.List(lane.BackupFolder);
                    if (backups.Count < 1) return "no backup was taken before the replace";
                    // reset removes the world and backs the replaced one up
                    Press(mc, "Reset the world…");
                    await McManagerView.WorldActionTask;
                    await Wave6Flows.Settle(cc, 500);
                    if (Directory.Exists(world)) return "the world folder is still there after a reset";
                    var after = McBackups.List(lane.BackupFolder);
                    if (after.Count <= backups.Count) return "the reset took no backup (" + after.Count + " before " + backups.Count + ")";
                    return null;
                }
                finally { McManagerView.WorldSourceForTest = null; McManagerView.WorldConfirmForTest = null; }
            });

            await CheckAsync("minecraft worlds: the generator has a Pause pre-generation button that is off until a build is running", async () =>
            {
                var lane = LaneServer();
                mc.Select(lane.Id);
                McManagerView.ShowGenerator = true;
                mc.ShowSection("worlds");
                await Wave6Flows.Settle(cc, 900);
                var pause = Descend<Button>(mc).FirstOrDefault(b => AutomationProperties.GetName(b) == "Pause pre-generation");
                McManagerView.ShowGenerator = false;
                if (pause == null) return "no Pause pre-generation button in the generator";
                return pause.IsEnabled ? "Pause is on with nothing running" : null;
            });

            // the throwaway server these checks made is not left in the list the later audits walk
            if (ServerManager.Servers.FirstOrDefault(x => x.Id == "lane-selftest") is ServerDef gone)
            {
                mc.Select("sample-friends");
                ServerManager.Servers.Remove(gone); ServerManager.Save();
                try { Directory.Delete(gone.Folder, true); } catch { }
            }
            McPeaks.Reload();
        }

        /// <summary>A real (not sample) stopped local server in the throwaway data folder, for the checks that need real folders.</summary>
        private static ServerDef LaneServer()
        {
            var s = ServerManager.Servers.FirstOrDefault(x => x.Id == "lane-selftest");
            if (s != null) return s;
            s = new ServerDef { Id = "lane-selftest", Name = "Lane test", Version = "1.21.4", Flavour = Flavour.Vanilla, Target = Target.LocalProcess, Port = 25977, MemoryMb = 1024, EulaAccepted = true };
            Directory.CreateDirectory(s.Folder);
            ServerManager.Servers.Add(s);
            ServerManager.Save();
            return s;
        }
    }
}
