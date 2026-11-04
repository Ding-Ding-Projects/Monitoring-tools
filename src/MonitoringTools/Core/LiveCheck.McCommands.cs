using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MonitoringTools.Minecraft;

namespace MonitoringTools.Core
{
    // The command builder against a real server: the jar's own command tree and registries, three task lines built by the
    // builder's code, sent through the same route the Run button uses, and the server's replies read from its console.
    public static partial class LiveCheck
    {
        private static async Task McCommands(List<Result> results)
        {
            await Check(results, "minecraft: the command builder builds and runs time set, give and gamerule on a real Vanilla server", McCommandsLive);
        }

        private static async Task<string> McCommandsLive()
        {
            var port = 25565;
            while (!ServerManager.PortFree(port)) port++;
            var s = new ServerDef { Name = "Live commands", Version = Version, Flavour = Flavour.Vanilla, Target = Target.LocalProcess, Port = port, MemoryMb = 1024, EulaAccepted = true };
            var sw = Stopwatch.StartNew();
            var (tree, unavailable) = await McCommandTree.LoadAsync(Version, null, true);
            if (tree == null) return "the command list could not be read: " + unavailable;
            McRegistries.Forget(Version);
            var (reg, regProblem) = await McRegistries.LoadAsync(Version);
            if (reg == null) return "the registries could not be read: " + regProblem;
            Say($"read {tree.Children.Count} commands, {reg.Get("minecraft:item")?.Count} items and {reg.Get("minecraft:enchantment")?.Count} enchantments from the {Version} jar in {sw.Elapsed.TotalSeconds:0.0} s");
            try
            {
                sw.Restart();
                await ServerManager.CreateAsync(s, _ => { }, new Dictionary<string, string> { ["online-mode"] = "false" });
                await ServerManager.StartAsync(s, _ => { });
                var live = McLive.For(s);
                var until = DateTime.Now.AddMinutes(6);
                while (DateTime.Now < until && !live.Lines().Any(l => l.Message.StartsWith("Done (", StringComparison.Ordinal)))
                {
                    if (ServerManager.ProcessOf(s) == null) return "the server exited before it was ready: " + Trim(string.Join(" | ", live.Lines().TakeLast(5).Select(l => l.Raw)));
                    await Task.Delay(1000);
                }
                if (!live.Lines().Any(l => l.Message.StartsWith("Done (", StringComparison.Ordinal))) return "the server did not report Done within six minutes";
                await McState.ProbeAsync(s);
                Say($"server ready in {sw.Elapsed.TotalSeconds:0} s");

                var cases = new (string Task, Dictionary<string, string> Values, string Line, string Reply)[]
                {
                    ("time", new Dictionary<string, string> { ["when"] = "day" }, "/time set day", "Set the time to 1000"),
                    ("give", new Dictionary<string, string> { ["targets"] = "@a", ["item"] = "minecraft:diamond", ["count"] = "2" }, "/give @a minecraft:diamond 2", "No player was found"),
                    ("gamerule", new Dictionary<string, string> { ["rule"] = "keepInventory", ["value"] = "true" }, "/gamerule keepInventory true", "Gamerule keepInventory is now set to: true"),
                };
                foreach (var (task, values, want, reply) in cases)
                {
                    var b = McCommandTasks.Build(McCommandTasks.Find(task), tree, values, reg);
                    if (b.Problem != null) return task + " was refused by the builder: " + b.Problem;
                    if (b.Line != want) return task + " was built as " + b.Line;
                    sw.Restart();
                    var answer = await McCommandRun.SendAsync(s, b.Line.TrimStart('/'), TimeSpan.FromSeconds(8));
                    if (!answer.Contains(reply, StringComparison.Ordinal)) return b.Line + " answered: " + Trim(answer);
                    Say($"{b.Line} → \"{Trim(answer)}\" in {sw.Elapsed.TotalMilliseconds:0} ms");
                }
                return null;
            }
            finally
            {
                try { await ServerManager.StopAsync(s); } catch { }
                if (ServerManager.ProcessOf(s) != null) Say("the server did not stop");
                else Say("stopped");
                ServerManager.Servers.Remove(s);
                try { ServerManager.Save(); } catch { }
                try { if (Directory.Exists(s.Folder)) Directory.Delete(s.Folder, true); Say("server folder removed"); } catch (Exception ex) { Say("folder left: " + ex.Message); }
            }
        }
    }
}
