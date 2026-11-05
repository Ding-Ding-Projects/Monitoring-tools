using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace MonitoringTools.Core
{
    /// <summary>
    /// A small instrumented backtracking interpreter used only to explain a match attempt step by step.
    /// .NET does not expose the internals of its own engine, so this is a separate implementation of a
    /// clearly limited subset. Before a trace is shown it is checked against System.Text.RegularExpressions
    /// on the same subject; if the two disagree the trace is withheld.
    ///
    /// Supported: literals, ".", character classes (including \d \w \s and their negations, ranges,
    /// negated sets, Unicode general categories and class subtraction such as [a-z-[aeiou]]), anchors
    /// ^ $ \A \z \Z \G, word boundaries \b \B, capturing, non-capturing and named groups, look-ahead and
    /// look-behind, atomic groups, conditionals on a group or on a look-around test, inline options
    /// (?imsn-imsn) and (?imsn-imsn:...), alternation, greedy and lazy * + ? {n} {n,} {n,m}, numbered and
    /// named backreferences. Supported options: ignore case, multiline, singleline, explicit capture,
    /// culture invariant.
    /// Not supported (reported by name, never faked): the ignore-whitespace option x, comments, Unicode
    /// scripts and blocks, a conditional whose condition names no group, octal and control escapes,
    /// right-to-left, non-backtracking and ECMAScript.
    /// </summary>
    public static class RegexTrace
    {
        public const int StepLimit = 100_000;
        public const int MaxSubject = 4000;
        private const int MaxDepth = 100_000;

        public enum StepKind { Attempt, Info, Match, Fail, Backtrack, Enter, Leave, Done }

        public readonly record struct Step(int PatStart, int PatLen, int TextPos, int TextLen, StepKind Kind, string Text);

        public sealed class Result
        {
            public string Subject = "";
            public string Pattern = "";
            /// <summary>Set (and Steps empty) when the pattern, an option or the subject cannot be traced.</summary>
            public string Unsupported;
            /// <summary>Set (and Steps empty) when the interpreter and the .NET engine disagree.</summary>
            public string Disagreement;
            public string Note;
            public List<Step> Steps = new List<Step>();
            public bool LimitHit;
            public bool Matched;
            public int MatchIndex, MatchLength;
            /// <summary>True only when the trace was checked against the real engine and agreed.</summary>
            public bool Verified;
            /// <summary>True when every capture group's position also agreed with the .NET engine, not only the overall match.</summary>
            public bool CapturesVerified;
            public int CaptureCount;
            public bool HasTrace => Unsupported == null && Disagreement == null && Steps.Count > 0;
        }

        // ------------------------------------------------------------------ syntax tree

        private abstract class Node { public int PS, PE; }
        private sealed class LitNode : Node { public char C; public bool IC; }
        private sealed class AnyNode : Node { public bool SL; }
        private sealed class ClassNode : Node
        {
            public bool Negated, IC;
            /// <summary>The set subtracted from this one, as in [a-z-[aeiou]], or null.</summary>
            public ClassNode Subtract;
            public List<(char Lo, char Hi)> Ranges = new List<(char, char)>();
            public List<char> Escapes = new List<char>(); // d D w W s S
            public List<(string Name, bool Not)> Props = new List<(string, bool)>(); // \p{..} and \P{..}
            public string Text;
        }
        private sealed class AnchorNode : Node { public char Kind; public bool ML; } // ^ $ b B A z Z G
        private sealed class GroupNode : Node { public Node Body; public bool Capturing; public string Name; public int Number; }
        private sealed class AltNode : Node { public List<Node> Alts = new List<Node>(); }
        private sealed class SeqNode : Node { public List<Node> Items = new List<Node>(); }
        private sealed class RepeatNode : Node { public Node Child; public int Min, Max; public bool Lazy; }
        private sealed class BackrefNode : Node { public int Number = -1; public string Name; public GroupNode Target; public bool IC; }
        private sealed class LookNode : Node { public Node Body; public bool Ahead, Negative; }
        private sealed class AtomicNode : Node { public Node Body; }
        /// <summary>(?(test)yes|no): the test is a group (RefName, resolved to Target) or a look-around (Test).</summary>
        private sealed class OptNode : Node { public string Opts; }
        private sealed class CondNode : Node { public string RefName; public GroupNode Target; public LookNode Test; public Node Yes, No; }

        // Unicode general categories, by their two-letter names, for \p{..} and \P{..}.
        private static readonly Dictionary<System.Globalization.UnicodeCategory, string> CategoryCodes = new Dictionary<System.Globalization.UnicodeCategory, string>
        {
            [System.Globalization.UnicodeCategory.UppercaseLetter] = "Lu", [System.Globalization.UnicodeCategory.LowercaseLetter] = "Ll", [System.Globalization.UnicodeCategory.TitlecaseLetter] = "Lt",
            [System.Globalization.UnicodeCategory.ModifierLetter] = "Lm", [System.Globalization.UnicodeCategory.OtherLetter] = "Lo", [System.Globalization.UnicodeCategory.NonSpacingMark] = "Mn",
            [System.Globalization.UnicodeCategory.SpacingCombiningMark] = "Mc", [System.Globalization.UnicodeCategory.EnclosingMark] = "Me", [System.Globalization.UnicodeCategory.DecimalDigitNumber] = "Nd",
            [System.Globalization.UnicodeCategory.LetterNumber] = "Nl", [System.Globalization.UnicodeCategory.OtherNumber] = "No", [System.Globalization.UnicodeCategory.SpaceSeparator] = "Zs",
            [System.Globalization.UnicodeCategory.LineSeparator] = "Zl", [System.Globalization.UnicodeCategory.ParagraphSeparator] = "Zp", [System.Globalization.UnicodeCategory.Control] = "Cc",
            [System.Globalization.UnicodeCategory.Format] = "Cf", [System.Globalization.UnicodeCategory.Surrogate] = "Cs", [System.Globalization.UnicodeCategory.PrivateUse] = "Co",
            [System.Globalization.UnicodeCategory.ConnectorPunctuation] = "Pc", [System.Globalization.UnicodeCategory.DashPunctuation] = "Pd", [System.Globalization.UnicodeCategory.OpenPunctuation] = "Ps",
            [System.Globalization.UnicodeCategory.ClosePunctuation] = "Pe", [System.Globalization.UnicodeCategory.InitialQuotePunctuation] = "Pi", [System.Globalization.UnicodeCategory.FinalQuotePunctuation] = "Pf",
            [System.Globalization.UnicodeCategory.OtherPunctuation] = "Po", [System.Globalization.UnicodeCategory.MathSymbol] = "Sm", [System.Globalization.UnicodeCategory.CurrencySymbol] = "Sc",
            [System.Globalization.UnicodeCategory.ModifierSymbol] = "Sk", [System.Globalization.UnicodeCategory.OtherSymbol] = "So", [System.Globalization.UnicodeCategory.OtherNotAssigned] = "Cn",
        };

        private static bool ValidPropertyName(string name) =>
            name.Length == 1 && "LMNPSZC".IndexOf(name[0]) >= 0 || name.Length == 2 && CategoryCodes.ContainsValue(name);

        private static bool PropertyHas(string name, char c)
        {
            var code = CategoryCodes[System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)];
            return name.Length == 1 ? code[0] == name[0] : code == name;
        }

        private sealed class UnsupportedException : Exception { public UnsupportedException(string m) : base(m) { } }
        private sealed class ParseErrorException : Exception { public ParseErrorException(string m) : base(m) { } }
        private sealed class LimitException : Exception { }
        private sealed class DepthException : Exception { }

        // ------------------------------------------------------------------ parser

        private sealed class Parser
        {
            private readonly string p;
            private int i, depth;
            // The options in effect at the current point; inline options change them until the end of the enclosing group.
            private bool ic, ml, sl, nx;
            public readonly List<GroupNode> Groups = new List<GroupNode>();
            public readonly List<BackrefNode> Backrefs = new List<BackrefNode>();
            public readonly List<CondNode> Conditions = new List<CondNode>();

            public Parser(string pattern, RegexOptions o)
            {
                p = pattern;
                ic = (o & RegexOptions.IgnoreCase) != 0; ml = (o & RegexOptions.Multiline) != 0; sl = (o & RegexOptions.Singleline) != 0; nx = (o & RegexOptions.ExplicitCapture) != 0;
            }

            /// <summary>Applies an option string such as "i-ms". The ignore-whitespace option changes how the pattern is read, so it is not traced.</summary>
            private void ApplyOptions(string opts)
            {
                bool on = true;
                foreach (var c in opts)
                {
                    switch (c)
                    {
                        case '-': on = false; break;
                        case 'i': ic = on; break;
                        case 'm': ml = on; break;
                        case 's': sl = on; break;
                        case 'n': nx = on; break;
                        case 'x': if (on) throw new UnsupportedException("the ignore-whitespace option x"); break;
                    }
                }
            }

            public Node ParseAll()
            {
                var n = ParseAlt();
                if (i < p.Length) throw new ParseErrorException("Unbalanced ')' at position " + i);
                // Unnamed groups are numbered first, then named ones, like the real engine.
                int num = 0;
                foreach (var g in Groups.Where(g => g.Name == null)) g.Number = ++num;
                foreach (var g in Groups.Where(g => g.Name != null)) g.Number = ++num;
                foreach (var b in Backrefs)
                {
                    b.Target = b.Name != null ? Groups.FirstOrDefault(g => g.Name == b.Name) : Groups.FirstOrDefault(g => g.Number == b.Number);
                    if (b.Target == null) throw new ParseErrorException("Reference to undefined group " + (b.Name ?? b.Number.ToString()));
                }
                foreach (var c in Conditions)
                {
                    if (c.Test != null) continue;
                    if (c.RefName.Length > 0 && c.RefName.All(char.IsDigit))
                    {
                        var want = int.Parse(c.RefName);
                        c.Target = Groups.FirstOrDefault(g => g.Number == want);
                        if (c.Target == null) throw new ParseErrorException("Reference to undefined group " + c.RefName);
                    }
                    else
                    {
                        c.Target = Groups.FirstOrDefault(g => g.Name == c.RefName);
                        if (c.Target == null) throw new UnsupportedException("conditional (?(" + c.RefName + ")...) whose condition names no group, which .NET tests as an expression");
                    }
                }
                return n;
            }

            private Node ParseAlt()
            {
                if (++depth > 200) throw new ParseErrorException("Groups are nested too deeply to trace.");
                int start = i;
                var alts = new List<Node> { ParseSeq() };
                while (i < p.Length && p[i] == '|') { i++; alts.Add(ParseSeq()); }
                depth--;
                if (alts.Count == 1) return alts[0];
                var a = new AltNode { PS = start, PE = i };
                a.Alts.AddRange(alts);
                return a;
            }

            private Node ParseSeq()
            {
                int start = i;
                var seq = new SeqNode { PS = start };
                while (i < p.Length && p[i] != '|' && p[i] != ')')
                {
                    // (?imsn-imsn) on its own changes the options for the rest of the enclosing group and matches nothing
                    var opts = InlineOptionsAt(i);
                    if (opts != null)
                    {
                        if (i + opts.Length + 3 < p.Length && "*+?".IndexOf(p[i + opts.Length + 3]) >= 0) throw new UnsupportedException("a quantifier after inline options");
                        ApplyOptions(opts);
                        seq.Items.Add(new OptNode { PS = i, PE = i + opts.Length + 3, Opts = opts });
                        i += opts.Length + 3;
                        continue;
                    }
                    seq.Items.Add(ParseQuant(ParseAtom()));
                }
                seq.PE = i;
                return seq.Items.Count == 1 ? seq.Items[0] : seq;
            }

            private Node ParseAtom()
            {
                int s = i;
                char c = p[i];
                switch (c)
                {
                    case '(': return ParseGroup();
                    case '[': return ParseClass();
                    case '.': i++; return new AnyNode { PS = s, PE = i, SL = sl };
                    case '^': case '$': i++; return new AnchorNode { Kind = c, PS = s, PE = i, ML = ml };
                    case '\\': return ParseEscape();
                    case '*': case '+': case '?':
                        throw new ParseErrorException("Quantifier '" + c + "' has nothing to repeat at position " + s);
                    case '{':
                        if (QuantAt(i, out _, out _, out _)) throw new ParseErrorException("Quantifier '{' has nothing to repeat at position " + s);
                        i++; return new LitNode { C = c, PS = s, PE = i, IC = ic };
                    default: i++; return new LitNode { C = c, PS = s, PE = i, IC = ic };
                }
            }

            private Node ParseGroup()
            {
                // Options changed inside a group last until its closing parenthesis.
                bool sic = ic, sml = ml, ssl = sl, snx = nx;
                try { return ParseGroupBody(); }
                finally { ic = sic; ml = sml; sl = ssl; nx = snx; }
            }

            private Node ParseGroupBody()
            {
                int s = i;
                i++; // (
                var g = new GroupNode { PS = s, Capturing = !nx };
                if (i < p.Length && p[i] == '?')
                {
                    string rest = p.Substring(i, Math.Min(3, p.Length - i));
                    if (rest.StartsWith("?:", StringComparison.Ordinal)) { g.Capturing = false; i += 2; }
                    else if (rest.StartsWith("?<=", StringComparison.Ordinal) || rest.StartsWith("?<!", StringComparison.Ordinal)) return ParseLook(s, false, rest[2] == '!', 3);
                    else if (rest.Length >= 3 && rest[1] == '<' && (char.IsLetter(rest[2]) || rest[2] == '_')) { i += 2; g.Name = ReadName('>'); g.Capturing = true; }
                    else if (rest.StartsWith("?'", StringComparison.Ordinal)) { i += 2; g.Name = ReadName('\''); g.Capturing = true; }
                    else if (rest.StartsWith("?=", StringComparison.Ordinal) || rest.StartsWith("?!", StringComparison.Ordinal)) return ParseLook(s, true, rest[1] == '!', 2);
                    else if (rest.StartsWith("?>", StringComparison.Ordinal))
                    {
                        i += 2;
                        var body = ParseAlt();
                        if (i >= p.Length || p[i] != ')') throw new ParseErrorException("Missing ')' for the group opened at position " + s);
                        i++;
                        return new AtomicNode { Body = body, PS = s, PE = i };
                    }
                    else if (rest.StartsWith("?(", StringComparison.Ordinal)) return ParseConditional(s);
                    else if (rest.StartsWith("?#", StringComparison.Ordinal)) throw new UnsupportedException("inline comment (?#...)");
                    else
                    {
                        // (?imsn-imsn:...) sets options for this group only; (?imsn-imsn) on its own is handled in ParseSeq
                        int j = i + 1;
                        while (j < p.Length && "imnsx-".IndexOf(p[j]) >= 0) j++;
                        if (j < p.Length && j > i + 1 && p[j] == ':')
                        {
                            ApplyOptions(p.Substring(i + 1, j - i - 1));
                            i = j + 1;
                            g.Capturing = false;
                        }
                        else throw new UnsupportedException("group construct (" + p.Substring(s, Math.Min(4, p.Length - s)) + "...)");
                    }
                }
                if (g.Capturing) Groups.Add(g);
                g.Body = ParseAlt();
                if (i >= p.Length || p[i] != ')') throw new ParseErrorException("Missing ')' for the group opened at position " + s);
                i++;
                g.PE = i;
                return g;
            }

            private Node ParseConditional(int s)
            {
                i++; // past "?", now at the "(" that opens the condition
                var c = new CondNode { PS = s };
                if (i + 1 < p.Length && p[i + 1] == '?')
                {
                    var test = ParseGroup();
                    c.Test = test as LookNode ?? throw new UnsupportedException("conditional whose condition is not a group name, a group number or a look-around test");
                }
                else
                {
                    int close = p.IndexOf(')', i);
                    if (close < 0) throw new ParseErrorException("Illegal conditional (?(...)) expression");
                    c.RefName = p.Substring(i + 1, close - i - 1);
                    if (!Regex.IsMatch(c.RefName, @"^\w+$")) throw new UnsupportedException("conditional (?(" + c.RefName + ")...) whose condition is an expression rather than a group or a look-around test");
                    i = close + 1;
                }
                var body = ParseAlt();
                if (i >= p.Length || p[i] != ')') throw new ParseErrorException("Missing ')' for the conditional opened at position " + s);
                i++;
                c.PE = i;
                if (body is AltNode alt)
                {
                    if (alt.Alts.Count > 2) throw new ParseErrorException("Too many | in (?()|)");
                    c.Yes = alt.Alts[0]; c.No = alt.Alts[1];
                }
                else c.Yes = body;
                Conditions.Add(c);
                return c;
            }

            /// <summary>The option letters of a (?imsn-imsn) that stands on its own at this position, or null.</summary>
            private string InlineOptionsAt(int at)
            {
                if (at + 2 >= p.Length || p[at] != '(' || p[at + 1] != '?') return null;
                int j = at + 2;
                while (j < p.Length && "imnsx-".IndexOf(p[j]) >= 0) j++;
                return j > at + 2 && j < p.Length && p[j] == ')' ? p.Substring(at + 2, j - at - 2) : null;
            }

            private Node ParseLook(int s, bool ahead, bool negative, int skip)
            {
                i += skip; // past "(" is already consumed by the caller; skip "?=", "?!", "?<=" or "?<!"
                var body = ParseAlt();
                if (i >= p.Length || p[i] != ')') throw new ParseErrorException("Missing ')' for the group opened at position " + s);
                i++;
                return new LookNode { Body = body, Ahead = ahead, Negative = negative, PS = s, PE = i };
            }

            private string ReadName(char close)
            {
                int start = i;
                while (i < p.Length && p[i] != close) i++;
                if (i >= p.Length) throw new ParseErrorException("Unterminated group name");
                var name = p.Substring(start, i - start);
                i++;
                return name;
            }

            private Node ParseEscape()
            {
                int s = i;
                i++;
                if (i >= p.Length) throw new ParseErrorException("A pattern cannot end with a single backslash");
                char e = p[i++];
                switch (e)
                {
                    case 'd': case 'D': case 'w': case 'W': case 's': case 'S':
                    {
                        var cn = new ClassNode { PS = s, PE = i, Text = "\\" + e, IC = ic };
                        cn.Escapes.Add(e);
                        return cn;
                    }
                    case 'b': case 'B': case 'A': case 'z': case 'Z': case 'G': return new AnchorNode { Kind = e, PS = s, PE = i, ML = ml };
                    case 'n': return Lit('\n', s);
                    case 'r': return Lit('\r', s);
                    case 't': return Lit('\t', s);
                    case 'f': return Lit('\f', s);
                    case 'v': return Lit('\v', s);
                    case 'a': return Lit('\a', s);
                    case 'e': return Lit('\u001B', s);
                    case 'x': return Lit(ReadHex(2), s);
                    case 'u': return Lit(ReadHex(4), s);
                    case 'p': case 'P':
                    {
                        var cn = new ClassNode { PS = s, IC = ic };
                        cn.Props.Add(ReadProperty(e == 'P'));
                        cn.PE = i; cn.Text = p.Substring(s, i - s);
                        return cn;
                    }
                    case 'c': throw new UnsupportedException("control character escape \\c");
                    case '0': throw new UnsupportedException("octal escape \\0");
                    case 'k':
                    {
                        if (i < p.Length && (p[i] == '<' || p[i] == '\''))
                        {
                            char close = p[i] == '<' ? '>' : '\'';
                            i++;
                            var name = ReadName(close);
                            var b = new BackrefNode { Name = name, PS = s, PE = i, IC = ic };
                            if (int.TryParse(name, out var n)) { b.Name = null; b.Number = n; }
                            Backrefs.Add(b);
                            return b;
                        }
                        throw new ParseErrorException("\\k must be followed by <name>");
                    }
                    default:
                        if (e >= '1' && e <= '9')
                        {
                            int start = i - 1;
                            while (i < p.Length && char.IsDigit(p[i])) i++;
                            var b = new BackrefNode { Number = int.Parse(p.Substring(start, i - start)), PS = s, PE = i, IC = ic };
                            Backrefs.Add(b);
                            return b;
                        }
                        if (char.IsLetterOrDigit(e)) throw new ParseErrorException("Unrecognized escape sequence \\" + e);
                        return Lit(e, s);
                }
            }

            /// <summary>Reads {Name} after \p or \P. Only the general categories are traced; scripts and blocks are named as unsupported.</summary>
            private (string, bool) ReadProperty(bool not)
            {
                if (i >= p.Length || p[i] != '{') throw new ParseErrorException("\\p must be followed by {name}");
                int close = p.IndexOf('}', i);
                if (close < 0) throw new ParseErrorException("Unterminated \\p{...}");
                var name = p.Substring(i + 1, close - i - 1);
                if (!ValidPropertyName(name)) throw new UnsupportedException("Unicode property \\p{" + name + "} (only the general categories such as L, Lu, Nd are traced)");
                i = close + 1;
                return (name, not);
            }

            private LitNode Lit(char c, int s) => new LitNode { C = c, PS = s, PE = i, IC = ic };

            private char ReadHex(int n)
            {
                if (i + n > p.Length || !int.TryParse(p.AsSpan(i, n), System.Globalization.NumberStyles.HexNumber, null, out var v))
                    throw new ParseErrorException("Not enough hexadecimal digits in the escape");
                i += n;
                return (char)v;
            }

            private Node ParseClass()
            {
                int s = i;
                i++; // [
                var cn = new ClassNode { PS = s, IC = ic };
                if (i < p.Length && p[i] == '^') { cn.Negated = true; i++; }
                bool first = true;
                while (true)
                {
                    if (i >= p.Length) throw new ParseErrorException("Missing ']' for the set opened at position " + s);
                    char c = p[i];
                    if (c == ']' && !first) { i++; break; }
                    first = false;
                    if (c == '-' && i + 1 < p.Length && p[i + 1] == '[') { i++; Subtraction(cn, s); break; }
                    if (c == '\\')
                    {
                        if (i + 1 >= p.Length) throw new ParseErrorException("Missing ']' for the set opened at position " + s);
                        char e = p[i + 1];
                        if ("dDwWsS".IndexOf(e) >= 0) { cn.Escapes.Add(e); i += 2; continue; }
                        if (e == 'p' || e == 'P') { i += 2; cn.Props.Add(ReadProperty(e == 'P')); continue; }
                    }
                    char lo = ReadClassChar();
                    if (i + 1 < p.Length && p[i] == '-' && p[i + 1] != ']')
                    {
                        if (p[i + 1] == '[') { cn.Ranges.Add((lo, lo)); i++; Subtraction(cn, s); break; }
                        i++;
                        if (p[i] == '\\' && i + 1 < p.Length && "dDwWsSpP".IndexOf(p[i + 1]) >= 0) throw new ParseErrorException("A range cannot end with a class escape");
                        char hi = ReadClassChar();
                        if (hi < lo) throw new ParseErrorException("Range in reverse order inside a set");
                        cn.Ranges.Add((lo, hi));
                    }
                    else cn.Ranges.Add((lo, lo));
                }
                cn.PE = i;
                cn.Text = p.Substring(s, i - s);
                return cn;
            }

            /// <summary>Reads the subtracted set of [base-[sub]]; it must be the last thing in the outer set.</summary>
            private void Subtraction(ClassNode cn, int s)
            {
                cn.Subtract = (ClassNode)ParseClass();
                if (i >= p.Length || p[i] != ']') throw new ParseErrorException("A subtraction must be the last element in a character class at position " + s);
                i++;
            }

            private char ReadClassChar()
            {
                char c = p[i++];
                if (c != '\\') return c;
                if (i >= p.Length) throw new ParseErrorException("A pattern cannot end with a single backslash");
                char e = p[i++];
                switch (e)
                {
                    case 'n': return '\n';
                    case 'r': return '\r';
                    case 't': return '\t';
                    case 'f': return '\f';
                    case 'v': return '\v';
                    case 'a': return '\a';
                    case 'e': return '\u001B';
                    case 'b': return '\b';
                    case 'x': return ReadHex(2);
                    case 'u': return ReadHex(4);
                    case 'c': throw new UnsupportedException("control character escape \\c");
                    default:
                        if (char.IsDigit(e)) throw new UnsupportedException("octal escape inside a set");
                        if (char.IsLetter(e)) throw new ParseErrorException("Unrecognized escape sequence \\" + e);
                        return e;
                }
            }

            private bool QuantAt(int pos, out int min, out int max, out int len)
            {
                min = max = len = 0;
                if (pos >= p.Length || p[pos] != '{') return false;
                int j = pos + 1, a = j;
                while (j < p.Length && char.IsDigit(p[j])) j++;
                if (j == a || j - a > 9) return false;
                min = int.Parse(p.Substring(a, j - a));
                max = min;
                if (j < p.Length && p[j] == ',')
                {
                    j++;
                    int b = j;
                    while (j < p.Length && char.IsDigit(p[j])) j++;
                    if (j - b > 9) return false;
                    max = j == b ? int.MaxValue : int.Parse(p.Substring(b, j - b));
                }
                if (j >= p.Length || p[j] != '}') return false;
                len = j + 1 - pos;
                return true;
            }

            private Node ParseQuant(Node atom)
            {
                if (i >= p.Length) return atom;
                int min, max;
                char c = p[i];
                if (c == '*') { min = 0; max = int.MaxValue; i++; }
                else if (c == '+') { min = 1; max = int.MaxValue; i++; }
                else if (c == '?') { min = 0; max = 1; i++; }
                else if (c == '{' && QuantAt(i, out min, out max, out var len)) i += len;
                else return atom;
                if (max < min) throw new ParseErrorException("Quantifier range is in reverse order");
                bool lazy = false;
                if (i < p.Length && p[i] == '?') { lazy = true; i++; }
                if (i < p.Length && (p[i] == '*' || p[i] == '+' || p[i] == '?' || (p[i] == '{' && QuantAt(i, out _, out _, out _))))
                    throw new ParseErrorException("Nested quantifier at position " + i);
                return new RepeatNode { Child = atom, Min = min, Max = max, Lazy = lazy, PS = atom.PS, PE = i };
            }
        }

        // ------------------------------------------------------------------ interpreter

        private sealed class Machine
        {
            private readonly string t;
            private readonly List<Step> steps;
            /// <summary>Where this search began: \G holds only there (0 for the first match).</summary>
            private readonly int scanStart = 0;
            private readonly (int S, int L)[] caps;
            private int depth;

            public Machine(string text, RegexOptions o, List<Step> steps, int groups)
            {
                t = text; this.steps = steps;
                caps = new (int, int)[groups + 1];
                Array.Fill(caps, (-1, 0));
            }

            private void Log(Node n, int pos, int len, StepKind k, string text)
            {
                if (steps.Count >= StepLimit) throw new LimitException();
                steps.Add(new Step(n.PS, n.PE - n.PS, pos, len, k, text));
            }

            public bool Attempt(Node root, int start, out int end)
            {
                Array.Fill(caps, (-1, 0));
                int e = -1;
                bool ok = M(root, start, p => { e = p; return true; });
                end = e;
                return ok;
            }

            public (int S, int L)[] Captures => caps;

            public void LogRoot(Node root, int pos, int len, StepKind k, string text) => Log(root, pos, len, k, text);

            private static string Q(char c)
            {
                switch (c)
                {
                    case '\n': return "'\\n'"; case '\r': return "'\\r'"; case '\t': return "'\\t'"; case ' ': return "' '";
                    default: return char.IsControl(c) ? "'\\u" + ((int)c).ToString("X4") + "'" : "'" + c + "'";
                }
            }

            private string Found(int pos) => pos < t.Length ? "found " + Q(t[pos]) : "reached the end of the text";

            private static bool Eq(char a, char b, bool ic) => a == b || (ic && (char.ToLowerInvariant(a) == char.ToLowerInvariant(b) || char.ToUpperInvariant(a) == char.ToUpperInvariant(b)));

            private static bool IsWord(char c) => char.IsLetterOrDigit(c) || c == '_';

            private static bool EscapeHas(char e, char c)
            {
                switch (e)
                {
                    case 'd': return char.IsDigit(c);
                    case 'D': return !char.IsDigit(c);
                    case 'w': return IsWord(c);
                    case 'W': return !IsWord(c);
                    case 's': return char.IsWhiteSpace(c);
                    default: return !char.IsWhiteSpace(c);
                }
            }

            private bool ClassHas(ClassNode cn, char c)
            {
                bool Raw(char x)
                {
                    foreach (var (lo, hi) in cn.Ranges) if (x >= lo && x <= hi) return true;
                    foreach (var e in cn.Escapes) if (EscapeHas(e, x)) return true;
                    foreach (var (nm, not) in cn.Props) if (PropertyHas(nm, x) != not) return true;
                    return false;
                }
                bool hit = Raw(c) || (cn.IC && (Raw(char.ToLowerInvariant(c)) || Raw(char.ToUpperInvariant(c))));
                bool r = hit != cn.Negated;
                // [base-[sub]]: a character in the subtracted set is taken out after the base set (and its negation) is applied
                if (r && cn.Subtract != null && ClassHas(cn.Subtract, c)) r = false;
                return r;
            }

            private bool AnchorOk(AnchorNode a, int pos)
            {
                switch (a.Kind)
                {
                    case '^': return pos == 0 || (a.ML && t[pos - 1] == '\n');
                    case '$': return pos == t.Length || (t[pos] == '\n' && (a.ML || pos == t.Length - 1));
                    case 'G': return pos == scanStart;
                    case 'A': return pos == 0;
                    case 'z': return pos == t.Length;
                    case 'Z': return pos == t.Length || (t[pos] == '\n' && pos == t.Length - 1);
                    default:
                    {
                        bool before = pos > 0 && IsWord(t[pos - 1]), after = pos < t.Length && IsWord(t[pos]);
                        return (before != after) == (a.Kind == 'b');
                    }
                }
            }

            private static string AnchorName(char k) => k switch
            {
                '^' => "start of text or line", '$' => "end of text or line", 'A' => "start of text", 'z' => "end of text",
                'Z' => "end of text", 'b' => "word boundary", 'G' => "start of this search (\\G)", _ => "not a word boundary",
            };

            private bool M(Node n, int pos, Func<int, bool> k)
            {
                if (++depth > MaxDepth) throw new DepthException();
                try { return Exec(n, pos, k); }
                finally { depth--; }
            }

            private bool Exec(Node n, int pos, Func<int, bool> k)
            {
                switch (n)
                {
                    case LitNode l:
                        if (pos < t.Length && Eq(t[pos], l.C, l.IC)) { Log(n, pos, 1, StepKind.Match, "Literal " + Q(l.C) + " matches " + Q(t[pos])); return k(pos + 1); }
                        Log(n, pos, 0, StepKind.Fail, "Literal " + Q(l.C) + " expected but " + Found(pos)); return false;
                    case AnyNode an:
                        if (pos < t.Length && (an.SL || t[pos] != '\n')) { Log(n, pos, 1, StepKind.Match, "'.' matches " + Q(t[pos])); return k(pos + 1); }
                        Log(n, pos, 0, StepKind.Fail, "'.' needs a character that is not a line break but " + Found(pos)); return false;
                    case ClassNode c:
                        if (pos < t.Length && ClassHas(c, t[pos])) { Log(n, pos, 1, StepKind.Match, c.Text + " matches " + Q(t[pos])); return k(pos + 1); }
                        Log(n, pos, 0, StepKind.Fail, c.Text + " does not match: " + Found(pos)); return false;
                    case AnchorNode a:
                        if (AnchorOk(a, pos)) { Log(n, pos, 0, StepKind.Match, "Position is at the " + AnchorName(a.Kind)); return k(pos); }
                        Log(n, pos, 0, StepKind.Fail, "Position is not at the " + AnchorName(a.Kind)); return false;
                    case SeqNode s: return Seq(s, 0, pos, k);
                    case AltNode alt:
                        for (int j = 0; j < alt.Alts.Count; j++)
                        {
                            Log(alt.Alts[j], pos, 0, StepKind.Info, "Try alternative " + (j + 1) + " of " + alt.Alts.Count);
                            if (M(alt.Alts[j], pos, k)) return true;
                            if (j < alt.Alts.Count - 1) Log(alt.Alts[j], pos, 0, StepKind.Backtrack, "Alternative " + (j + 1) + " failed, back to position " + pos + " for the next one");
                        }
                        return false;
                    case GroupNode g: return Group(g, pos, k);
                    case LookNode lk:
                    {
                        string kind = (lk.Negative ? "negative " : "") + (lk.Ahead ? "look-ahead" : "look-behind");
                        Log(n, pos, 0, StepKind.Enter, "Enter " + kind + ": test without consuming text");
                        var saved = ((int, int)[])caps.Clone();
                        bool found = false;
                        if (lk.Ahead) found = M(lk.Body, pos, e => true);
                        else for (int st = pos; st >= 0 && !found; st--) found = M(lk.Body, st, e => e == pos);
                        if (lk.Negative)
                        {
                            Array.Copy(saved, caps, caps.Length);
                            if (found) { Log(n, pos, 0, StepKind.Fail, "The " + kind + " body matched, so the negative test fails"); return false; }
                            Log(n, pos, 0, StepKind.Leave, "The " + kind + " body did not match, so the negative test passes");
                            return k(pos);
                        }
                        if (!found) { Log(n, pos, 0, StepKind.Fail, "The " + kind + " body did not match here"); return false; }
                        Log(n, pos, 0, StepKind.Leave, "The " + kind + " body matched; continue from position " + pos);
                        if (k(pos)) return true;
                        Array.Copy(saved, caps, caps.Length);
                        return false;
                    }
                    case AtomicNode at:
                    {
                        Log(n, pos, 0, StepKind.Enter, "Enter atomic group: take the first way the body matches and never go back into it");
                        var saved = ((int, int)[])caps.Clone();
                        int end = -1;
                        if (!M(at.Body, pos, e => { end = e; return true; })) { Log(n, pos, 0, StepKind.Fail, "The atomic group body did not match"); return false; }
                        Log(n, pos, end - pos, StepKind.Leave, "Leave atomic group, matched \"" + Clip(t.Substring(pos, end - pos)) + "\" and locked it in");
                        if (k(end)) return true;
                        Array.Copy(saved, caps, caps.Length);
                        Log(n, pos, 0, StepKind.Backtrack, "The rest failed, and an atomic group gives nothing back");
                        return false;
                    }
                    case RepeatNode r: return Rep(r, 0, pos, k);
                    case CondNode cd: return Conditional(cd, pos, k);
                    case OptNode op:
                        Log(n, pos, 0, StepKind.Info, "Inline options (?" + op.Opts + ") apply from here to the end of the enclosing group");
                        return k(pos);
                    case BackrefNode b:
                    {
                        var c = caps[b.Target.Number];
                        string label = "Backreference " + (b.Name != null ? "\\k<" + b.Name + ">" : "\\" + b.Number);
                        if (c.S < 0) { Log(n, pos, 0, StepKind.Fail, label + ": the group has not matched anything"); return false; }
                        var sub = t.Substring(c.S, c.L);
                        if (pos + c.L <= t.Length && string.Compare(t, pos, sub, 0, c.L, b.IC ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) == 0)
                        { Log(n, pos, c.L, StepKind.Match, label + " matches the repeated text \"" + Clip(sub) + "\""); return k(pos + c.L); }
                        Log(n, pos, 0, StepKind.Fail, label + " expected \"" + Clip(sub) + "\" here"); return false;
                    }
                }
                throw new InvalidOperationException();
            }

            private bool Conditional(CondNode cd, int pos, Func<int, bool> k)
            {
                var saved = ((int, int)[])caps.Clone();
                bool truth;
                if (cd.Target != null)
                {
                    truth = caps[cd.Target.Number].S >= 0;
                    string label = cd.Target.Name != null ? "group \"" + cd.Target.Name + "\"" : "group " + cd.Target.Number;
                    Log(cd, pos, 0, StepKind.Info, "Conditional: " + label + (truth ? " has captured text, so try the yes branch" : " has not captured anything, so try the " + (cd.No != null ? "no branch" : "empty no branch")));
                }
                else
                {
                    var lk = cd.Test;
                    string kind = (lk.Negative ? "negative " : "") + (lk.Ahead ? "look-ahead" : "look-behind");
                    Log(cd, pos, 0, StepKind.Enter, "Conditional: test the " + kind + " condition without consuming text");
                    bool found = false;
                    if (lk.Ahead) found = M(lk.Body, pos, e => true);
                    else for (int st = pos; st >= 0 && !found; st--) found = M(lk.Body, st, e => e == pos);
                    if (lk.Negative) Array.Copy(saved, caps, caps.Length);
                    truth = found != lk.Negative;
                    Log(cd, pos, 0, StepKind.Leave, "The condition " + (truth ? "holds, so try the yes branch" : "does not hold, so try the " + (cd.No != null ? "no branch" : "empty no branch")));
                }
                var branch = truth ? cd.Yes : cd.No;
                if (branch == null) { if (k(pos)) return true; }
                else if (M(branch, pos, k)) return true;
                Array.Copy(saved, caps, caps.Length);
                return false;
            }

            private static string Clip(string s) => (s.Length > 24 ? s[..24] + "..." : s).Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");

            private bool Seq(SeqNode s, int i, int pos, Func<int, bool> k)
            {
                if (i == s.Items.Count) return k(pos);
                return M(s.Items[i], pos, p => Seq(s, i + 1, p, k));
            }

            private bool Group(GroupNode g, int pos, Func<int, bool> k)
            {
                string label = g.Capturing ? (g.Name != null ? "named group \"" + g.Name + "\"" : "group " + g.Number) : "non-capturing group";
                Log(g, pos, 0, StepKind.Enter, "Enter " + label);
                bool ok = M(g.Body, pos, p =>
                {
                    var old = g.Capturing ? caps[g.Number] : (-1, 0);
                    if (g.Capturing) caps[g.Number] = (pos, p - pos);
                    Log(g, pos, p - pos, StepKind.Leave, "Leave " + label + (g.Capturing ? ", captured \"" + Clip(t.Substring(pos, p - pos)) + "\"" : ""));
                    if (k(p)) return true;
                    if (g.Capturing) caps[g.Number] = old;
                    return false;
                });
                return ok;
            }

            private bool Rep(RepeatNode r, int count, int pos, Func<int, bool> k)
            {
                string q = (r.Lazy ? "lazy " : "greedy ") + Range(r);
                if (r.Lazy)
                {
                    if (count >= r.Min)
                    {
                        Log(r, pos, 0, StepKind.Info, "Lazy repeat after " + count + ": try the rest of the pattern first");
                        if (k(pos)) return true;
                        if (count < r.Max) Log(r, pos, 0, StepKind.Backtrack, "The rest failed, so take one more iteration (" + (count + 1) + ")");
                    }
                    if (count >= r.Max) return false;
                    return Iterate(r, count, pos, k);
                }
                if (count < r.Max)
                {
                    if (Iterate(r, count, pos, k)) return true;
                }
                else Log(r, pos, 0, StepKind.Info, "Reached the maximum of " + r.Max + " iterations (" + q + ")");
                if (count >= r.Min)
                {
                    if (count < r.Max) Log(r, pos, 0, StepKind.Backtrack, "Give up iteration " + (count + 1) + ", continue after " + count + " iteration" + (count == 1 ? "" : "s"));
                    return k(pos);
                }
                Log(r, pos, 0, StepKind.Fail, "Only " + count + " iteration" + (count == 1 ? "" : "s") + ", at least " + r.Min + " needed");
                return false;
            }

            private bool Iterate(RepeatNode r, int count, int pos, Func<int, bool> k)
            {
                Log(r, pos, 0, StepKind.Info, "Iteration " + (count + 1) + " of " + (r.Lazy ? "lazy " : "greedy ") + Range(r));
                return M(r.Child, pos, p =>
                {
                    // An iteration that consumed nothing cannot make progress; stop looping and move on.
                    if (p == pos) return k(p);
                    return Rep(r, count + 1, p, k);
                });
            }

            private static string Range(RepeatNode r) =>
                r.Min == 0 && r.Max == int.MaxValue ? "*" : r.Min == 1 && r.Max == int.MaxValue ? "+" : r.Min == 0 && r.Max == 1 ? "?" :
                r.Max == int.MaxValue ? "{" + r.Min + ",}" : r.Min == r.Max ? "{" + r.Min + "}" : "{" + r.Min + "," + r.Max + "}";
        }

        // ------------------------------------------------------------------ entry point

        private const RegexOptions Traceable = RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant | RegexOptions.Compiled;

        public static Result Run(string pattern, RegexOptions options, string subject)
        {
            var res = new Result { Pattern = pattern ?? "" };
            pattern ??= "";
            subject ??= "";
            var bad = options & ~Traceable & ~RegexOptions.None;
            if (bad != 0)
            {
                res.Unsupported = "The trace follows the default backtracking engine with ignore case, multiline, singleline and explicit capture only. These options are not supported: " + bad + ".";
                return res;
            }
            if (pattern.Length > RegexKit.MaxPatternLength) { res.Unsupported = "The pattern is longer than " + RegexKit.MaxPatternLength + " characters."; return res; }
            if (subject.Length > MaxSubject)
            {
                subject = subject[..MaxSubject];
                res.Note = "Only the first " + MaxSubject + " characters of the subject are traced.";
            }
            res.Subject = subject;

            Parser parser = new Parser(pattern, options);
            Node root;
            try { root = parser.ParseAll(); }
            catch (UnsupportedException ex) { res.Unsupported = "Not supported by the trace: " + ex.Message + ". The trace never guesses, so no trace is shown for this pattern."; return res; }
            catch (ParseErrorException ex) { res.Unsupported = "The pattern could not be parsed for tracing: " + ex.Message + "."; return res; }
            if (root.PE == 0 && root.PS == 0) { root.PE = pattern.Length; }

            Exception failure = null;
            (int S, int L)[] finalCaps = null;
            var steps = res.Steps;
            var worker = new Thread(() =>
            {
                try
                {
                    var m = new Machine(subject, options, steps, parser.Groups.Count);
                    for (int start = 0; start <= subject.Length; start++)
                    {
                        m.LogRoot(root, start, 0, StepKind.Attempt, "Start a match attempt at position " + start);
                        if (m.Attempt(root, start, out var end))
                        {
                            res.Matched = true; res.MatchIndex = start; res.MatchLength = end - start;
                            finalCaps = ((int, int)[])m.Captures.Clone();
                            m.LogRoot(root, start, end - start, StepKind.Done, "Match found at " + start + ", length " + (end - start));
                            return;
                        }
                        if (start < subject.Length) m.LogRoot(root, start, 0, StepKind.Backtrack, "No match from position " + start + ", move the start forward");
                    }
                    m.LogRoot(root, subject.Length, 0, StepKind.Done, "No match at any start position");
                }
                catch (Exception ex) { failure = ex; }
            }, 128 * 1024 * 1024);
            worker.Start();
            worker.Join();

            if (failure is LimitException)
            {
                res.LimitHit = true;
                res.Note = (res.Note == null ? "" : res.Note + " ") + "Stopped after " + StepLimit.ToString("N0") + " steps. The trace is incomplete, so it was not compared with the real engine.";
                return res;
            }
            if (failure is DepthException) { res.Steps.Clear(); res.Unsupported = "The match nests too deeply to trace safely on this subject."; return res; }
            if (failure != null) { res.Steps.Clear(); res.Unsupported = "The trace failed unexpectedly (" + failure.GetType().Name + ")."; return res; }

            // Agreement check against the real engine, before any trace is shown.
            try
            {
                var rx = new Regex(pattern, options & (RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant), RegexKit.Timeout);
                var real = rx.Match(subject);
                if (real.Success != res.Matched || (real.Success && (real.Index != res.MatchIndex || real.Length != res.MatchLength)))
                {
                    res.Disagreement = "The trace and the .NET engine disagree on this subject (" +
                        (res.Matched ? "trace: match at " + res.MatchIndex + " length " + res.MatchLength : "trace: no match") + "; " +
                        (real.Success ? ".NET: match at " + real.Index + " length " + real.Length : ".NET: no match") + "), so the trace is not shown.";
                    res.Steps.Clear();
                    return res;
                }
                res.Verified = true;
                if (real.Success && finalCaps != null)
                {
                    res.CaptureCount = parser.Groups.Count;
                    for (int gi = 1; gi < finalCaps.Length; gi++)
                    {
                        var rg = real.Groups[gi];
                        var (cs, cl) = finalCaps[gi];
                        bool traceHas = cs >= 0;
                        if (rg.Success != traceHas || traceHas && (rg.Index != cs || rg.Length != cl))
                        {
                            res.Disagreement = "The trace and the .NET engine agree on the match but not on capture group " + gi + " (" +
                                (traceHas ? "trace: " + cs + " length " + cl : "trace: did not take part") + "; " + (rg.Success ? ".NET: " + rg.Index + " length " + rg.Length : ".NET: did not take part") + "), so the trace is not shown.";
                            res.Steps.Clear();
                            res.Verified = false;
                            return res;
                        }
                    }
                    res.CapturesVerified = true;
                }
                else if (real.Success) res.CapturesVerified = true;
            }
            catch (RegexMatchTimeoutException)
            {
                res.Disagreement = "The .NET engine did not finish within " + RegexKit.Timeout.TotalMilliseconds.ToString("0") + " ms on this subject, so the trace could not be checked and is not shown.";
                res.Steps.Clear();
            }
            catch (ArgumentException ex)
            {
                res.Disagreement = "The .NET engine rejects this pattern (" + ex.Message.Split('\n')[0] + "), so no trace is shown.";
                res.Steps.Clear();
            }
            return res;
        }
    }
}
