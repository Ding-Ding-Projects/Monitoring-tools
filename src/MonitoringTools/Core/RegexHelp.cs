using System;
using System.Collections.Generic;
using System.Linq;

namespace MonitoringTools.Core
{
    /// <summary>
    /// Help pages for the regex builder: one short page per kind of syntax, each with a worked example that is tested against the real
    /// engine. The builder opens the page that belongs to whatever part of the pattern is selected.
    /// </summary>
    public static class RegexHelp
    {
        public sealed record Topic(string Id, string Title, string Body, string Example, string Sample, string Keywords);

        public static readonly Topic[] Topics =
        {
            new("characters", "Characters and escapes", "Most characters match themselves. A backslash turns a special character such as . or ( into plain text, and gives letters special meaning: \\t is a tab, \\n a line feed, \\xNN and \\uNNNN a character by its code.", @"3\.14", "pi is 3.14 and 3x14 is not", "literal escape backslash tab dot"),
            new("shorthands", "Shorthand classes", "\\d is a digit, \\w a letter, digit or underscore, \\s whitespace. The capital forms \\D, \\W and \\S mean everything else. Dot matches any character except a line break.", @"\w+\s\d+", "item 42 and part 7", "digit word space shorthand dot"),
            new("sets", "Character sets", "Square brackets match one character out of a list or range: [abc], [a-z]. A caret first negates it: [^0-9]. .NET also subtracts one set from another: [a-z-[aeiou]] is a consonant.", @"[a-z-[aeiou]]+", "rhythm and flute", "class set range negate subtraction"),
            new("properties", "Unicode properties", "\\p{L} is any letter in any script, \\p{Lu} an uppercase letter, \\p{Nd} a decimal digit, and \\P{...} the opposite. Use them instead of ranges such as [a-z] when text is not only English.", @"\p{Lu}\p{Ll}+", "Zürich and Ōsaka", "unicode property category letter script"),
            new("anchors", "Anchors", "^ and $ match at the start and end of the text, or of each line with the Multiline option. \\A and \\z always mean the very start and end. \\b is the edge of a word and \\B is anywhere else.", @"^\w+", "first line" + "\n" + "second line", "anchor start end boundary line multiline"),
            new("quantifiers", "Quantifiers", "* means zero or more, + one or more, ? optional, {n} exactly n and {n,m} between n and m. They take as much as they can (greedy). A question mark after them makes them take as little as possible (lazy).", @"\d{2,4}", "1 22 333 4444 55555", "repeat greedy lazy star plus optional count"),
            new("alternation", "Alternation", "A vertical bar offers choices: red|blue matches either word. The engine tries the choices from left to right, so put the longer or more specific choice first.", @"red|blue", "a red door and a blue door", "or choice pipe"),
            new("groups", "Groups and captures", "Parentheses group part of a pattern so a quantifier can repeat it, and remember what it matched. Groups are numbered from the left. (?:...) groups without remembering.", @"(\d+)-(\d+)", "10-20 and 3-4", "group capture number parentheses"),
            new("named", "Named groups", "(?<year>\\d{4}) remembers the match under a name instead of a number. In a replacement, ${year} puts it back. Named groups are numbered after the unnamed ones.", @"(?<year>\d{4})-(?<month>\d\d)", "2026-11 and 2027-02", "name named capture"),
            new("backreferences", "Backreferences", "\\1 or \\k<name> matches the same text again that an earlier group matched. It finds repeated words such as the the.", @"\b(\w+)\s+\1\b", "this is is a test", "backreference repeat same"),
            new("lookaround", "Lookahead and lookbehind", "(?=...) requires what follows, (?!...) forbids it, (?<=...) requires what precedes and (?<!...) forbids it. They look without consuming text, so they check a context and leave it out of the match.", @"(?<=\$)\d+", "cost $15 and 20 euro", "lookahead lookbehind lookaround context"),
            new("atomic", "Atomic groups", "(?>...) matches like a normal group but never gives characters back once it has matched. That makes some patterns much faster and stops catastrophic backtracking.", @"(?>a+)b", "aaab and aaac", "atomic possessive backtracking"),
            new("balancing", "Balancing groups", "(?<open>...) pushes a capture and (?<-open>...) pops one, failing when there is none. Together they match nested pairs such as parentheses. This exists only in .NET.", @"^(?:[^()]|(?<o>\()|(?<-o>\)))*(?(o)(?!))$", "(a(b)c)", "balancing nested pairs stack"),
            new("conditionals", "Conditionals", "(?(1)yes|no) matches yes when group 1 took part in the match and no otherwise. (?(name)...) tests a named group.", @"(\()?\d+(?(1)\))", "(42) and 7", "conditional if then else"),
            new("options", "Inline options and comments", "(?i) turns on ignore case from that point, (?-i) turns it off, (?i:...) limits it to a group. (?#text) is a comment, and with the Ignore whitespace option # starts one that runs to the end of the line.", @"(?i)hello", "Hello HELLO hello", "inline option flag comment ignore case"),
            new("flags", "Options", "Multiline lets ^ and $ work per line. Singleline lets . match line breaks. Ignore case matches either case. Explicit capture keeps only named groups. Right to left searches from the end. Non-backtracking runs in linear time but has no lookaround, backreferences or atomic groups. Culture invariant ignores the computer's language when comparing case.", @"^b", "a\nb", "option flag multiline singleline ignore case"),
            new("replacement", "Replacement syntax", "In a replacement, $1 or ${name} insert a group, $0 or $& the whole match, $` the text before and $' the text after, $+ the last group that matched, $_ the whole input and $$ a dollar sign.", @"(\w+)@(\w+)", "alice@example", "replace replacement dollar group"),
            new("backtracking", "Backtracking and slow patterns", "A pattern with nested quantifiers such as (a+)+ can take exponential time on text that almost matches. This builder stops every match at 200 ms and warns about risky shapes. Use atomic groups, tighter classes or the non-backtracking option.", @"^(a+)+$", "aaaa!", "slow catastrophic backtracking timeout redos"),
            new("unavailable", "Features .NET does not have", "Recursion (?R), subroutine calls (?1), POSIX classes such as [[:alpha:]] and case-converting replacements such as \\U are not part of .NET. Balancing groups cover many nested cases; Unicode properties replace POSIX classes.", @"\p{L}+", "word", "recursion subroutine posix case conversion unsupported"),
        };

        /// <summary>The help page for a part of a pattern, by the kind of syntax tree node it is.</summary>
        public static Topic For(RegexTree.Node n)
        {
            if (n == null) return null;
            string id = n.Kind switch
            {
                RegexTree.NodeKind.Literal => "characters",
                RegexTree.NodeKind.Escape => "characters",
                RegexTree.NodeKind.Shorthand => "shorthands",
                RegexTree.NodeKind.Any => "shorthands",
                RegexTree.NodeKind.Property => "properties",
                RegexTree.NodeKind.Anchor => "anchors",
                RegexTree.NodeKind.CharClass => "sets",
                RegexTree.NodeKind.Subtraction => "sets",
                RegexTree.NodeKind.Quantifier => "quantifiers",
                RegexTree.NodeKind.Alternation => "alternation",
                RegexTree.NodeKind.Branch => "alternation",
                RegexTree.NodeKind.Backreference => "backreferences",
                RegexTree.NodeKind.Conditional => "conditionals",
                RegexTree.NodeKind.Condition => "conditionals",
                RegexTree.NodeKind.Comment => "options",
                RegexTree.NodeKind.InlineOptions => "options",
                RegexTree.NodeKind.Group => n.Group switch
                {
                    RegexTree.GroupKind.Named => "named",
                    RegexTree.GroupKind.Balancing => "balancing",
                    RegexTree.GroupKind.Atomic => "atomic",
                    RegexTree.GroupKind.Lookahead or RegexTree.GroupKind.NegativeLookahead or RegexTree.GroupKind.Lookbehind or RegexTree.GroupKind.NegativeLookbehind => "lookaround",
                    RegexTree.GroupKind.Options => "options",
                    _ => "groups",
                },
                _ => null,
            };
            return id == null ? null : Topics.First(t => t.Id == id);
        }

        /// <summary>Topics whose title, page or keywords contain every word of the query (any case).</summary>
        public static List<Topic> Search(string query)
        {
            var words = (query ?? "").Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return Topics.ToList();
            return Topics.Where(t => { var h = t.Title + " " + t.Body + " " + t.Keywords; return words.All(w => h.IndexOf(w, StringComparison.CurrentCultureIgnoreCase) >= 0); }).ToList();
        }
    }
}
