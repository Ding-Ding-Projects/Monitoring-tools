using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MonitoringTools.Minecraft
{
    public enum BedrockChange { AllowAdd, AllowRemove, Operator, Member, Visitor }

    /// <summary>
    /// The players of a Bedrock Dedicated Server: allowlist.json (a name and, once the player has joined, an Xbox user id)
    /// and permissions.json (a permission level per Xbox user id). While the server runs, changes go through its console
    /// (allowlist add, allowlist remove, op, deop); while it is stopped, the two files are edited directly, on this computer
    /// or on the SSH host. permissions.json knows players only by Xbox user id, so a player can be made an operator from the
    /// files only when allowlist.json already holds their id, or when the id is typed in; the console path needs only the name.
    /// </summary>
    public static class McBedrockPlayers
    {
        public const string AllowFile = "allowlist.json", PermissionFile = "permissions.json";
        private static readonly Regex NameRx = new Regex(@"^[A-Za-z0-9_.#-]([A-Za-z0-9 _.#-]{0,30}[A-Za-z0-9_.#-])?$", RegexOptions.Compiled);
        private static readonly Regex XuidRx = new Regex(@"^\d{6,20}$", RegexOptions.Compiled);

        public static bool ValidName(string name) => name != null && NameRx.IsMatch(name);
        public static bool ValidXuid(string xuid) => xuid != null && XuidRx.IsMatch(xuid);

        public static List<McPlayer> Read(IMcFiles files, IEnumerable<string> online = null)
        {
            var t = files.ReadMany(new[] { AllowFile, PermissionFile });
            return Build(t.TryGetValue(AllowFile, out var a) ? a : null, t.TryGetValue(PermissionFile, out var p) ? p : null, online);
        }

        internal static List<McPlayer> Build(string allow, string permissions, IEnumerable<string> online)
        {
            var byName = new Dictionary<string, McPlayer>(StringComparer.OrdinalIgnoreCase);
            var perm = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var e in McPlayers.Entries(permissions)) { var x = McPlayers.Str(e, "xuid"); if (!string.IsNullOrEmpty(x)) perm[x] = McPlayers.Str(e, "permission") ?? ""; }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in McPlayers.Entries(allow))
            {
                var name = McPlayers.Str(e, "name"); if (string.IsNullOrEmpty(name)) continue;
                var x = McPlayers.Str(e, "xuid") ?? "";
                var p = byName[name] = new McPlayer { Name = name, Xuid = x, Whitelisted = true };
                if (x.Length > 0) { seen.Add(x); if (perm.TryGetValue(x, out var lv)) { p.Permission = lv; p.Operator = lv == "operator"; } }
            }
            foreach (var n in online ?? Enumerable.Empty<string>())
                if (n.Length > 0) { if (!byName.TryGetValue(n, out var p)) byName[n] = p = new McPlayer { Name = n }; p.Online = true; }
            var list = byName.Values.ToList();
            // Ids permissions.json lists that allowlist.json does not name: shown by id, so nothing the server holds is hidden.
            foreach (var (x, lv) in perm)
                if (!seen.Contains(x)) list.Add(new McPlayer { Name = "XUID " + x, Xuid = x, Permission = lv, Operator = lv == "operator", Anonymous = true });
            return list.OrderByDescending(p => p.Online).ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>The console line for a change, or null when only the files can make it (making someone a visitor).</summary>
        public static string Command(BedrockChange c, string name) => c switch
        {
            BedrockChange.AllowAdd => "allowlist add " + Quote(name),
            BedrockChange.AllowRemove => "allowlist remove " + Quote(name),
            BedrockChange.Operator => "op " + Quote(name),
            BedrockChange.Member => "deop " + Quote(name),
            _ => null,
        };

        private static string Quote(string name) => name.Contains(' ') ? "\"" + name + "\"" : name;

        /// <summary>
        /// Edits allowlist.json and permissions.json of a stopped server. <paramref name="xuid"/> is used when the player has
        /// no id in allowlist.json yet; a permission change with no id at all is refused with the reason.
        /// </summary>
        public static void ApplyToFiles(IMcFiles files, BedrockChange c, string name, string xuid = null)
        {
            if (!ValidName(name)) throw new ArgumentException("A Bedrock name is 1 to 32 letters, digits, spaces and . _ # -.");
            if (!string.IsNullOrEmpty(xuid) && !ValidXuid(xuid)) throw new ArgumentException("An Xbox user id is a number of 6 to 20 digits.");
            var t = files.ReadMany(new[] { AllowFile, PermissionFile });
            var allow = McPlayers.Load(t.TryGetValue(AllowFile, out var at) ? at : null);
            int i = allow.Select((n, k) => (n, k)).FirstOrDefault(z => string.Equals((z.n as JsonObject)?["name"]?.GetValue<string>(), name, StringComparison.OrdinalIgnoreCase), (null, -1)).k;
            if (c == BedrockChange.AllowRemove) { if (i >= 0) allow.RemoveAt(i); files.WriteText(AllowFile, McPlayers.Store(allow)); return; }
            if (c == BedrockChange.AllowAdd)
            {
                if (i < 0)
                {
                    var o = new JsonObject { ["ignoresPlayerLimit"] = false, ["name"] = name };
                    if (!string.IsNullOrEmpty(xuid)) o["xuid"] = xuid;
                    allow.Add(o);
                }
                else if (!string.IsNullOrEmpty(xuid) && string.IsNullOrEmpty(McPlayers.Str((JsonObject)allow[i], "xuid"))) ((JsonObject)allow[i])["xuid"] = xuid;
                files.WriteText(AllowFile, McPlayers.Store(allow));
                return;
            }
            var id = i >= 0 ? McPlayers.Str((JsonObject)allow[i], "xuid") : null;
            if (string.IsNullOrEmpty(id)) id = xuid;
            if (string.IsNullOrEmpty(id))
                throw new InvalidOperationException($"permissions.json knows players only by Xbox user id, and {name} has none recorded yet (the server learns it when they first join). Type their Xbox user id, or start the server and use its console.");
            var perms = McPlayers.Load(t.TryGetValue(PermissionFile, out var pt) ? pt : null);
            int j = perms.Select((n, k) => (n, k)).FirstOrDefault(z => (z.n as JsonObject)?["xuid"]?.GetValue<string>() == id, (null, -1)).k;
            if (c == BedrockChange.Member) { if (j >= 0) perms.RemoveAt(j); }   // no entry means the server's default permission level
            else
            {
                var level = c == BedrockChange.Operator ? "operator" : "visitor";
                if (j >= 0) ((JsonObject)perms[j])["permission"] = level;
                else perms.Add(new JsonObject { ["permission"] = level, ["xuid"] = id });
            }
            files.WriteText(PermissionFile, McPlayers.Store(perms));
        }

        /// <summary>Removes an id that permissions.json holds but allowlist.json does not name.</summary>
        public static void RemovePermission(IMcFiles files, string xuid)
        {
            var perms = McPlayers.Load(files.ReadText(PermissionFile));
            for (int k = perms.Count - 1; k >= 0; k--) if ((perms[k] as JsonObject)?["xuid"]?.GetValue<string>() == xuid) perms.RemoveAt(k);
            files.WriteText(PermissionFile, McPlayers.Store(perms));
        }

        public static async Task<string> ApplyAsync(ServerDef s, bool running, BedrockChange c, string name, string xuid = null)
        {
            if (!ValidName(name)) return "A Bedrock name is 1 to 32 letters, digits, spaces and . _ # -.";
            var cmd = Command(c, name);
            if (running)
            {
                if (cmd == null) return "A running server takes allow list, operator and member changes from its console. Stop it to make someone a visitor through the files.";
                return await ServerManager.CommandAsync(s, cmd);
            }
            await Task.Run(() => ApplyToFiles(McFiles.For(s), c, name, xuid));
            return s.IsSsh ? "Saved to the server's files on the SSH host. It reads them on its next start." : "Saved to the server's files. It reads them on its next start.";
        }
    }
}
