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

        /// <summary>A trimmed copy of a real 1.21.4 reports/commands.json, so the command builder can be shown and tested offline.</summary>
        public const string SampleCommands = @"{""type"":""root"",""children"":{
""difficulty"":{""type"":""literal"",""executable"":true,""children"":{""peaceful"":{""type"":""literal"",""executable"":true},""easy"":{""type"":""literal"",""executable"":true},""normal"":{""type"":""literal"",""executable"":true},""hard"":{""type"":""literal"",""executable"":true}}},
""gamerule"":{""type"":""literal"",""children"":{""keepInventory"":{""type"":""literal"",""executable"":true,""children"":{""value"":{""type"":""argument"",""parser"":""brigadier:bool"",""executable"":true}}},""randomTickSpeed"":{""type"":""literal"",""executable"":true,""children"":{""value"":{""type"":""argument"",""parser"":""brigadier:integer"",""executable"":true}}}}},
""give"":{""type"":""literal"",""children"":{""targets"":{""type"":""argument"",""parser"":""minecraft:entity"",""properties"":{""type"":""players"",""amount"":""multiple""},""children"":{""item"":{""type"":""argument"",""parser"":""minecraft:item_stack"",""executable"":true,""children"":{""count"":{""type"":""argument"",""parser"":""brigadier:integer"",""properties"":{""min"":1},""executable"":true}}}}}}},
""kick"":{""type"":""literal"",""children"":{""targets"":{""type"":""argument"",""parser"":""minecraft:entity"",""properties"":{""type"":""players"",""amount"":""multiple""},""executable"":true,""children"":{""reason"":{""type"":""argument"",""parser"":""minecraft:message"",""executable"":true}}}}},
""list"":{""type"":""literal"",""executable"":true,""children"":{""uuids"":{""type"":""literal"",""executable"":true}}},
""save-all"":{""type"":""literal"",""executable"":true,""children"":{""flush"":{""type"":""literal"",""executable"":true}}},
""say"":{""type"":""literal"",""children"":{""message"":{""type"":""argument"",""parser"":""minecraft:message"",""executable"":true}}},
""time"":{""type"":""literal"",""children"":{""add"":{""type"":""literal"",""children"":{""time"":{""type"":""argument"",""parser"":""minecraft:time"",""properties"":{""min"":0},""executable"":true}}},""query"":{""type"":""literal"",""children"":{""daytime"":{""type"":""literal"",""executable"":true},""gametime"":{""type"":""literal"",""executable"":true},""day"":{""type"":""literal"",""executable"":true}}},""set"":{""type"":""literal"",""children"":{""day"":{""type"":""literal"",""executable"":true},""noon"":{""type"":""literal"",""executable"":true},""night"":{""type"":""literal"",""executable"":true},""midnight"":{""type"":""literal"",""executable"":true},""time"":{""type"":""argument"",""parser"":""minecraft:time"",""properties"":{""min"":0},""executable"":true}}}}},
""weather"":{""type"":""literal"",""children"":{""clear"":{""type"":""literal"",""executable"":true,""children"":{""duration"":{""type"":""argument"",""parser"":""minecraft:time"",""properties"":{""min"":1},""executable"":true}}},""rain"":{""type"":""literal"",""executable"":true},""thunder"":{""type"":""literal"",""executable"":true}}},
""whitelist"":{""type"":""literal"",""children"":{""add"":{""type"":""literal"",""children"":{""targets"":{""type"":""argument"",""parser"":""minecraft:game_profile"",""executable"":true}}},""list"":{""type"":""literal"",""executable"":true},""off"":{""type"":""literal"",""executable"":true},""on"":{""type"":""literal"",""executable"":true},""reload"":{""type"":""literal"",""executable"":true},""remove"":{""type"":""literal"",""children"":{""targets"":{""type"":""argument"",""parser"":""minecraft:game_profile"",""executable"":true}}}}}
}}";

        private static void CommandsSample()
        {
            foreach (var v in new[] { "1.21.4", "1.20.6" })
            {
                var f = McCommandTree.FileFor(v);
                Directory.CreateDirectory(Path.GetDirectoryName(f));
                if (!File.Exists(f)) File.WriteAllText(f, SampleCommands);
            }
        }

        public static void Remove()
        {
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
