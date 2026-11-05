using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MonitoringTools.Minecraft;

namespace MonitoringTools.Core
{
    // MT_LIVE_ONLY=minecraft-lane: the command builder's items, NBT, plugin commands and a player's position against a real
    // Paper server with a headless bot player; the builder's commands over Docker and SSH; the Overview's tps and update
    // check; and bounded pre-generation with pause and resume. Every server, container and process is removed afterwards.
    public static partial class LiveCheck
    {
        private static async Task McLane(List<Result> results)
        {
            var only = (Environment.GetEnvironmentVariable("MT_LIVE_LANE") ?? "paper,docker,ssh,pregen,updates,fabric,forge,neoforge,sshdocker,gen").Split(',');
            if (only.Contains("paper")) await Check(results, "minecraft: on a real Paper server with a bot player, tps, the update check, plugin commands, a player's position, item components and NBT all work", PaperLane);
            if (only.Contains("docker")) await Check(results, "minecraft: the command builder's lines run and answer on the Docker target", DockerLane);
            if (only.Contains("ssh")) await Check(results, "minecraft: the command builder's lines run and answer on the SSH target", SshLane);
            if (only.Contains("pregen")) await Check(results, "minecraft: bounded pre-generation pauses, resumes and writes region files", PreGenLane);
            await McLane2(results, only);
        }

        private static string NodeExe()
        {
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                try { var f = Path.Combine(dir, "node.exe"); if (File.Exists(f)) return f; } catch { }
            }
            throw new Skip("Node.js is not installed, so there is no way to run a bot player.");
        }

        private static async Task<bool> WaitDone(ServerDef s, McLive live, TimeSpan limit)
        {
            var until = DateTime.Now + limit;
            while (DateTime.Now < until)
            {
                if (live.Lines().Any(l => l.Message.StartsWith("Done (", StringComparison.Ordinal))) return true;
                if (ServerManager.ProcessOf(s) == null) return false;
                await Task.Delay(1000);
            }
            return false;
        }

        private static string NbtLine(string target)
        {
            var root = new SnbtNode { Type = SnbtType.Compound };
            root.Items.Add(new SnbtNode { Name = "Invulnerable", Type = SnbtType.Byte, Value = "1" });
            root.Items.Add(new SnbtNode { Name = "CustomName", Type = SnbtType.String, Value = "Bessie" });
            var tags = new SnbtNode { Name = "Tags", Type = SnbtType.List };
            tags.Items.Add(new SnbtNode { Type = SnbtType.String, Value = "lane" });
            root.Items.Add(tags);
            return target + McSnbt.Render(root);
        }

        private static async Task<string> PaperLane()
        {
            var node = NodeExe();
            var port = 25565;
            while (!ServerManager.PortFree(port)) port++;
            var s = new ServerDef { Name = "Live lane", Version = Version, Flavour = Flavour.Paper, Target = Target.LocalProcess, Port = port, MemoryMb = 2048, EulaAccepted = true };
            var work = Path.Combine(Settings.DataDir, "lane-bot-" + Stamp());
            Directory.CreateDirectory(work);
            Process bot = null;
            var botLog = new List<string>();
            var sw = Stopwatch.StartNew();
            try
            {
                var (tree, unavailable) = await McCommandTree.LoadAsync(Version, null, true);
                if (tree == null) return "the command list could not be read: " + unavailable;
                McRegistries.Forget(Version);
                var (reg, regProblem) = await McRegistries.LoadAsync(Version);
                if (reg == null) return "the registries could not be read: " + regProblem;
                Say($"data component list: {reg.Get(McItemComponents.Registry)?.Count.ToString() ?? "absent"} components");
                if (reg.Get(McItemComponents.Registry) == null) return "the registries report has no data component list";

                await ServerManager.CreateAsync(s, _ => { }, new Dictionary<string, string> { ["online-mode"] = "false", ["max-players"] = "5" });
                Say($"Paper build {s.Build} created in {sw.Elapsed.TotalSeconds:0} s");
                sw.Restart();
                await ServerManager.StartAsync(s, _ => { });
                var live = McLive.For(s);
                if (!await WaitDone(s, live, TimeSpan.FromMinutes(6))) return "the Paper server did not report Done: " + Trim(string.Join(" | ", live.Lines().TakeLast(5).Select(l => l.Raw)));
                await McState.ProbeAsync(s);
                Say($"Paper ready in {sw.Elapsed.TotalSeconds:0} s");

                // ---- the update check, against the real Paper API
                var (newer, why) = await McSoftware.UpdateAsync(s);
                if (why != null) return "the update check could not run: " + why;
                var recorded = s.Build;
                s.Build = "1";
                var (old, why2) = await McSoftware.UpdateAsync(s);
                s.Build = recorded;
                if (old == null) return "an old build (1) was not reported as having a newer one: " + why2;
                Say($"update check: build {recorded} → {(newer == null ? "nothing newer" : newer.Id)}; build 1 → {old.Id} is newer");

                // ---- tps
                sw.Restart();
                await live.PollAsync(s);
                for (int i = 0; i < 20 && live.Tps == null; i++) await Task.Delay(500);
                if (live.Tps is not double tps || tps <= 0 || tps > 20.5) return "tps came back as " + live.Tps;
                Say($"tps: {tps:0.0} read in {sw.Elapsed.TotalMilliseconds:0} ms");

                // ---- plugin and mod commands from help
                sw.Restart();
                var read = await McLiveCommands.ReadAsync(s, tree);
                if (read.Commands.Count == 0) return "the live read found no commands: " + read.Problem;
                var sample = read.Commands.Take(6).Select(c => c.Name + (c.Source != "server" ? "@" + c.Source : "")).ToList();
                Say($"live help: {read.Commands.Count} commands the jar does not list, e.g. {string.Join(", ", sample)} ({sw.Elapsed.TotalSeconds:0.0} s)");
                var withUsage = read.Commands.Count(c => c.Usage.Length > 0);
                Say($"{withUsage} came with usage text");
                var task = McLiveCommands.ToTask(read.Commands.First(c => c.Usage.Length > 0 || c.Description.Length > 0));
                var form = McCommandTasks.Build(task, task.Synthetic, new Dictionary<string, string>(), null);
                Say($"form for {task.Label} ({task.Usage.Length}-char usage): template '{task.Template}', {form.Fields.Count} fields");

                // ---- a bot player joins
                File.WriteAllText(Path.Combine(work, "package.json"), "{\"name\":\"lane-bot\",\"version\":\"1.0.0\",\"private\":true}");
                File.WriteAllText(Path.Combine(work, "bot.js"),
                    "const mineflayer=require('mineflayer');\n" +
                    $"const bot=mineflayer.createBot({{host:'127.0.0.1',port:{port},username:'LaneBot',version:'{Version}',auth:'offline'}});\n" +
                    "bot.once('spawn',()=>{console.log('SPAWN');});\n" +
                    "setInterval(()=>{ if(bot.entity){const p=bot.entity.position;console.log('POS '+p.x+' '+p.y+' '+p.z);} },1000);\n" +
                    "bot.on('error',e=>console.log('ERR '+e.message)); bot.on('kicked',r=>console.log('KICK '+r)); bot.on('end',r=>console.log('END '+r));\n");
                sw.Restart();
                var npm = ServerManager.Run("cmd.exe", $"/c cd /d \"{work}\" && npm install mineflayer --no-audit --no-fund --loglevel=error", 300000);
                if (npm.Code != 0) throw new Skip("mineflayer could not be installed: " + ServerManager.ActionLine(npm.Out));
                Say($"mineflayer installed in {sw.Elapsed.TotalSeconds:0} s");
                var psi = new ProcessStartInfo(node, "bot.js") { WorkingDirectory = work, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                bot = Process.Start(psi);
                bot.OutputDataReceived += (o, e) => { if (e.Data != null) lock (botLog) botLog.Add(e.Data); };
                bot.ErrorDataReceived += (o, e) => { if (e.Data != null) lock (botLog) botLog.Add("STDERR " + e.Data); };
                bot.BeginOutputReadLine(); bot.BeginErrorReadLine();
                var joined = DateTime.Now.AddSeconds(60);
                while (DateTime.Now < joined && !live.Lines().Any(l => l.Message.Contains("LaneBot joined the game"))) await Task.Delay(500);
                if (!live.Lines().Any(l => l.Message.Contains("LaneBot joined the game"))) return "the bot did not join: " + Trim(string.Join(" | ", botLog.TakeLast(5)));
                await Task.Delay(4000);   // let the bot land
                Say("bot LaneBot joined");

                // ---- list names the player, as the builder's player chips read it
                await ServerManager.CommandAsync(s, "list");
                for (int i = 0; i < 20 && live.PlayerNames.Length == 0; i++) await Task.Delay(500);
                if (!live.PlayerNames.Contains("LaneBot")) return "list did not name the bot: " + string.Join(",", live.PlayerNames);

                // ---- use a player's position, through the same function the button calls
                sw.Restart();
                var (pos, problem) = await McCommandRun.PositionAsync(s, "LaneBot");
                if (pos == null) return "the position could not be read: " + problem;
                string last; lock (botLog) last = botLog.LastOrDefault(l => l.StartsWith("POS ", StringComparison.Ordinal));
                var parts = last?.Split(' ');
                if (parts == null || parts.Length != 4) return "the bot reported no position: " + Trim(string.Join(" | ", botLog.TakeLast(5)));
                var bx = double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture); var by = double.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture); var bz = double.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture);
                Say($"server says LaneBot is at {pos[0]:0.00} {pos[1]:0.00} {pos[2]:0.00}; the bot says {bx:0.00} {by:0.00} {bz:0.00} ({sw.Elapsed.TotalMilliseconds:0} ms)");
                if (Math.Abs(pos[0] - bx) > 0.8 || Math.Abs(pos[2] - bz) > 0.8 || Math.Abs(pos[1] - by) > 1.5) return "the server's position and the bot's own differ";

                // ---- item components built by the builder's classes, given to the real player and read back from the inventory
                var comps = McItemComponents.Compose(new[]
                {
                    new McComponent { Key = "custom_name", Value = McItemComponents.Text(Version, "Big sword") },
                    new McComponent { Key = "lore", Value = McItemComponents.Lore(Version, new[] { "line one" }) },
                    new McComponent { Key = "enchantments", Value = McItemComponents.Enchantments(Version, new[] { new KeyValuePair<string, int>("minecraft:sharpness", 4) }) },
                    new McComponent { Key = "damage", Value = "3" },
                    new McComponent { Key = "unbreakable", Value = "{}" },
                    new McComponent { Key = "custom_model_data", Value = McItemComponents.ModelData(Version, 7) },
                    new McComponent { Key = "max_stack_size", Value = "1" },
                });
                var give = McCommandTasks.Build(McCommandTasks.Find("give"), tree, new Dictionary<string, string> { ["targets"] = "LaneBot", ["item"] = "minecraft:diamond_sword" + comps }, reg);
                if (give.Problem != null) return "the builder refused the give with components: " + give.Problem;
                var answer = await McCommandRun.SendAsync(s, give.Line.TrimStart('/'), TimeSpan.FromSeconds(8));
                if (!answer.Contains("Gave 1", StringComparison.Ordinal)) return give.Line + " answered: " + Trim(answer);
                Say("give with 7 components → " + Trim(answer));
                var inv = string.Join(" ", await McCommandRun.SendAllAsync(s, "data get entity LaneBot Inventory", TimeSpan.FromSeconds(8)));
                foreach (var need in new[] { "Big sword", "line one", "minecraft:sharpness", "custom_model_data" })
                    if (!inv.Contains(need, StringComparison.Ordinal)) return "the item in the inventory lacks " + need + ": " + Trim(inv);
                Say("the inventory read back the name, lore, enchantment and model data");

                // ---- NBT built from the editor's tree
                var line = "summon minecraft:cow ~ ~ ~ " + NbtLine("");
                var steps = McCommandTree.ParseLine(tree, line);
                if (steps == null || McCommandTree.Validate(steps, reg) != null) return "the walker refused the summon with NBT: " + (steps == null ? "unreadable" : McCommandTree.Validate(steps, reg));
                var summoned = await McCommandRun.SendAsync(s, line, TimeSpan.FromSeconds(8));
                if (!summoned.Contains("Summoned new", StringComparison.Ordinal)) return line + " answered: " + Trim(summoned);
                Say("summon with NBT → " + Trim(summoned));
                var badNbt = McCommandTree.Check(new McCommandNode { Name = "nbt", Type = "argument", Parser = "minecraft:nbt_compound_tag" }, "{Invulnerable:1b", reg);
                if (badNbt == null) return "unclosed NBT was accepted by the builder";
                return null;
            }
            finally
            {
                try { if (bot != null && !bot.HasExited) bot.Kill(true); } catch { }
                try { await ServerManager.StopAsync(s); } catch { }
                if (ServerManager.ProcessOf(s) != null) Say("the Paper server did not stop"); else Say("Paper stopped");
                ServerManager.Servers.Remove(s);
                try { ServerManager.Save(); } catch { }
                try { if (Directory.Exists(s.Folder)) Directory.Delete(s.Folder, true); } catch { }
                try { Directory.Delete(work, true); } catch { }
            }
        }

        /// <summary>The builder's lines through the Docker target: the reply comes straight back from the container's console.</summary>
        private static async Task<string> DockerLane()
        {
            NeedDocker();
            int port = 25565; while (!ServerManager.PortFree(port) || ServerManager.Servers.Any(x => x.Port == port)) port++;
            var s = new ServerDef { Name = "Live lane Docker", Version = Version, Flavour = Flavour.Vanilla, Target = Target.LocalDocker, Port = port, MemoryMb = 1024, EulaAccepted = true };
            var sw = Stopwatch.StartNew();
            var (tree, unavailable) = await McCommandTree.LoadAsync(Version, null, true);
            if (tree == null) return "the command list could not be read: " + unavailable;
            var (reg, _) = await McRegistries.LoadAsync(Version);
            await ServerManager.CreateAsync(s, _ => { }, new Dictionary<string, string> { ["max-players"] = "7" });
            try
            {
                await ServerManager.StartAsync(s, _ => { });
                var until = DateTime.Now.AddMinutes(8);
                string log = "";
                while (DateTime.Now < until && !(log = await ServerManager.LogTailAsync(s, 400)).Contains("Done (")) await Task.Delay(2000);
                if (!log.Contains("Done (")) return "no Done line within eight minutes";
                Say($"container ready after {sw.Elapsed.TotalSeconds:0} s");
                return await BuilderLines(s, tree, reg);
            }
            finally
            {
                try { await ServerManager.StopAsync(s); } catch { }
                try { ServerManager.DeleteContainer(s); } catch { }
                ServerManager.Servers.Remove(s); ServerManager.Save();
                try { Directory.Delete(s.Folder, true); } catch { }
            }
        }

        private static async Task<string> BuilderLines(ServerDef s, McCommandNode tree, McRegistries reg)
        {
            var cases = new (string Task, Dictionary<string, string> Values, string Line, string Reply)[]
            {
                ("time", new() { ["when"] = "day" }, "/time set day", "Set the time to 1000"),
                ("give", new() { ["targets"] = "@a", ["item"] = "minecraft:diamond_sword[minecraft:custom_name='\"Big sword\"',minecraft:damage=3]", ["count"] = "2" }, "/give @a minecraft:diamond_sword[minecraft:custom_name='\"Big sword\"',minecraft:damage=3] 2", "No player was found"),
                ("gamerule", new() { ["rule"] = "keepInventory", ["value"] = "true" }, "/gamerule keepInventory true", "Gamerule keepInventory is now set to: true"),
            };
            foreach (var (task, values, want, reply) in cases)
            {
                var b = McCommandTasks.Build(McCommandTasks.Find(task), tree, values, reg);
                if (b.Problem != null) return task + " was refused by the builder: " + b.Problem;
                if (b.Line != want) return task + " was built as " + b.Line;
                var sw = Stopwatch.StartNew();
                var answer = await McCommandRun.SendAsync(s, b.Line.TrimStart('/'), TimeSpan.FromSeconds(10));
                if (!answer.Contains(reply, StringComparison.Ordinal)) return b.Line + " answered: " + Trim(answer);
                Say($"{b.Line.Split('[')[0]} → \"{Trim(answer)}\" in {sw.Elapsed.TotalMilliseconds:0} ms");
            }
            var help = await McLiveCommands.ReadAsync(s, tree);
            Say($"help read: {help.Commands.Count} commands the jar does not list" + (help.Problem != null ? " (" + help.Problem + ")" : ""));
            return null;
        }

        /// <summary>The builder's lines through an SSH process target (a throwaway SSH host in Docker): the reply is read from the console.</summary>
        private static async Task<string> SshLane()
        {
            NeedDocker();
            var dir = Path.Combine(Settings.DataDir, "ssh-lane-" + Stamp());
            Directory.CreateDirectory(dir);
            var (key, pub) = KeyPair(dir);
            int sshPort = 22420; while (!ServerManager.PortFree(sshPort)) sshPort++;
            var name = "mt-live-sshlane-" + Stamp();
            var user = ThrowawayUser();
            ServerDef s = null;
            try
            {
                var run = Docker($"run -d --name {name} -e PUID=1000 -e PGID=1000 -e USER_NAME={user} -e \"PUBLIC_KEY={pub}\" -p 127.0.0.1:{sshPort}:2222 lscr.io/linuxserver/openssh-server:latest", 600000);
                if (run.Code != 0) return "docker run failed: " + ServerManager.ActionLine(run.Out);
                var apk = Docker($"exec {name} apk add --no-cache openjdk21-jre-headless curl", 900000);
                if (apk.Code != 0) return "installing Java in the SSH host failed: " + ServerManager.ActionLine(apk.Out);
                var host = SshHostSpec(user, sshPort);
                var approve = await ApproveHost(host);
                if (approve != null) return approve;
                var (tree, unavailable) = await McCommandTree.LoadAsync(Version, null, true);
                if (tree == null) return "the command list could not be read: " + unavailable;
                var (reg, _) = await McRegistries.LoadAsync(Version);
                s = new ServerDef { Name = "Live lane SSH", Version = Version, Flavour = Flavour.Vanilla, Target = Target.SshProcess, SshHost = host, SshKey = key, Port = 25565, MemoryMb = 1024, EulaAccepted = true, RemoteDir = "live-lane" };
                await ServerManager.CreateAsync(s, _ => { }, new Dictionary<string, string> { ["max-players"] = "7", ["online-mode"] = "false" });
                await ServerManager.StartAsync(s, _ => { });
                var until = DateTime.Now.AddMinutes(8);
                string log = "";
                while (DateTime.Now < until && !(log = await ServerManager.LogTailAsync(s, 300)).Contains("Done (")) await Task.Delay(2000);
                if (!log.Contains("Done (")) return "no Done line within eight minutes";
                await McState.ProbeAsync(s);
                // over SSH the reply is the console text that follows; the builder reads it from the log tail
                var cases = new (string Task, Dictionary<string, string> Values, string Reply)[]
                {
                    ("time", new() { ["when"] = "day" }, "Set the time to 1000"),
                    ("gamerule", new() { ["rule"] = "keepInventory", ["value"] = "true" }, "Gamerule keepInventory is now set to: true"),
                };
                foreach (var (task, values, reply) in cases)
                {
                    var b = McCommandTasks.Build(McCommandTasks.Find(task), tree, values, reg);
                    if (b.Problem != null) return task + " was refused by the builder: " + b.Problem;
                    var sw = Stopwatch.StartNew();
                    var sent = await ServerManager.CommandAsync(s, b.Line.TrimStart('/'));
                    string seen = null;
                    for (int i = 0; i < 20 && seen == null; i++)
                    {
                        await Task.Delay(1000);
                        seen = (await ServerManager.LogTailAsync(s, 60)).Split('\n').LastOrDefault(l => l.Contains(reply, StringComparison.Ordinal));
                    }
                    if (seen == null) return b.Line + " (" + sent + ") was not answered in the host's console log";
                    Say($"{b.Line} → \"{McLogLine.Parse(seen).Message.Trim()}\" in {sw.Elapsed.TotalSeconds:0.0} s");
                }
                await ServerManager.StopAsync(s);
                return null;
            }
            finally
            {
                if (s != null) { ServerManager.Servers.Remove(s); ServerManager.Save(); }
                Docker($"rm -f {name}", 60000);
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        /// <summary>Bounded pre-generation on a throwaway staging server: nine batches, a pause after the first, then resume, and the region files counted.</summary>
        private static async Task<string> PreGenLane()
        {
            var radii = (Environment.GetEnvironmentVariable("MT_LIVE_RADIUS") ?? "16").Split(',').Select(int.Parse).ToList();
            for (int i = 0; i < radii.Count; i++)
            {
                Say($"radius {radii[i]}:");
                var bad = await PreGenOne(radii[i], i == 0);
                if (bad != null) return $"radius {radii[i]}: {bad}";
            }
            return null;
        }

        private static async Task<string> PreGenOne(int radius, bool pauseCheck)
        {
            var dest = Path.Combine(Settings.DataDir, "pregen-lane-" + Stamp());
            Directory.CreateDirectory(dest);
            try
            {
                var o = new WorldGenOptions { Version = Version, Seed = Environment.GetEnvironmentVariable("MT_LIVE_SEED") ?? "lane", RadiusChunks = radius, EulaAccepted = true, Destination = dest, WorldName = "lane-world" };
                var pause = new GenPause();
                var messages = new List<string>();
                var sw = Stopwatch.StartNew();
                int batchesBeforePause = 0, batchesDuringPause = 0;
                bool pausedSet = false, noPause = !pauseCheck || Environment.GetEnvironmentVariable("MT_LIVE_NOPAUSE") == "1";
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(25));
                var run = Task.Run(() => WorldGenerator.GenerateAsync(o, (t, f) =>
                {
                    lock (messages)
                    {
                        messages.Add(t);
                        if (t.StartsWith("Pre-generating", StringComparison.Ordinal))
                        {
                            if (!pausedSet) batchesBeforePause++; else if (pause.Paused) batchesDuringPause++;
                            if (!pausedSet && batchesBeforePause >= 2 && noPause) pausedSet = true; else if (!pausedSet && batchesBeforePause >= 2) { pausedSet = true; pause.Paused = true; }
                        }
                    }
                }, cts.Token, pause));
                var until = DateTime.Now.AddMinutes(12);
                while (DateTime.Now < until && !(pausedSet && (noPause || messages.Any(m => m.StartsWith("Paused after batch", StringComparison.Ordinal)))) && !run.IsCompleted) await Task.Delay(500);
                if (run.IsFaulted) return "generation failed before the pause: " + run.Exception.InnerException?.Message;
                if (!pausedSet) return "pre-generation never reached batch 2: " + Trim(string.Join(" | ", messages.TakeLast(3)));
                int countAtPause; lock (messages) countAtPause = messages.Count;
                Say(noPause ? $"{batchesBeforePause} batches done at {sw.Elapsed.TotalSeconds:0} s (no pause this run)" : $"paused after {batchesBeforePause} batches at {sw.Elapsed.TotalSeconds:0} s");
                if (!noPause) await Task.Delay(8000);
                lock (messages) if (!noPause && (messages.Count != countAtPause || batchesDuringPause != 0)) return $"progress moved while paused ({messages.Count - countAtPause} new messages)";
                if (!noPause && run.IsCompleted) return "the run finished while paused";
                if (!noPause) Say("8 s paused with no progress");
                sw.Restart();
                pause.Paused = false;
                var written = await run;
                Say($"{(noPause ? "finished" : "resumed and finished")} in {sw.Elapsed.TotalSeconds:0} s, {WorldGenerator.BatchCount(o.RadiusChunks)} batches");
                if (!noPause && pause.Waits < 1) return "the run never waited for the pause";
                lock (messages) Say($"the staging server was restarted for a stalled batch {messages.Count(m => m.Contains("was restarted"))} times");
                var path = written.Split('\n')[0].Trim();
                if (!Directory.Exists(path)) return "the output folder is missing: " + path;
                var regions = Directory.GetFiles(path, "r.*.mca", SearchOption.AllDirectories).Where(f => new FileInfo(f).Length > 0).ToList();
                var names = string.Join(" ", regions.Select(Path.GetFileName).OrderBy(x => x));
                Say($"{regions.Count} non-empty region files: {names}, {regions.Sum(f => new FileInfo(f).Length) / 1024} KB");
                var want = new[] { "r.-1.-1.mca", "r.-1.0.mca", "r.0.-1.mca", "r.0.0.mca" };
                foreach (var w in want) if (!regions.Any(r => Path.GetFileName(r) == w)) return "the region file " + w + " is missing; found " + names;
                var lastBatch = WorldGenerator.BatchCount(o.RadiusChunks); if (!messages.Any(m => m.Contains($"batch {lastBatch} of {lastBatch}"))) return $"batch {lastBatch} of {lastBatch} was never reported";
                return null;
            }
            finally { try { Directory.Delete(dest, true); } catch { } }
        }
    }
}
