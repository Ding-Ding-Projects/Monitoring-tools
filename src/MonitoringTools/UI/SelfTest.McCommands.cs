using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using MonitoringTools.Minecraft;
using MonitoringTools.UI.Minecraft;

namespace MonitoringTools.UI
{
    // Checks for the command builder: task lines from the sample reports, bounds, relative positions, the every-command
    // walker, favourites, the run gate, the confirmation, scheduling and the registries read from a jar. Nothing is sent.
    public static partial class SelfTest
    {
        private static Button Named(DependencyObject root, string name) { Window.GetWindow(root)?.UpdateLayout(); return Descend<Button>(root).FirstOrDefault(b => AutomationProperties.GetName(b) == name && b.IsVisible); }

        private static (McCommandNode Tree, McRegistries Reg) SampleReports()
        {
            var tree = McCommandTree.Parse(McDemo.SampleCommands);
            var reg = McRegistries.Parse(McSampleReports.Registries, "1.21.4");
            reg.AddData(McSampleReports.DataRegistries);
            return (tree, reg);
        }

        private static string TaskLine(McCommandNode tree, McRegistries reg, string id, Dictionary<string, string> values, out string problem)
        {
            var b = McCommandTasks.Build(McCommandTasks.Find(id), tree, values, reg);
            problem = b.Problem;
            return b.Line;
        }

        private static async Task McCommandChecksAsync(ControlCentreWindow cc)
        {
            var mc = cc.MinecraftManager;

            Check("minecraft commands: tasks build give, tp, effect, summon, gamerule and fill as the exact lines from the sample reports", () =>
            {
                var (tree, reg) = SampleReports();
                var cases = new (string Id, Dictionary<string, string> V, string Want)[]
                {
                    ("give", new() { ["targets"] = "Ava", ["item"] = "minecraft:diamond", ["count"] = "5" }, "/give Ava minecraft:diamond 5"),
                    ("tp", new() { ["targets"] = "@a[distance=..10]", ["location"] = "~ ~5 ~" }, "/tp @a[distance=..10] ~ ~5 ~"),
                    ("effect", new() { ["targets"] = "@p", ["effect"] = "minecraft:speed", ["seconds"] = "30", ["amplifier"] = "1" }, "/effect give @p minecraft:speed 30 1"),
                    ("summon", new() { ["entity"] = "minecraft:cow", ["pos"] = "10 64 -20" }, "/summon minecraft:cow 10 64 -20"),
                    ("gamerule", new() { ["rule"] = "keepInventory", ["value"] = "true" }, "/gamerule keepInventory true"),
                    ("fill", new() { ["from"] = "0 64 0", ["to"] = "~10 ~ ~10", ["block"] = "minecraft:stone", ["filling"] = "hollow" }, "/fill 0 64 0 ~10 ~ ~10 minecraft:stone hollow"),
                    ("time", new() { ["when"] = "{time}", ["time"] = "6000" }, "/time set 6000"),
                    ("xp", new() { ["change"] = "add", ["target"] = "Ben", ["amount"] = "3", ["unit"] = "levels" }, "/experience add Ben 3 levels"),
                    ("title", new() { ["targets"] = "@a", ["titlekind"] = "title", ["title"] = McCommandTasks.Component("Welcome \"in\"") }, "/title @a title \"Welcome \\\"in\\\"\""),
                };
                foreach (var (id, v, want) in cases)
                {
                    var line = TaskLine(tree, reg, id, v, out var problem);
                    if (line != want) return $"{id} came out as {line}, expected {want}";
                    if (problem != null) return $"{id} was refused: {problem}";
                }
                var none = TaskLine(tree, reg, "effect", new() { ["targets"] = "@a", ["effect"] = "minecraft:speed", ["amplifier"] = "2" }, out var gap);
                return gap == "Fill in Seconds first, or clear Strength." ? null : "an amplifier without seconds gave: " + gap + " for " + none;
            });

            Check("minecraft commands: numbers out of the parser's bounds, unknown ids and wrong targets are refused with the field named", () =>
            {
                var (tree, reg) = SampleReports();
                var bad = new (string Id, Dictionary<string, string> V, string Want)[]
                {
                    ("give", new() { ["targets"] = "Ava", ["item"] = "minecraft:diamond", ["count"] = "0" }, "How many must be at least 1."),
                    ("effect", new() { ["targets"] = "Ava", ["effect"] = "minecraft:speed", ["seconds"] = "30", ["amplifier"] = "256" }, "Strength must be at most 255."),
                    ("effect", new() { ["targets"] = "Ava", ["effect"] = "minecraft:speed", ["seconds"] = "1000001" }, "Seconds must be at most 1000000."),
                    ("give", new() { ["targets"] = "Ava", ["item"] = "minecraft:not_an_item" }, "minecraft:not_an_item is not in this version's item list."),
                    ("give", new() { ["targets"] = "@e", ["item"] = "minecraft:diamond" }, "Who takes players only, so @e cannot be used here."),
                    ("give", new() { ["item"] = "minecraft:diamond" }, "Fill in Who."),
                    ("setblock", new() { ["pos"] = "1.5 64 0", ["block"] = "minecraft:stone" }, "Where: \"1.5\" is not a whole number, ~ or ~ with a number."),
                };
                foreach (var (id, v, want) in bad)
                {
                    TaskLine(tree, reg, id, v, out var problem);
                    if (problem != want) return $"{id} said \"{problem}\", expected \"{want}\"";
                }
                return null;
            });

            Check("minecraft commands: relative positions are written as ~ and ~5, and a data get reply is read as a position", () =>
            {
                if (McPosition.Axis(true, "") != "~" || McPosition.Axis(true, "0") != "~" || McPosition.Axis(true, "5") != "~5" || McPosition.Axis(true, "-3") != "~-3" || McPosition.Axis(false, "12") != "12") return "an axis was written wrongly";
                if (McPosition.Join(new[] { (true, ""), (true, "5"), (false, "-340") }) != "~ ~5 -340") return "the axes were joined wrongly";
                var p = McPosition.FromDataReply("Ava has the following entity data: [12.5d, 64.0d, -3.25d]");
                if (p == null || p[0] != 12 || p[1] != 64 || p[2] != -3.25) return "the data get reply was not read";
                var sel = new McSelector { Base = "@a", MaxDistance = 10, Limit = 2, Sort = "nearest", GameMode = "survival" }.ToString();
                if (sel != "@a[distance=..10,limit=2,sort=nearest,gamemode=survival]") return "the selector came out as " + sel;
                return McSelector.Parse(sel)?.ToString() == sel ? null : "the selector did not read back";
            });

            Check("minecraft commands: the every-command walker reads and validates commands of eight argument types", () =>
            {
                var (tree, reg) = SampleReports();
                foreach (var line in new[]
                {
                    "time set 1000", "weather rain 600", "experience add @a 5 levels", "setworldspawn 0 64 0", "kick Ava griefing again",
                    "enchant @p minecraft:sharpness 3", "gamemode creative Ava", "title @a title \"Hi there\"", "tp Ava ~ ~10 ~", "gamerule keepInventory false",
                })
                {
                    var steps = McCommandTree.ParseLine(tree, line);
                    if (steps == null) return "the walker could not read " + line;
                    var bad = McCommandTree.Validate(steps, reg);
                    if (bad != null) return line + " was refused: " + bad;
                    if (McCommandTree.Line(steps) != "/" + line) return line + " came back as " + McCommandTree.Line(steps);
                }
                var tp = McCommandTree.ParseLine(tree, "tp Ava ~ ~10 ~");
                if (tp[1].Node.Parser != "minecraft:entity" || tp[2].Node.Parser != "minecraft:vec3") return "tp did not follow its redirect to teleport";
                if (McCommandTree.Validate(McCommandTree.ParseLine(tree, "enchant @p minecraft:sharpness"), reg) != null) return "enchant without a level was refused";
                return McCommandTree.ParseLine(tree, "enchant @p minecraft:sharpness") is { } e && McCommandTree.Validate(e.Take(2).ToList(), reg) != null ? null : "an incomplete enchant was accepted";
            });

            Check("minecraft commands: enchantments and other data-driven lists are read from inside a bundler server jar", () =>
            {
                var dir = McTemp("registry-jar");
                var jar = Path.Combine(dir, "server.jar");
                using (var fs = File.Create(jar))
                using (var outer = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    var ms = new MemoryStream();
                    using (var inner = new ZipArchive(ms, ZipArchiveMode.Create, true))
                        foreach (var n in new[] { "data/minecraft/enchantment/sharpness.json", "data/minecraft/enchantment/mending.json", "data/minecraft/damage_type/fall.json", "data/minecraft/tags/enchantment/curse.json" })
                            using (var w = new StreamWriter(inner.CreateEntry(n).Open())) w.Write("{}");
                    using var es = outer.CreateEntry("META-INF/versions/1.21.4/server-1.21.4.jar").Open();
                    ms.Position = 0; ms.CopyTo(es);
                }
                var output = Path.Combine(dir, "data-registries.json");
                McRegistries.WriteDataFromJar(jar, output);
                var r = new McRegistries();
                r.AddData(File.ReadAllText(output));
                var e = r.Get("minecraft:enchantment");
                if (e == null || !e.SequenceEqual(new[] { "minecraft:mending", "minecraft:sharpness" })) return "enchantments read: " + string.Join(", ", e ?? new List<string>());
                return r.Get("minecraft:damage_type")?.Single() == "minecraft:fall" ? null : "the damage types were not read";
            });

            await CheckAsync("minecraft commands: an incomplete task names the missing field and keeps Run off; a complete one turns Run on", async () =>
            {
                mc.Select("sample-friends");
                var s = McManagerView.Current;
                McManagerView.ValuesFor(s, "give").Clear();
                mc.OpenCommandTask("give");
                await Wave6Flows.Settle(cc, 900);
                if (mc.RunEnabled) return "Run is on for an empty give";
                if (mc.CommandStatus != "Fill in Who.") return "the status says: " + mc.CommandStatus;
                if (!Press(mc, "Ava")) return "the online player Ava is not offered";
                await Task.Delay(150);
                if (mc.CommandStatus != "Fill in Item.") return "after choosing Ava the status says: " + mc.CommandStatus;
                if (Named(mc, "Diamond sword, minecraft:diamond_sword") == null) return "the item list has no diamond sword";
                var item = Descend<Button>(mc).First(b => AutomationProperties.GetName(b) == "Diamond sword, minecraft:diamond_sword");
                item.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                await Task.Delay(150);
                if (!mc.RunEnabled || !mc.CommandStatus.StartsWith("Complete and valid for 1.21.4")) return "a complete give is not runnable: " + mc.CommandStatus;
                if (mc.PreviewLine != "/give Ava minecraft:diamond_sword") return "the preview shows " + mc.PreviewLine;
                // the parts are coloured and each one takes you to its field
                var parts = Descend<Button>(mc.PreviewParts).ToList();
                if (parts.Count != 3) return "the preview has " + parts.Count + " parts";
                var who = parts.First(b => AutomationProperties.GetName(b).StartsWith("Target · Who", StringComparison.Ordinal));
                who.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                await Task.Delay(100);
                var focus = mc.FieldFocus("targets");
                if (focus == null || !focus.IsFocused) return "clicking the Who part did not focus its field";
                var r = await mc.RunCurrent(s);
                return r == "Sample server: nothing was sent." ? null : "running on the sample answered: " + r;
            });

            await CheckAsync("minecraft commands: stop, ban and op ask for the two-key confirmation, and nothing runs without it", async () =>
            {
                var s = McManagerView.Current;
                var asked = new List<string>();
                McManagerView.ConfirmForTest = line => { asked.Add(line); return false; };
                try
                {
                    mc.OpenCommandTask("stop");
                    await Wave6Flows.Settle(cc, 600);
                    if (await mc.RunCurrent(s) != "not confirmed") return "stop ran without being confirmed";
                    McManagerView.ValuesFor(s, "ban")["targets"] = "Finn";
                    mc.OpenCommandTask("ban");
                    await Wave6Flows.Settle(cc, 500);
                    if (await mc.RunCurrent(s) != "not confirmed") return "ban ran without being confirmed";
                    McManagerView.ValuesFor(s, "op")["targets"] = "Ben";
                    mc.OpenCommandTask("op");
                    await Wave6Flows.Settle(cc, 500);
                    await mc.RunCurrent(s);
                    if (!asked.SequenceEqual(new[] { "/stop", "/ban Finn", "/op Ben" })) return "confirmation was asked for: " + string.Join(", ", asked);
                    McManagerView.ConfirmForTest = line => true;
                    var r = await mc.RunCurrent(s);
                    return r == "Sample server: nothing was sent." ? null : "a confirmed op answered: " + r;
                }
                finally { McManagerView.ConfirmForTest = null; }
            });

            await CheckAsync("minecraft commands: a favourite is saved to servers.json, shown in the gallery and the console, and opens again in the builder", async () =>
            {
                var s = McManagerView.Current;
                s.FavouriteCommands.Clear();
                var v = McManagerView.ValuesFor(s, "weather");
                v.Clear(); v["weather"] = "rain"; v["duration"] = "600";
                mc.OpenCommandTask("weather");
                await Wave6Flows.Settle(cc, 700);
                if (mc.PreviewLine != "/weather rain 600") return "the weather task shows " + mc.PreviewLine;
                if (!Press(mc, "Save as a favourite")) return "no Save as a favourite button";
                await Wave6Flows.Settle(cc, 500);
                var file = Path.Combine(ServerManager.Root, "servers.json");
                var stored = JsonSerializer.Deserialize<List<ServerDef>>(File.ReadAllText(file)).First(x => x.Id == s.Id);
                if (!stored.FavouriteCommands.SequenceEqual(new[] { "/weather rain 600" })) return "servers.json holds: " + string.Join(", ", stored.FavouriteCommands);
                if (Named(mc, "Open favourite /weather rain 600") == null) return "the favourite is not at the top of the gallery";
                mc.ShowSection("console");
                await Wave6Flows.Settle(cc, 500);
                if (Named(mc, "Run favourite: /weather rain 600") == null) return "the console has no favourite chip";
                mc.ShowSection("commands");
                await Wave6Flows.Settle(cc, 600);
                if (Named(mc, "Open favourite /weather rain 600") is not Button fb) return "the favourite row is missing"; fb.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                await Wave6Flows.Settle(cc, 500);
                if (McManagerView.CommandMode != "tree" || mc.PreviewLine != "/weather rain 600" || mc.CommandStatus?.StartsWith("Complete") != true) return "the favourite opened as " + mc.PreviewLine + " · " + mc.CommandStatus;
                s.FavouriteCommands.Clear(); ServerManager.Save();
                return null;
            });

            await CheckAsync("minecraft commands: Schedule it opens a Run a command schedule with the line, and the runner sends it", async () =>
            {
                var s = McManagerView.Current;
                var v = McManagerView.ValuesFor(s, "say");
                v.Clear(); v["message"] = "Event starts at eight";
                mc.OpenCommandTask("say");
                await Wave6Flows.Settle(cc, 600);
                if (!Press(mc, "Schedule it")) return "no Schedule it button";
                await Wave6Flows.Settle(cc, 500);
                var d = McManagerView.ScheduleDraft;
                if (McManagerView.Section != "schedules" || d == null || d.Kind != McScheduleKind.Command || d.Command != "say Event starts at eight") return "the schedule form got " + d?.Kind + " " + d?.Command;
                McManagerView.ScheduleDraft = null;
                mc.ShowSection("commands");
                var job = new McSchedule { Kind = McScheduleKind.Command, Command = "say hi" };
                var off = ServerManager.Servers.First(x => x.Id == "sample-creative");
                var r = await McScheduler.ExecuteAsync(off, job);
                if (r != "Skipped: the server was not running.") return "a command schedule on a stopped server recorded: " + r;
                return job.Describe().EndsWith(": say hi") && McSchedule.KindLabel(McScheduleKind.Command) == "Run a command" ? null : "the schedule describes itself as " + job.Describe();
            });

            await CheckAsync("minecraft commands: Every command walks the tree with a control per argument and builds the line", async () =>
            {
                McManagerView.CommandMode = "tree";
                mc.ShowSection("commands");
                await Wave6Flows.Settle(cc, 700);
                foreach (var token in new[] { "time", "set", "day" })
                {
                    if (!Press(mc, token)) return "the walker offers no " + token;
                    await Task.Delay(150);
                }
                if (mc.PreviewLine != "/time set day" || mc.CommandStatus?.StartsWith("Complete") != true) return "the walker shows " + mc.PreviewLine + " · " + mc.CommandStatus;
                if (!Press(mc, "Clear")) return "no Clear";
                await Task.Delay(150);
                if (!Press(mc, "effect")) return "the walker offers no effect";
                await Task.Delay(150);
                // "give" is also a top-level command, so take the one offered as what comes next
                if (Named(mc, "Add give") is not Button addGive) return "effect offers no give";
                addGive.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                await Task.Delay(150);
                if (!Press(mc, "<targets>")) return "no <targets> after effect give";
                await Task.Delay(200);
                if (!Press(mc, "@a")) return "the who control offers no @a";
                await Task.Delay(150);
                if (!Press(mc, "<effect>")) return "no <effect> after the targets";
                await Task.Delay(200);
                var speed = Descend<Button>(mc).FirstOrDefault(b => AutomationProperties.GetName(b) == "Speed, minecraft:speed");
                if (speed == null) return "the effect control does not list Speed from the registry";
                speed.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                await Task.Delay(150);
                McManagerView.CommandMode = "tasks";
                return mc.PreviewLine == "/effect give @a minecraft:speed" && mc.CommandStatus?.StartsWith("Complete") == true ? null : "the walker built " + mc.PreviewLine + " · " + mc.CommandStatus;
            });
        }
    }
}
