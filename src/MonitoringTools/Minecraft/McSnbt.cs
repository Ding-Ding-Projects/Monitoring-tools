using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MonitoringTools.Minecraft
{
    public enum SnbtType { Byte, Short, Int, Long, Float, Double, String, List, Compound }

    /// <summary>
    /// One tag of an NBT tree the structured editor builds: a name (when inside a compound), a type and either a value
    /// (the text the user typed for a number or a string) or child tags (for a list or a compound).
    /// </summary>
    public sealed class SnbtNode
    {
        public string Name = "";
        public SnbtType Type = SnbtType.String;
        public string Value = "";
        public List<SnbtNode> Items = new List<SnbtNode>();
        public bool IsContainer => Type == SnbtType.List || Type == SnbtType.Compound;
    }

    /// <summary>
    /// Writes and reads SNBT, the text form of NBT that commands take: {Name:"x",Count:3b,Tags:[1,2]}. The editor builds a
    /// tree and this renders it, so nothing is typed by hand; reading a value back lets an existing command open in the
    /// editor. Typed arrays ([B;1b]) are not part of the editor's vocabulary, so a value that uses one is left as text.
    /// </summary>
    public static class McSnbt
    {
        public static readonly SnbtType[] Types = { SnbtType.Byte, SnbtType.Short, SnbtType.Int, SnbtType.Long, SnbtType.Float, SnbtType.Double, SnbtType.String, SnbtType.List, SnbtType.Compound };

        public static string TypeName(SnbtType t) => t switch
        {
            SnbtType.Byte => "byte", SnbtType.Short => "short", SnbtType.Int => "int", SnbtType.Long => "long", SnbtType.Float => "float",
            SnbtType.Double => "double", SnbtType.String => "string", SnbtType.List => "list", _ => "compound",
        };

        public static string TypeHelp(SnbtType t) => t switch
        {
            SnbtType.Byte => "A whole number from -128 to 127. Also used for true (1) and false (0).",
            SnbtType.Short => "A whole number from -32768 to 32767.",
            SnbtType.Int => "A whole number from -2147483648 to 2147483647.",
            SnbtType.Long => "A very large whole number.",
            SnbtType.Float => "A number with a decimal part, single precision.",
            SnbtType.Double => "A number with a decimal part, double precision.",
            SnbtType.String => "Text.",
            SnbtType.List => "An ordered list of values that are all the same type.",
            _ => "A set of named values.",
        };

        private static readonly Regex BareKey = new Regex(@"^[A-Za-z0-9_.+\-]+$", RegexOptions.CultureInvariant);

        /// <summary>Quotes a string the way the game reads it, with the quote character that needs the fewest escapes.</summary>
        public static string Quote(string s)
        {
            s ??= "";
            int dq = s.Count(c => c == '"'), sq = s.Count(c => c == '\'');
            char q = dq <= sq ? '"' : '\'';
            var sb = new StringBuilder().Append(q);
            foreach (var c in s)
            {
                if (c == q || c == '\\') sb.Append('\\');
                sb.Append(c);
            }
            return sb.Append(q).ToString();
        }

        public static string Key(string name) => BareKey.IsMatch(name ?? "") ? name : Quote(name);

        /// <summary>The SNBT of one tag's value (its name is not part of it).</summary>
        public static string Render(SnbtNode n)
        {
            switch (n.Type)
            {
                case SnbtType.Compound:
                    return "{" + string.Join(",", n.Items.Select(c => Key(c.Name) + ":" + Render(c))) + "}";
                case SnbtType.List:
                    return "[" + string.Join(",", n.Items.Select(Render)) + "]";
                case SnbtType.String:
                    return Quote(n.Value);
                case SnbtType.Byte: return n.Value.Trim() + "b";
                case SnbtType.Short: return n.Value.Trim() + "s";
                case SnbtType.Long: return n.Value.Trim() + "L";
                case SnbtType.Float: return n.Value.Trim() + "f";
                case SnbtType.Double: return (n.Value.Contains('.') || n.Value.Contains('e') || n.Value.Contains('E') ? n.Value.Trim() : n.Value.Trim() + ".0") + "d";
                default: return n.Value.Trim();
            }
        }

        /// <summary>Null when the value fits the tag's type, otherwise a plain reason.</summary>
        public static string CheckValue(SnbtType t, string value)
        {
            var v = (value ?? "").Trim();
            string Whole(long min, long max, string what)
            {
                if (!long.TryParse(v, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n)) return $"A {what} is a whole number.";
                return n < min || n > max ? $"A {what} is from {min.ToString(CultureInfo.InvariantCulture)} to {max.ToString(CultureInfo.InvariantCulture)}." : null;
            }
            switch (t)
            {
                case SnbtType.Byte: return Whole(sbyte.MinValue, sbyte.MaxValue, "byte");
                case SnbtType.Short: return Whole(short.MinValue, short.MaxValue, "short");
                case SnbtType.Int: return Whole(int.MinValue, int.MaxValue, "int");
                case SnbtType.Long: return Whole(long.MinValue, long.MaxValue, "long");
                case SnbtType.Float:
                case SnbtType.Double:
                    return double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out _) ? null : $"A {TypeName(t)} is a number.";
                default: return null;
            }
        }

        /// <summary>Null when the whole tree is valid: every number fits its type, every compound key is named once, every list is one type.</summary>
        public static string Validate(SnbtNode n, string path = "")
        {
            if (n.Type == SnbtType.Compound)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var c in n.Items)
                {
                    var here = path.Length == 0 ? c.Name : path + "." + c.Name;
                    if (string.IsNullOrWhiteSpace(c.Name)) return "A key in " + (path.Length == 0 ? "the value" : path) + " has no name.";
                    if (!seen.Add(c.Name)) return $"The key {c.Name} is used twice in {(path.Length == 0 ? "the value" : path)}.";
                    var bad = Validate(c, here);
                    if (bad != null) return bad;
                }
                return null;
            }
            if (n.Type == SnbtType.List)
            {
                int i = 0;
                foreach (var c in n.Items)
                {
                    if (c.Type != n.Items[0].Type) return $"{(path.Length == 0 ? "The list" : path)} mixes {TypeName(n.Items[0].Type)} and {TypeName(c.Type)}; a list holds one type.";
                    var bad = Validate(c, $"{path}[{i++}]");
                    if (bad != null) return bad;
                }
                return null;
            }
            var problem = CheckValue(n.Type, n.Value);
            return problem == null ? null : (path.Length == 0 ? "" : path + ": ") + problem;
        }

        // ------------------------------------------------------------------ reading

        /// <summary>Reads SNBT into a tree; null when it is not valid or uses a form the editor does not build.</summary>
        public static SnbtNode Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            int i = 0;
            try
            {
                var n = ReadValue(text, ref i);
                SkipSpace(text, ref i);
                return n != null && i == text.Length ? n : null;
            }
            catch (FormatException) { return null; }
        }

        /// <summary>True when the text is SNBT the game would accept the shape of (typed arrays included, which the editor itself does not build).</summary>
        public static bool LooksValid(string text)
        {
            if (Parse(text) != null) return true;
            return Regex.IsMatch(text ?? "", @"\[[BIL];") && Balanced(text);
        }

        public static bool Balanced(string text)
        {
            var stack = new Stack<char>();
            char quote = '\0';
            for (int i = 0; i < (text ?? "").Length; i++)
            {
                char c = text[i];
                if (quote != '\0') { if (c == '\\') i++; else if (c == quote) quote = '\0'; continue; }
                if (c == '"' || c == '\'') quote = c;
                else if (c == '{' || c == '[') stack.Push(c);
                else if (c == '}' || c == ']')
                {
                    if (stack.Count == 0 || (c == '}') != (stack.Pop() == '{')) return false;
                }
            }
            return quote == '\0' && stack.Count == 0;
        }

        private static void SkipSpace(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        private static SnbtNode ReadValue(string s, ref int i)
        {
            SkipSpace(s, ref i);
            if (i >= s.Length) throw new FormatException();
            char c = s[i];
            if (c == '{')
            {
                i++;
                var n = new SnbtNode { Type = SnbtType.Compound };
                SkipSpace(s, ref i);
                if (i < s.Length && s[i] == '}') { i++; return n; }
                while (true)
                {
                    SkipSpace(s, ref i);
                    var key = ReadKey(s, ref i);
                    SkipSpace(s, ref i);
                    if (i >= s.Length || s[i] != ':') throw new FormatException();
                    i++;
                    var v = ReadValue(s, ref i);
                    v.Name = key;
                    n.Items.Add(v);
                    SkipSpace(s, ref i);
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    if (i < s.Length && s[i] == '}') { i++; return n; }
                    throw new FormatException();
                }
            }
            if (c == '[')
            {
                i++;
                if (i + 1 < s.Length && "BIL".IndexOf(s[i]) >= 0 && s[i + 1] == ';') throw new FormatException();   // typed arrays stay text
                var n = new SnbtNode { Type = SnbtType.List };
                SkipSpace(s, ref i);
                if (i < s.Length && s[i] == ']') { i++; return n; }
                while (true)
                {
                    n.Items.Add(ReadValue(s, ref i));
                    SkipSpace(s, ref i);
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    if (i < s.Length && s[i] == ']') { i++; return n; }
                    throw new FormatException();
                }
            }
            if (c == '"' || c == '\'') return new SnbtNode { Type = SnbtType.String, Value = ReadQuoted(s, ref i) };
            int start = i;
            while (i < s.Length && ",]} \t".IndexOf(s[i]) < 0) i++;
            var word = s.Substring(start, i - start);
            if (word.Length == 0) throw new FormatException();
            return Bare(word);
        }

        private static SnbtNode Bare(string w)
        {
            if (w == "true") return new SnbtNode { Type = SnbtType.Byte, Value = "1" };
            if (w == "false") return new SnbtNode { Type = SnbtType.Byte, Value = "0" };
            char last = w[^1];
            var body = w[..^1];
            if (body.Length > 0)
            {
                switch (char.ToLowerInvariant(last))
                {
                    case 'b' when CheckValue(SnbtType.Byte, body) == null: return new SnbtNode { Type = SnbtType.Byte, Value = body };
                    case 's' when CheckValue(SnbtType.Short, body) == null: return new SnbtNode { Type = SnbtType.Short, Value = body };
                    case 'l' when CheckValue(SnbtType.Long, body) == null: return new SnbtNode { Type = SnbtType.Long, Value = body };
                    case 'f' when CheckValue(SnbtType.Float, body) == null: return new SnbtNode { Type = SnbtType.Float, Value = body };
                    case 'd' when CheckValue(SnbtType.Double, body) == null: return new SnbtNode { Type = SnbtType.Double, Value = body };
                }
            }
            if (CheckValue(SnbtType.Int, w) == null) return new SnbtNode { Type = SnbtType.Int, Value = w };
            if (w.Contains('.') && CheckValue(SnbtType.Double, w) == null) return new SnbtNode { Type = SnbtType.Double, Value = w };
            return new SnbtNode { Type = SnbtType.String, Value = w };   // an unquoted word is a string
        }

        private static string ReadKey(string s, ref int i)
        {
            if (i < s.Length && (s[i] == '"' || s[i] == '\'')) return ReadQuoted(s, ref i);
            int start = i;
            while (i < s.Length && (char.IsLetterOrDigit(s[i]) || "_.+-".IndexOf(s[i]) >= 0)) i++;
            if (i == start) throw new FormatException();
            return s.Substring(start, i - start);
        }

        private static string ReadQuoted(string s, ref int i)
        {
            char q = s[i++];
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '\\' && i < s.Length) { sb.Append(s[i++]); continue; }
                if (c == q) return sb.ToString();
                sb.Append(c);
            }
            throw new FormatException();
        }

        /// <summary>Splits text at the commas that are not inside quotes, brackets or braces.</summary>
        public static List<string> SplitTop(string text, char sep = ',')
        {
            var parts = new List<string>();
            int depth = 0, from = 0;
            char quote = '\0';
            text ??= "";
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (quote != '\0') { if (c == '\\') i++; else if (c == quote) quote = '\0'; continue; }
                if (c == '"' || c == '\'') quote = c;
                else if (c == '{' || c == '[') depth++;
                else if (c == '}' || c == ']') depth--;
                else if (c == sep && depth == 0) { parts.Add(text.Substring(from, i - from)); from = i + 1; }
            }
            parts.Add(text.Substring(from));
            return parts;
        }
    }
}
