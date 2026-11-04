using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace MonitoringTools.Core
{
    /// <summary>
    /// A syntax tree for the .NET regular expression flavour. The parser here understands the full
    /// .NET syntax (literals, escapes, character classes with subtraction, Unicode properties,
    /// anchors, every group kind including balancing groups, lookaround, atomic groups,
    /// conditionals, inline options and comments, and greedy or lazy quantifiers). Every tree is
    /// cross-checked by compiling the same pattern with System.Text.RegularExpressions: when .NET
    /// rejects a pattern, the engine's own message and offset are reported next to whatever
    /// partial tree could be built, and the number of capturing groups must agree with the engine.
    /// Nothing here stores or sends a pattern.
    /// </summary>
    public static class RegexTree
    {
        public const int MaxDepth = 150;

        public enum NodeKind { Pattern, Alternation, Branch, Literal, Escape, Shorthand, Property, Anchor, Any, CharClass, Subtraction, Group, Backreference, Conditional, Condition, Quantifier, Comment, InlineOptions }

        public enum GroupKind { None, Capturing, Named, Balancing, NonCapturing, Atomic, Lookahead, NegativeLookahead, Lookbehind, NegativeLookbehind, Options }

        public sealed class Node
        {
            public NodeKind Kind;
            public GroupKind Group;
            /// <summary>Half-open span in the pattern: Start is the first character, End is one past the last.</summary>
            public int Start, End;
            public string Brief = "", Detailed = "";
            public List<Node> Children = new List<Node>();
            public Node Parent;

            // quantifier: Max of -1 means unbounded; QuantStart is where the quantifier text begins inside the span
            public int Min, Max, QuantStart;
            public bool Lazy;

            // group: the body sits between BodyStart and BodyEnd; Number is the capture number when Captures is true
            public string Name, Other, Options;
            public int Number, BodyStart, BodyEnd;
            public bool Captures;

            // character class: the member text sits between BodyStart and BodyEnd
            public bool Negated;

            public string Text(string pattern) => pattern.Substring(Start, Math.Max(0, End - Start));
            public string Key => Kind + ":" + Start + "-" + End;
            public int Depth { get { var d = 0; for (var n = Parent; n != null; n = n.Parent) d++; return d; } }
        }

        public sealed class Result
        {
            public string Pattern = "";
            public Node Root;
            /// <summary>Set when this parser could not finish the tree.</summary>
            public string Error;
            public int ErrorOffset = -1;
            /// <summary>Set when .NET rejects the pattern: its own message and offset.</summary>
            public string EngineError;
            public int EngineOffset = -1;
            /// <summary>Anything worth saying that is not an error, such as an engine-option conflict.</summary>
            public string Note;
            public int NodeCount;
            public int Captures;
            public bool Valid => Error == null && EngineError == null;
            public string Message => EngineError ?? Error;
            public int MessageOffset => EngineError != null ? EngineOffset : ErrorOffset;
        }

        // ---------------------------------------------------------------- public parse

        public static Result Parse(string pattern, RegexOptions options = RegexOptions.None)
        {
            pattern ??= "";
            var res = new Result { Pattern = pattern };
            if (pattern.Length > RegexKit.MaxPatternLength)
            {
                res.Root = new Node { Kind = NodeKind.Pattern, Start = 0, End = 0, Brief = "Pattern", Detailed = "The pattern is too long to analyse." };
                res.Error = $"Pattern is longer than {RegexKit.MaxPatternLength} characters."; res.ErrorOffset = 0;
                return res;
            }
            var ps = new Parser(pattern, options);
            res.Root = ps.Run();
            res.Error = ps.Err; res.ErrorOffset = ps.ErrPos;
            Finish(res.Root, ps, pattern);
            res.NodeCount = Count(res.Root) - 1;
            res.Captures = ps.CaptureCount;

            // Cross-check against the real engine. Syntax is judged without the options that change which constructs are legal.
            var syntaxOptions = options & ~(RegexOptions.NonBacktracking | RegexOptions.ECMAScript | RegexOptions.RightToLeft);
            try
            {
                var rx = new Regex(pattern, syntaxOptions, RegexKit.Timeout);
                var engineCaptures = rx.GetGroupNumbers().Length - 1;
                if (engineCaptures != ps.CaptureCount && ps.Err == null)
                    res.Note = $"The tree counts {ps.CaptureCount} capturing group(s) but the engine reports {engineCaptures}; trust the engine.";
                if (ps.Err != null && res.Note == null)
                    res.Note = "The engine accepts this pattern but the tree builder did not finish; the tree is partial.";
                if (syntaxOptions != options)
                {
                    try { _ = new Regex(pattern, options, RegexKit.Timeout); }
                    catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException)
                    { res.Note = "The syntax is valid, but the current engine options reject it: " + CleanMessage(ex.Message); }
                }
            }
            catch (RegexParseException ex)
            {
                res.EngineError = CleanMessage(ex.Message); res.EngineOffset = ex.Offset;
            }
            catch (ArgumentException ex) { res.EngineError = CleanMessage(ex.Message); res.EngineOffset = ParseOffset(ex.Message); }
            catch (NotSupportedException ex) { res.EngineError = CleanMessage(ex.Message); res.EngineOffset = ParseOffset(ex.Message); }
            return res;
        }

        private static int ParseOffset(string message)
        {
            var m = Regex.Match(message ?? "", @"at offset (\d+)");
            return m.Success && int.TryParse(m.Groups[1].Value, out var n) ? n : -1;
        }

        private static string CleanMessage(string message)
        {
            message = (message ?? "").Split('\n')[0].Trim();
            var m = Regex.Match(message, @"at offset \d+\.\s*(.*)$");
            return m.Success ? m.Groups[1].Value.TrimEnd('.') : message;
        }

        private static int Count(Node n) { var c = 1; foreach (var k in n.Children) c += Count(k); return c; }

        public static IEnumerable<Node> Walk(Node n)
        {
            yield return n;
            foreach (var c in n.Children) foreach (var d in Walk(c)) yield return d;
        }

        // ---------------------------------------------------------------- locating nodes

        /// <summary>The deepest node whose span contains the position, or the root when none does.</summary>
        public static Node Deepest(Node root, int pos)
        {
            if (root == null) return null;
            if (pos >= root.End && root.End > 0) pos = root.End - 1;
            var cur = root;
            while (true)
            {
                var next = cur.Children.FirstOrDefault(c => c.Start <= pos && pos < c.End);
                if (next == null) return cur;
                cur = next;
            }
        }

        /// <summary>The smallest node containing the whole range; a zero-length range falls back to Deepest.</summary>
        public static Node Smallest(Node root, int start, int length)
        {
            if (root == null) return null;
            if (length <= 0) return Deepest(root, start);
            var end = start + length;
            var cur = root;
            while (true)
            {
                var next = cur.Children.FirstOrDefault(c => c.Start <= start && end <= c.End);
                if (next == null) return cur;
                cur = next;
            }
        }

        // ---------------------------------------------------------------- the parser

        private sealed class Parser
        {
            private readonly string p;
            private readonly bool explicitCapture;
            private int i, depth;
            private bool x, n;
            public string Err; public int ErrPos = -1;
            public readonly List<Node> Captured = new List<Node>();
            public int CaptureCount;

            public Parser(string pattern, RegexOptions o)
            {
                p = pattern;
                x = (o & RegexOptions.IgnorePatternWhitespace) != 0;
                explicitCapture = (o & RegexOptions.ExplicitCapture) != 0;
                n = explicitCapture;
            }

            private void Fail(int pos, string message) { if (Err == null) { Err = message; ErrPos = pos; } }

            public Node Run()
            {
                var root = new Node { Kind = NodeKind.Pattern, Start = 0, End = p.Length, Brief = "Pattern", Detailed = "The whole pattern" };
                Body(root, false, false);
                while (i < p.Length) // a stray ")" leaves the body loop only when not in a group; Body consumes it, so this is a safety net
                    i++;
                return root;
            }

            private bool Blank(char c) => c == ' ' || c == '\t' || c == '\n' || c == '\r' || c == '\f' || c == '\v';

            /// <summary>Parses alternatives until the end of the pattern or, inside a group, the closing parenthesis.</summary>
            private void Body(Node parent, bool inGroup, bool conditional)
            {
                var branches = new List<Node>();
                var cur = new Node { Kind = NodeKind.Branch, Start = i };
                var bodyStart = i;
                while (i < p.Length)
                {
                    var c = p[i];
                    if (c == ')')
                    {
                        if (inGroup) break;
                        Fail(i, "Too many closing parentheses");
                        var stray = new Node { Kind = NodeKind.Literal, Start = i, End = i + 1, Brief = "\")\"", Detailed = "a closing parenthesis with no group to close (the engine rejects this)" };
                        i++;
                        cur.Children.Add(stray);
                        continue;
                    }
                    if (c == '|')
                    {
                        cur.End = i; branches.Add(cur);
                        i++;
                        cur = new Node { Kind = NodeKind.Branch, Start = i };
                        continue;
                    }
                    var before = i;
                    var item = Item();
                    if (item != null) cur.Children.Add(item);
                    if (i == before) i++; // never stall
                }
                cur.End = i;
                if (branches.Count == 0 && !conditional)
                {
                    foreach (var k in cur.Children) parent.Children.Add(k);
                    return;
                }
                branches.Add(cur);
                if (conditional)
                {
                    for (var b = 0; b < branches.Count; b++)
                    {
                        branches[b].Brief = b == 0 ? "If true" : b == 1 ? "Otherwise" : "Extra alternative";
                        branches[b].Detailed = b == 0 ? "The branch tried when the condition holds" : b == 1 ? "The branch tried when the condition does not hold" : "A third alternative, which the engine rejects in a conditional";
                        parent.Children.Add(branches[b]);
                    }
                    return;
                }
                var alt = new Node { Kind = NodeKind.Alternation, Start = bodyStart, End = i, Brief = $"One of {branches.Count} alternatives", Detailed = $"Match any one of {branches.Count} alternatives, trying them from left to right" };
                for (var b = 0; b < branches.Count; b++)
                {
                    branches[b].Brief = $"Alternative {b + 1}";
                    branches[b].Detailed = $"Alternative {b + 1} of {branches.Count}" + (branches[b].Children.Count == 0 ? " (empty: matches nothing)" : "");
                    alt.Children.Add(branches[b]);
                }
                parent.Children.Add(alt);
            }

            private bool PlainLiteral(char c) =>
                "\\[(.^$|)*+?{".IndexOf(c) < 0 && !(x && (c == '#' || Blank(c)));

            private bool QuantifierAt(int pos, out int len, out int min, out int max, out bool lazy)
            {
                len = 0; min = 0; max = -1; lazy = false;
                if (pos >= p.Length) return false;
                switch (p[pos])
                {
                    case '*': len = 1; min = 0; max = -1; break;
                    case '+': len = 1; min = 1; max = -1; break;
                    case '?': len = 1; min = 0; max = 1; break;
                    case '{':
                    {
                        var j = pos + 1;
                        var ds = j;
                        while (j < p.Length && char.IsAsciiDigit(p[j])) j++;
                        if (j == ds) return false;
                        if (!int.TryParse(p.AsSpan(ds, j - ds), NumberStyles.None, CultureInfo.InvariantCulture, out min)) min = int.MaxValue;
                        max = min;
                        if (j < p.Length && p[j] == ',')
                        {
                            j++;
                            var ms = j;
                            while (j < p.Length && char.IsAsciiDigit(p[j])) j++;
                            if (j == ms) max = -1;
                            else if (!int.TryParse(p.AsSpan(ms, j - ms), NumberStyles.None, CultureInfo.InvariantCulture, out max)) max = int.MaxValue;
                        }
                        if (j >= p.Length || p[j] != '}') return false;
                        len = j + 1 - pos;
                        break;
                    }
                    default: return false;
                }
                if (pos + len < p.Length && p[pos + len] == '?') { lazy = true; len++; }
                return true;
            }

            private Node Item()
            {
                var c = p[i];
                if (x && Blank(c)) { i++; return null; }
                if (x && c == '#')
                {
                    var nl = p.IndexOf('\n', i);
                    var end = nl < 0 ? p.Length : nl + 1;
                    var cm = new Node { Kind = NodeKind.Comment, Start = i, End = end, Brief = "Comment", Detailed = "A comment up to the end of the line (ignored because Ignore whitespace is on)" };
                    i = end;
                    return cm;
                }
                var atom = Atom();
                if (atom == null) return null;
                if (atom.Kind == NodeKind.Comment) return atom;
                var save = i;
                if (x) while (i < p.Length && Blank(p[i])) i++;
                if (QuantifierAt(i, out var len, out var min, out var max, out var lazy))
                {
                    var q = new Node { Kind = NodeKind.Quantifier, Start = atom.Start, QuantStart = i, End = i + len, Min = min, Max = max, Lazy = lazy };
                    i += len;
                    q.Children.Add(atom);
                    q.Brief = QuantBrief(min, max, lazy);
                    q.Detailed = $"Repeat the previous item {QuantPhrase(min, max)}" + (lazy ? ", as few times as possible" : max == 1 && min == 0 ? ", preferring to match it" : ", as many times as possible");
                    if (max != -1 && min > max) Fail(q.QuantStart, "Range in reverse order: the minimum is larger than the maximum");
                    return q;
                }
                i = save;
                return atom;
            }

            private Node Atom()
            {
                var s = i;
                var c = p[i];
                switch (c)
                {
                    case '\\': return Escape();
                    case '[': return Class();
                    case '(': return GroupNode();
                    case '.': i++; return new Node { Kind = NodeKind.Any, Start = s, End = i, Brief = "Any character", Detailed = "any character except a line break (any character with Singleline)" };
                    case '^': i++; return new Node { Kind = NodeKind.Anchor, Start = s, End = i, Brief = "Start", Detailed = "the start of the text (of each line with Multiline)" };
                    case '$': i++; return new Node { Kind = NodeKind.Anchor, Start = s, End = i, Brief = "End", Detailed = "the end of the text, or before a final line break (of each line with Multiline)" };
                    case '*':
                    case '+':
                    case '?':
                        i++;
                        Fail(s, "Quantifier '" + c + "' following nothing");
                        return new Node { Kind = NodeKind.Literal, Start = s, End = i, Brief = "\"" + c + "\"", Detailed = "a quantifier with nothing before it to repeat (the engine rejects this)" };
                    case '{':
                        i++;
                        return new Node { Kind = NodeKind.Literal, Start = s, End = i, Brief = "\"{\"", Detailed = "a literal opening brace (it does not start a valid quantifier here)" };
                }
                // a run of ordinary characters; leave the last one out when a quantifier follows, because it binds to that character alone
                var j = i;
                while (j < p.Length && PlainLiteral(p[j])) j++;
                if (j == i) j = i + 1; // e.g. whitespace-sensitive edge: take one character
                if (j - i > 1 && QuantifierAt(x ? SkipBlanks(j) : j, out _, out _, out _, out _)) j--;
                if (char.IsHighSurrogate(p[j - 1]) && j < p.Length && char.IsLowSurrogate(p[j])) j++;
                i = j;
                var text = p.Substring(s, j - s);
                return new Node { Kind = NodeKind.Literal, Start = s, End = j, Brief = "\"" + Shown(text) + "\"", Detailed = text.Length == 1 ? "the character " + Describe(text[0]) : "the text \"" + Shown(text) + "\"" };
            }

            private int SkipBlanks(int pos) { while (pos < p.Length && Blank(p[pos])) pos++; return pos; }

            // ------------------------------------------------------------ escapes

            private Node Escape()
            {
                var s = i;
                if (i + 1 >= p.Length)
                {
                    i++;
                    Fail(s, "Illegal \\ at end of pattern");
                    return new Node { Kind = NodeKind.Escape, Start = s, End = i, Brief = "\\", Detailed = "a backslash with nothing after it (the engine rejects this)" };
                }
                var e = p[i + 1];
                Node make(NodeKind k, int len, string brief, string detailed)
                {
                    i = s + len;
                    return new Node { Kind = k, Start = s, End = s + len, Brief = brief, Detailed = detailed };
                }
                switch (e)
                {
                    case 'd': return make(NodeKind.Shorthand, 2, "Digit", "one digit (a decimal digit in any script)");
                    case 'D': return make(NodeKind.Shorthand, 2, "Not a digit", "one character that is not a digit");
                    case 'w': return make(NodeKind.Shorthand, 2, "Word character", "one word character (a letter, digit, connector such as _)");
                    case 'W': return make(NodeKind.Shorthand, 2, "Not a word character", "one character that is not a word character");
                    case 's': return make(NodeKind.Shorthand, 2, "Whitespace", "one whitespace character (space, tab, line break and similar)");
                    case 'S': return make(NodeKind.Shorthand, 2, "Not whitespace", "one character that is not whitespace");
                    case 'b': return make(NodeKind.Anchor, 2, "Word boundary", "a word boundary: between a word character and a non-word character");
                    case 'B': return make(NodeKind.Anchor, 2, "Not a word boundary", "a position that is not a word boundary");
                    case 'A': return make(NodeKind.Anchor, 2, "Start of text", "the very start of the text, even with Multiline");
                    case 'Z': return make(NodeKind.Anchor, 2, "End of text (before line break)", "the end of the text, or just before a final line break");
                    case 'z': return make(NodeKind.Anchor, 2, "Very end of text", "the very end of the text");
                    case 'G': return make(NodeKind.Anchor, 2, "End of previous match", "the position where the previous match ended");
                    case 'n': return make(NodeKind.Escape, 2, "Line feed", "a line feed (U+000A)");
                    case 'r': return make(NodeKind.Escape, 2, "Carriage return", "a carriage return (U+000D)");
                    case 't': return make(NodeKind.Escape, 2, "Tab", "a tab (U+0009)");
                    case 'f': return make(NodeKind.Escape, 2, "Form feed", "a form feed (U+000C)");
                    case 'v': return make(NodeKind.Escape, 2, "Vertical tab", "a vertical tab (U+000B)");
                    case 'a': return make(NodeKind.Escape, 2, "Bell", "the bell character (U+0007)");
                    case 'e': return make(NodeKind.Escape, 2, "Escape character", "the escape character (U+001B)");
                    case 'p':
                    case 'P':
                    {
                        var neg = e == 'P';
                        if (i + 2 < p.Length && p[i + 2] == '{')
                        {
                            var close = p.IndexOf('}', i + 3);
                            if (close > 0)
                            {
                                var name = p.Substring(i + 3, close - i - 3);
                                return make(NodeKind.Property, close + 1 - s, (neg ? "Not " : "") + "Unicode " + name,
                                    (neg ? "one character not in" : "one character in") + " the Unicode category or block " + name);
                            }
                        }
                        Fail(s, "Incomplete \\p{X} character escape");
                        return make(NodeKind.Property, 2, "\\" + e, "an incomplete Unicode property escape (the engine rejects this)");
                    }
                    case 'k':
                    {
                        if (i + 2 < p.Length && (p[i + 2] == '<' || p[i + 2] == '\''))
                        {
                            var closeCh = p[i + 2] == '<' ? '>' : '\'';
                            var close = p.IndexOf(closeCh, i + 3);
                            if (close > 0)
                            {
                                var name = p.Substring(i + 3, close - i - 3);
                                return make(NodeKind.Backreference, close + 1 - s, "Same as group " + name, "the same text that the group \"" + name + "\" matched");
                            }
                        }
                        Fail(s, "Malformed \\k<...> named back reference");
                        return make(NodeKind.Backreference, 2, "\\k", "an incomplete named backreference (the engine rejects this)");
                    }
                    case 'x':
                    {
                        if (i + 3 < p.Length + 0 && IsHex(p, i + 2, 2)) return make(NodeKind.Escape, 4, "U+00" + p.Substring(i + 2, 2).ToUpperInvariant(), "the character " + Describe((char)Convert.ToInt32(p.Substring(i + 2, 2), 16)));
                        Fail(s, "Insufficient hexadecimal digits");
                        return make(NodeKind.Escape, 2, "\\x", "an incomplete hexadecimal escape (the engine rejects this)");
                    }
                    case 'u':
                    {
                        if (IsHex(p, i + 2, 4)) return make(NodeKind.Escape, 6, "U+" + p.Substring(i + 2, 4).ToUpperInvariant(), "the character " + Describe((char)Convert.ToInt32(p.Substring(i + 2, 4), 16)));
                        Fail(s, "Insufficient hexadecimal digits");
                        return make(NodeKind.Escape, 2, "\\u", "an incomplete Unicode escape (the engine rejects this)");
                    }
                    case 'c':
                    {
                        if (i + 2 < p.Length && char.IsAsciiLetter(p[i + 2])) return make(NodeKind.Escape, 3, "Ctrl+" + char.ToUpperInvariant(p[i + 2]), "the control character Ctrl+" + char.ToUpperInvariant(p[i + 2]));
                        Fail(s, "Missing control character");
                        return make(NodeKind.Escape, 2, "\\c", "an incomplete control-character escape (the engine rejects this)");
                    }
                }
                if (e >= '1' && e <= '9')
                {
                    var j = i + 1;
                    while (j < p.Length && char.IsAsciiDigit(p[j])) j++;
                    var num = p.Substring(i + 1, j - i - 1);
                    return make(NodeKind.Backreference, j - s, "Same as group " + num, "the same text that group " + num + " matched (or an octal character code when there is no such group)");
                }
                if (e == '0')
                {
                    var j = i + 2;
                    while (j < p.Length && j < i + 4 && p[j] >= '0' && p[j] <= '7') j++;
                    return make(NodeKind.Escape, j - s, "Octal " + p.Substring(i + 1, j - i - 1), "the character with octal code " + p.Substring(i + 1, j - i - 1));
                }
                // any other escaped character stands for itself
                var len = char.IsHighSurrogate(e) && i + 2 < p.Length ? 3 : 2;
                return make(NodeKind.Escape, len, "Literal " + Describe(e), "the literal character " + Describe(e));
            }

            private static bool IsHex(string s, int at, int count)
            {
                if (at + count > s.Length) return false;
                for (var k = 0; k < count; k++) if (!char.IsAsciiHexDigit(s[at + k])) return false;
                return true;
            }

            // ------------------------------------------------------------ character classes

            private Node Class()
            {
                var s = i;
                if (++depth > MaxDepth) { depth--; Fail(s, "Character classes are nested too deeply"); i = p.Length; return null; }
                var cls = new Node { Kind = NodeKind.CharClass, Start = s };
                i++; // [
                if (i < p.Length && p[i] == '^') { cls.Negated = true; i++; }
                cls.BodyStart = i;
                cls.BodyEnd = -1;
                var first = true;
                var closed = false;
                while (i < p.Length)
                {
                    var c = p[i];
                    if (c == ']' && !first) { if (cls.BodyEnd < 0) cls.BodyEnd = i; i++; closed = true; break; }
                    first = false;
                    if (c == '\\' && i + 1 < p.Length)
                    {
                        if ((p[i + 1] == 'p' || p[i + 1] == 'P') && i + 2 < p.Length && p[i + 2] == '{')
                        {
                            var close = p.IndexOf('}', i + 3);
                            i = close < 0 ? i + 2 : close + 1;
                        }
                        else i += 2;
                        continue;
                    }
                    if (c == '-' && i + 1 < p.Length && p[i + 1] == '[')
                    {
                        cls.BodyEnd = i;
                        i++;
                        var sub = Class();
                        if (sub != null)
                        {
                            sub.Kind = NodeKind.Subtraction;
                            cls.Children.Add(sub);
                        }
                        if (i < p.Length && p[i] == ']') { i++; closed = true; }
                        break;
                    }
                    i++;
                }
                depth--;
                if (!closed) { Fail(s, "Unterminated [] set"); if (cls.BodyEnd < 0) cls.BodyEnd = i; }
                cls.End = i;
                var body = p.Substring(cls.BodyStart, Math.Max(0, cls.BodyEnd - cls.BodyStart));
                var set = cls.Negated ? "[^" + body + "]" : "[" + body + "]";
                cls.Brief = (cls.Negated ? "Any except " : "One of ") + Shown(body);
                cls.Detailed = (cls.Negated ? "one character that is not in the set " : "one character from the set ") + Shown(set);
                if (cls.Children.Count > 0) cls.Detailed += ", with the subtracted set removed";
                return cls;
            }

            // ------------------------------------------------------------ groups

            private Node GroupNode()
            {
                var s = i;
                if (++depth > MaxDepth) { depth--; Fail(s, "Groups are nested too deeply"); i = p.Length; return null; }
                var savedX = x; var savedN = n;
                var g = new Node { Kind = NodeKind.Group, Start = s };
                i++; // (
                var conditional = false;
                if (i >= p.Length || p[i] != '?')
                {
                    g.Group = GroupKind.Capturing; g.Captures = !n;
                    g.BodyStart = i;
                }
                else
                {
                    i++; // ?
                    if (i >= p.Length) { Fail(s, "Unrecognized grouping construct"); g.End = p.Length; g.BodyStart = g.BodyEnd = p.Length; g.Group = GroupKind.NonCapturing; depth--; return g; }
                    var c = p[i];
                    switch (c)
                    {
                        case ':': g.Group = GroupKind.NonCapturing; i++; break;
                        case '=': g.Group = GroupKind.Lookahead; i++; break;
                        case '!': g.Group = GroupKind.NegativeLookahead; i++; break;
                        case '>': g.Group = GroupKind.Atomic; i++; break;
                        case '#':
                        {
                            var close = p.IndexOf(')', i);
                            if (close < 0) { Fail(s, "Unterminated (?#...) comment"); close = p.Length - 1; }
                            i = close + 1;
                            depth--;
                            return new Node { Kind = NodeKind.Comment, Start = s, End = i, Brief = "Comment", Detailed = "a comment, ignored when matching" };
                        }
                        case '<':
                            if (i + 1 < p.Length && p[i + 1] == '=') { g.Group = GroupKind.Lookbehind; i += 2; }
                            else if (i + 1 < p.Length && p[i + 1] == '!') { g.Group = GroupKind.NegativeLookbehind; i += 2; }
                            else Named(g, '>', s);
                            break;
                        case '\'': Named(g, '\'', s); break;
                        case '(': conditional = true; g.Kind = NodeKind.Conditional; g.Group = GroupKind.None; break;
                        default:
                        {
                            var j = i;
                            while (j < p.Length && "imnsx-".IndexOf(p[j]) >= 0) j++;
                            if (j < p.Length && p[j] == ':')
                            {
                                g.Group = GroupKind.Options; g.Options = p.Substring(i, j - i);
                                ApplyOptions(g.Options);
                                i = j + 1;
                            }
                            else if (j < p.Length && p[j] == ')' && j > i)
                            {
                                var opts = p.Substring(i, j - i);
                                ApplyOptions(opts);
                                // inline options last until the end of the enclosing group, so the saved flags must not be restored
                                savedX = x; savedN = n;
                                i = j + 1;
                                depth--;
                                return new Node { Kind = NodeKind.InlineOptions, Group = GroupKind.Options, Options = opts, Start = s, End = i, Brief = "Options " + opts, Detailed = DescribeOptions(opts, false) };
                            }
                            else
                            {
                                Fail(s, "Unrecognized grouping construct");
                                g.Group = GroupKind.NonCapturing;
                            }
                            break;
                        }
                    }
                }
                if (conditional)
                {
                    // i is at the "(" that opens the condition
                    Node cond;
                    if (i + 1 < p.Length && p[i + 1] == '?')
                    {
                        var inner = GroupNode();
                        cond = new Node { Kind = NodeKind.Condition, Start = inner?.Start ?? i, End = inner?.End ?? i, Brief = "Condition", Detailed = "the condition: this look-around test must succeed" };
                        if (inner != null) cond.Children.Add(inner);
                    }
                    else
                    {
                        var close = p.IndexOf(')', i);
                        if (close < 0) { Fail(s, "Illegal conditional (?(...)) expression"); close = p.Length - 1; }
                        var refName = p.Substring(i + 1, Math.Max(0, close - i - 1));
                        cond = new Node { Kind = NodeKind.Condition, Start = i, End = close + 1, Brief = "Condition: group " + refName, Detailed = "the condition: the group \"" + refName + "\" has captured something" };
                        i = close + 1;
                    }
                    g.Children.Add(cond);
                    g.BodyStart = i;
                    Body(g, true, true);
                }
                else
                {
                    g.BodyStart = i;
                    Body(g, true, false);
                }
                g.BodyEnd = i;
                if (i < p.Length && p[i] == ')') i++;
                else Fail(s, "Not enough )'s");
                g.End = i;
                if (g.Kind == NodeKind.Conditional)
                {
                    g.Brief = "Conditional"; g.Detailed = "if the condition holds, match the first branch, otherwise the second";
                }
                x = savedX; n = savedN;
                depth--;
                if (g.Captures) Captured.Add(g);
                return g;
            }

            private void Named(Node g, char close, int s)
            {
                i++; // < or '
                var e = p.IndexOf(close, i);
                if (e < 0) { Fail(s, "Unterminated (?<name>) capture"); e = p.Length; }
                var text = p.Substring(i, e - i);
                i = Math.Min(p.Length, e + 1);
                var dash = text.IndexOf('-');
                if (dash >= 0)
                {
                    g.Group = GroupKind.Balancing; g.Name = text[..dash]; g.Other = text[(dash + 1)..];
                    g.Captures = g.Name.Length > 0;
                }
                else { g.Group = GroupKind.Named; g.Name = text; g.Captures = true; }
                if (g.Name.Length == 0 && g.Group == GroupKind.Named) Fail(s, "Capture group name cannot be empty");
            }

            private void ApplyOptions(string opts)
            {
                var on = true;
                foreach (var c in opts)
                {
                    if (c == '-') on = false;
                    else if (c == 'x') x = on;
                    else if (c == 'n') n = on;
                }
            }
        }

        // ---------------------------------------------------------------- numbering and descriptions

        private static void Finish(Node root, Parser ps, string pattern)
        {
            // set parents
            void link(Node nd) { foreach (var c in nd.Children) { c.Parent = nd; link(c); } }
            link(root);

            // .NET numbers unnamed groups first, left to right, then named groups in order of first appearance
            var all = Walk(root).Where(g => g.Kind == NodeKind.Group && g.Captures).OrderBy(g => g.Start).ToList();
            var next = 1;
            foreach (var g in all.Where(g => g.Group == GroupKind.Capturing)) g.Number = next++;
            var byName = new Dictionary<string, int>();
            foreach (var g in all.Where(g => g.Group == GroupKind.Named || g.Group == GroupKind.Balancing))
            {
                if (int.TryParse(g.Name, NumberStyles.None, CultureInfo.InvariantCulture, out var num)) { g.Number = num; byName[g.Name] = num; }
                else if (byName.TryGetValue(g.Name, out var existing)) g.Number = existing;
                else { g.Number = next++; byName[g.Name] = g.Number; }
            }
            ps.CaptureCount = all.Select(g => g.Number).Distinct().Count();

            foreach (var g in Walk(root).Where(g => g.Kind == NodeKind.Group)) DescribeGroup(g);
            // a lookaround used as a condition keeps its own description
        }

        private static void DescribeGroup(Node g)
        {
            switch (g.Group)
            {
                case GroupKind.Capturing:
                    if (g.Captures) { g.Brief = "Group " + g.Number; g.Detailed = $"capturing group {g.Number}: remembers the text it matches"; }
                    else { g.Brief = "Group (not captured)"; g.Detailed = "a group that does not capture, because Explicit capture is on"; }
                    break;
                case GroupKind.Named:
                    g.Brief = $"Group \"{g.Name}\" ({g.Number})";
                    g.Detailed = $"capturing group named \"{g.Name}\" (number {g.Number}): remembers the text it matches";
                    break;
                case GroupKind.Balancing:
                    if (g.Name.Length > 0)
                    {
                        g.Brief = $"Balancing group {g.Name}-{g.Other}";
                        g.Detailed = $"balancing group: matches only while \"{g.Other}\" has a capture, removes the latest \"{g.Other}\" capture, and records the text between that capture and here as a capture of \"{g.Name}\"";
                    }
                    else
                    {
                        g.Brief = $"Balancing group -{g.Other}";
                        g.Detailed = $"balancing group: matches only while \"{g.Other}\" has a capture, and removes the latest \"{g.Other}\" capture without capturing anything itself";
                    }
                    break;
                case GroupKind.NonCapturing: g.Brief = "Group (no capture)"; g.Detailed = "a group that does not capture"; break;
                case GroupKind.Atomic: g.Brief = "Atomic group"; g.Detailed = "an atomic group: once it has matched, the engine never backtracks into it"; break;
                case GroupKind.Lookahead: g.Brief = "Followed by"; g.Detailed = "lookahead: the next text must match this, but it is not consumed"; break;
                case GroupKind.NegativeLookahead: g.Brief = "Not followed by"; g.Detailed = "negative lookahead: the next text must not match this"; break;
                case GroupKind.Lookbehind: g.Brief = "Preceded by"; g.Detailed = "lookbehind: the text just before must match this, and it is not consumed"; break;
                case GroupKind.NegativeLookbehind: g.Brief = "Not preceded by"; g.Detailed = "negative lookbehind: the text just before must not match this"; break;
                case GroupKind.Options: g.Brief = "Options " + g.Options; g.Detailed = DescribeOptions(g.Options, true); break;
            }
        }

        private static string DescribeOptions(string opts, bool scoped)
        {
            var on = true; var onList = new List<string>(); var offList = new List<string>();
            foreach (var c in opts)
            {
                if (c == '-') { on = false; continue; }
                var name = c switch { 'i' => "ignore case", 'm' => "multiline", 'n' => "explicit capture", 's' => "singleline", 'x' => "ignore pattern whitespace", _ => null };
                if (name != null) (on ? onList : offList).Add(name);
            }
            var parts = new List<string>();
            if (onList.Count > 0) parts.Add("turn on " + string.Join(", ", onList));
            if (offList.Count > 0) parts.Add("turn off " + string.Join(", ", offList));
            var what = parts.Count == 0 ? "change no options" : string.Join(" and ", parts);
            return scoped ? "a group that applies these options only inside it: " + what : "from here to the end of the enclosing group: " + what;
        }

        private static string QuantPhrase(int min, int max)
        {
            if (max == -1) return min == 0 ? "zero or more times" : min == 1 ? "one or more times" : $"{min} or more times";
            if (min == max) return min == 1 ? "exactly once" : $"exactly {min} times";
            if (min == 0 && max == 1) return "zero or one time";
            return $"{min} to {max} times";
        }

        private static string QuantBrief(int min, int max, bool lazy)
        {
            var s = max == -1 ? (min == 0 ? "0 or more" : min == 1 ? "1 or more" : min + " or more")
                : min == max ? "Exactly " + min
                : min == 0 && max == 1 ? "Optional"
                : min + " to " + max;
            return s + (lazy ? ", lazy" : "");
        }

        private static string Shown(string s)
        {
            var sb = new StringBuilder();
            foreach (var c in s) sb.Append(c == '\n' ? "\\n" : c == '\r' ? "\\r" : c == '\t' ? "\\t" : c == ' ' ? "\u00B7" : c.ToString());
            return sb.ToString();
        }

        private static string Describe(char c) =>
            c == ' ' ? "space" : c == '\t' ? "tab" : c == '\n' ? "line feed" : c == '\r' ? "carriage return" : char.IsControl(c) ? $"U+{(int)c:X4}" : "\"" + c + "\"";

        // ---------------------------------------------------------------- outline, export

        /// <summary>The tree as indented text, one line per node.</summary>
        public static string Outline(Result r, bool detailed)
        {
            var sb = new StringBuilder();
            void walk(Node n, int depth)
            {
                foreach (var c in n.Children)
                {
                    sb.Append(' ', depth * 2).Append(c.Text(r.Pattern)).Append("  ").Append(detailed ? Cap(c.Detailed) : c.Brief).AppendLine();
                    walk(c, depth + 1);
                }
            }
            walk(r.Root, 0);
            return sb.ToString();
        }

        private static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];

        public static string ToMarkdown(Result r, RegexOptions options, bool detailed)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Regular expression analysis").AppendLine();
            sb.AppendLine("Pattern:").AppendLine().AppendLine("```").AppendLine(r.Pattern).AppendLine("```").AppendLine();
            sb.AppendLine("- Engine: " + RegexKit.EngineLabel(options));
            sb.AppendLine("- Options: " + OptionsText(options));
            sb.AppendLine("- Detail level: " + (detailed ? "detailed" : "brief"));
            sb.AppendLine("- Result: " + (r.Valid ? $"valid, {r.NodeCount} tree node(s), {r.Captures} capturing group(s)" : "not valid: " + r.Message + (r.MessageOffset >= 0 ? $" (at offset {r.MessageOffset})" : "")));
            if (r.Note != null) sb.AppendLine("- Note: " + r.Note);
            sb.AppendLine().AppendLine("## Syntax tree").AppendLine();
            if (r.Root.Children.Count == 0) sb.AppendLine("_Nothing to show._");
            void walk(Node n, int depth)
            {
                foreach (var c in n.Children)
                {
                    sb.Append(' ', depth * 2).Append("- `").Append(c.Text(r.Pattern).Replace("`", "\\`").Replace("\n", "\\n").Replace("\r", "\\r")).Append("` (").Append(c.Start).Append('–').Append(c.End).Append("): ").AppendLine(detailed ? Cap(c.Detailed) : c.Brief);
                    walk(c, depth + 1);
                }
            }
            walk(r.Root, 0);
            sb.AppendLine().AppendLine("Spans are zero-based character offsets, start inclusive and end exclusive. The tree comes from this app's parser for the .NET flavour and is cross-checked by compiling the pattern with the real engine.");
            return sb.ToString();
        }

        public static string ToHtml(Result r, RegexOptions options, bool detailed)
        {
            string h(string s) => WebUtility.HtmlEncode(s ?? "");
            var sb = new StringBuilder();
            sb.AppendLine("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>Regular expression analysis</title>");
            sb.AppendLine("<style>:root{color-scheme:light dark}body{font:15px/1.5 system-ui,sans-serif;max-width:60rem;margin:2rem auto;padding:0 1rem}code,pre{font-family:Consolas,monospace}pre{padding:.75rem 1rem;border-radius:12px;background:rgba(127,127,127,.15);overflow:auto}ul{list-style:none;padding-left:1.25rem;border-left:1px solid rgba(127,127,127,.4)}li{margin:.25rem 0}.span{opacity:.6;font-size:.85em}.bad{color:#b3261e}</style></head><body>");
            sb.AppendLine("<h1>Regular expression analysis</h1><pre>" + h(r.Pattern) + "</pre><ul>");
            sb.AppendLine("<li>Engine: " + h(RegexKit.EngineLabel(options)) + "</li><li>Options: " + h(OptionsText(options)) + "</li><li>Detail level: " + (detailed ? "detailed" : "brief") + "</li>");
            sb.AppendLine("<li" + (r.Valid ? "" : " class=\"bad\"") + ">Result: " + h(r.Valid ? $"valid, {r.NodeCount} tree node(s), {r.Captures} capturing group(s)" : "not valid: " + r.Message + (r.MessageOffset >= 0 ? $" (at offset {r.MessageOffset})" : "")) + "</li>");
            if (r.Note != null) sb.AppendLine("<li>Note: " + h(r.Note) + "</li>");
            sb.AppendLine("</ul><h2>Syntax tree</h2>");
            void walk(Node n)
            {
                if (n.Children.Count == 0) return;
                sb.Append("<ul>");
                foreach (var c in n.Children)
                {
                    sb.Append("<li><code>").Append(h(c.Text(r.Pattern))).Append("</code> <span class=\"span\">(").Append(c.Start).Append('–').Append(c.End).Append(")</span> ").Append(h(detailed ? Cap(c.Detailed) : c.Brief));
                    walk(c);
                    sb.Append("</li>");
                }
                sb.Append("</ul>");
            }
            if (r.Root.Children.Count == 0) sb.Append("<p>Nothing to show.</p>"); else walk(r.Root);
            sb.AppendLine("<p class=\"span\">Spans are zero-based character offsets, start inclusive and end exclusive. The tree comes from the app's parser for the .NET flavour and is cross-checked by compiling the pattern with the real engine.</p></body></html>");
            return sb.ToString();
        }

        private static string OptionsText(RegexOptions o)
        {
            var names = RegexKit.OptionDefs.Where(d => (o & d.Flag) != 0).Select(d => d.Label).ToList();
            if ((o & RegexOptions.IgnoreCase) != 0) names.Insert(0, "Ignore case");
            return names.Count == 0 ? "none" : string.Join(", ", names);
        }

        // ---------------------------------------------------------------- guided editing

        /// <summary>The quantifier text for the given range, such as "*", "+?" or "{2,5}".</summary>
        public static string BuildQuantifier(int min, int max, bool lazy)
        {
            string q;
            if (min == 0 && max == -1) q = "*";
            else if (min == 1 && max == -1) q = "+";
            else if (min == 0 && max == 1) q = "?";
            else if (max == -1) q = "{" + min + ",}";
            else if (min == max) q = "{" + min + "}";
            else q = "{" + min + "," + max + "}";
            return q + (lazy ? "?" : "");
        }

        /// <summary>Rewrites the quantifier of a Quantifier node. Returns null when the range is not valid.</summary>
        public static string ApplyQuantifier(string pattern, Node quantifier, int min, int max, bool lazy)
        {
            if (quantifier == null || quantifier.Kind != NodeKind.Quantifier || min < 0 || (max != -1 && max < min)) return null;
            return pattern.Substring(0, quantifier.QuantStart) + BuildQuantifier(min, max, lazy) + pattern.Substring(quantifier.End);
        }

        /// <summary>The member text of a character class, without the brackets, caret or subtraction.</summary>
        public static string ClassMembers(string pattern, Node cls) =>
            cls == null || cls.Kind != NodeKind.CharClass || cls.BodyEnd < cls.BodyStart ? "" : pattern.Substring(cls.BodyStart, cls.BodyEnd - cls.BodyStart);

        /// <summary>Rewrites the members and negation of a character class, keeping any subtraction. Returns null when there is nothing to build.</summary>
        public static string ApplyClass(string pattern, Node cls, string members, bool negated)
        {
            if (cls == null || cls.Kind != NodeKind.CharClass || string.IsNullOrEmpty(members)) return null;
            var tail = pattern.Substring(cls.BodyEnd, cls.End - cls.BodyEnd); // the subtraction (if any) and the closing bracket
            return pattern.Substring(0, cls.Start) + "[" + (negated ? "^" : "") + members + tail + pattern.Substring(cls.End);
        }

        public static readonly GroupKind[] EditableGroupKinds =
        {
            GroupKind.Capturing, GroupKind.Named, GroupKind.NonCapturing, GroupKind.Atomic,
            GroupKind.Lookahead, GroupKind.NegativeLookahead, GroupKind.Lookbehind, GroupKind.NegativeLookbehind,
        };

        public static bool IsEditableGroup(Node n) => n != null && n.Kind == NodeKind.Group && EditableGroupKinds.Contains(n.Group);

        public static string GroupKindLabel(GroupKind k) => k switch
        {
            GroupKind.Capturing => "Numbered", GroupKind.Named => "Named", GroupKind.NonCapturing => "No capture", GroupKind.Atomic => "Atomic",
            GroupKind.Lookahead => "Followed by", GroupKind.NegativeLookahead => "Not followed by", GroupKind.Lookbehind => "Preceded by", GroupKind.NegativeLookbehind => "Not preceded by",
            _ => k.ToString(),
        };

        public static string Opener(GroupKind kind, string name) => kind switch
        {
            GroupKind.Capturing => "(",
            GroupKind.Named => "(?<" + name + ">",
            GroupKind.NonCapturing => "(?:",
            GroupKind.Atomic => "(?>",
            GroupKind.Lookahead => "(?=",
            GroupKind.NegativeLookahead => "(?!",
            GroupKind.Lookbehind => "(?<=",
            GroupKind.NegativeLookbehind => "(?<!",
            _ => null,
        };

        /// <summary>Rewrites only the opening of a group, keeping its body and closing parenthesis. Returns null for an unusable name or kind.</summary>
        public static string ApplyGroupKind(string pattern, Node group, GroupKind kind, string name)
        {
            if (!IsEditableGroup(group)) return null;
            if (kind == GroupKind.Named && !Regex.IsMatch(name ?? "", @"^\w+$")) return null;
            var opener = Opener(kind, name);
            if (opener == null) return null;
            return pattern.Substring(0, group.Start) + opener + pattern.Substring(group.BodyStart);
        }
    }
}
