using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MonitoringTools.Minecraft
{
    /// <summary>One command the running server reports that its jar's own command list does not have: from a plugin, a mod or the server software.</summary>
    public sealed class McLiveCommand
    {
        public string Name = "", Usage = "", Description = "", Source = "server";
    }

    /// <summary>What was read from a running server, and when. A sample read says so, because nothing was asked of a real server.</summary>
    public sealed class McLiveRead
    {
        public List<McLiveCommand> Commands = new List<McLiveCommand>();
        public string Problem;
        public DateTime At = DateTime.Now;
        public bool Sample;
        public bool Reading;
    }

    /// <summary>
    /// Plugin and mod commands. The jar's command report knows only vanilla commands, so while a server runs the builder
    /// asks it: Paper and Purpur list every command with "help", vanilla and Fabric list usage lines with "help" too. Those
    /// lines are parsed into a form: the command, and one typed field per usage token. Everything read this way is labelled
    /// as coming from the live server, and nothing is guessed when the server does not answer.
    /// </summary>
    public static class McLiveCommands
    {
        private static readonly Dictionary<string, McLiveRead> Reads = new Dictionary<string, McLiveRead>(StringComparer.Ordinal);
        private static readonly Regex Colour = new Regex("\u00a7.|\u001b\\[[0-9;]*m", RegexOptions.Compiled);
        private static readonly Regex Timestamp = new Regex(@"^\[\d\d:\d\d:\d\d( [A-Z]+)?\]:?\s*", RegexOptions.Compiled);
        private static readonly Regex Described = new Regex(@"^/(?<n>[\w.\-:]+):(\s+(?<d>.*))?$", RegexOptions.Compiled);
        private static readonly Regex UsageLine = new Regex(@"^/(?<n>[\w.\-:]+)(?:\s+(?<u>\S.*))?$", RegexOptions.Compiled);
        private static readonly Regex PageOf = new Regex(@"\((\d+)/(\d+)\)", RegexOptions.Compiled);

        public static McLiveRead Get(string serverId) { lock (Reads) return Reads.TryGetValue(serverId, out var r) ? r : null; }
        public static void Set(string serverId, McLiveRead read) { lock (Reads) Reads[serverId] = read; }
        public static void Forget(string serverId) { lock (Reads) Reads.Remove(serverId); }

        /// <summary>Commands from help output. Lines are "/name: description" (Bukkit and Paper) or "/name usage" (vanilla and Fabric); anything else is ignored.</summary>
        public static List<McLiveCommand> Parse(IEnumerable<string> lines, McCommandNode tree)
        {
            var byName = new Dictionary<string, McLiveCommand>(StringComparer.OrdinalIgnoreCase);
            var inJar = new HashSet<string>((tree?.Children ?? new List<McCommandNode>()).Where(c => c.IsLiteral).Select(c => c.Name), StringComparer.OrdinalIgnoreCase);
            foreach (var raw in lines ?? Enumerable.Empty<string>())
            {
                var line = Timestamp.Replace(Colour.Replace(raw ?? "", ""), "").Trim();
                if (!line.StartsWith("/", StringComparison.Ordinal)) continue;
                string name, usage = "", description = "";
                var d = Described.Match(line);
                if (d.Success) { name = d.Groups["n"].Value; description = d.Groups["d"].Value.Trim(); }
                else
                {
                    var u = UsageLine.Match(line);
                    if (!u.Success) continue;
                    name = u.Groups["n"].Value; usage = u.Groups["u"].Value.Trim();
                }
                string source = "server", bare = name;
                int colon = name.IndexOf(':');
                if (colon > 0) { source = name.Substring(0, colon); bare = name.Substring(colon + 1); }
                if (source.Equals("minecraft", StringComparison.OrdinalIgnoreCase) || inJar.Contains(bare)) continue;   // the jar's own commands are in the builder already
                if (bare.Length == 0) continue;
                // the plain name wins over a namespaced duplicate
                if (!byName.TryGetValue(bare, out var c)) byName[bare] = c = new McLiveCommand { Name = bare, Source = source };
                else if (colon <= 0) c.Source = source == "server" ? c.Source : source;
                if (description.Length > 0 && c.Description.Length == 0) c.Description = description;
                if (usage.Length > 0 && (c.Usage.Length == 0 || usage.Length > c.Usage.Length)) c.Usage = usage;
            }
            return byName.Values.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>Asks a running server for its command list. The sample servers answer with sample text, flagged as such.</summary>
        public static async Task<McLiveRead> ReadAsync(ServerDef s, McCommandNode tree)
        {
            var read = new McLiveRead { At = DateTime.Now };
            if (McDemo.IsSample(s))
            {
                read.Sample = true;
                read.Commands = Parse(McSampleReports.LiveHelp.Split('\n'), tree);
                return read;
            }
            var lines = new List<string>();
            var wait = TimeSpan.FromSeconds(6);
            var first = await McCommandRun.SendAllAsync(s, "help", wait);
            lines.AddRange(first);
            // Paper pages its index: read the rest of the pages the first one announces.
            foreach (var l in first)
            {
                var m = PageOf.Match(l);
                if (!m.Success || !l.Contains("Index", StringComparison.OrdinalIgnoreCase)) continue;
                int pages = Math.Min(int.Parse(m.Groups[2].Value), 12);
                for (int p = 2; p <= pages; p++) lines.AddRange(await McCommandRun.SendAllAsync(s, "help " + p, wait));
                break;
            }
            lines.AddRange(await McCommandRun.SendAllAsync(s, "minecraft:help", wait));
            read.Commands = Parse(lines, tree);
            if (read.Commands.Count == 0)
                read.Problem = lines.Count == 0 ? "The server did not answer help." : "The server answered help, but no command in its answer is missing from the jar's own list.";
            return read;
        }

        // ------------------------------------------------------------------ usage text to a form

        private sealed class Tok { public string Id, Literal; public List<string> Options; public bool Optional; public string Parser; }

        /// <summary>Splits a usage such as "&lt;player&gt; [minutes] (on|off)" into tokens at spaces outside brackets.</summary>
        private static List<string> Pieces(string usage)
        {
            var list = new List<string>();
            var sb = new StringBuilder();
            int depth = 0;
            foreach (var ch in usage)
            {
                if (ch == '<' || ch == '[' || ch == '(') depth++;
                else if (ch == '>' || ch == ']' || ch == ')') depth = Math.Max(0, depth - 1);
                if (ch == ' ' && depth == 0) { if (sb.Length > 0) list.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(ch);
            }
            if (sb.Length > 0) list.Add(sb.ToString());
            return list;
        }

        /// <summary>The parser a usage word suggests by its name: players, whole numbers, true or false, coordinates and text.</summary>
        public static string GuessParser(string name)
        {
            var n = (name ?? "").ToLowerInvariant();
            if (Regex.IsMatch(n, @"^(x|y|z)$")) return "brigadier:double";
            if (Regex.IsMatch(n, @"player|target|user|^who$|^name$|nick")) return "minecraft:game_profile";
            if (Regex.IsMatch(n, @"amount|count|number|level|radius|second|minute|hour|page|size|limit|max|min|distance|duration|tick|index|^id$|days")) return "brigadier:integer";
            if (Regex.IsMatch(n, @"^(bool|boolean|enable|enabled|flag|toggle)$")) return "brigadier:bool";
            if (Regex.IsMatch(n, @"message|msg|reason|text|description|broadcast|comment")) return "minecraft:message";
            if (Regex.IsMatch(n, @"^(world|dimension)$")) return "brigadier:string";
            return "brigadier:string";
        }

        private static List<Tok> Tokens(string usage)
        {
            var toks = new List<Tok>();
            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (var piece in Pieces(usage ?? ""))
            {
                var p = piece.Trim().TrimEnd('.');
                if (p.Length == 0 || p == "...") continue;
                bool opt = p.StartsWith("[", StringComparison.Ordinal);
                string inner = p;
                if (p.Length >= 2 && ((p[0] == '<' && p[^1] == '>') || (p[0] == '[' && p[^1] == ']') || (p[0] == '(' && p[^1] == ')'))) inner = p.Substring(1, p.Length - 2);
                inner = inner.Trim().Trim('<', '>', '[', ']').Trim();
                if (inner.Length == 0) continue;
                if (inner.Contains('|') && inner.Split('|').All(o => Regex.IsMatch(o.Trim(), @"^[\w\-]+$")))
                {
                    toks.Add(new Tok { Options = inner.Split('|').Select(o => o.Trim()).Distinct().ToList(), Optional = opt, Id = Unique("choice", used) });
                    continue;
                }
                bool isArg = p[0] == '<' || p[0] == '[' || p[0] == '(' || inner.Contains(' ');
                if (!isArg) { toks.Add(new Tok { Literal = inner }); continue; }
                var id = Regex.Replace(inner.ToLowerInvariant(), @"[^a-z0-9]+", "_").Trim('_');
                if (id.Length == 0) id = "value";
                id = Unique(id, used);
                toks.Add(new Tok { Id = id, Optional = opt, Parser = GuessParser(id) });
            }
            return toks;
        }

        private static string Unique(string id, HashSet<string> used)
        {
            var u = id; int n = 2;
            while (!used.Add(u)) u = id + n++;
            return u;
        }

        /// <summary>A task whose form walks a small command tree made from the command's usage text.</summary>
        public static McTask ToTask(McLiveCommand c)
        {
            var toks = Tokens(c.Usage);
            var root = new McCommandNode { Name = "" };
            var node = new McCommandNode { Name = c.Name, Type = "literal", Executable = toks.All(t => t.Optional) };
            root.Children.Add(node);
            node.Children = Chain(toks, 0);
            var seg = new List<string> { c.Name };
            foreach (var t in toks)
            {
                if (t.Literal != null) seg.Add(t.Literal);
                else if (t.Options != null) seg.Add("(" + string.Join("|", t.Options) + ")@" + t.Id + (t.Optional ? "?" : ""));
                else seg.Add("{" + t.Id + (t.Optional ? "?" : "") + "}");
            }
            return new McTask
            {
                Id = "live:" + c.Name, Group = "Plugins and mods", Label = "/" + c.Name,
                Help = c.Description.Length > 0 ? c.Description : "A command the running server reports. Its form comes from the usage text the server printed.",
                Template = string.Join(" ", seg), Icon = "list", Synthetic = root, Live = true, Source = c.Source, Usage = c.Usage,
            };
        }

        private static List<McCommandNode> Chain(List<Tok> toks, int i)
        {
            var list = new List<McCommandNode>();
            if (i >= toks.Count) return list;
            var t = toks[i];
            bool end = toks.Skip(i + 1).All(x => x.Optional);
            if (t.Literal != null) list.Add(new McCommandNode { Name = t.Literal, Type = "literal", Executable = end, Children = Chain(toks, i + 1) });
            else if (t.Options != null) foreach (var o in t.Options) list.Add(new McCommandNode { Name = o, Type = "literal", Executable = end, Children = Chain(toks, i + 1) });
            else
            {
                var n = new McCommandNode { Name = t.Id, Type = "argument", Parser = t.Parser, Executable = end, Children = Chain(toks, i + 1) };
                if (t.Parser == "brigadier:string") n.StringKind = "phrase";
                if (t.Parser == "minecraft:game_profile") n.EntityKind = "players";
                list.Add(n);
            }
            return list;
        }
    }
}
