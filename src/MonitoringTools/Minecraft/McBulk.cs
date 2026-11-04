using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MonitoringTools.Minecraft
{
    public enum BulkVerb { Kick, Ban, Pardon, Op, Deop, AllowAdd, AllowRemove, GameMode, Teleport, Give }

    public sealed class BulkArgs
    {
        public string Reason = "", Mode = "survival", Target = "", Item = "";
        public int Count = 1;
    }

    public sealed class BulkResult
    {
        public string Name = "", Message = "";
        public bool Ok;
    }

    /// <summary>
    /// One action on several players. While the server runs, each player gets one console line built here from checked
    /// values (never free text), and the reply or the failure is kept per player. While it is stopped, the actions that
    /// live in the server's files (ban, pardon, operator, allow list) edit them, on this computer or on the SSH host;
    /// kick, game mode, teleport and give need a running server and say so.
    /// </summary>
    public static class McBulk
    {
        public static readonly string[] Modes = { "survival", "creative", "adventure", "spectator" };
        private static readonly Regex ItemRx = new Regex(@"^[a-z0-9_.-]+(:[a-z0-9_./-]+)?$", RegexOptions.Compiled);
        private static readonly Regex PosRx = new Regex(@"^(~?-?\d+(\.\d+)?|~)\s+(~?-?\d+(\.\d+)?|~)\s+(~?-?\d+(\.\d+)?|~)$", RegexOptions.Compiled);

        /// <summary>Replaceable in the self-test to record the lines; by default a sample server is never sent anything.</summary>
        internal static Func<ServerDef, string, Task<string>> Sender = (s, line) => McDemo.IsSample(s) ? Task.FromResult("Sample server: not sent.") : ServerManager.CommandAsync(s, line);

        public static bool Supported(ServerDef s, BulkVerb v) => !(s.IsBedrock && (v == BulkVerb.Ban || v == BulkVerb.Pardon));
        public static bool NeedsConfirm(BulkVerb v) => v == BulkVerb.Ban || v == BulkVerb.Op || v == BulkVerb.Kick;
        public static bool InFiles(BulkVerb v) => v is BulkVerb.Ban or BulkVerb.Pardon or BulkVerb.Op or BulkVerb.Deop or BulkVerb.AllowAdd or BulkVerb.AllowRemove;

        public static string Verb(BulkVerb v) => v switch
        {
            BulkVerb.Kick => "Kick", BulkVerb.Ban => "Ban", BulkVerb.Pardon => "Pardon", BulkVerb.Op => "Make operator", BulkVerb.Deop => "Remove operator",
            BulkVerb.AllowAdd => "Add to the allow list", BulkVerb.AllowRemove => "Remove from the allow list", BulkVerb.GameMode => "Set game mode", BulkVerb.Teleport => "Teleport", _ => "Give",
        };

        /// <summary>Null when the arguments are fine for the action, otherwise what to fix.</summary>
        public static string Problem(ServerDef s, BulkVerb v, BulkArgs a)
        {
            if (!Supported(s, v)) return "A Bedrock server has no ban or pardon command.";
            switch (v)
            {
                case BulkVerb.GameMode: return Modes.Contains(a.Mode) ? null : "Choose a game mode.";
                case BulkVerb.Teleport:
                    var t = (a.Target ?? "").Trim();
                    if (t.Length == 0) return "Enter a player to teleport to, or three coordinates such as 10 64 -20 (a ~ means relative).";
                    return Valid(s, t) || PosRx.IsMatch(t) ? null : "A teleport target is a player's name or three coordinates such as 10 64 -20.";
                case BulkVerb.Give:
                    if (!ItemRx.IsMatch((a.Item ?? "").Trim())) return "Enter an item id such as minecraft:diamond (lowercase letters, digits and _ . - only).";
                    return a.Count >= 1 && a.Count <= 6400 ? null : "The count is 1 to 6400.";
            }
            return null;
        }

        private static bool Valid(ServerDef s, string name) => s.IsBedrock ? McBedrockPlayers.ValidName(name) : McPlayers.ValidName(name);
        private static string Q(ServerDef s, string name) => s.IsBedrock && name.Contains(' ') ? "\"" + name + "\"" : name;
        private static string OneLine(string t) => (t ?? "").Replace("\r", " ").Replace("\n", " ").Trim();

        /// <summary>The console line for each player, in order.</summary>
        public static List<(string Name, string Line)> Lines(ServerDef s, BulkVerb v, IEnumerable<string> names, BulkArgs a)
        {
            var list = new List<(string, string)>();
            foreach (var n in names)
            {
                var q = Q(s, n);
                string line = v switch
                {
                    BulkVerb.Kick => "kick " + q + (OneLine(a.Reason).Length > 0 ? " " + OneLine(a.Reason) : ""),
                    BulkVerb.Ban => "ban " + q + (OneLine(a.Reason).Length > 0 ? " " + OneLine(a.Reason) : ""),
                    BulkVerb.Pardon => "pardon " + q,
                    BulkVerb.Op => "op " + q,
                    BulkVerb.Deop => "deop " + q,
                    BulkVerb.AllowAdd => (s.IsBedrock ? "allowlist add " : "whitelist add ") + q,
                    BulkVerb.AllowRemove => (s.IsBedrock ? "allowlist remove " : "whitelist remove ") + q,
                    BulkVerb.GameMode => "gamemode " + a.Mode + " " + q,
                    BulkVerb.Teleport => "tp " + q + " " + (PosRx.IsMatch(a.Target.Trim()) ? Regex.Replace(a.Target.Trim(), @"\s+", " ") : Q(s, a.Target.Trim())),
                    _ => "give " + q + " " + a.Item.Trim() + " " + a.Count,
                };
                list.Add((n, line));
            }
            return list;
        }

        /// <summary>Whether a console reply says the command did not work.</summary>
        internal static bool Failed(string reply)
        {
            if (string.IsNullOrWhiteSpace(reply)) return false;
            return Regex.IsMatch(reply, @"^(The server is not running|This server was started outside|RCON:|Unknown target|Could not)|No player was found|Unknown or incomplete|Incorrect argument|That player does not exist|ERROR", RegexOptions.IgnoreCase);
        }

        public static async Task<List<BulkResult>> RunAsync(ServerDef s, bool running, BulkVerb v, IEnumerable<string> names, BulkArgs a)
        {
            var results = new List<BulkResult>();
            names = names.ToList();
            var problem = Problem(s, v, a);
            if (problem != null) return names.Select(n => new BulkResult { Name = n, Message = problem }).ToList();
            if (running)
            {
                foreach (var (n, line) in Lines(s, v, names, a))
                {
                    try { var r = (await Sender(s, line)) ?? ""; results.Add(new BulkResult { Name = n, Ok = !Failed(r), Message = r.Length == 0 ? "sent" : r.Trim() }); }
                    catch (Exception ex) { results.Add(new BulkResult { Name = n, Message = ex.Message }); }
                }
                return results;
            }
            if (!InFiles(v))
                return names.Select(n => new BulkResult { Name = n, Message = Verb(v) + " needs the server to be running; it is stopped." }).ToList();
            var files = McFiles.For(s);
            await Task.Run(() =>
            {
                foreach (var n in names)
                {
                    try
                    {
                        if (s.IsBedrock)
                            McBedrockPlayers.ApplyToFiles(files, v switch { BulkVerb.Op => BedrockChange.Operator, BulkVerb.Deop => BedrockChange.Member, BulkVerb.AllowAdd => BedrockChange.AllowAdd, _ => BedrockChange.AllowRemove }, n);
                        else
                            McPlayers.ApplyToFiles(files, v switch { BulkVerb.Ban => McPlayers.Change.Ban, BulkVerb.Pardon => McPlayers.Change.Pardon, BulkVerb.Op => McPlayers.Change.Op, BulkVerb.Deop => McPlayers.Change.Deop, BulkVerb.AllowAdd => McPlayers.Change.WhitelistAdd, _ => McPlayers.Change.WhitelistRemove }, n, a.Reason);
                        results.Add(new BulkResult { Name = n, Ok = true, Message = "saved to the files" });
                    }
                    catch (Exception ex) { results.Add(new BulkResult { Name = n, Message = ex.Message }); }
                }
            });
            return results;
        }

        /// <summary>One line: how many worked, and each failure with its reason.</summary>
        public static string Summary(BulkVerb v, IList<BulkResult> r)
        {
            int ok = r.Count(x => x.Ok);
            var fails = r.Where(x => !x.Ok).ToList();
            var head = $"{Verb(v)}: {ok} of {r.Count} done.";
            return fails.Count == 0 ? head : head + " Failed: " + string.Join("; ", fails.Select(f => f.Name + " (" + f.Message + ")"));
        }
    }
}
