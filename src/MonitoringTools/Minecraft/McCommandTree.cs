using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MonitoringTools.Minecraft
{
    /// <summary>One node of the server's own command tree (Brigadier), as the jar's data generator writes it to reports/commands.json.</summary>
    public sealed class McCommandNode
    {
        public string Name = "", Type = "literal", Parser;
        public bool Executable;
        public double? Min, Max;
        public string StringKind;   // "word", "phrase" or "greedy" for brigadier:string
        /// <summary>For minecraft:resource, resource_key, resource_or_tag and the like: the registry the value comes from.</summary>
        public string Registry;
        /// <summary>For minecraft:entity: "players" or "entities", and "single" or "multiple".</summary>
        public string EntityKind, Amount;
        public bool HasRedirect;
        /// <summary>The node a redirect points at (tp points at teleport, xp at experience); its children are this node's children.</summary>
        public McCommandNode RedirectTo;
        internal string[] RedirectPath;
        public List<McCommandNode> Children = new List<McCommandNode>();
        public bool IsLiteral => Type == "literal";
        public bool IsArgument => Type == "argument";

        /// <summary>What can follow this node: its own children, or those of the node it redirects to.</summary>
        public List<McCommandNode> Next => Children.Count == 0 && RedirectTo != null ? RedirectTo.Children : Children;
        /// <summary>True when the command may end after this node.</summary>
        public bool CanEnd => Executable || (Children.Count == 0 && RedirectTo != null && RedirectTo.Executable);

        /// <summary>A short plain description of the value an argument takes.</summary>
        public string Hint => Parser switch
        {
            "brigadier:integer" or "brigadier:long" => Min != null || Max != null ? $"a whole number{(Min != null ? " from " + Min.Value.ToString(CultureInfo.InvariantCulture) : "")}{(Max != null ? " to " + Max.Value.ToString(CultureInfo.InvariantCulture) : "")}" : "a whole number",
            "brigadier:double" or "brigadier:float" => Min != null || Max != null ? $"a number{(Min != null ? " from " + Min.Value.ToString(CultureInfo.InvariantCulture) : "")}{(Max != null ? " to " + Max.Value.ToString(CultureInfo.InvariantCulture) : "")}" : "a number",
            "brigadier:bool" => "true or false",
            "brigadier:string" => StringKind == "greedy" ? "text to the end of the line" : StringKind == "phrase" ? "a word or a quoted phrase" : "one word",
            "minecraft:game_profile" or "minecraft:entity" => "a player name or a selector such as @a",
            "minecraft:score_holder" => "a player name, a selector or *",
            "minecraft:message" => "a message",
            "minecraft:time" => "a time such as 1000, 2d or 300s",
            "minecraft:block_pos" or "minecraft:vec3" => "three coordinates such as ~ ~ ~",
            "minecraft:vec2" or "minecraft:column_pos" or "minecraft:rotation" => "two coordinates such as ~ ~",
            "minecraft:item_stack" => "an item id such as minecraft:diamond",
            "minecraft:block_state" => "a block id such as minecraft:stone",
            "minecraft:block_predicate" => "a block id or a block tag",
            "minecraft:gamemode" => "survival, creative, adventure or spectator",
            "minecraft:dimension" => "a dimension such as minecraft:the_nether",
            "minecraft:component" => "a text",
            "minecraft:angle" => "an angle in degrees",
            "minecraft:resource" or "minecraft:resource_key" or "minecraft:resource_or_tag" or "minecraft:resource_or_tag_key" => "an id from the game's list",
            _ => "a value of type " + (Parser ?? "unknown"),
        };
    }

    /// <summary>
    /// The typed command builder's data: the command tree a specific server jar reports for itself. The builder walks it one
    /// token at a time, so every command line it produces is one the server's own parser accepts the shape of.
    /// </summary>
    public static class McCommandTree
    {
        public static string FileFor(string version) => Path.Combine(WorldGenerator.CacheRoot, version ?? "", "gen", "reports", "commands.json");

        public static McCommandNode Parse(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = Node("", doc.RootElement);
            Resolve(root, root);
            return root;
        }

        /// <summary>Points every redirect at its target, so the walkers follow tp to teleport and xp to experience.</summary>
        private static void Resolve(McCommandNode root, McCommandNode n)
        {
            if (n.HasRedirect && n.RedirectPath != null)
            {
                var at = root;
                foreach (var part in n.RedirectPath) at = at?.Children.FirstOrDefault(c => c.Name == part);
                n.RedirectTo = at;
            }
            foreach (var c in n.Children) Resolve(root, c);
        }

        private static McCommandNode Node(string name, JsonElement e)
        {
            var n = new McCommandNode { Name = name };
            if (e.TryGetProperty("type", out var t)) n.Type = t.GetString();
            if (e.TryGetProperty("executable", out var ex)) n.Executable = ex.GetBoolean();
            if (e.TryGetProperty("parser", out var p)) n.Parser = p.GetString();
            if (e.TryGetProperty("redirect", out var red))
            {
                n.HasRedirect = true;
                if (red.ValueKind == JsonValueKind.Array) n.RedirectPath = red.EnumerateArray().Select(x => x.GetString()).ToArray();
            }
            if (e.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object)
            {
                if (props.TryGetProperty("min", out var mn) && mn.ValueKind == JsonValueKind.Number) n.Min = mn.GetDouble();
                if (props.TryGetProperty("max", out var mx) && mx.ValueKind == JsonValueKind.Number) n.Max = mx.GetDouble();
                if (props.TryGetProperty("type", out var st) && st.ValueKind == JsonValueKind.String)
                {
                    if (n.Parser == "minecraft:entity") n.EntityKind = st.GetString(); else n.StringKind = st.GetString();
                }
                if (props.TryGetProperty("amount", out var am) && am.ValueKind == JsonValueKind.String) n.Amount = am.GetString();
                if (props.TryGetProperty("registry", out var rg) && rg.ValueKind == JsonValueKind.String) n.Registry = rg.GetString();
            }
            if (e.TryGetProperty("children", out var kids))
                foreach (var k in kids.EnumerateObject()) n.Children.Add(Node(k.Name, k.Value));
            // Literals first, in the order the jar lists them, then arguments.
            n.Children = n.Children.OrderBy(c => c.IsLiteral ? 0 : 1).ToList();
            return n;
        }

        private static readonly Dictionary<string, McCommandNode> Parsed = new Dictionary<string, McCommandNode>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Reads the tree for a version from the cache the world generator fills, or asks the jar for it.</summary>
        public static async Task<(McCommandNode Root, string Unavailable)> LoadAsync(string version, Action<string> report, bool allowDownload)
        {
            var f = FileFor(version);
            if (!File.Exists(f))
            {
                if (!allowDownload) return (null, "This version's command list has not been read yet.");
                var facts = await WorldGenerator.JarFactsAsync(version, report);
                if (!File.Exists(f)) return (null, facts.Reason ?? "The jar did not list its commands.");
            }
            try
            {
                // One parsed tree per file, so steps kept between page rebuilds point into the same tree.
                var key = f + "|" + File.GetLastWriteTimeUtc(f).Ticks;
                lock (Parsed) if (Parsed.TryGetValue(key, out var hit)) return (hit, null);
                var root = Parse(await File.ReadAllTextAsync(f));
                lock (Parsed) Parsed[key] = root;
                return (root, null);
            }
            catch (Exception ex) { return (null, "The command list had an unexpected shape: " + ex.Message); }
        }

        /// <summary>The game rules the jar's own "gamerule" command lists, with the parser of each rule's value.</summary>
        public static List<(string Name, string Parser)> GameRules(McCommandNode root)
        {
            var gr = root?.Children.FirstOrDefault(c => c.Name == "gamerule" && c.IsLiteral);
            if (gr == null) return new List<(string, string)>();
            return gr.Children.Where(c => c.IsLiteral)
                .Select(c => (c.Name, c.Children.FirstOrDefault(a => a.IsArgument)?.Parser ?? "unknown"))
                .OrderBy(x => x.Name, StringComparer.Ordinal).ToList();
        }

        private static readonly Regex IdRx = new Regex(@"^#?([a-z0-9_.\-]+:)?[a-z0-9_./\-]+$", RegexOptions.CultureInvariant);
        private static readonly Regex NameRx = new Regex(@"^[A-Za-z0-9_]{1,16}$", RegexOptions.CultureInvariant);
        private static readonly Regex SelectorRx = new Regex(@"^@[parsen](\[[^\]\r\n]*\])?$", RegexOptions.CultureInvariant);
        private static readonly Regex UuidRx = new Regex(@"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$", RegexOptions.CultureInvariant);
        private static readonly Regex TimeRx = new Regex(@"^(\d+(\.\d+)?)([dst]?)$", RegexOptions.CultureInvariant);
        public static readonly string[] GameModes = { "survival", "creative", "adventure", "spectator" };

        /// <summary>The number of coordinates a position parser takes, or 0 when it is not a position.</summary>
        public static int Axes(string parser) => parser switch
        {
            "minecraft:block_pos" or "minecraft:vec3" => 3,
            "minecraft:vec2" or "minecraft:column_pos" or "minecraft:rotation" => 2,
            _ => 0,
        };

        /// <summary>True for parsers whose value is a number or a set of coordinates.</summary>
        public static bool IsNumeric(string parser) => parser is "brigadier:integer" or "brigadier:long" or "brigadier:float" or "brigadier:double" or "minecraft:time" or "minecraft:angle" || Axes(parser) > 0;

        /// <summary>True for parsers whose value names players or entities.</summary>
        public static bool IsTarget(string parser) => parser is "minecraft:entity" or "minecraft:game_profile" or "minecraft:score_holder";

        /// <summary>Checks one coordinate: a number, ~ or ~number (relative), or ^number (local). Block positions take whole numbers.</summary>
        public static bool Coordinate(string c, bool whole)
        {
            if (string.IsNullOrEmpty(c)) return false;
            var rest = c;
            if (c[0] == '~' || c[0] == '^') { rest = c.Substring(1); if (rest.Length == 0) return true; }
            return whole && c[0] != '~' && c[0] != '^'
                ? int.TryParse(rest, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _)
                : double.TryParse(rest, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
        }

        /// <summary>Checks one argument value against its parser, returning a plain reason or null.</summary>
        public static string Check(McCommandNode arg, string value, McRegistries registries = null)
        {
            if (value == null || value.Trim().Length == 0) return $"Fill in {arg.Name}.";
            if (value.IndexOfAny(new[] { '\r', '\n' }) >= 0) return "A command is one line.";
            var v = value.Trim();
            string Bounds(double d)
            {
                if (arg.Min != null && d < arg.Min) return $"{arg.Name} must be at least {arg.Min.Value.ToString(CultureInfo.InvariantCulture)}.";
                if (arg.Max != null && d > arg.Max) return $"{arg.Name} must be at most {arg.Max.Value.ToString(CultureInfo.InvariantCulture)}.";
                return null;
            }
            string Known(string id, string registry)
            {
                if (registries == null || registry == null || id.StartsWith("#", StringComparison.Ordinal)) return null;
                var list = registries.Get(registry);
                if (list == null || list.Count == 0) return null;
                var full = id.Contains(':') ? id : "minecraft:" + id;
                return list.Contains(full) ? null : $"{full} is not in this version's {McRegistries.Readable(registry).ToLowerInvariant()} list.";
            }
            switch (arg.Parser)
            {
                case "brigadier:integer":
                case "brigadier:long":
                    if (!long.TryParse(v, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n)) return $"{arg.Name} must be a whole number.";
                    return Bounds(n);
                case "brigadier:double":
                case "brigadier:float":
                case "minecraft:angle":
                {
                    var num = arg.Parser == "minecraft:angle" && v.StartsWith("~", StringComparison.Ordinal) ? (v.Length == 1 ? "0" : v.Substring(1)) : v;
                    if (!double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return $"{arg.Name} must be a number.";
                    return arg.Parser == "minecraft:angle" ? null : Bounds(d);
                }
                case "brigadier:bool":
                    return v == "true" || v == "false" ? null : $"{arg.Name} must be true or false.";
                case "brigadier:string":
                    if (arg.StringKind == "word" && v.Contains(' ')) return $"{arg.Name} is one word.";
                    return null;
                case "minecraft:time":
                {
                    var m = TimeRx.Match(v);
                    if (!m.Success) return $"{arg.Name} must be a number of ticks, or a number followed by d, s or t.";
                    double ticks = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) * (m.Groups[3].Value == "d" ? 24000 : m.Groups[3].Value == "s" ? 20 : 1);
                    return Bounds(Math.Round(ticks));
                }
                case "minecraft:entity":
                case "minecraft:game_profile":
                case "minecraft:score_holder":
                {
                    if (arg.Parser == "minecraft:score_holder" && v == "*") return null;
                    if (SelectorRx.IsMatch(v))
                    {
                        if (arg.Parser == "minecraft:entity" && arg.EntityKind == "players" && v.StartsWith("@e", StringComparison.Ordinal) && !v.Contains("type=minecraft:player") && !v.Contains("type=player"))
                            return $"{arg.Name} takes players only, so @e cannot be used here.";
                        if (arg.Amount == "single" && (v.StartsWith("@a", StringComparison.Ordinal) || v.StartsWith("@e", StringComparison.Ordinal)) && !v.Contains("limit=1"))
                            return $"{arg.Name} takes one target, so {v.Substring(0, 2)} needs limit=1.";
                        return null;
                    }
                    if (NameRx.IsMatch(v) || UuidRx.IsMatch(v)) return null;
                    return $"{arg.Name} must be a player name (letters, digits and _, up to 16) or a selector such as @a.";
                }
                case "minecraft:block_pos":
                case "minecraft:vec3":
                case "minecraft:vec2":
                case "minecraft:column_pos":
                case "minecraft:rotation":
                {
                    var parts = v.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    int want = Axes(arg.Parser);
                    if (parts.Length != want) return $"{arg.Name} needs {want} coordinates.";
                    bool whole = arg.Parser == "minecraft:block_pos" || arg.Parser == "minecraft:column_pos";
                    bool local = parts.Any(p => p.StartsWith("^", StringComparison.Ordinal));
                    if (local && !parts.All(p => p.StartsWith("^", StringComparison.Ordinal))) return $"{arg.Name} cannot mix ^ with other coordinates.";
                    for (int i = 0; i < parts.Length; i++)
                        if (!Coordinate(parts[i], whole)) return $"{arg.Name}: \"{parts[i]}\" is not a {(whole ? "whole number" : "number")}, ~ or ~ with a number.";
                    return null;
                }
                case "minecraft:item_stack":
                case "minecraft:item_predicate":
                {
                    var id = v.Split('[', '{')[0];
                    if (!IdRx.IsMatch(id)) return $"{arg.Name} must be an item id such as minecraft:diamond.";
                    return Known(id, "minecraft:item");
                }
                case "minecraft:block_state":
                case "minecraft:block_predicate":
                {
                    var id = v.Split('[', '{')[0];
                    if (!IdRx.IsMatch(id)) return $"{arg.Name} must be a block id such as minecraft:stone.";
                    return Known(id, "minecraft:block");
                }
                case "minecraft:resource":
                case "minecraft:resource_key":
                case "minecraft:resource_or_tag":
                case "minecraft:resource_or_tag_key":
                case "minecraft:resource_location":
                case "minecraft:dimension":
                    if (!IdRx.IsMatch(v)) return $"{arg.Name} must be an id such as minecraft:name.";
                    return arg.Parser == "minecraft:dimension" ? null : Known(v, arg.Registry);
                case "minecraft:gamemode":
                    return GameModes.Contains(v) ? null : $"{arg.Name} must be survival, creative, adventure or spectator.";
                default:
                    return null;
            }
        }

        /// <summary>One step of a built command: the node chosen at that position and the value typed for an argument.</summary>
        public sealed class Step { public McCommandNode Node; public string Value = ""; public string Field; }

        /// <summary>The exact line the builder sends: literal names and argument values joined by single spaces, with a leading slash.</summary>
        public static string Line(IEnumerable<Step> steps) => "/" + string.Join(" ", steps.Select(s => s.Node.IsLiteral ? s.Node.Name : (s.Value ?? "").Trim()));

        /// <summary>Null when the steps make a complete command the server can run, otherwise what is missing.</summary>
        public static string Validate(IReadOnlyList<Step> steps, McRegistries registries = null)
        {
            if (steps.Count == 0) return "Choose a command.";
            foreach (var s in steps.Where(s => s.Node.IsArgument)) { var bad = Check(s.Node, s.Value, registries); if (bad != null) return bad; }
            var last = steps[^1].Node;
            return last.CanEnd ? null : "This command needs more: " + string.Join(" or ", last.Next.Take(4).Select(c => c.IsLiteral ? c.Name : "<" + c.Name + ">")) + ".";
        }

        /// <summary>
        /// Reads a saved command line back into steps of the tree, so a favourite opens in the builder with its fields filled.
        /// Literal words must match; an argument takes one word, or as many as its coordinates, or the rest of the line for
        /// text. Returns null when the line does not fit this version's tree.
        /// </summary>
        public static List<Step> ParseLine(McCommandNode root, string line)
        {
            if (root == null || string.IsNullOrWhiteSpace(line)) return null;
            var words = line.Trim().TrimStart('/').Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var steps = new List<Step>();
            return Walk(root, words, 0, steps) ? steps : null;
        }

        // Tries each way the words can follow the node, and backs out of a choice that leaves words nothing can take
        // (teleport's single destination also fits a player name that is really the targets before a position).
        private static bool Walk(McCommandNode node, string[] words, int i, List<Step> steps)
        {
            if (i == words.Length) return true;
            var kids = node.Next;
            var lit = kids.FirstOrDefault(c => c.IsLiteral && c.Name == words[i]);
            if (lit != null)
            {
                steps.Add(new Step { Node = lit });
                if (Walk(lit, words, i + 1, steps)) return true;
                steps.RemoveAt(steps.Count - 1);
            }
            foreach (var a in kids.Where(c => c.IsArgument))
            {
                int n = Axes(a.Parser) > 0 ? Axes(a.Parser) : a.Parser == "minecraft:message" || a.Parser == "minecraft:component" || a.StringKind == "greedy" ? words.Length - i : 1;
                if (i + n > words.Length || Check(a, string.Join(" ", words, i, n)) != null) continue;
                steps.Add(new Step { Node = a, Value = string.Join(" ", words, i, n) });
                if (Walk(a, words, i + n, steps)) return true;
                steps.RemoveAt(steps.Count - 1);
            }
            return false;
        }

        /// <summary>What a part of the line is, for the coloured preview: the command words, a target, a number or another value.</summary>
        public enum PartKind { Command, Target, Number, Value }

        public static PartKind KindOf(Step s) =>
            s.Node.IsLiteral ? (s.Field != null ? PartKind.Value : PartKind.Command)
            : IsTarget(s.Node.Parser) ? PartKind.Target
            : IsNumeric(s.Node.Parser) ? PartKind.Number : PartKind.Value;
    }
}
