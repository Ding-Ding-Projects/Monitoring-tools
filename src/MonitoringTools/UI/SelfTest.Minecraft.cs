using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Controls;
using MonitoringTools.Core;
using MonitoringTools.Minecraft;
using MonitoringTools.UI.Minecraft;

namespace MonitoringTools.UI
{
    // Checks for the Minecraft server manager. Every folder is a temporary one inside the throwaway data folder, the sample
    // servers are never started, and no network is needed except where a check says so.
    public static partial class SelfTest
    {
        private static string McTemp(string name)
        {
            var d = Path.Combine(Settings.DataDir, "mc-selftest-" + name);
            if (Directory.Exists(d)) Directory.Delete(d, true);
            Directory.CreateDirectory(d);
            return d;
        }

        private static GuidedForm McForm(System.Windows.DependencyObject root) => Descend<GuidedForm>(root).FirstOrDefault(f => AutomationProperties.GetName(f) == "New server");
        private static string McStep(GuidedForm f) => f == null ? "" : AutomationProperties.GetItemStatus(f) ?? "";

        private static async Task McChecksAsync()
        {
            McDemo.Seed();
            ControlCentreWindow cc = null;
            try
            {
                cc = new ControlCentreWindow { Width = 1400, Height = 900 };
                cc.Show();
                await Task.Delay(700);
                cc.Navigate("minecraft", animate: false);
                await Wave6Flows.Settle(cc, 600);

                await CheckAsync("minecraft: the server list filters by state and searches with the shared search field", async () =>
                {
                    var mc = cc.MinecraftManager;
                    if (mc == null) return "the server manager did not build";
                    McManagerView.ListFilter = "all";
                    mc.ListSearch.State.SetQuery("");
                    if (!Press(mc, "Running 2")) return "no Running chip with a count of 2: " + string.Join(", ", Descend<Button>(mc).Select(b => b.Content as string).Where(c => c != null && c.Contains(' ')).Take(8));
                    await Task.Delay(150);
                    var running = mc.VisibleServerNames.ToList();
                    if (!running.SequenceEqual(new[] { "Friends survival", "Modded test" })) return "Running shows: " + string.Join(", ", running);
                    if (!Press(mc, "Offline host 1")) return "no Offline host chip with a count of 1";
                    await Task.Delay(150);
                    if (!mc.VisibleServerNames.SequenceEqual(new[] { "Minigames" })) return "Offline host shows: " + string.Join(", ", mc.VisibleServerNames);
                    Press(mc, "All 4");
                    mc.ListSearch.State.SetQuery("creative");
                    await Task.Delay(200);
                    if (!mc.VisibleServerNames.SequenceEqual(new[] { "Creative build" })) return "searching creative shows: " + string.Join(", ", mc.VisibleServerNames);
                    mc.ListSearch.State.SetRegex(true);
                    mc.ListSearch.State.SetQuery("^(Mod|Mini)");
                    await Task.Delay(200);
                    var rx = mc.VisibleServerNames.ToList();
                    mc.ListSearch.State.SetRegex(false);
                    mc.ListSearch.State.SetQuery("");
                    return rx.SequenceEqual(new[] { "Modded test", "Minigames" }) ? null : "the regex ^(Mod|Mini) shows: " + string.Join(", ", rx);
                });

                await CheckAsync("minecraft: an SSH host that cannot be reached is Host offline, never Stopped", async () =>
                {
                    var probe = new ServerDef { Id = "probe-offline", Name = "probe", Target = Target.SshProcess, SshHost = "nobody@203.0.113.9" };
                    var st = await McState.ProbeAsync(probe);
                    return st.State == McRunState.HostOffline ? null : "the unreachable host was reported as " + st.State + ": " + st.Detail;
                });

                Check("minecraft: server.properties round trip keeps comments, order and unknown keys byte for byte", () =>
                {
                    var text = "#Minecraft server properties\r\n#Sat Nov 01 14:02:05 EDT 2026\r\npvp=true\r\n  difficulty = easy\r\npaper-custom-key=kept: as=written\r\n\r\nmotd=Hello\\u00e9\r\n";
                    var doc = McProperties.Parse(text);
                    if (doc.ToString() != text) return "an unchanged file did not write back identically";
                    doc.Set("pvp", "false");
                    doc.Set("max-players", "12");
                    var want = text.Replace("pvp=true", "pvp=false") + "max-players=12\r\n";
                    if (doc.ToString() != want) return "a change touched more than its own line: " + doc.ToString().Replace("\r\n", " | ");
                    if (doc.Get("paper-custom-key") != "kept: as=written") return "the unknown key's value was not kept";
                    var lf = McProperties.Parse("a=1\nb=2");
                    lf.Set("b", "3");
                    return lf.ToString() == "a=1\nb=3" ? null : "a file without a final newline gained one";
                });

                await CheckAsync("minecraft: the Settings section saves a change and keeps unknown keys and comments", async () =>
                {
                    var mc = cc.MinecraftManager;
                    mc.Select("sample-friends");
                    mc.ShowSection("settings");
                    McManagerView.SettingsGroup = "Gameplay";
                    mc.ShowSection("settings");
                    await Wave6Flows.Settle(cc, 700);
                    var pvp = Descend<CheckBox>(mc).FirstOrDefault(c => AutomationProperties.GetName(c) == "Player versus player");
                    if (pvp == null) return "no Player versus player switch in Gameplay";
                    pvp.IsChecked = false;
                    await Task.Delay(100);
                    if (!Press(mc, "Save, apply at next start")) return "the unsaved bar did not offer Save";
                    await Task.Delay(500);
                    var s = ServerManager.Servers.First(x => x.Id == "sample-friends");
                    var file = File.ReadAllText(Path.Combine(s.Folder, "server.properties"));
                    if (!file.Contains("\npvp=false\n")) return "pvp was not saved as false";
                    if (!file.Contains("paper-custom-key=kept as written")) return "the unknown key was lost";
                    if (!file.StartsWith("#Minecraft server properties\n#Sat Nov 01")) return "the comments were lost";
                    return McManagerView.Pending.TryGetValue(s.Id, out var p) && p.Count > 0 ? "the change stayed unsaved" : null;
                });

                await CheckAsync("minecraft: players are read from and written to the JSON files while the server is stopped", async () =>
                {
                    var dir = McTemp("players");
                    File.WriteAllText(Path.Combine(dir, "whitelist.json"), "[ { \"uuid\": \"6f1c2d3e-0000-4000-8000-00000000b001\", \"name\": \"Ava\" } ]");
                    File.WriteAllText(Path.Combine(dir, "usercache.json"), "[ { \"name\": \"Ben\", \"uuid\": \"6f1c2d3e-0000-4000-8000-00000000b002\", \"expiresOn\": \"2027-01-01 00:00:00 +0000\" } ]");
                    var s = new ServerDef { Id = "probe-players", Target = Target.LocalProcess };
                    McPlayers.ApplyToFiles(dir, McPlayers.Change.WhitelistAdd, "Ben");
                    McPlayers.ApplyToFiles(dir, McPlayers.Change.Op, "Ben");
                    McPlayers.ApplyToFiles(dir, McPlayers.Change.Ban, "Griefer_1", "griefing");
                    var list = McPlayers.Read(dir);
                    var ben = list.FirstOrDefault(p => p.Name == "Ben");
                    if (ben == null || !ben.Whitelisted || !ben.Operator || ben.Uuid != "6f1c2d3e-0000-4000-8000-00000000b002") return "Ben was not whitelisted and opped with the UUID the server knows";
                    if (!list.Any(p => p.Name == "Ava" && p.Whitelisted)) return "the existing whitelist entry was lost";
                    var banned = list.FirstOrDefault(p => p.Name == "Griefer_1");
                    if (banned == null || !banned.Banned || banned.BanReason != "griefing") return "the ban was not written";
                    using (var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "banned-players.json"))))
                        if (doc.RootElement[0].GetProperty("expires").GetString() != "forever") return "the ban entry is not in the server's format";
                    McPlayers.ApplyToFiles(dir, McPlayers.Change.Pardon, "Griefer_1");
                    McPlayers.ApplyToFiles(dir, McPlayers.Change.Deop, "Ben");
                    McPlayers.ApplyToFiles(dir, McPlayers.Change.WhitelistRemove, "Ava");
                    list = McPlayers.Read(dir);
                    if (list.Any(p => p.Banned) || list.Any(p => p.Operator) || list.Any(p => p.Name == "Ava")) return "pardon, deop or whitelist remove did not take";
                    try { McPlayers.ApplyToFiles(dir, McPlayers.Change.Op, "bad name!"); return "an invalid name was accepted"; } catch (ArgumentException) { }
                    try { await McPlayers.ApplyAsync(s, false, McPlayers.Change.Kick, "Ben"); return "a kick was accepted while the server was stopped"; } catch (InvalidOperationException) { }
                    // the Players section reads the sample server's files
                    var mc = cc.MinecraftManager;
                    mc.Select("sample-creative");
                    mc.Select("sample-friends");
                    mc.ShowSection("players");
                    await Wave6Flows.Settle(cc, 400);
                    var text = string.Join(" ", Descend<TextBlock>(mc).Select(t => t.Text));
                    foreach (var n in new[] { "Ava", "Ben", "Chloe", "Dev", "Finn" }) if (!text.Contains(n)) return "the Players section does not list " + n;
                    return Descend<CheckBox>(mc).Count(c => AutomationProperties.GetName(c)?.StartsWith("Operator: ") == true) >= 5 ? null : "the players have no operator switches";
                });

                Check("minecraft: a backup and a restore of a test world folder bring the old world back and keep the current one", () =>
                {
                    var server = McTemp("backup-server");
                    var backups = McTemp("backup-store");
                    Directory.CreateDirectory(Path.Combine(server, "world", "region"));
                    File.WriteAllText(Path.Combine(server, "world", "level.dat"), "first");
                    File.WriteAllText(Path.Combine(server, "world", "region", "r.0.0.mca"), "terrain A");
                    Directory.CreateDirectory(Path.Combine(server, "world_nether"));
                    File.WriteAllText(Path.Combine(server, "world_nether", "level.dat"), "nether");
                    var b = McBackups.Create(server, "world", backups, "By hand", "before the test", 3);
                    if (!b.Contents.SequenceEqual(new[] { "world", "world_nether" })) return "the backup holds " + string.Join(", ", b.Contents);
                    File.WriteAllText(Path.Combine(server, "world", "region", "r.0.0.mca"), "terrain B");
                    File.WriteAllText(Path.Combine(server, "world", "new-file.txt"), "added later");
                    var safety = McBackups.Restore(server, "world", backups, b, 3);
                    if (File.ReadAllText(Path.Combine(server, "world", "region", "r.0.0.mca")) != "terrain A") return "the restore did not bring back the old terrain";
                    if (File.Exists(Path.Combine(server, "world", "new-file.txt"))) return "the restore left a file that was not in the backup";
                    if (safety == null || safety.Reason != "Before a restore") return "no backup of the current world was kept";
                    using (var z = ZipFile.OpenRead(safety.Path))
                    using (var r = new StreamReader(z.GetEntry("world/region/r.0.0.mca").Open()))
                        if (r.ReadToEnd() != "terrain B") return "the safety backup does not hold the world as it was";
                    for (int i = 0; i < 5; i++) McBackups.Create(server, "world", backups, "Scheduled", "", 3);
                    var left = McBackups.List(backups).Count;
                    return left == 3 ? null : $"keep 3 left {left} backups";
                });

                await CheckAsync("minecraft: schedules compute the next run and a missed run happens once", async () =>
                {
                    var day1 = new DateTime(2026, 11, 2, 10, 0, 0);
                    var job = new McSchedule { Kind = McScheduleKind.Restart, Repeat = McRepeat.Daily, Time = "04:00", Created = day1 };
                    if (job.Due(day1.AddHours(1))) return "a daily 04:00 job made at 10:00 was due the same day";
                    if (job.Next(day1) != new DateTime(2026, 11, 3, 4, 0, 0)) return "next run is " + job.Next(day1);
                    var day4 = new DateTime(2026, 11, 5, 9, 0, 0);   // three 04:00 runs were missed
                    if (!job.Due(day4)) return "the missed runs were not due";
                    job.LastRun = day4;
                    if (job.Due(day4.AddMinutes(5))) return "a missed run was due a second time";
                    if (!job.Due(new DateTime(2026, 11, 6, 4, 0, 0))) return "the next day's run was not due";
                    var every = new McSchedule { Kind = McScheduleKind.Backup, Repeat = McRepeat.Every, EveryMinutes = 240, Created = day1 };
                    if (every.Next(day1.AddMinutes(10)) != day1.AddHours(4)) return "every-4-hours next is " + every.Next(day1.AddMinutes(10));
                    var weekly = new McSchedule { Kind = McScheduleKind.UpdateCheck, Repeat = McRepeat.Weekly, Days = { DayOfWeek.Monday }, Time = "09:00", Created = day1 };
                    if (weekly.Next(day1) != new DateTime(2026, 11, 9, 9, 0, 0)) return "weekly Monday next is " + weekly.Next(day1);
                    var off = new McSchedule { Enabled = false, Created = day1 };
                    if (off.Due(day4)) return "a schedule that is off was due";
                    // the background runner records a skipped message for a stopped server
                    var s = ServerManager.Servers.First(x => x.Id == "sample-creative");
                    var msg = new McSchedule { Kind = McScheduleKind.Message, Repeat = McRepeat.Every, EveryMinutes = 5, Message = "hello", Created = DateTime.Now.AddMinutes(-11) };
                    s.Schedules.Add(msg);
                    await McScheduler.TickAsync(DateTime.Now);
                    for (int i = 0; i < 20 && string.IsNullOrEmpty(msg.LastResult); i++) await Task.Delay(100);
                    s.Schedules.Remove(msg);
                    if (msg.LastRun == null) return "the runner did not run the due schedule";
                    await McScheduler.TickAsync(DateTime.Now.AddSeconds(30));
                    return msg.LastResult == "Skipped: the server was not running." ? null : "the runner recorded: " + msg.LastResult;
                });

                await CheckAsync("minecraft: the command builder makes the exact line from a sample commands.json", async () =>
                {
                    var root = McCommandTree.Parse(McDemo.SampleCommands);
                    McCommandNode Kid(McCommandNode n, string name) => n.Children.First(c => c.Name == name);
                    var time = Kid(root, "time"); var set = Kid(time, "set");
                    var steps = new List<McCommandTree.Step> { new() { Node = time }, new() { Node = set }, new() { Node = Kid(set, "time"), Value = "1000" } };
                    if (McCommandTree.Line(steps) != "/time set 1000" || McCommandTree.Validate(steps) != null) return "time set 1000 came out as " + McCommandTree.Line(steps);
                    if (McCommandTree.Validate(steps.Take(2).ToList()) == null) return "an incomplete time set was accepted";
                    var give = Kid(root, "give"); var targets = Kid(give, "targets"); var item = Kid(targets, "item"); var count = Kid(item, "count");
                    var g = new List<McCommandTree.Step> { new() { Node = give }, new() { Node = targets, Value = "Ava" }, new() { Node = item, Value = "minecraft:diamond" }, new() { Node = count, Value = "0" } };
                    if (McCommandTree.Validate(g) == null) return "a count below the jar's minimum of 1 was accepted";
                    g[3].Value = "5";
                    if (McCommandTree.Line(g) != "/give Ava minecraft:diamond 5") return "give came out as " + McCommandTree.Line(g);
                    if (McCommandTree.GameRules(root).Count != 4) return "the game rules were not read from the gamerule command";
                    // the console opens the command builder
                    var mc = cc.MinecraftManager;
                    mc.Select("sample-friends");
                    mc.ShowSection("console");
                    await Wave6Flows.Settle(cc, 700);
                    if (!Press(mc, "Open the command builder")) return "the console has no way into the command builder";
                    await Wave6Flows.Settle(cc, 500);
                    return McManagerView.Section == "commands" ? null : "the builder did not open";
                });
                await McCommandChecksAsync(cc);

                Check("minecraft: a plugin turns off and on by renaming its jar, after backing the folder up", () =>
                {
                    var dir = McTemp("plugins");
                    var jar = Path.Combine(dir, "Sample-1.0.jar");
                    McDemo.Plugin(jar, "SamplePlugin", "1.0");
                    var a = McContent.List(dir).Single();
                    if (a.Name != "SamplePlugin" || a.Version != "1.0" || !a.Enabled) return "the jar's plugin.yml was not read";
                    McContent.SetEnabled(a, false);
                    if (!File.Exists(jar + ".disabled") || File.Exists(jar)) return "turning it off did not rename it to .jar.disabled";
                    if (!Directory.Exists(dir + "-backups") || !Directory.EnumerateFiles(dir + "-backups", "Sample-1.0.jar", SearchOption.AllDirectories).Any()) return "the folder was not backed up first";
                    var b = McContent.List(dir).Single();
                    if (b.Enabled || b.Name != "SamplePlugin") return "the turned-off jar is not listed as off";
                    McContent.SetEnabled(b, true);
                    if (!File.Exists(jar)) return "turning it on did not rename it back";
                    var notJar = Path.Combine(dir, "readme.txt");
                    File.WriteAllText(notJar, "x");
                    try { McContent.Install(dir, notJar); return "a file that is not a jar was installed"; } catch (ArgumentException) { }
                    return McContent.List(dir).Count == 1 ? null : "the list counts files that are not jars";
                });

                await CheckAsync("minecraft: the create flow holds Bedrock until Mojang's link is read, and refuses software a target cannot run and numbers out of range", async () =>
                {
                    var mc = cc.MinecraftManager;
                    McManagerView.Draft = null;
                    mc.OpenCreate();
                    await Wave6Flows.Settle(cc, 400);
                    var d = McManagerView.Draft;
                    d.Bedrock = true; d.Version = "1.21.4";
                    Press(McForm(mc), "Next"); await Task.Delay(120);
                    if (!McStep(McForm(mc)).StartsWith("Step 1 of 6")) return "Bedrock was allowed through before its version was known";
                    d.Bedrock = false;
                    Press(McForm(mc), "Next"); await Task.Delay(200);
                    if (!McStep(McForm(mc)).StartsWith("Step 2 of 6")) return "Java 1.21.4 did not reach step 2: " + McStep(McForm(mc));
                    d.Flavour = Flavour.Fabric; d.Build = "0.16.9";
                    var seen = typeof(McManagerView).GetField("SoftwareSeen", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).GetValue(null) as Dictionary<string, McBuildList>;
                    seen["Fabric|1.21.4"] = new McBuildList { Builds = { new McBuild { Id = "0.16.9", Channel = "stable" } } };
                    Press(McForm(mc), "Next"); await Task.Delay(200);
                    d.Target = Target.LocalProcess;
                    Press(McForm(mc), "Next"); await Task.Delay(150);
                    if (!McStep(McForm(mc)).StartsWith("Step 3 of 6") || !string.Join(" ", Descend<TextBlock>(McForm(mc)).Select(t => t.Text)).Contains("runs in Docker")) return "Fabric as a local process was not refused";
                    d.Flavour = Flavour.Vanilla; d.Build = "";
                    Press(McForm(mc), "Next"); await Task.Delay(200);
                    if (!McStep(McForm(mc)).StartsWith("Step 4 of 6")) return "step 4 not reached: " + McStep(McForm(mc));
                    var mem = Descend<TextBox>(McForm(mc)).First(t => AutomationProperties.GetName(t) == "Memory");
                    mem.Text = "100";
                    Press(McForm(mc), "Next"); await Task.Delay(150);
                    if (!McStep(McForm(mc)).StartsWith("Step 4 of 6")) return "100 MB of memory was allowed";
                    mem.Text = "2048";
                    var name = Descend<TextBox>(McForm(mc)).First(t => AutomationProperties.GetName(t) == "Server name");
                    name.Text = " ";
                    Press(McForm(mc), "Next"); await Task.Delay(150);
                    if (!McStep(McForm(mc)).StartsWith("Step 4 of 6")) return "an empty name was allowed";
                    name.Text = "Selftest server";
                    Press(McForm(mc), "Next"); await Task.Delay(200);
                    if (!McStep(McForm(mc)).StartsWith("Step 5 of 6")) return "a valid step 4 did not continue: " + McStep(McForm(mc));
                    d.WorldMode = "existing";
                    Press(McForm(mc), "Next"); await Task.Delay(150);
                    if (!McStep(McForm(mc)).StartsWith("Step 5 of 6")) return "an existing world without a folder was allowed";
                    var notWorld = McTemp("not-a-world");
                    d.WorldSource = notWorld;
                    Press(McForm(mc), "Next"); await Task.Delay(150);
                    if (!McStep(McForm(mc)).StartsWith("Step 5 of 6")) return "a folder without level.dat was accepted as a world";
                    McManagerView.Draft = null;
                    mc.CloseCreate();
                    return ServerManager.Servers.Any(x => x.Name == "Selftest server") ? "a server was created" : null;
                });

                Check("minecraft: a servers.json from before the remake loads with every server and gains the new fields", () =>
                {
                    var old = "[{\"Id\":\"abc1234567\",\"Name\":\"Old one\",\"Version\":\"1.20.1\",\"Flavour\":1,\"Target\":1,\"Port\":25570,\"MemoryMb\":3072,\"EulaAccepted\":true,\"SshHost\":\"\",\"RemoteDir\":\"\",\"Created\":\"2026-01-02T03:04:05\"}]";
                    var list = JsonSerializer.Deserialize<List<ServerDef>>(old);
                    foreach (var s in list) ServerManager.Migrate(s);
                    var s0 = list.Single();
                    if (s0.Name != "Old one" || s0.Flavour != Flavour.Paper || s0.Target != Target.LocalDocker || s0.Port != 25570 || s0.MemoryMb != 3072) return "a stored value changed";
                    if (s0.Schedules == null || s0.RecentCommands == null || s0.KeepBackups != 14 || s0.Build != "") return "the new fields have no defaults";
                    var again = JsonSerializer.Deserialize<List<ServerDef>>(JsonSerializer.Serialize(list)).Single();
                    return again.Name == "Old one" && again.Id == "abc1234567" ? null : "the migrated list did not round trip";
                });

                await McWave8ChecksAsync(cc);
            }
            finally
            {
                McManagerView.Draft = null; McManagerView.Creating = false;
                cc?.Close();
                McDemo.Remove();
            }
        }
    }
}
