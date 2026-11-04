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
    /// Supported: literals, ".", character classes (including \d \w \s and their negations, ranges and
    /// negated sets), anchors ^ $ \A \z \Z, word boundaries \b \B, capturing, non-capturing and named
    /// groups, alternation, greedy and lazy * + ? {n} {n,} {n,m}, numbered and named backreferences.
    /// Supported options: ignore case, multiline, singleline, culture invariant.
    /// Not supported (reported by name, never faked): lookaround, atomic groups, conditionals, inline
    /// options, comments, Unicode properties, class subtraction, \G, octal and control escapes,
    /// right-to-left, non-backtracking, ECMAScript, explicit capture and ignore-pattern-whitespace.
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
            public bool HasTrace => Unsupported == null && Disagreement == null && Steps.Count > 0;
        }

        // ------------------------------------------------------------------ syntax tree

        private abstract class Node { public int PS, PE; }
        private sealed class LitNode : Node { public char C; }
        private sealed class AnyNode : Node { }
        private sealed class ClassNode : Node
        {
            public bool Negated;
            public List<(char Lo, char Hi)> Ranges = new List<(char, char)>();
            public List<char> Escapes = new List<char>(); // d D w W s S
            public string Text;
        }
        private sealed class AnchorNode : Node { public char Kind; } // ^ $ b B A z Z
        private sealed class GroupNode : Node { public Node Body; public bool Capturing; public string Name; public int Number; }
        private sealed class AltNode : Node { public List<Node> Alts = new List<Node>(); }
        private sealed class SeqNode : Node { public List<Node> Items = new List<Node>(); }
        private sealed class RepeatNode : Node { public Node Child; public int Min, Max; public bool Lazy; }
        private sealed class BackrefNode : Node { public int Number = -1; public string Name; public GroupNode Target; }

        private sealed class UnsupportedException : Exception { public UnsupportedException(string m) : base(m) { } }
        private sealed class ParseErrorException : Exception { public ParseErrorException(string m) : base(m) { } }
        private sealed class LimitException : Exception { }
        private sealed class DepthException : Exception { }

        // ------------------------------------------------------------------ parser

        private sealed class Parser
        {
            private readonly string p;
            private int i, depth;
            public readonly List<GroupNode> Groups = new List<GroupNode>();
            public readonly List<BackrefNode> Backrefs = new List<BackrefNode>();

            public Parser(string pattern) { p = pattern; }

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
                    seq.Items.Add(ParseQuant(ParseAtom()));
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
                    case '.': i++; return new AnyNode { PS = s, PE = i };
                    case '^': case '$': i++; return new AnchorNode { Kind = c, PS = s, PE = i };
                    case '\\': return ParseEscape();
                    case '*': case '+': case '?':
                        throw new ParseErrorException("Quantifier '" + c + "' has nothing to repeat at position " + s);
                    case '{':
                        if (QuantAt(i, out _, out _, out _)) throw new ParseErrorException("Quantifier '{' has nothing to repeat at position " + s);
                        i++; return new LitNode { C = c, PS = s, PE = i };
                    default: i++; return new LitNode { C = c, PS = s, PE = i };
                }
            }

            private Node ParseGroup()
            {
                int s = i;
                i++; // (
                var g = new GroupNode { PS = s, Capturing = true };
                if (i < p.Length && p[i] == '?')
                {
                    string rest = p.Substring(i, Math.Min(3, p.Length - i));
                    if (rest.StartsWith("?:", StringComparison.Ordinal)) { g.Capturing = false; i += 2; }
                    else if (rest.StartsWith("?<=", StringComparison.Ordinal) || rest.StartsWith("?<!", StringComparison.Ordinal))
                        throw new UnsupportedException("lookbehind (" + p.Substring(s, Math.Min(4, p.Length - s)) + ")");
                    else if (rest.Length >= 3 && rest[1] == '<' && (char.IsLetter(rest[2]) || rest[2] == '_')) { i += 2; g.Name = ReadName('>'); }
                    else if (rest.StartsWith("?'", StringComparison.Ordinal)) { i += 2; g.Name = ReadName('\''); }
                    else if (rest.StartsWith("?=", StringComparison.Ordinal) || rest.StartsWith("?!", StringComparison.Ordinal))
                        throw new UnsupportedException("lookahead (" + p.Substring(s, Math.Min(3, p.Length - s)) + ")");
                    else if (rest.StartsWith("?>", StringComparison.Ordinal)) throw new UnsupportedException("atomic group (?>...)");
                    else if (rest.StartsWith("?(", StringComparison.Ordinal)) throw new UnsupportedException("conditional (?(...)yes|no)");
                    else if (rest.StartsWith("?#", StringComparison.Ordinal)) throw new UnsupportedException("inline comment (?#...)");
                    else throw new UnsupportedException("inline options or group construct (" + p.Substring(s, Math.Min(4, p.Length - s)) + "...)");
                }
                if (g.Capturing) Groups.Add(g);
                g.Body = ParseAlt();
                if (i >= p.Length || p[i] != ')') throw new ParseErrorException("Missing ')' for the group opened at position " + s);
                i++;
                g.PE = i;
                return g;
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
                        var cn = new ClassNode { PS = s, PE = i, Text = "\\" + e };
                        cn.Escapes.Add(e);
                        return cn;
                    }
                    case 'b': case 'B': case 'A': case 'z': case 'Z': return new AnchorNode { Kind = e, PS = s, PE = i };
                    case 'n': return Lit('\n', s);
                    case 'r': return Lit('\r', s);
                    case 't': return Lit('\t', s);
                    case 'f': return Lit('\f', s);
                    case 'v': return Lit('\v', s);
                    case 'a': return Lit('\a', s);
                    case 'e': return Lit('\u001B', s);
                    case 'x': return Lit(ReadHex(2), s);
                    case 'u': return Lit(ReadHex(4), s);
                    case 'p': case 'P': throw new UnsupportedException("Unicode property \\" + e + "{...}");
                    case 'G': throw new UnsupportedException("\\G (continue from the previous match)");
                    case 'c': throw new UnsupportedException("control character escape \\c");
                    case '0': throw new UnsupportedException("octal escape \\0");
                    case 'k':
                    {
                        if (i < p.Length && (p[i] == '<' || p[i] == '\''))
                        {
                            char close = p[i] == '<' ? '>' : '\'';
                            i++;
                            var name = ReadName(close);
                            var b = new BackrefNode { Name = name, PS = s, PE = i };
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
                            var b = new BackrefNode { Number = int.Parse(p.Substring(start, i - start)), PS = s, PE = i };
                            Backrefs.Add(b);
                            return b;
                        }
                        if (char.IsLetterOrDigit(e)) throw new ParseErrorException("Unrecognized escape sequence \\" + e);
                        return Lit(e, s);
                }
            }

            private LitNode Lit(char c, int s) => new LitNode { C = c, PS = s, PE = i };

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
                var cn = new ClassNode { PS = s };
                if (i < p.Length && p[i] == '^') { cn.Negated = true; i++; }
                bool first = true;
                while (true)
                {
                    if (i >= p.Length) throw new ParseErrorException("Missing ']' for the set opened at position " + s);
                    char c = p[i];
                    if (c == ']' && !first) { i++; break; }
                    first = false;
                    if (c == '-' && i + 1 < p.Length && p[i + 1] == '[') throw new UnsupportedException("character class subtraction ([a-z-[aeiou]])");
                    if (c == '\\')
                    {
                        if (i + 1 >= p.Length) throw new ParseErrorException("Missing ']' for the set opened at position " + s);
                        char e = p[i + 1];
                        if ("dDwWsS".IndexOf(e) >= 0) { cn.Escapes.Add(e); i += 2; continue; }
                        if (e == 'p' || e == 'P') throw new UnsupportedException("Unicode property \\" + e + "{...} inside a set");
                    }
                    char lo = ReadClassChar();
                    if (i + 1 < p.Length && p[i] == '-' && p[i + 1] != ']')
                    {
                        if (p[i + 1] == '[') throw new UnsupportedException("character class subtraction ([a-z-[aeiou]])");
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
            private readonly bool ic, ml, sl;
            private readonly List<Step> steps;
            private readonly (int S, int L)[] caps;
            private int depth;

            public Machine(string text, RegexOptions o, List<Step> steps, int groups)
            {
                t = text; this.steps = steps;
                ic = (o & RegexOptions.IgnoreCase) != 0; ml = (o & RegexOptions.Multiline) != 0; sl = (o & RegexOptions.Singleline) != 0;
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

            private bool Eq(char a, char b) => a == b || (ic && (char.ToLowerInvariant(a) == char.ToLowerInvariant(b) || char.ToUpperInvariant(a) == char.ToUpperInvariant(b)));

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
                    return false;
                }
                bool hit = Raw(c) || (ic && (Raw(char.ToLowerInvariant(c)) || Raw(char.ToUpperInvariant(c))));
                return hit != cn.Negated;
            }

            private bool AnchorOk(AnchorNode a, int pos)
            {
                switch (a.Kind)
                {
                    case '^': return pos == 0 || (ml && t[pos - 1] == '\n');
                    case '$': return pos == t.Length || (t[pos] == '\n' && (ml || pos == t.Length - 1));
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
                'Z' => "end of text", 'b' => "word boundary", _ => "not a word boundary",
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
                        if (pos < t.Length && Eq(t[pos], l.C)) { Log(n, pos, 1, StepKind.Match, "Literal " + Q(l.C) + " matches " + Q(t[pos])); return k(pos + 1); }
                        Log(n, pos, 0, StepKind.Fail, "Literal " + Q(l.C) + " expected but " + Found(pos)); return false;
                    case AnyNode:
                        if (pos < t.Length && (sl || t[pos] != '\n')) { Log(n, pos, 1, StepKind.Match, "'.' matches " + Q(t[pos])); return k(pos + 1); }
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
                    case RepeatNode r: return Rep(r, 0, pos, k);
                    case BackrefNode b:
                    {
                        var c = caps[b.Target.Number];
                        string label = "Backreference " + (b.Name != null ? "\\k<" + b.Name + ">" : "\\" + b.Number);
                        if (c.S < 0) { Log(n, pos, 0, StepKind.Fail, label + ": the group has not matched anything"); return false; }
                        var sub = t.Substring(c.S, c.L);
                        if (pos + c.L <= t.Length && string.Compare(t, pos, sub, 0, c.L, ic ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) == 0)
                        { Log(n, pos, c.L, StepKind.Match, label + " matches the repeated text \"" + Clip(sub) + "\""); return k(pos + c.L); }
                        Log(n, pos, 0, StepKind.Fail, label + " expected \"" + Clip(sub) + "\" here"); return false;
                    }
                }
                throw new InvalidOperationException();
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

        private const RegexOptions Traceable = RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.Compiled;

        public static Result Run(string pattern, RegexOptions options, string subject)
        {
            var res = new Result { Pattern = pattern ?? "" };
            pattern ??= "";
            subject ??= "";
            var bad = options & ~Traceable & ~RegexOptions.None;
            if (bad != 0)
            {
                res.Unsupported = "The trace follows the default backtracking engine with ignore case, multiline and singleline only. These options are not supported: " + bad + ".";
                return res;
            }
            if (pattern.Length > RegexKit.MaxPatternLength) { res.Unsupported = "The pattern is longer than " + RegexKit.MaxPatternLength + " characters."; return res; }
            if (subject.Length > MaxSubject)
            {
                subject = subject[..MaxSubject];
                res.Note = "Only the first " + MaxSubject + " characters of the subject are traced.";
            }
            res.Subject = subject;

            Parser parser = new Parser(pattern);
            Node root;
            try { root = parser.ParseAll(); }
            catch (UnsupportedException ex) { res.Unsupported = "Not supported by the trace: " + ex.Message + ". The trace never guesses, so no trace is shown for this pattern."; return res; }
            catch (ParseErrorException ex) { res.Unsupported = "The pattern could not be parsed for tracing: " + ex.Message + "."; return res; }
            if (root.PE == 0 && root.PS == 0) { root.PE = pattern.Length; }

            Exception failure = null;
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
                var rx = new Regex(pattern, options & (RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.CultureInvariant), RegexKit.Timeout);
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
