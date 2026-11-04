using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace MonitoringTools.Minecraft
{
    /// <summary>
    /// One task in the command builder's gallery: a plain name for what someone wants to do, and a template that walks the
    /// server's own command tree. The template's words are literal command words; {name} is an argument the tree names
    /// (with ? when it may be left out); (a|b|c)@id is a choice between literal words, where * offers every literal the tree
    /// has at that point and {name} offers an argument as one of the choices.
    /// </summary>
    public sealed class McTask
    {
        public string Id, Group, Label, Help, Template, Icon;
        /// <summary>Set for commands that need the two-key confirmation: the confirm button's label.</summary>
        public string Confirm;
        /// <summary>For a command read from the live server: the small command tree made from its usage text, the namespace it came from and the usage itself.</summary>
        public McCommandNode Synthetic;
        public bool Live;
        public string Source, Usage;
        /// <summary>The command word shown beside the task in the gallery.</summary>
        public string Shown => "/" + Template.Split(' ')[0].Trim('(', ')').Split('|')[0].Split('@')[0];
    }

    /// <summary>One field of a task form: an argument of the command tree, or a choice between literal words.</summary>
    public sealed class McTaskField
    {
        public string Id, Label;
        public bool Optional;
        /// <summary>The argument node for an argument field (or the chosen argument of a choice).</summary>
        public McCommandNode Node;
        /// <summary>For a choice: the literal words (and "{name}" for an argument choice) the tree offers here.</summary>
        public List<string> Options;
        public bool IsChoice => Options != null;
    }

    public sealed class McTaskBuild
    {
        public List<McCommandTree.Step> Steps = new List<McCommandTree.Step>();
        public List<McTaskField> Fields = new List<McTaskField>();
        /// <summary>Null when the command is complete and valid, otherwise exactly what is missing or wrong.</summary>
        public string Problem;
        /// <summary>Set when this version's command tree does not have the command at all.</summary>
        public string Unavailable;
        public string Line => McCommandTree.Line(Steps);
    }

    public static class McCommandTasks
    {
        public static readonly string[] Groups = { "Players", "World", "Rules", "Server" };

        public static readonly McTask[] All =
        {
            T("give", "Players", "Give an item", "Puts items straight into a player's inventory.", "give {targets} {item} {count?}", "gift"),
            T("tp", "Players", "Teleport", "Moves players to a place, exactly or relative to where they stand.", "tp {targets} {location}", "pin"),
            T("effect", "Players", "Give an effect", "Speed, night vision, regeneration and the rest, for a time you choose.", "effect give {targets} {effect} {seconds?} {amplifier?}", "flask"),
            T("effect-clear", "Players", "Clear an effect", "Removes one effect, or every effect when none is chosen.", "effect clear {targets} {effect?}", "flask"),
            T("gamemode", "Players", "Change game mode", "Survival, creative, adventure or spectator for chosen players.", "gamemode {gamemode} {target?}", "game"),
            T("enchant", "Players", "Enchant the held item", "Adds an enchantment to the item a player holds; it has to suit that item.", "enchant {targets} {enchantment} {level?}", "spark"),
            T("xp", "Players", "Experience", "Adds or sets experience levels or points.", "experience (add|set)@change {target} {amount} (levels|points)@unit", "spark"),
            T("kick", "Players", "Kick a player", "Disconnects players with an optional reason. They can join again.", "kick {targets} {reason?}", "door"),
            T("ban", "Players", "Ban a player", "Disconnects a player and keeps them out until they are pardoned.", "ban {targets} {reason?}", "shield", "Ban"),
            T("op", "Players", "Operator", "Makes a player an operator, who can run every command, or removes that.", "(op|deop)@operator {targets}", "shield", "Change operator"),
            T("whitelist", "Players", "Whitelist", "Adds a player to the whitelist or removes them from it.", "whitelist (add|remove)@listchange {targets}", "list"),
            T("time", "World", "Set the time", "Day, noon, night, midnight or an exact time.", "time set (day|noon|night|midnight|{time})@when", "sun"),
            T("weather", "World", "Change the weather", "Clear, rain or thunder, for a while or until it changes on its own.", "weather (clear|rain|thunder)@weather {duration?}", "cloud"),
            T("summon", "World", "Summon a mob", "Spawns any entity at a position.", "summon {entity} {pos?}", "paw"),
            T("setblock", "World", "Set a block", "Places one block at a position.", "setblock {pos} {block} (replace|keep|destroy)@placing?", "cube"),
            T("fill", "World", "Fill an area", "Fills the box between two corners with one block. A box holds at most 32,768 blocks.", "fill {from} {to} {block} (replace|hollow|outline|keep|destroy)@filling?", "cube"),
            T("spawn", "World", "Set the world spawn", "Where new players and players without a bed appear.", "setworldspawn {pos?}", "home"),
            T("gamerule", "Rules", "Set a game rule", "Keep inventory, mob griefing, daylight cycle and every other rule this version has.", "gamerule (*)@rule {value}", "rule"),
            T("difficulty", "Rules", "Difficulty", "Peaceful, easy, normal or hard, at once for everyone.", "difficulty (*)@difficulty", "rule"),
            T("say", "Server", "Say in chat", "Sends a message to everyone, from the server.", "say {message}", "chat"),
            T("title", "Server", "Show a title", "Shows large text, a subtitle or a line above the hotbar to chosen players.", "title {targets} (title|subtitle|actionbar)@titlekind {title}", "chat"),
            T("save", "Server", "Save the world", "Writes the world to disk now.", "save-all", "save"),
            T("list", "Server", "List players", "Asks the server who is online.", "list", "list"),
            T("stop", "Server", "Stop the server", "Saves the world and stops the server. Players are disconnected.", "stop", "power", "Stop the server"),
        };

        private static McTask T(string id, string group, string label, string help, string template, string icon, string confirm = null) =>
            new McTask { Id = id, Group = group, Label = label, Help = help, Template = template, Icon = icon, Confirm = confirm };

        /// <summary>Tasks made from a running server's own command list, by task id. Filled when the builder reads the server.</summary>
        private static readonly Dictionary<string, McTask> LiveTasks = new Dictionary<string, McTask>(StringComparer.Ordinal);

        public static void SetLive(string serverId, IEnumerable<McTask> tasks)
        {
            lock (LiveTasks)
            {
                foreach (var k in LiveTasks.Keys.Where(k => k.StartsWith(serverId + "|", StringComparison.Ordinal)).ToList()) LiveTasks.Remove(k);
                foreach (var t in tasks) LiveTasks[serverId + "|" + t.Id] = t;
            }
        }

        public static List<McTask> LiveFor(string serverId)
        {
            lock (LiveTasks) return LiveTasks.Where(kv => kv.Key.StartsWith(serverId + "|", StringComparison.Ordinal)).Select(kv => kv.Value).OrderBy(t => t.Label, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static McTask Find(string id, string serverId = null)
        {
            if (id != null && id.StartsWith("live:", StringComparison.Ordinal))
                lock (LiveTasks) return serverId != null && LiveTasks.TryGetValue(serverId + "|" + id, out var t) ? t : null;
            return All.FirstOrDefault(t => t.Id == id);
        }

        /// <summary>Friendly field names; an argument without one keeps the name the command tree gives it.</summary>
        public static readonly Dictionary<string, string> Labels = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["targets"] = "Who", ["target"] = "Who", ["item"] = "Item", ["count"] = "How many", ["location"] = "Where", ["pos"] = "Where",
            ["effect"] = "Effect", ["seconds"] = "Seconds", ["amplifier"] = "Strength", ["gamemode"] = "Game mode", ["enchantment"] = "Enchantment",
            ["level"] = "Level", ["amount"] = "Amount", ["reason"] = "Reason", ["time"] = "Exact time", ["duration"] = "How long", ["entity"] = "Mob",
            ["block"] = "Block", ["from"] = "From corner", ["to"] = "To corner", ["value"] = "Value", ["message"] = "Message", ["title"] = "Text",
            ["change"] = "Change", ["unit"] = "Count in", ["operator"] = "Operator", ["listchange"] = "Whitelist", ["when"] = "Time",
            ["weather"] = "Weather", ["placing"] = "How to place", ["filling"] = "How to fill", ["rule"] = "Rule", ["difficulty"] = "Difficulty",
            ["titlekind"] = "Where it shows",
        };

        public static string LabelOf(string id) => Labels.TryGetValue(id ?? "", out var l) ? l : id;

        private sealed class Seg { public string Literal, Arg, Id; public List<string> Options; public bool Optional; }

        private static List<Seg> Segments(string template)
        {
            var list = new List<Seg>();
            foreach (var raw in template.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var t = raw;
                bool opt = t.EndsWith("?", StringComparison.Ordinal);
                if (opt) t = t[..^1];
                if (t.StartsWith("{", StringComparison.Ordinal)) { list.Add(new Seg { Arg = t.Trim('{', '}', '?'), Id = t.Trim('{', '}', '?'), Optional = opt || raw.EndsWith("?}", StringComparison.Ordinal) }); continue; }
                if (t.StartsWith("(", StringComparison.Ordinal))
                {
                    int at = t.IndexOf(")@", StringComparison.Ordinal);
                    list.Add(new Seg { Options = t.Substring(1, at - 1).Split('|').ToList(), Id = t.Substring(at + 2), Optional = opt });
                    continue;
                }
                list.Add(new Seg { Literal = t });
            }
            return list;
        }

        /// <summary>
        /// Walks a task's template through the server's command tree with the values typed so far. The fields are listed even
        /// past a gap, so the form can show them; the command line stops at the first optional field left empty.
        /// </summary>
        public static McTaskBuild Build(McTask task, McCommandNode root, IReadOnlyDictionary<string, string> values, McRegistries registries = null)
        {
            var b = new McTaskBuild();
            string V(string id) => values != null && values.TryGetValue(id, out var v) ? (v ?? "").Trim() : "";
            var node = root;
            bool stopped = false;
            string stoppedAt = null;
            foreach (var seg in Segments(task.Template))
            {
                var kids = node?.Next ?? new List<McCommandNode>();
                if (seg.Literal != null)
                {
                    var lit = kids.FirstOrDefault(c => c.IsLiteral && c.Name == seg.Literal);
                    if (lit == null) { b.Unavailable = $"This version has no \"{string.Join(" ", b.Steps.Where(s => s.Node.IsLiteral).Select(s => s.Node.Name).Append(seg.Literal))}\" command."; break; }
                    if (!stopped) b.Steps.Add(new McCommandTree.Step { Node = lit });
                    node = lit;
                    continue;
                }
                if (seg.Arg != null)
                {
                    var arg = kids.FirstOrDefault(c => c.IsArgument && c.Name == seg.Arg) ?? kids.FirstOrDefault(c => c.IsArgument && kids.Count(k => k.IsArgument) == 1);
                    if (arg == null) { b.Unavailable = $"This version's command has no {LabelOf(seg.Id).ToLowerInvariant()} here."; break; }
                    var field = new McTaskField { Id = seg.Id, Label = LabelOf(seg.Id), Optional = seg.Optional, Node = arg };
                    b.Fields.Add(field);
                    var val = V(seg.Id);
                    if (seg.Optional && val.Length == 0) { if (!stopped) { stopped = true; stoppedAt = field.Label; } }
                    else if (stopped) { if (val.Length > 0) b.Problem ??= $"Fill in {stoppedAt} first, or clear {field.Label}."; }
                    else b.Steps.Add(new McCommandTree.Step { Node = arg, Value = val, Field = seg.Id });
                    node = arg;
                    continue;
                }
                // a choice between literal words (and perhaps one argument)
                var options = new List<string>();
                foreach (var o in seg.Options)
                {
                    if (o == "*") options.AddRange(kids.Where(c => c.IsLiteral).Select(c => c.Name));
                    else if (o.StartsWith("{", StringComparison.Ordinal)) { if (kids.Any(c => c.IsArgument && c.Name == o.Trim('{', '}'))) options.Add(o); }
                    else if (kids.Any(c => c.IsLiteral && c.Name == o)) options.Add(o);
                }
                if (options.Count == 0) { b.Unavailable = $"This version's command has no choices for {LabelOf(seg.Id).ToLowerInvariant()} here."; break; }
                var choiceField = new McTaskField { Id = seg.Id, Label = LabelOf(seg.Id), Optional = seg.Optional, Options = options };
                b.Fields.Add(choiceField);
                var chosen = V(seg.Id);
                if (chosen.Length == 0 && !seg.Optional) chosen = options[0];
                if (!options.Contains(chosen)) chosen = seg.Optional ? "" : options[0];
                if (chosen.Length == 0) { if (!stopped) { stopped = true; stoppedAt = choiceField.Label; } continue; }
                if (stopped) { b.Problem ??= $"Fill in {stoppedAt} first, or clear {choiceField.Label}."; continue; }
                if (chosen.StartsWith("{", StringComparison.Ordinal))
                {
                    var argName = chosen.Trim('{', '}');
                    var arg = kids.First(c => c.IsArgument && c.Name == argName);
                    var sub = new McTaskField { Id = argName, Label = LabelOf(argName), Node = arg };
                    b.Fields.Add(sub);
                    b.Steps.Add(new McCommandTree.Step { Node = arg, Value = V(argName), Field = argName });
                    node = arg;
                }
                else
                {
                    var lit = kids.First(c => c.IsLiteral && c.Name == chosen);
                    b.Steps.Add(new McCommandTree.Step { Node = lit, Field = seg.Id });
                    node = lit;
                }
            }
            if (b.Unavailable != null) { b.Problem = b.Unavailable; return b; }
            // Required fields first, by their friendly names, then the parser's own checks.
            foreach (var f in b.Fields.Where(f => !f.IsChoice && !f.Optional))
                if (V(f.Id).Length == 0 && b.Steps.Any(s => s.Field == f.Id)) { b.Problem ??= $"Fill in {f.Label}."; }
            if (b.Problem == null)
                foreach (var s in b.Steps.Where(s => s.Node.IsArgument))
                {
                    var bad = McCommandTree.Check(s.Node, s.Value, registries);
                    if (bad != null) { b.Problem = Relabel(bad, s.Node.Name, LabelOf(s.Field ?? s.Node.Name)); break; }
                }
            if (b.Problem == null && b.Steps.Count > 0 && !b.Steps[^1].Node.CanEnd) b.Problem = "This command needs more: " + string.Join(" or ", b.Steps[^1].Node.Next.Take(4).Select(c => c.IsLiteral ? c.Name : LabelOf(c.Name))) + ".";
            return b;
        }

        /// <summary>Puts the friendly field name in place of the tree's argument name at the start of a parser message.</summary>
        public static string Relabel(string message, string argName, string label) =>
            message.StartsWith(argName + " ", StringComparison.Ordinal) || message.StartsWith(argName + ":", StringComparison.Ordinal) ? label + message.Substring(argName.Length) : message.Replace("Fill in " + argName + ".", "Fill in " + label + ".");

        /// <summary>A text as the JSON string that minecraft:component takes: "Hello" with quotes and escapes.</summary>
        public static string Component(string text)
        {
            var sb = new StringBuilder("\"");
            foreach (var ch in text ?? "")
            {
                if (ch == '"' || ch == '\\') sb.Append('\\').Append(ch);
                else if (ch < ' ') sb.Append(' ');
                else sb.Append(ch);
            }
            return sb.Append('"').ToString();
        }

        /// <summary>The words of a JSON string component, for showing the text box its plain text again.</summary>
        public static string Uncomponent(string json)
        {
            if (string.IsNullOrEmpty(json) || json.Length < 2 || json[0] != '"' || json[^1] != '"') return json ?? "";
            return json.Substring(1, json.Length - 2).Replace("\\\"", "\"").Replace("\\\\", "\\");
        }
    }

    /// <summary>A target selector with the common filters: @a[distance=..10,limit=2,sort=nearest,gamemode=survival].</summary>
    public sealed class McSelector
    {
        public string Base = "@a";
        public int? MaxDistance, Limit;
        public string Sort = "", GameMode = "";

        public static readonly string[] Bases = { "@p", "@a", "@r", "@s", "@e" };
        public static readonly string[] Sorts = { "nearest", "furthest", "random", "arbitrary" };

        public override string ToString()
        {
            var f = new List<string>();
            if (MaxDistance is int d) f.Add("distance=.." + d.ToString(CultureInfo.InvariantCulture));
            if (Limit is int l) f.Add("limit=" + l.ToString(CultureInfo.InvariantCulture));
            if (!string.IsNullOrEmpty(Sort)) f.Add("sort=" + Sort);
            if (!string.IsNullOrEmpty(GameMode)) f.Add("gamemode=" + GameMode);
            return Base + (f.Count == 0 ? "" : "[" + string.Join(",", f) + "]");
        }

        /// <summary>Reads a selector the builder wrote back into its parts; anything else gives null.</summary>
        public static McSelector Parse(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length < 2 || s[0] != '@' || !Bases.Contains(s.Substring(0, 2))) return null;
            var sel = new McSelector { Base = s.Substring(0, 2) };
            if (s.Length == 2) return sel;
            if (s[2] != '[' || s[^1] != ']') return null;
            foreach (var part in s.Substring(3, s.Length - 4).Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split('=', 2);
                if (kv.Length != 2) return null;
                switch (kv[0])
                {
                    case "distance" when kv[1].StartsWith("..", StringComparison.Ordinal) && int.TryParse(kv[1].Substring(2), out var d): sel.MaxDistance = d; break;
                    case "limit" when int.TryParse(kv[1], out var l): sel.Limit = l; break;
                    case "sort": sel.Sort = kv[1]; break;
                    case "gamemode": sel.GameMode = kv[1]; break;
                    default: return null;
                }
            }
            return sel;
        }

        public static string Explain(string b) => b switch
        {
            "@p" => "The player nearest to where the command runs. From the console that is the nearest to the world spawn.",
            "@a" => "Every player online.",
            "@r" => "One random player.",
            "@s" => "Whoever runs the command. From the console that is no one, so most commands fail with it.",
            "@e" => "Every entity: players, mobs, items on the ground and the rest.",
            _ => "",
        };
    }

    /// <summary>A position written as the game takes it: each axis exact, or relative to the target with ~ and an optional offset.</summary>
    public static class McPosition
    {
        public static string Axis(bool relative, string value)
        {
            value = (value ?? "").Trim();
            if (relative) return value.Length == 0 || value == "0" ? "~" : "~" + value;
            return value;
        }

        public static string Join(IEnumerable<(bool Relative, string Value)> axes) => string.Join(" ", axes.Select(a => Axis(a.Relative, a.Value)));

        /// <summary>Reads "Pos: [12.5d, 64.0d, -3.25d]" from the reply to data get entity, as numbers.</summary>
        public static double[] FromDataReply(string reply)
        {
            if (string.IsNullOrEmpty(reply)) return null;
            int at = reply.IndexOf('[');
            int end = at < 0 ? -1 : reply.IndexOf(']', at);
            if (at < 0 || end < 0) return null;
            var parts = reply.Substring(at + 1, end - at - 1).Split(',');
            if (parts.Length != 3) return null;
            var r = new double[3];
            for (int i = 0; i < 3; i++)
                if (!double.TryParse(parts[i].Trim().TrimEnd('d', 'D', 'f', 'F'), NumberStyles.Float, CultureInfo.InvariantCulture, out r[i])) return null;
            return r;
        }
    }
}
