using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace MonitoringTools.Minecraft
{
    /// <summary>One data component on an item: its id and its value as SNBT text.</summary>
    public sealed class McComponent
    {
        public string Key = "", Value = "";
    }

    /// <summary>
    /// Item components (Java 1.20.5 and newer), written in the form each version takes: give @a minecraft:diamond_sword[
    /// minecraft:custom_name='"Hi"',minecraft:damage=3]. The text form of a few components changed between versions, so every
    /// builder here takes the version; the generic row accepts any component id from the version's own data component
    /// registry with a value checked as SNBT.
    /// </summary>
    public static class McItemComponents
    {
        public const string Registry = "minecraft:data_component_type";

        /// <summary>The numbers of a version id such as 1.21.4, or null for a snapshot or anything else.</summary>
        public static (int Major, int Minor, int Patch)? Version(string version)
        {
            var p = (version ?? "").Split('.');
            if (p.Length < 2 || !int.TryParse(p[0], out var a) || !int.TryParse(p[1], out var b)) return null;
            int c = p.Length > 2 && int.TryParse(p[2], out var cc) ? cc : 0;
            return (a, b, c);
        }

        private static int Cmp(string version, int minor, int patch)
        {
            var v = Version(version);
            if (v == null) return 1;   // an unrecognised id is treated as newest
            if (v.Value.Major != 1) return v.Value.Major > 1 ? 1 : -1;
            return v.Value.Minor != minor ? v.Value.Minor.CompareTo(minor) : v.Value.Patch.CompareTo(patch);
        }

        /// <summary>True when the version writes item data as components; older versions use NBT after the id.</summary>
        public static bool Supports(string version) => Cmp(version, 20, 5) >= 0;

        /// <summary>Java 1.21.5 writes text components and enchantments as plain SNBT; before that they were JSON text and a levels compound.</summary>
        public static bool PlainSnbt(string version) => Cmp(version, 21, 5) >= 0;

        /// <summary>1.21.4 changed custom_model_data from a number to a compound of floats, flags, strings and colours.</summary>
        public static bool CompoundModelData(string version) => Cmp(version, 21, 4) >= 0;

        public static string Namespaced(string key) => key.Contains(':') ? key : "minecraft:" + key;

        public static string Text(string version, string text)
        {
            if (PlainSnbt(version)) return McSnbt.Quote(text);
            var json = new StringBuilder("\"");
            foreach (var ch in text ?? "")
            {
                if (ch == '"' || ch == '\\') json.Append('\\').Append(ch);
                else if (ch < ' ') json.Append(' ');
                else json.Append(ch);
            }
            return McSnbt.Quote(json.Append('"').ToString());
        }

        /// <summary>The words of a text component this class wrote, or null when the value is some other shape.</summary>
        public static string UnText(string version, string snbt)
        {
            var n = McSnbt.Parse(snbt);
            if (n == null || n.Type != SnbtType.String) return null;
            if (PlainSnbt(version)) return n.Value;
            var v = n.Value;
            if (v.Length >= 2 && v[0] == '"' && v[^1] == '"') return v.Substring(1, v.Length - 2).Replace("\\\"", "\"").Replace("\\\\", "\\");
            return null;
        }

        public static string Lore(string version, IEnumerable<string> lines) => "[" + string.Join(",", lines.Select(l => Text(version, l))) + "]";

        public static List<string> UnLore(string version, string snbt)
        {
            var n = McSnbt.Parse(snbt);
            if (n == null || n.Type != SnbtType.List) return null;
            var lines = new List<string>();
            foreach (var c in n.Items)
            {
                var t = UnText(version, McSnbt.Render(c));
                if (t == null) return null;
                lines.Add(t);
            }
            return lines;
        }

        public static string Enchantments(string version, IEnumerable<KeyValuePair<string, int>> levels)
        {
            var body = string.Join(",", levels.Select(kv => McSnbt.Quote(Namespaced(kv.Key)) + ":" + kv.Value.ToString(CultureInfo.InvariantCulture)));
            return PlainSnbt(version) ? "{" + body + "}" : "{levels:{" + body + "}}";
        }

        public static List<KeyValuePair<string, int>> UnEnchantments(string version, string snbt)
        {
            var n = McSnbt.Parse(snbt);
            if (n == null || n.Type != SnbtType.Compound) return null;
            if (!PlainSnbt(version))
            {
                if (n.Items.Count != 1 || n.Items[0].Name != "levels" || n.Items[0].Type != SnbtType.Compound) return null;
                n = n.Items[0];
            }
            var list = new List<KeyValuePair<string, int>>();
            foreach (var c in n.Items)
            {
                if (c.Type != SnbtType.Int || !int.TryParse(c.Value, out var lvl)) return null;
                list.Add(new KeyValuePair<string, int>(c.Name, lvl));
            }
            return list;
        }

        public static string ModelData(string version, int n) =>
            CompoundModelData(version) ? "{floats:[" + n.ToString(CultureInfo.InvariantCulture) + ".0f]}" : n.ToString(CultureInfo.InvariantCulture);

        public static int? UnModelData(string version, string snbt)
        {
            var n = McSnbt.Parse(snbt);
            if (n == null) return null;
            if (!CompoundModelData(version)) return n.Type == SnbtType.Int && int.TryParse(n.Value, out var i) ? i : null;
            if (n.Type != SnbtType.Compound || n.Items.Count != 1 || n.Items[0].Name != "floats" || n.Items[0].Items.Count != 1) return null;
            return double.TryParse(n.Items[0].Items[0].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? (int)d : null;
        }

        /// <summary>The components of a value as the command takes them: [a=1,b=2], or an empty string for none.</summary>
        public static string Compose(IEnumerable<McComponent> parts)
        {
            var list = parts.Where(p => p.Key.Length > 0).ToList();
            return list.Count == 0 ? "" : "[" + string.Join(",", list.Select(p => Namespaced(p.Key) + "=" + p.Value)) + "]";
        }

        /// <summary>Splits an item argument into its id and what follows it ([components] or {nbt}).</summary>
        public static (string Id, string Suffix) SplitValue(string value)
        {
            value ??= "";
            int at = value.IndexOfAny(new[] { '[', '{' });
            return at < 0 ? (value.Trim(), "") : (value.Substring(0, at).Trim(), value.Substring(at));
        }

        /// <summary>Reads [k=v,k=v] into components; null with the reason when the brackets or a pair are wrong.</summary>
        public static List<McComponent> ReadList(string rest, out string problem)
        {
            problem = null;
            var list = new List<McComponent>();
            if (string.IsNullOrEmpty(rest)) return list;
            if (rest[0] != '[' || rest[^1] != ']' || !McSnbt.Balanced(rest)) { problem = "Item components go in square brackets, like [minecraft:damage=3]."; return null; }
            var inner = rest.Substring(1, rest.Length - 2);
            if (inner.Trim().Length == 0) return list;
            foreach (var part in McSnbt.SplitTop(inner))
            {
                var eq = part.IndexOf('=');
                if (eq < 0 && part.Trim().StartsWith("!", StringComparison.Ordinal)) { list.Add(new McComponent { Key = part.Trim() }); continue; }
                if (eq <= 0) { problem = $"\"{part.Trim()}\" is not a component. Write name=value."; return null; }
                list.Add(new McComponent { Key = part.Substring(0, eq).Trim(), Value = part.Substring(eq + 1).Trim() });
            }
            return list;
        }

        /// <summary>Null when the data after an item id is valid for the version: known component ids with SNBT values, or NBT on older versions.</summary>
        public static string Check(string version, string rest, McRegistries registries)
        {
            if (string.IsNullOrEmpty(rest)) return null;
            if (!Supports(version))
                return rest[0] == '{' && McSnbt.LooksValid(rest) ? null : "Item data here is NBT in curly braces, like {Damage:3}.";
            if (rest[0] == '{') return "This version takes item data as components in square brackets, not NBT.";
            var list = ReadList(rest, out var problem);
            if (list == null) return problem;
            var known = registries?.Get(Registry);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var c in list)
            {
                var key = c.Key.StartsWith("!", StringComparison.Ordinal) ? c.Key.Substring(1) : c.Key;
                if (key.Length == 0 || key.Any(ch => !(char.IsAsciiLetterLower(ch) || char.IsAsciiDigit(ch) || ch == '_' || ch == ':' || ch == '.' || ch == '/' || ch == '-')))
                    return $"\"{c.Key}\" is not a component id.";
                var full = Namespaced(key);
                if (!seen.Add(full)) return $"{full} is listed twice.";
                if (known != null && known.Count > 0 && !known.Contains(full)) return $"{full} is not a component in this version's list.";
                if (c.Key.StartsWith("!", StringComparison.Ordinal)) { if (c.Value.Length > 0) return $"{c.Key} removes a component, so it takes no value."; continue; }
                if (c.Value.Length == 0 || !McSnbt.LooksValid(c.Value)) return $"The value of {full} is not valid SNBT.";
            }
            return null;
        }
    }
}
