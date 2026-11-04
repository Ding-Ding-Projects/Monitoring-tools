using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MonitoringTools.Minecraft
{
    public sealed class McPlayer
    {
        public string Name = "", Uuid = "";
        public bool Operator, Whitelisted, Banned, Online;
        public int OpLevel;
        public string BanReason = "";
        /// <summary>Bedrock only: the Xbox user id from allowlist.json, and the level permissions.json gives that id.</summary>
        public string Xuid = "", Permission = "";
        /// <summary>Bedrock only: an id permissions.json holds that allowlist.json gives no name for.</summary>
        public bool Anonymous;
    }

    /// <summary>
    /// Players a server knows about, from its own ops.json, whitelist.json and banned-players.json, plus the names its last
    /// "list" answer gave. While the server runs, changes go through its console (op, deop, whitelist, ban, pardon, kick) so
    /// it applies them itself and saves the files; while it is stopped, the files are edited directly, on this computer or
    /// on the SSH host, and every entry the app does not change is kept as it was.
    /// </summary>
    public static class McPlayers
    {
        private static readonly Regex NameRx = new Regex("^[A-Za-z0-9_]{1,16}$", RegexOptions.Compiled);
        private static readonly string[] Files = { "ops.json", "whitelist.json", "banned-players.json", "usercache.json" };

        /// <summary>Minecraft names are 1 to 16 letters, digits and underscores.</summary>
        public static bool ValidName(string name) => name != null && NameRx.IsMatch(name);

        /// <summary>Players from the server folder on this computer.</summary>
        public static List<McPlayer> Read(string folder, IEnumerable<string> online = null) => Read(new McLocalFiles(folder), online);

        /// <summary>Players from the server's files wherever they are (this computer or the SSH host), in one round trip.</summary>
        public static List<McPlayer> Read(IMcFiles files, IEnumerable<string> online = null) => Build(files.ReadMany(Files), online);

        internal static List<McPlayer> Build(IDictionary<string, string> texts, IEnumerable<string> online)
        {
            var map = new Dictionary<string, McPlayer>(StringComparer.OrdinalIgnoreCase);
            McPlayer Get(string name, string uuid)
            {
                if (!map.TryGetValue(name, out var p)) map[name] = p = new McPlayer { Name = name, Uuid = uuid ?? "" };
                if (string.IsNullOrEmpty(p.Uuid) && uuid != null) p.Uuid = uuid;
                return p;
            }
            foreach (var e in Entries(Text(texts, "ops.json"))) { var p = Get(Str(e, "name"), Str(e, "uuid")); p.Operator = true; p.OpLevel = e["level"]?.GetValue<int>() ?? 4; }
            foreach (var e in Entries(Text(texts, "whitelist.json"))) Get(Str(e, "name"), Str(e, "uuid")).Whitelisted = true;
            foreach (var e in Entries(Text(texts, "banned-players.json"))) { var p = Get(Str(e, "name"), Str(e, "uuid")); p.Banned = true; p.BanReason = Str(e, "reason") ?? ""; }
            foreach (var n in online ?? System.Linq.Enumerable.Empty<string>()) if (n.Length > 0) Get(n, null).Online = true;
            return map.Values.Where(p => p.Name.Length > 0).OrderByDescending(p => p.Online).ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static string Text(IDictionary<string, string> texts, string file) => texts.TryGetValue(file, out var t) ? t : null;
        internal static string Str(JsonObject o, string key) => o[key]?.GetValue<string>();

        internal static List<JsonObject> Entries(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new List<JsonObject>();
            return (JsonNode.Parse(text) as JsonArray ?? new JsonArray()).OfType<JsonObject>().ToList();
        }

        internal static JsonArray Load(string text) => string.IsNullOrWhiteSpace(text) ? new JsonArray() : JsonNode.Parse(text) as JsonArray ?? new JsonArray();

        internal static string Store(JsonArray a) => a.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

        private static int Find(JsonArray a, string name) =>
            a.Select((n, i) => (n, i)).FirstOrDefault(x => string.Equals((x.n as JsonObject)?["name"]?.GetValue<string>(), name, StringComparison.OrdinalIgnoreCase), (null, -1)).i;

        /// <summary>The UUID the server already knows for a name, so a file entry keeps the identity the server recorded.</summary>
        private static string KnownUuid(IDictionary<string, string> texts, string name)
        {
            foreach (var f in Files)
                foreach (var e in Entries(Text(texts, f)))
                    if (string.Equals(Str(e, "name"), name, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(Str(e, "uuid"))) return Str(e, "uuid");
            return null;
        }

        public enum Change { Op, Deop, WhitelistAdd, WhitelistRemove, Ban, Pardon, Kick }

        /// <summary>The console line for a change, which the running server applies and saves itself.</summary>
        public static string Command(Change c, string name, string reason = null) => c switch
        {
            Change.Op => "op " + name,
            Change.Deop => "deop " + name,
            Change.WhitelistAdd => "whitelist add " + name,
            Change.WhitelistRemove => "whitelist remove " + name,
            Change.Ban => "ban " + name + (string.IsNullOrWhiteSpace(reason) ? "" : " " + reason.Replace("\n", " ").Trim()),
            Change.Pardon => "pardon " + name,
            _ => "kick " + name + (string.IsNullOrWhiteSpace(reason) ? "" : " " + reason.Replace("\n", " ").Trim()),
        };

        /// <summary>Edits the JSON files of a stopped server on this computer. A player the server has never seen gets no UUID; the server fills it in on its next start.</summary>
        public static void ApplyToFiles(string folder, Change c, string name, string reason = null) => ApplyToFiles(new McLocalFiles(folder), c, name, reason);

        /// <summary>Edits the JSON files of a stopped server wherever they are. Only the one file the change belongs to is written back.</summary>
        public static void ApplyToFiles(IMcFiles files, Change c, string name, string reason = null)
        {
            if (!ValidName(name)) throw new ArgumentException("A Minecraft name has 1 to 16 letters, digits or underscores.");
            if (c == Change.Kick) throw new InvalidOperationException("A player can only be kicked while the server runs.");
            string file = c switch { Change.Op or Change.Deop => "ops.json", Change.WhitelistAdd or Change.WhitelistRemove => "whitelist.json", _ => "banned-players.json" };
            var texts = files.ReadMany(Files);
            var uuid = KnownUuid(texts, name);
            var a = Load(Text(texts, file));
            int i = Find(a, name);
            bool add = c == Change.Op || c == Change.WhitelistAdd || c == Change.Ban;
            if (!add) { if (i >= 0) a.RemoveAt(i); files.WriteText(file, Store(a)); return; }
            if (i >= 0) { files.WriteText(file, Store(a)); return; }
            var o = new JsonObject { ["uuid"] = uuid ?? "", ["name"] = name };
            if (c == Change.Op) { o["level"] = 4; o["bypassesPlayerLimit"] = false; }
            if (c == Change.Ban)
            {
                var now = DateTimeOffset.Now;
                var off = now.Offset;
                o["created"] = now.ToString("yyyy-MM-dd HH:mm:ss ", System.Globalization.CultureInfo.InvariantCulture) + (off < TimeSpan.Zero ? "-" : "+") + off.ToString("hhmm");
                o["source"] = "Monitoring Tools";
                o["expires"] = "forever";
                o["reason"] = string.IsNullOrWhiteSpace(reason) ? "Banned by an operator." : reason.Trim();
            }
            a.Add(o);
            files.WriteText(file, Store(a));
        }

        /// <summary>Applies a change the right way for the server's state: through the console while it runs, through the files (here or on the SSH host) while it is stopped.</summary>
        public static async Task<string> ApplyAsync(ServerDef s, bool running, Change c, string name, string reason = null)
        {
            if (!ValidName(name)) return "A Minecraft name has 1 to 16 letters, digits or underscores.";
            if (running) return await ServerManager.CommandAsync(s, Command(c, name, reason));
            await Task.Run(() => ApplyToFiles(McFiles.For(s), c, name, reason));
            return s.IsSsh ? "Saved to the server's files on the SSH host. It reads them on its next start." : "Saved to the server's files. It reads them on its next start.";
        }
    }
}
