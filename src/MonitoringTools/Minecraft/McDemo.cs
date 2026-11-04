using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace MonitoringTools.Minecraft
{
    /// <summary>
    /// Sample servers for the capture run, the language audit and the self-test. They are written only into the throwaway
    /// data folder those runs use (Settings.DataDirOverride); no process or container is ever started for them. Their live
    /// values (players online, memory, uptime) are sample values and the captures say so.
    /// </summary>
    public static class McDemo
    {
        public const string Prefix = "sample";
        public static bool IsSample(ServerDef s) => s.Id.StartsWith(Prefix, StringComparison.Ordinal);

        public static void Seed()
        {
            if (Core.Settings.DataDirOverride == null) throw new InvalidOperationException("Sample servers are only written into a throwaway data folder.");
            if (ServerManager.Servers.Any(IsSample)) return;
            var a = new ServerDef { Id = Prefix + "-friends", Name = "Friends survival", Version = "1.21.4", Flavour = Flavour.Paper, Build = "121", Target = Target.LocalProcess, Port = 25565, MemoryMb = 4096, EulaAccepted = true, KeepBackups = 14, Created = DateTime.Now.AddDays(-12) };
            var b = new ServerDef { Id = Prefix + "-creative", Name = "Creative build", Version = "1.21.4", Flavour = Flavour.Vanilla, Target = Target.LocalDocker, Port = 25566, MemoryMb = 2048, EulaAccepted = true, Created = DateTime.Now.AddDays(-30) };
            var c = new ServerDef { Id = Prefix + "-modded", Name = "Modded test", Version = "1.20.6", Flavour = Flavour.Fabric, Build = "0.16.9", Target = Target.SshProcess, SshHost = "builder@198.51.100.20", Port = 25565, MemoryMb = 6144, EulaAccepted = true };
            var d = new ServerDef { Id = Prefix + "-minigames", Name = "Minigames", Version = "1.21.1", Flavour = Flavour.Paper, Build = "130", Target = Target.SshDocker, SshHost = "media@203.0.113.40", Port = 25565, MemoryMb = 4096, EulaAccepted = true };
            foreach (var s in new[] { a, b }) Directory.CreateDirectory(s.Folder);

            // ---- Friends survival: a full folder, as a Paper server leaves it
            File.WriteAllText(Path.Combine(a.Folder, "server.properties"),
                "#Minecraft server properties\n#Sat Nov 01 14:02:05 EDT 2026\nallow-flight=false\ndifficulty=easy\nenable-command-block=false\ngamemode=survival\nhardcore=false\n" +
                "level-name=world\nlevel-seed=\nmax-players=20\nmotd=Friends survival\nonline-mode=true\npvp=true\nserver-port=25565\nsimulation-distance=10\nspawn-protection=16\n" +
                "view-distance=10\nwhite-list=true\npaper-custom-key=kept as written\n");
            File.WriteAllText(Path.Combine(a.Folder, "ops.json"), "[\n  {\n    \"uuid\": \"6f1c2d3e-0000-4000-8000-00000000a001\",\n    \"name\": \"Ava\",\n    \"level\": 4,\n    \"bypassesPlayerLimit\": false\n  }\n]");
            File.WriteAllText(Path.Combine(a.Folder, "whitelist.json"), "[\n" + string.Join(",\n", new[] { "Ava", "Ben", "Chloe", "Dev" }.Select((n, i) => $"  {{ \"uuid\": \"6f1c2d3e-0000-4000-8000-00000000a00{i + 1}\", \"name\": \"{n}\" }}")) + "\n]");
            File.WriteAllText(Path.Combine(a.Folder, "banned-players.json"), "[\n  { \"uuid\": \"6f1c2d3e-0000-4000-8000-00000000a009\", \"name\": \"Finn\", \"created\": \"2026-10-30 21:14:00 -0400\", \"source\": \"Ava\", \"expires\": \"forever\", \"reason\": \"griefing\" }\n]");
            World(Path.Combine(a.Folder, "world"), 4127803392L, 900_000);
            World(Path.Combine(a.Folder, "world_nether"), 4127803392L, 300_000);
            World(Path.Combine(a.Folder, "world_the_end"), 4127803392L, 90_000);
            var plugins = Path.Combine(a.Folder, "plugins");
            Directory.CreateDirectory(plugins);
            Plugin(Path.Combine(plugins, "EssentialsX-2.21.0.jar"), "EssentialsX", "2.21.0");
            Plugin(Path.Combine(plugins, "LuckPerms-Bukkit-5.4.141.jar"), "LuckPerms", "5.4.141");
            Plugin(Path.Combine(plugins, "Chunky-1.4.28.jar.disabled"), "Chunky", "1.4.28");
            McBackups.Create(a.Folder, "world", a.BackupFolder, "By hand", "before the nether rebuild", 14);
            McBackups.Create(a.Folder, "world", a.BackupFolder, "Scheduled", "", 14);
            a.Schedules.Add(new McSchedule { Kind = McScheduleKind.Restart, Repeat = McRepeat.Daily, Time = "04:00", WarnMinutes = 5 });
            a.Schedules.Add(new McSchedule { Kind = McScheduleKind.Backup, Repeat = McRepeat.Every, EveryMinutes = 240 });
            a.Schedules.Add(new McSchedule { Kind = McScheduleKind.Message, Repeat = McRepeat.Every, EveryMinutes = 30, Message = "Vote for the next event in #general" });
            a.Schedules.Add(new McSchedule { Kind = McScheduleKind.UpdateCheck, Repeat = McRepeat.Weekly, Days = { DayOfWeek.Monday }, Time = "09:00", Enabled = false });
            a.RecentCommands.AddRange(new[] { "/weather clear", "/save-all", "/list" });

            // ---- Creative build: a stopped Docker server with a fresh world
            File.WriteAllText(Path.Combine(b.Folder, "server.properties"), "level-name=world\ngamemode=creative\nmax-players=10\nserver-port=25565\nmotd=Creative build\n");
            World(Path.Combine(b.Folder, "world"), -812233L, 200_000);

            ServerManager.Servers.AddRange(new[] { a, b, c, d });
            ServerManager.Save();
            CommandsSample();

            McState.Set(a, McRunState.Running, "sample server");
            McState.Set(b, McRunState.Stopped, "sample server");
            McState.Set(c, McRunState.Running, "sample server");
            McState.Set(d, McRunState.HostOffline, "The SSH host could not be reached (sample server).");
            var live = McLive.For(a);
            live.SetSample(3, 20, new[] { "Ava", "Ben", "Chloe" }, 19.9, 2_254_857_830L, DateTime.Now.AddMinutes(-134));
            foreach (var l in new[]
            {
                "[14:02:05 INFO]: Starting minecraft server version 1.21.4", "[14:02:05 INFO]: Loading properties", "[14:02:07 INFO]: Preparing level \"world\"",
                "[14:02:11 INFO]: Done (6.214s)! For help, type \"help\"", "[14:05:40 INFO]: Ava joined the game", "[14:06:02 INFO]: <Ava> anyone up for the nether?",
                "[14:07:30 INFO]: Ava issued server command: /time set day", "[14:09:18 INFO]: Ben joined the game",
                "[14:12:51 WARN]: Can't keep up! Running 2041ms behind, skipping 40 ticks", "[14:13:07 INFO]: Saved the game",
                "[14:15:44 INFO]: <Ben> found diamonds at y -58", "[14:16:20 INFO]: Chloe joined the game",
            }) live.Add(l);
            McLive.For(c).SetSample(0, 20, Array.Empty<string>(), null, 3_400_000_000L, DateTime.Now.AddMinutes(-52));

            // What the catalogues would say about the three sample plugins (sample values; nothing is looked up).
            McCatalogue.Last[a.Id] = new Dictionary<string, McCompatInfo>(StringComparer.OrdinalIgnoreCase)
            {
                ["EssentialsX-2.21.0.jar"] = new McCompatInfo { Kind = McCompat.Compatible, Source = "Modrinth", Current = "2.21.0", Newest = "2.21.0" },
                ["LuckPerms-Bukkit-5.4.141.jar"] = new McCompatInfo { Kind = McCompat.Update, Source = "Modrinth", Current = "5.4.141", Newest = "5.4.156" },
                ["Chunky-1.4.28.jar.disabled"] = new McCompatInfo { Kind = McCompat.MayNotWork, Source = "Hangar", Current = "1.4.28" },
            };
        }

        /// <summary>
        /// A sample of what discovery finds: a Paper server and a Bedrock server running as processes, and a Docker
        /// container, none made by this app. Their folders are made in the throwaway data folder; nothing runs.
        /// </summary>
        public static (List<McProcInfo> Procs, List<McContainerInfo> Containers) FoundSample()
        {
            if (Core.Settings.DataDirOverride == null) throw new InvalidOperationException("Sample servers are only written into a throwaway data folder.");
            var paper = Path.Combine(Core.Settings.DataDir, "found-sample", "survival-2024");
            var bedrock = Path.Combine(Core.Settings.DataDir, "found-sample", "bedrock-family");
            Directory.CreateDirectory(paper); Directory.CreateDirectory(bedrock);
            File.WriteAllText(Path.Combine(paper, "server.properties"), "server-port=25600\nmax-players=12\nlevel-name=world\n");
            File.WriteAllText(Path.Combine(bedrock, "server.properties"), "server-name=Family\nserver-port=19140\n");
            File.WriteAllText(Path.Combine(bedrock, McBedrock.VersionFile), "1.26.61.1");
            var procs = new List<McProcInfo>
            {
                new McProcInfo { Pid = 4120, Name = "java", CommandLine = "\"C:\\Program Files\\Java\\bin\\java.exe\" -Xmx4G -jar paper-1.21.4-232.jar --nogui", WorkingDir = paper },
                new McProcInfo { Pid = 5560, Name = "bedrock_server", CommandLine = "bedrock_server.exe", WorkingDir = bedrock },
                new McProcInfo { Pid = 7004, Name = "java", CommandLine = "java -jar tools/report-builder.jar --input data", WorkingDir = Path.Combine(Core.Settings.DataDir, "found-sample") },
            };
            var containers = new List<McContainerInfo>
            {
                new McContainerInfo { Id = "c0ffee", Name = "mc-creative-old", Image = "itzg/minecraft-server:java21", State = "running", HostPort = 25610, Env = { ["TYPE"] = "PURPUR", ["VERSION"] = "1.21.1" } },
                new McContainerInfo { Id = "beef01", Name = "web-cache", Image = "nginx:alpine", State = "running" },
            };
            return (procs, containers);
        }

        /// <summary>A small but real world folder: a level.dat holding the seed, and a region file of the given size.</summary>
        private static void World(string dir, long seed, int regionBytes)
        {
            Directory.CreateDirectory(Path.Combine(dir, "region"));
            var nbt = new List<byte> { 0x0A, 0, 0, 0x0A, 0, 4 };
            nbt.AddRange(Encoding.ASCII.GetBytes("Data"));
            nbt.AddRange(new byte[] { 0x04, 0, 10 });
            nbt.AddRange(Encoding.ASCII.GetBytes("RandomSeed"));
            for (int i = 7; i >= 0; i--) nbt.Add((byte)(seed >> (i * 8)));
            nbt.AddRange(new byte[] { 0, 0 });
            using (var fs = File.Create(Path.Combine(dir, "level.dat")))
            using (var gz = new GZipStream(fs, CompressionLevel.Optimal)) gz.Write(nbt.ToArray());
            var rnd = new Random(regionBytes);
            var bytes = new byte[regionBytes];
            rnd.NextBytes(bytes);
            File.WriteAllBytes(Path.Combine(dir, "region", "r.0.0.mca"), bytes);
        }

        internal static void Plugin(string path, string name, string version)
        {
            using var fs = File.Create(path);
            using var z = new ZipArchive(fs, ZipArchiveMode.Create);
            using var w = new StreamWriter(z.CreateEntry("plugin.yml").Open());
            w.Write($"name: {name}\nversion: {version}\nmain: sample.{name}\napi-version: '1.21'\n");
        }

        /// <summary>A trimmed copy of a real 1.21.4 reports/commands.json (see McSampleReports), so the command builder can be shown and tested offline.</summary>
        public static string SampleCommands => McSampleReports.Commands;

        /// <summary>Writes the sample reports for the sample servers' versions into the throwaway data folder's cache.</summary>
        internal static void CommandsSample()
        {
            if (Core.Settings.DataDirOverride == null) throw new InvalidOperationException("Sample reports are only written into a throwaway data folder.");
            foreach (var v in new[] { "1.21.4", "1.20.6" })
            {
                var f = McCommandTree.FileFor(v);
                Directory.CreateDirectory(Path.GetDirectoryName(f));
                if (!File.Exists(f)) File.WriteAllText(f, SampleCommands);
                var r = McRegistries.FileFor(v);
                if (!File.Exists(r)) File.WriteAllText(r, McSampleReports.Registries);
                var data = Path.Combine(Path.GetDirectoryName(r), "data-registries.json");
                if (!File.Exists(data)) File.WriteAllText(data, McSampleReports.DataRegistries);
                McRegistries.Forget(v);
            }
        }

        public static void Remove()
        {
            McCatalogue.Last.Remove(Prefix + "-friends");
            foreach (var s in ServerManager.Servers.Where(IsSample).ToList())
            {
                ServerManager.Servers.Remove(s);
                try { if (Directory.Exists(s.Folder)) Directory.Delete(s.Folder, true); } catch { }
                try { if (Directory.Exists(s.BackupFolder)) Directory.Delete(s.BackupFolder, true); } catch { }
            }
            ServerManager.Save();
        }
    }
}
