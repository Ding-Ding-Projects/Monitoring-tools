using System;
using System.Collections.Generic;
using System.Linq;

namespace MonitoringTools.Minecraft
{
    public enum McPropKind { Switch, Number, Choice, Text }

    /// <summary>When a changed setting takes effect.</summary>
    public enum McApplies { Now, Restart, NewWorlds, NotOnThisVersion }

    /// <summary>One documented server.properties key with its label, its explanation, its default and its real bounds.</summary>
    public sealed class McPropDef
    {
        public string Key, Label, Help, Group, Default, Unit = "";
        public McPropKind Kind;
        public long Min, Max = long.MaxValue, Step = 1;
        public string[] Choices;
        public McApplies Applies = McApplies.Restart;
        /// <summary>The first release that reads the key, and the last one, when the key is not in every version.</summary>
        public string Since, Until;
        /// <summary>The console command that applies a new value while the server runs ({0} is the value), when there is one.</summary>
        public string LiveCommand;

        public bool AvailableOn(string version) =>
            (Since == null || McPropertyCatalog.AtLeast(version, Since)) && (Until == null || !McPropertyCatalog.AtLeast(version, Next(Until)));

        private static string Next(string v) { var p = v.Split('.'); p[^1] = (int.Parse(p[^1]) + 1).ToString(); return string.Join(".", p); }
    }

    /// <summary>
    /// The settings the Settings section explains, grouped as the design groups them. Values, defaults and limits follow
    /// the Minecraft wiki's server.properties table for Java edition; a key the file has that is not listed here is shown
    /// under "Other keys" and kept exactly as written.
    /// </summary>
    public static class McPropertyCatalog
    {
        public const string MemoryKey = "(launcher)";
        public static readonly string[] Groups = { "Gameplay", "World", "Players and access", "Network", "Performance", "Game rules", "Management", "Other keys" };

        private static McPropDef Sw(string g, string key, string label, string def, string help, McApplies a = McApplies.Restart, string live = null, string since = null) =>
            new McPropDef { Group = g, Key = key, Label = label, Default = def, Help = help, Kind = McPropKind.Switch, Applies = a, LiveCommand = live, Since = since };
        private static McPropDef Num(string g, string key, string label, string def, string help, long min, long max, string unit = "", long step = 1, McApplies a = McApplies.Restart, string live = null, string since = null) =>
            new McPropDef { Group = g, Key = key, Label = label, Default = def, Help = help, Kind = McPropKind.Number, Min = min, Max = max, Unit = unit, Step = step, Applies = a, LiveCommand = live, Since = since };
        private static McPropDef Ch(string g, string key, string label, string def, string help, string[] choices, McApplies a = McApplies.Restart, string live = null) =>
            new McPropDef { Group = g, Key = key, Label = label, Default = def, Help = help, Kind = McPropKind.Choice, Choices = choices, Applies = a, LiveCommand = live };
        private static McPropDef Tx(string g, string key, string label, string def, string help, McApplies a = McApplies.Restart) =>
            new McPropDef { Group = g, Key = key, Label = label, Default = def, Help = help, Kind = McPropKind.Text, Applies = a };

        public static readonly List<McPropDef> All = new List<McPropDef>
        {
            // ---- gameplay
            Sw("Gameplay", "pvp", "Player versus player", "true", "Players can hurt each other."),
            Ch("Gameplay", "difficulty", "Difficulty", "easy", "How dangerous mobs are and how fast hunger drains.", new[] { "peaceful", "easy", "normal", "hard" }, McApplies.Now, "difficulty {0}"),
            Ch("Gameplay", "gamemode", "Game mode for new players", "survival", "The mode a player starts in the first time they join.", new[] { "survival", "creative", "adventure", "spectator" }, McApplies.Now, "defaultgamemode {0}"),
            Sw("Gameplay", "force-gamemode", "Force the game mode", "false", "Puts every player back in the default game mode each time they join."),
            Sw("Gameplay", "hardcore", "Hardcore", "false", "Players are switched to spectator when they die. Takes effect only for a new world.", McApplies.NewWorlds),
            Sw("Gameplay", "allow-flight", "Allow flight", "false", "Stops the server kicking players whose game flies, for example with a flight mod."),
            Num("Gameplay", "spawn-protection", "Spawn protection", "16", "Radius around spawn that only operators can build in. 0 turns it off.", 0, 29999984, "blocks"),
            Sw("Gameplay", "enable-command-block", "Command blocks", "false", "Lets command blocks run commands."),
            Sw("Gameplay", "allow-nether", "Allow the Nether", "true", "Players can travel to the Nether through portals."),
            Sw("Gameplay", "spawn-monsters", "Spawn monsters", "true", "Hostile mobs appear in the world.", since: null),

            // ---- world
            Tx("World", "level-name", "World folder", "world", "The folder the world is saved in, inside the server folder."),
            Tx("World", "level-seed", "Seed", "", "The seed for a new world. Empty picks one at random.", McApplies.NewWorlds),
            Ch("World", "level-type", "World type", "minecraft:normal", "The generator for a new world.", new[] { "minecraft:normal", "minecraft:flat", "minecraft:large_biomes", "minecraft:amplified", "minecraft:single_biome_surface" }, McApplies.NewWorlds),
            Sw("World", "generate-structures", "Generate structures", "true", "Villages, temples and other structures in new chunks.", McApplies.NewWorlds),
            Num("World", "max-world-size", "World size limit", "29999984", "Radius of the world border the server allows.", 1, 29999984, "blocks", 1000),
            Num("World", "view-distance", "View distance", "10", "How many chunks around each player are sent to their game.", 3, 32, "chunks"),
            Num("World", "simulation-distance", "Simulation distance", "10", "How far from players mobs move and crops grow.", 3, 32, "chunks", since: "1.18"),

            // ---- players and access
            Tx("Players and access", "motd", "Message of the day", "A Minecraft Server", "The line players see under the server's name in their server list."),
            Num("Players and access", "max-players", "Maximum players", "20", "How many people can be online at once.", 0, 2147483647, "players"),
            Sw("Players and access", "white-list", "Whitelist", "false", "Only players on the whitelist can join.", McApplies.Now, "whitelist {0}"),
            Sw("Players and access", "enforce-whitelist", "Enforce the whitelist", "false", "Kicks players who are online but not on the whitelist when it is reloaded."),
            Sw("Players and access", "online-mode", "Online mode", "true", "Checks every player with Mojang. Turning it off lets anyone use any name."),
            Num("Players and access", "player-idle-timeout", "Idle kick", "0", "Minutes a player can stand still before being kicked. 0 never kicks.", 0, 2147483647, "min", 1, McApplies.Now, "setidletimeout {0}"),
            Num("Players and access", "op-permission-level", "Operator level", "4", "What operators may do: 1 build at spawn, 2 cheat commands, 3 player commands, 4 every command.", 1, 4),
            Sw("Players and access", "enforce-secure-profile", "Require signed chat", "true", "Only players with a Mojang-signed profile key can join.", since: "1.19"),
            Sw("Players and access", "hide-online-players", "Hide the player list", "false", "Leaves player names out of the server list ping.", since: "1.18"),

            // ---- network
            Num("Network", "server-port", "Port", "25565", "The port players connect to.", 1, 65535),
            Tx("Network", "server-ip", "Listen address", "", "The address to listen on. Empty listens on every address of the computer."),
            Num("Network", "network-compression-threshold", "Compression threshold", "256", "Packets larger than this are compressed. -1 turns compression off.", -1, 2147483647, "bytes", 64),
            Num("Network", "rate-limit", "Packet rate limit", "0", "Packets a player may send per second before being kicked. 0 has no limit.", 0, 2147483647, "per second"),
            Sw("Network", "enable-status", "Show in the server list", "true", "Answers the server list ping, so the server shows as online."),
            Sw("Network", "enable-query", "Query protocol", "false", "Answers GameSpy4 query requests, used by some server list websites."),
            Sw("Network", "enable-rcon", "Remote console", "false", "Accepts remote console connections. Keep it off unless you use it, and set a strong password."),
            Num("Network", "rcon.port", "Remote console port", "25575", "The port for remote console connections.", 1, 65535),
            Sw("Network", "prevent-proxy-connections", "Block proxy connections", "false", "Refuses players whose connection comes through a proxy or VPN."),

            // ---- performance
            new McPropDef { Group = "Performance", Key = MemoryKey, Label = "Memory", Default = "2048", Help = "Memory given to the Java server. Stored by this app, not in server.properties.", Kind = McPropKind.Number, Min = 512, Max = 65536, Step = 256, Unit = "MB" },
            Num("Performance", "max-tick-time", "Max tick time", "60000", "A tick slower than this stops the server as frozen. -1 turns the watchdog off.", -1, 2147483647, "ms", 1000),
            Num("Performance", "entity-broadcast-range-percentage", "Entity distance", "100", "How far away entities are sent to players, as a percentage of the normal distance.", 10, 1000, "%", 10),
            Sw("Performance", "sync-chunk-writes", "Write chunks at once", "true", "Saves chunks straight to disk. Safer after a crash, slower on slow disks."),
            Num("Performance", "max-chained-neighbor-updates", "Chained block updates", "1000000", "How many block updates in a row are allowed before the rest are skipped.", -1, 2147483647, "", 1000, since: "1.19"),

            // ---- management (1.21.9 and newer)
            new McPropDef { Group = "Management", Key = "management-server-enabled", Label = "Management API", Default = "false", Help = "JSON-RPC management over WebSocket.", Kind = McPropKind.Switch, Since = "1.21.9" },
        };

        /// <summary>Known defaults of common game rules, shown beside the control; the list of rules itself always comes from the jar.</summary>
        public static readonly Dictionary<string, string> RuleDefaults = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["keepInventory"] = "false", ["doDaylightCycle"] = "true", ["doWeatherCycle"] = "true", ["doMobSpawning"] = "true", ["mobGriefing"] = "true",
            ["doFireTick"] = "true", ["randomTickSpeed"] = "3", ["playersSleepingPercentage"] = "100", ["spawnRadius"] = "10", ["doInsomnia"] = "true",
            ["naturalRegeneration"] = "true", ["showDeathMessages"] = "true", ["announceAdvancements"] = "true", ["doImmediateRespawn"] = "false",
            ["fallDamage"] = "true", ["fireDamage"] = "true", ["drowningDamage"] = "true", ["freezeDamage"] = "true", ["doTileDrops"] = "true",
            ["doEntityDrops"] = "true", ["doMobLoot"] = "true", ["commandBlockOutput"] = "true", ["sendCommandFeedback"] = "true", ["maxEntityCramming"] = "24",
        };

        public static McApplies AppliesOn(McPropDef d, string version) => d.AvailableOn(version) ? d.Applies : McApplies.NotOnThisVersion;

        /// <summary>True when a release id like "1.21.4" is at or after "1.21". Snapshots and unknown ids count as new.</summary>
        public static bool AtLeast(string version, string min)
        {
            var a = Parts(version); var b = Parts(min);
            if (a == null || b == null) return true;
            for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                int x = i < a.Length ? a[i] : 0, y = i < b.Length ? b[i] : 0;
                if (x != y) return x > y;
            }
            return true;
        }

        private static int[] Parts(string v)
        {
            if (string.IsNullOrWhiteSpace(v)) return null;
            var p = v.Split('.');
            var r = new int[p.Length];
            for (int i = 0; i < p.Length; i++) if (!int.TryParse(p[i], out r[i])) return null;
            return r;
        }

        public static McPropDef Find(string key) => All.FirstOrDefault(d => d.Key == key);

        /// <summary>The catalogue that matches the server: Bedrock reads a different server.properties from Java edition.</summary>
        public static List<McPropDef> For(ServerDef s) => s != null && s.IsBedrock ? Bedrock : All;
        public static McPropDef Find(string key, ServerDef s) => For(s).FirstOrDefault(d => d.Key == key);

        /// <summary>
        /// The Bedrock Dedicated Server's own server.properties keys, as the file that ships with the server documents them.
        /// A key the file has that is not listed here is shown under "Other keys" and kept exactly as written.
        /// </summary>
        public static readonly List<McPropDef> Bedrock = new List<McPropDef>
        {
            Ch("Gameplay", "gamemode", "Game mode for new players", "survival", "The mode a player starts in the first time they join.", new[] { "survival", "creative", "adventure" }),
            Sw("Gameplay", "force-gamemode", "Force the game mode", "false", "Puts every player back in the default game mode each time they join."),
            Ch("Gameplay", "difficulty", "Difficulty", "easy", "How dangerous mobs are and how fast hunger drains.", new[] { "peaceful", "easy", "normal", "hard" }),
            Sw("Gameplay", "allow-cheats", "Allow cheats", "false", "Lets players use commands such as give and time."),
            Tx("World", "level-name", "World name", "Bedrock level", "The world's folder inside the worlds folder."),
            Tx("World", "level-seed", "Seed", "", "The seed for a new world. Empty picks one at random.", McApplies.NewWorlds),
            Num("World", "view-distance", "View distance", "32", "How many chunks around each player are sent to their game.", 5, 96, "chunks"),
            Num("World", "tick-distance", "Simulation distance", "4", "How far from players the world keeps running.", 4, 12, "chunks"),
            Tx("Players and access", "server-name", "Server name", "Dedicated Server", "The name players see in their server list."),
            Num("Players and access", "max-players", "Maximum players", "10", "How many people can be online at once.", 1, 2147483647, "players"),
            Sw("Players and access", "online-mode", "Online mode", "true", "Every player must sign in with a Microsoft account."),
            Sw("Players and access", "allow-list", "Allow list", "false", "Only players in allowlist.json can join."),
            Num("Players and access", "player-idle-timeout", "Idle kick", "30", "Minutes a player can stand still before being kicked. 0 never kicks.", 0, 2147483647, "min"),
            Ch("Players and access", "default-player-permission-level", "Permission for new players", "member", "What a player who joins for the first time may do.", new[] { "visitor", "member", "operator" }),
            Sw("Players and access", "texturepack-required", "Require the server's packs", "false", "Players must accept the server's resource packs to join."),
            Num("Network", "server-port", "Port (IPv4)", "19132", "The UDP port players connect to.", 1, 65535),
            Num("Network", "server-portv6", "Port (IPv6)", "19133", "The UDP port for IPv6 players.", 1, 65535),
            Sw("Network", "enable-lan-visibility", "Show on the local network", "true", "Players on the same network see the server in their Friends tab."),
            Num("Network", "compression-threshold", "Compression threshold", "1", "Packets larger than this are compressed.", 0, 65535, "bytes"),
            Num("Performance", "max-threads", "Worker threads", "8", "Threads the server may use. 0 lets it pick as many as it can.", 0, 256),
            Sw("Performance", "content-log-file-enabled", "Content log file", "false", "Writes errors from packs and add-ons to a log file."),
        };
    }
}
