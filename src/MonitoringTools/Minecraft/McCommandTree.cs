using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
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
        public bool HasRedirect;
        public List<McCommandNode> Children = new List<McCommandNode>();
        public bool IsLiteral => Type == "literal";
        public bool IsArgument => Type == "argument";

        /// <summary>A short plain description of the value an argument takes.</summary>
        public string Hint => Parser switch
        {
            "brigadier:integer" or "brigadier:long" => Min != null || Max != null ? $"a whole number{(Min != null ? " from " + Min.Value.ToString(CultureInfo.InvariantCulture) : "")}{(Max != null ? " to " + Max.Value.ToString(CultureInfo.InvariantCulture) : "")}" : "a whole number",
            "brigadier:double" or "brigadier:float" => "a number",
            "brigadier:bool" => "true or false",
            "brigadier:string" => StringKind == "greedy" ? "text to the end of the line" : StringKind == "phrase" ? "a word or a quoted phrase" : "one word",
            "minecraft:game_profile" or "minecraft:entity" => "a player name or a selector such as @a",
            "minecraft:message" => "a message",
            "minecraft:time" => "a time such as 1000, 2d or 300s",
            "minecraft:block_pos" or "minecraft:vec3" => "three coordinates such as ~ ~ ~",
            "minecraft:item_stack" => "an item id such as minecraft:diamond",
            _ => (Parser ?? "a value").Replace("minecraft:", "").Replace('_', ' '),
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
            return Node("", doc.RootElement);
        }

        private static McCommandNode Node(string name, JsonElement e)
        {
            var n = new McCommandNode { Name = name };
            if (e.TryGetProperty("type", out var t)) n.Type = t.GetString();
            if (e.TryGetProperty("executable", out var ex)) n.Executable = ex.GetBoolean();
            if (e.TryGetProperty("parser", out var p)) n.Parser = p.GetString();
            if (e.TryGetProperty("redirect", out _)) n.HasRedirect = true;
            if (e.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object)
            {
                if (props.TryGetProperty("min", out var mn) && mn.ValueKind == JsonValueKind.Number) n.Min = mn.GetDouble();
                if (props.TryGetProperty("max", out var mx) && mx.ValueKind == JsonValueKind.Number) n.Max = mx.GetDouble();
                if (props.TryGetProperty("type", out var st) && st.ValueKind == JsonValueKind.String) n.StringKind = st.GetString();
            }
            if (e.TryGetProperty("children", out var kids))
                foreach (var k in kids.EnumerateObject()) n.Children.Add(Node(k.Name, k.Value));
            // Literals first, in the order the jar lists them, then arguments.
            n.Children = n.Children.OrderBy(c => c.IsLiteral ? 0 : 1).ToList();
            return n;
        }

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
            try { return (Parse(await File.ReadAllTextAsync(f)), null); }
            catch (Exception ex) { return (null, "The command list had an unexpected shape: " + ex.Message); }
        }

        /// <summary>Checks one argument value against its parser, returning a plain reason or null.</summary>
        public static string Check(McCommandNode arg, string value)
        {
            if (value == null || value.Trim().Length == 0) return $"Fill in {arg.Name}.";
            if (value.IndexOfAny(new[] { '\r', '\n' }) >= 0) return "A command is one line.";
            switch (arg.Parser)
            {
                case "brigadier:integer":
                case "brigadier:long":
                    if (!long.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) return $"{arg.Name} must be a whole number.";
                    if (arg.Min != null && n < arg.Min) return $"{arg.Name} must be at least {arg.Min.Value.ToString(CultureInfo.InvariantCulture)}.";
                    if (arg.Max != null && n > arg.Max) return $"{arg.Name} must be at most {arg.Max.Value.ToString(CultureInfo.InvariantCulture)}.";
                    return null;
                case "brigadier:double":
                case "brigadier:float":
                    if (!double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return $"{arg.Name} must be a number.";
                    if (arg.Min != null && d < arg.Min) return $"{arg.Name} must be at least {arg.Min.Value.ToString(CultureInfo.InvariantCulture)}.";
                    if (arg.Max != null && d > arg.Max) return $"{arg.Name} must be at most {arg.Max.Value.ToString(CultureInfo.InvariantCulture)}.";
                    return null;
                case "brigadier:bool":
                    return value == "true" || value == "false" ? null : $"{arg.Name} must be true or false.";
                case "brigadier:string":
                    if (arg.StringKind == "word" && value.Trim().Contains(' ')) return $"{arg.Name} is one word.";
                    return null;
                default:
                    return null;
            }
        }

        /// <summary>One step of a built command: the node chosen at that position and the value typed for an argument.</summary>
        public sealed class Step { public McCommandNode Node; public string Value = ""; }

        /// <summary>The exact line the builder sends: literal names and argument values joined by single spaces, with a leading slash.</summary>
        public static string Line(IEnumerable<Step> steps) => "/" + string.Join(" ", steps.Select(s => s.Node.IsLiteral ? s.Node.Name : (s.Value ?? "").Trim()));

        /// <summary>Null when the steps make a complete command the server can run, otherwise what is missing.</summary>
        public static string Validate(IReadOnlyList<Step> steps)
        {
            if (steps.Count == 0) return "Choose a command.";
            foreach (var s in steps.Where(s => s.Node.IsArgument)) { var bad = Check(s.Node, s.Value); if (bad != null) return bad; }
            var last = steps[^1].Node;
            return last.Executable || last.HasRedirect ? null : "This command needs more: " + string.Join(" or ", last.Children.Take(4).Select(c => c.IsLiteral ? c.Name : "<" + c.Name + ">")) + ".";
        }
    }
}
