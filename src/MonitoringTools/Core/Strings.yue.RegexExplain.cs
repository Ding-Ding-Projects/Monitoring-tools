using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MonitoringTools.Core
{
    /// <summary>
    /// The Hong Kong Cantonese wording for every explanation line the regex builder writes: the tree rows (brief and detailed),
    /// the flat token list and the replacement explanation. Registered before the other pattern groups so these specific
    /// sentences win over the general ones. Text quoted from the .NET engine itself (its own error messages) stays as the
    /// engine wrote it.
    /// </summary>
    internal static partial class Strings
    {
        private static void AddRegexExplain(Dictionary<string, string> t)
        {
            void Add(string en, string yue) => t.TryAdd(en, yue);

            // ---- flat token list: anchors, shorthands, escapes
            Add("start of the text (of each line with Multiline)", "文字開頭（用咗多行選項就係每一行開頭）");
            Add("end of the text (of each line with Multiline)", "文字結尾（用咗多行選項就係每一行結尾）");
            Add("one digit", "一個數字"); Add("one character that is not a digit", "一個唔係數字嘅字元");
            Add("one word character (letter, digit or _)", "一個字詞字元（字母、數字或 _）");
            Add("one whitespace character", "一個空白字元"); Add("one non-whitespace character", "一個非空白字元");
            Add("a word boundary", "字詞邊界"); Add("not a word boundary", "唔係字詞邊界");
            Add("the very start of the text", "文字最開頭");
            Add("the end of the text, before a final line break", "文字結尾，喺最後一個換行之前");
            Add("the very end of the text", "文字最尾");
            Add("where the previous match ended", "上一個配對結束嘅位置");
            Add("a line feed", "換行"); Add("a carriage return", "歸位字元"); Add("a tab", "Tab 鍵"); Add("a space", "一個空格");
            Add("a line break", "一個換行"); Add("a whitespace character", "一個空白字元");
            Add("or", "或");
            Add("any character except a line break (any character with Singleline)", "除咗換行以外嘅任何字元（用咗單行選項就係任何字元）");
            // ---- flat token list: groups
            Add("a group that does not capture", "唔擷取嘅群組");
            Add("start of numbered group", "編號群組開始"); Add("end of group", "群組結束");
            Add("start of a group that does not capture", "唔擷取嘅群組開始");
            Add("start of a lookahead: must be followed by", "前瞻開始：後面一定要跟住");
            Add("start of a negative lookahead: must not be followed by", "否定前瞻開始：後面一定唔可以跟住");
            Add("start of a lookbehind: must be preceded by", "後顧開始：前面一定要有");
            Add("start of a negative lookbehind: must not be preceded by", "否定後顧開始：前面一定唔可以有");
            Add("start of an atomic group: no backtracking into it", "原子群組開始：唔會回溯入去");
            Add("start of a conditional: (?(condition)yes|no)", "條件式開始：(?(條件)是|否)");
            Add("a backslash with nothing after it (the engine rejects this)", "反斜線後面冇嘢（引擎會拒絕）");
            Add("a comment, ignored when matching", "註解，配對時會被忽略");
            // ---- flat token list: quantifiers
            Add("zero or more of the previous item, as many as possible", "前一項出現零次或以上，盡量多");
            Add("one or more of the previous item, as many as possible", "前一項出現一次或以上，盡量多");
            Add("the previous item is optional", "前一項可有可無");
            Add("zero or more, as few as possible", "零次或以上，盡量少");
            Add("one or more, as few as possible", "一次或以上，盡量少");
            Add("optional, preferring to skip it", "可有可無，優先跳過");
            // ---- incomplete escapes
            Add("an incomplete Unicode property escape (the engine rejects this)", "未寫完嘅 Unicode 屬性跳脫（引擎會拒絕）");
            Add("an incomplete named backreference (the engine rejects this)", "未寫完嘅具名反向參照（引擎會拒絕）");
            Add("an incomplete hexadecimal escape (the engine rejects this)", "未寫完嘅十六進制跳脫（引擎會拒絕）");
            Add("an incomplete Unicode escape (the engine rejects this)", "未寫完嘅 Unicode 跳脫（引擎會拒絕）");
            Add("an incomplete control-character escape (the engine rejects this)", "未寫完嘅控制字元跳脫（引擎會拒絕）");
            // ---- replacement explanation
            Add("literal text, copied to the result as it is", "字面文字，原封不動複製到結果");
            Add("a single dollar sign", "一個錢號");
            Add("the whole match (same as $0)", "成個配對（同 $0 一樣）");
            Add("the text of the highest-numbered group that captured", "編號最大而有擷取到嘅群組嘅文字");
            Add("the whole input text", "成段輸入文字");
            Add("an unclosed ${ reference: .NET leaves it as plain text", "冇收尾嘅 ${ 參照：.NET 會當純文字保留");
            Add("a dollar sign followed by a character that makes no token: kept as plain text", "錢號後面跟住嘅字元組唔成標記：當純文字保留");
        }

        private static readonly Dictionary<string, string> RegexOptionNames = new Dictionary<string, string>
        {
            ["ignore case"] = "忽略大細楷", ["multiline"] = "多行", ["explicit capture"] = "明確擷取", ["singleline"] = "單行", ["ignore pattern whitespace"] = "忽略表達式內嘅空白",
        };

        private static string RegexOptionList(string english)
        {
            var names = new List<string>();
            foreach (var part in english.Split(new[] { ", " }, StringSplitOptions.None))
                names.Add(RegexOptionNames.TryGetValue(part, out var y) ? y : part);
            return string.Join("、", names);
        }

        /// <summary>"turn on A, B and turn off C" or "change no options", as the tree writes it.</summary>
        private static string RegexOptionChange(string what)
        {
            if (what == "change no options") return "唔改任何選項";
            var m = Regex.Match(what, @"^turn on (.+?)(?: and turn off (.+))?$");
            if (m.Success) return "開啟" + RegexOptionList(m.Groups[1].Value) + (m.Groups[2].Success ? "，同時關閉" + RegexOptionList(m.Groups[2].Value) : "");
            m = Regex.Match(what, @"^turn off (.+)$");
            return m.Success ? "關閉" + RegexOptionList(m.Groups[1].Value) : null;
        }

        private static void AddRegexExplainPatterns(List<(Regex, Func<Match, string>)> p)
        {
            void Add(string rx, string yue) => p.Add((new Regex(rx, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline), m => m.Result(yue)));
            void AddFn(string rx, Func<Match, string> fn) => p.Add((new Regex(rx, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline), fn));

            Add(@"^the control character Ctrl\+(.)$", "控制字元 Ctrl+$1");
            Add(@"^Octal (\d+)$", "八進制 $1"); Add(@"^the character with octal code (\d+)$", "八進制碼 $1 嘅字元");
            AddFn(@"^(Not )?Unicode (.+)$", m => (m.Groups[1].Success ? "非 " : "") + "Unicode 屬性 " + m.Groups[2].Value);
            AddFn(@"^one character (not )?in the Unicode category or block (.+)$", m => "一個" + (m.Groups[1].Success ? "唔屬於" : "屬於") + " Unicode 類別或區塊 " + m.Groups[2].Value + " 嘅字元");
            Add(@"^the same text (?:that )?group (\d+) matched$", "同群組 $1 配對到嘅文字一樣");
            Add(@"^the same text (?:that )?the group ""(.*)"" matched$", "同群組「$1」配對到嘅文字一樣");
            Add(@"^the UTF-16 surrogate half (U\+[0-9A-F]{4})$", "UTF-16 代理對其中一半 $1");
            AddFn(@"^the character (space|tab|line feed|carriage return)$", m => "字元：" + (m.Groups[1].Value switch { "space" => "空格", "tab" => "Tab 鍵", "line feed" => "換行", _ => "歸位" }));
            Add(@"^start of the named group ""(.*)""$", "具名群組「$1」開始");
            Add(@"^start of a group with inline options (.+)$", "帶內嵌選項 $1 嘅群組開始");
            Add(@"^turn on inline options (.*) from here$", "由呢度起開啟內嵌選項 $1");
            AddFn(@"^a group that applies these options only inside it: (.+)$", m => { var c = RegexOptionChange(m.Groups[1].Value); return c == null ? null : "只喺群組入面生效嘅選項：" + c; });
            AddFn(@"^from here to the end of the enclosing group: (.+)$", m => { var c = RegexOptionChange(m.Groups[1].Value); return c == null ? null : "由呢度到所屬群組結尾：" + c; });
            AddFn(@"^repeat the previous item (?:(exactly once)|exactly (\d*) times|(\d+) or more times|(\d+) to (\d+) times)(, as few as possible)?$", m =>
            {
                string phrase = m.Groups[1].Success ? "剛好一次" : m.Groups[2].Success ? "剛好 " + m.Groups[2].Value + " 次" : m.Groups[3].Success ? m.Groups[3].Value + " 次或以上" : m.Groups[4].Value + " 至 " + m.Groups[5].Value + " 次";
                return "重複前一項 " + phrase + (m.Groups[6].Success ? "，盡量少重複" : "");
            });
            AddFn(@"^one character not in the set (.+)$", m => "一個唔喺字元集 " + m.Groups[1].Value + " 入面嘅字元");
            AddFn(@"^one character from the set (.+), with the subtracted set removed$", m => "一個喺字元集 " + m.Groups[1].Value + " 入面嘅字元，再減去被減嘅字元集");
            AddFn(@"^the text captured by group (\d+) \(""(.*)""\)$", m => "群組 " + m.Groups[1].Value + "（「" + m.Groups[2].Value + "」）擷取到嘅文字");
            Add(@"^the text captured by group (\d+)$", "群組 $1 擷取到嘅文字");
            Add(@"^this pattern has no group (\d+): \.NET leaves this as plain text$", "呢個表達式冇群組 $1：.NET 會當純文字保留");
            Add(@"^no group ""(.*)"" in this pattern: \.NET leaves this as plain text$", "呢個表達式冇群組「$1」：.NET 會當純文字保留");
            Add(@"^The syntax is valid, but the current engine options reject it: (.+)$", "語法有效，但目前嘅引擎選項會拒絕佢：$1");
        }
    }
}
