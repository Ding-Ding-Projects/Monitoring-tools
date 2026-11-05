using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MonitoringTools.Core
{
    /// <summary>The Hong Kong Cantonese wording for the regex builder's sample tools, engines, benchmark extras, code templates, library, help and file search additions.</summary>
    internal static partial class Strings
    {
        // Names that appear inside built sentences. Looked up here so a sentence never carries English words next to its Cantonese.
        private static Dictionary<string, string> RegexParityNames => _regexParityNames ??= new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Build"] = "砌法", ["Test"] = "測試", ["Debug"] = "除錯", ["Compare"] = "比較", ["Benchmark"] = "效能測試", ["Replace"] = "取代", ["Files"] = "檔案", ["Code"] = "程式碼", ["Library"] = "收藏庫", ["Help"] = "說明",
            ["Whole text"] = "整段文字", ["Each line"] = "逐行", ["Each page"] = "逐頁", ["Match"] = "配對", ["Split"] = "分割",
            ["Auto"] = "自動", ["UTF-8"] = "UTF-8", ["UTF-16"] = "UTF-16", ["Latin-1"] = "Latin-1",
            ["Match the search bar"] = "同搜尋列一樣闊", ["Narrow"] = "窄", ["Wide"] = "闊", ["Extra wide"] = "特闊", ["Short"] = "矮", ["Normal"] = "正常", ["Tall"] = "高",
            ["Alternation"] = "或選擇", ["Anchors"] = "錨點", ["Atomic groups"] = "原子群組", ["Backreferences"] = "反向參照", ["Backtracking and slow patterns"] = "回溯同慢嘅表達式", ["Balancing groups"] = "平衡群組",
            ["Character sets"] = "字元集", ["Characters and escapes"] = "字元同跳脫", ["Conditionals"] = "條件式", ["Features .NET does not have"] = ".NET 冇嘅功能", ["Groups and captures"] = "群組同擷取",
            ["Inline options and comments"] = "內嵌選項同註解", ["Lookahead and lookbehind"] = "前瞻同後顧", ["Named groups"] = "具名群組", ["Options"] = "選項", ["Quantifiers"] = "數量詞",
            ["Replacement syntax"] = "取代語法", ["Shorthand classes"] = "簡寫字元類", ["Unicode properties"] = "Unicode 屬性",
        };

        private static Dictionary<string, string> _regexParityNames;

        private static string RpName(string n) => RegexParityNames.TryGetValue(n, out var y) ? y : n;

        private static void AddRegexParity(Dictionary<string, string> t)
        {
            void Add(string en, string yue) => t.TryAdd(en, yue);

            // ---- tab and section headings
            Add("Help", "說明"); Add("Show the Help view", "顯示說明檢視"); Add("Syntax help", "語法說明"); Add("ALL PAGES", "所有頁面"); Add("All pages", "所有頁面");
            Add("Builder preferences", "砌法器偏好設定"); Add("BUILDER PREFERENCES", "砌法器偏好設定"); Add("For the selected part of your pattern", "針對你揀咗嘅表達式部分");
            Add("FOR THE SELECTED PART OF YOUR PATTERN", "針對你揀咗嘅表達式部分"); Add("SYNTAX HELP", "語法說明"); Add("Example", "例子"); Add("EXAMPLE", "例子");
            Add("Short pages for each kind of syntax. When a part of the pattern is selected in the Build view, or the caret is inside it, its page comes first.", "每種語法一頁短說明。喺砌法檢視揀咗表達式嘅一部分，或者游標喺入面，該部分嘅頁面會排最先。");
            Add("These stay on this computer and apply to every regex builder in the app.", "呢啲設定只留喺呢部電腦，並套用喺應用程式入面每個正則表達式砌法器。");
            Add("Search the help pages", "搜尋說明頁面"); Add("No page matches.", "冇符合嘅頁面。");
            Add("Open on", "開啟時顯示"); Add("Width", "闊度"); Add("Height", "高度"); Add("Views shown", "顯示嘅檢視");
            Add("Detailed explanations by default", "預設顯示詳細解釋"); Add("Reset preferences", "重設偏好設定"); Add("Use this example", "用呢個例子");
            Add("Put the example in the pattern box and its sample text in the Test view", "將例子放入表達式框，並將例子文字放入測試檢視");
            Add("Open the Build view with full sentences instead of short labels", "砌法檢視用完整句子而唔係短標籤");

            // ---- Test: sample tools
            Add("Match against", "配對範圍"); Add("Encoding", "編碼"); Add("Whole text", "整段文字"); Add("Each line", "逐行"); Add("Each page", "逐頁");
            Add("Convert to LF", "轉做 LF"); Add("Convert to CRLF", "轉做 CRLF"); Add("Paste from clipboard", "由剪貼簿貼上"); Add("Open file...", "開啟檔案…"); Add("Hex view", "十六進制檢視");
            Add("Change every line break in the sample to a line feed", "將樣本入面每個換行改為換行字元 (LF)");
            Add("Change every line break in the sample to a carriage return and line feed", "將樣本入面每個換行改為歸位加換行 (CRLF)");
            Add("Replace the sample with the text on the clipboard", "用剪貼簿嘅文字取代樣本");
            Add("Read a text file into the sample, in the encoding chosen below", "用下面揀嘅編碼，將文字檔讀入樣本");
            Add("Show the sample as bytes in hexadecimal, in the encoding chosen below", "用下面揀嘅編碼，以十六進制位元組顯示樣本");
            Add("Sample as hexadecimal bytes", "樣本嘅十六進制位元組");
            Add("The clipboard has no text.", "剪貼簿冇文字。");
            Add("Sample text to test the pattern against", "用嚟測試表達式嘅樣本文字");

            // ---- Compare: other engines
            Add("Other engines on this computer", "呢部電腦上嘅其他引擎"); Add("OTHER ENGINES ON THIS COMPUTER", "呢部電腦上嘅其他引擎"); Add("Run in other engines", "喺其他引擎執行");
            Add("Run the pattern over the sample in every other engine found on this computer", "喺呢部電腦搵到嘅每個其他引擎入面，對樣本執行表達式");
            Add("Engines outside .NET are compared further down.", ".NET 以外嘅引擎喺下面比較。");
            Add("Runs the pattern over the sample in each mode this runtime really has: the default backtracking engine, the non-backtracking engine, ECMAScript mode and compiled. Each result shows whether the mode accepts the pattern, the match count, the first difference from the default engine and the time of one run. Engines outside .NET are compared further down.",
                "喺呢個執行環境真正有嘅每個模式對樣本執行表達式：預設回溯引擎、非回溯引擎、ECMAScript 模式同編譯模式。每個結果顯示該模式接唔接受表達式、配對數量、同預設引擎嘅第一個分別，同一次執行嘅時間。.NET 以外嘅引擎喺下面比較。");
            Add("Runs the same pattern text in the other regular expression engines this computer really has: the Windows JScript engine (ECMAScript 3), the .NET Framework engine in Windows PowerShell 5.1, and Python or Node.js when they are installed. An engine that is missing is listed as not available. PCRE, Java, Perl, Ruby and Go engines are not installed here, so they cannot be run; the Code view translates a pattern for Java but cannot run it. Each engine receives the pattern as data, is stopped after a few seconds, and uses its own syntax rules, so a construct it lacks shows its own error.",
                "用呢部電腦真正有嘅其他正則表達式引擎執行同一段表達式文字：Windows JScript 引擎（ECMAScript 3）、Windows PowerShell 5.1 入面嘅 .NET Framework 引擎，同埋已安裝嘅 Python 或 Node.js。冇安裝嘅引擎會標示為無法使用。呢度冇安裝 PCRE、Java、Perl、Ruby 同 Go 引擎，所以唔能夠執行；程式碼檢視可以將表達式翻譯做 Java，但係唔能夠執行。每個引擎都將表達式當資料接收，幾秒後會停止，並用自己嘅語法規則，所以佢冇嘅語法會顯示佢自己嘅錯誤。");
            Add("The engines could not run.", "引擎無法執行。");

            // ---- Benchmark
            Add("COMPARE WITH ANOTHER PATTERN", "同另一個表達式比較"); Add("Compare with another pattern", "同另一個表達式比較");
            Add("Pattern A is the pattern above. Type a second pattern to run both on the sample with the same options and see which finds what and which is faster.", "表達式 A 係上面嘅表達式。輸入第二個表達式，用同樣嘅選項喺樣本上執行兩個，睇各自搵到咩同邊個快啲。");
            Add("Second pattern to compare with the first", "用嚟同第一個比較嘅第二個表達式"); Add("Compare the two patterns", "比較兩個表達式");
            Add("Run pattern A and pattern B on the sample and compare their matches and times", "喺樣本上執行表達式 A 同 B，比較配對同時間");
            Add("TIME PER PART OF THE PATTERN", "表達式每部分嘅時間"); Add("Time per part of the pattern", "表達式每部分嘅時間");
            Add("Times the pattern one top-level part at a time: the first part alone, then the first two, and so on. The jump between neighbours is the cost that part added on the sample. These are measurements of the real engine, not an internal profile of it, so short times are noisy.",
                "逐個頂層部分量度表達式時間：先量第一部分，再量頭兩部分，如此類推。相鄰兩次之間嘅差距就係該部分喺樣本上增加嘅成本。呢啲係真引擎嘅量度，唔係引擎內部分析，所以好短嘅時間會有雜訊。");
            Add("Profile the parts", "量度各部分"); Add("Measure how much each top-level part of the pattern adds to the time on the sample", "量度表達式每個頂層部分喺樣本上增加幾多時間");
            Add("SAVED BENCHMARK HISTORY", "已儲存嘅效能測試紀錄"); Add("Saved benchmark history", "已儲存嘅效能測試紀錄");
            Add("Save last result", "儲存上次結果"); Add("Clear history", "清除紀錄");
            Add("Keep the result of the last benchmark run, with its pattern, so a later version of the pattern can be compared with it", "保留上次效能測試嘅結果同表達式，方便之後同新版本比較");
            Add("Delete every saved benchmark result", "刪除所有已儲存嘅效能測試結果"); Add("Use pattern", "用返呢個表達式");
            Add("Put this saved pattern back in the pattern box", "將呢個已儲存嘅表達式放返入表達式框");
            Add("Nothing saved yet.", "未有儲存任何紀錄。");
            Add("SUBJECT THAT SHOULD MATCH", "應該配對到嘅文字"); Add("SUBJECT THAT SHOULD NOT MATCH", "應該配對唔到嘅文字");
            Add("Subject that should match, for the benchmark", "效能測試用：應該配對到嘅文字"); Add("Subject that should not match, for the benchmark", "效能測試用：應該配對唔到嘅文字");
            Add("Run benchmark", "執行效能測試"); Add("Time the pattern on both subjects and run the input-growth test", "喺兩段文字上計時，並執行輸入增長測試"); Add("Stop the running benchmark", "停止進行中嘅效能測試");
            Add("Press Run benchmark to measure this pattern.", "撳「執行效能測試」嚟量度呢個表達式。");
            Add("Times the pattern on a subject that matches and one that does not, then repeats each subject 1x, 2x, 4x, 8x and 16x to see how the time grows. Runs stop at the 200 ms limit and never block the window. Defaults come from the sample; edit them freely.",
                "喺配對到同配對唔到嘅文字上各計時一次，再將每段文字重複 1 倍、2 倍、4 倍、8 倍同 16 倍，睇時間點樣增長。執行喺 200 毫秒上限停止，唔會卡住視窗。預設值嚟自樣本，可以自由修改。");

            // ---- Code
            Add("Snippet for", "程式碼用途"); Add("Edit template", "編輯範本"); Add("Save template", "儲存範本"); Add("Restore built-in", "還原內建範本"); Add("Code template", "程式碼範本");
            Add("Show the template this snippet is made from so you can change it", "顯示呢段程式碼所用嘅範本，方便你修改");
            Add("JavaScript", "JavaScript"); Add("Java", "Java"); Add("Match snippet", "配對程式碼"); Add("Replace snippet", "取代程式碼"); Add("Split snippet", "分割程式碼");
            Add("Generate Java", "產生 Java"); Add("Generate JavaScript", "產生 JavaScript");
            Add("Placeholders: {{literal}} the pattern as a string in this language, {{flags}} its option expression, {{replacement}} the replacement as a string, {{timeout}} the match limit in milliseconds, {{notes}} comment lines about anything that could not be translated. Everything else is copied as written. The change is kept on this computer and applies to this language and snippet only.",
                "佔位符：{{literal}} 係用呢種語言寫嘅表達式字串，{{flags}} 係選項表達式，{{replacement}} 係取代文字字串，{{timeout}} 係配對時間上限（毫秒），{{notes}} 係關於無法翻譯部分嘅註解行。其他內容照抄。修改只會留喺呢部電腦，並只適用於呢種語言同呢類程式碼。");
            Add("Your saved template is in use.", "正在使用你儲存嘅範本。"); Add("The built-in template is in use.", "正在使用內建範本。");
            Add("Saved. Your template is in use.", "已儲存，正在使用你嘅範本。"); Add("That is the built-in template, so nothing needs saving.", "呢個就係內建範本，唔使儲存。");

            // ---- Library
            Add("Description of the saved pattern", "已儲存表達式嘅描述"); Add("Parameters of the saved pattern, one name=default per line", "已儲存表達式嘅參數，每行一個 名稱=預設值");
            Add("One parameter per line as name=default. Write {{name}} in the pattern where its value goes.", "每行一個參數，格式為 名稱=預設值。喺表達式入面用 {{名稱}} 標示數值嘅位置。");
            Add("Search the saved patterns", "搜尋已儲存嘅表達式"); Add("Words are matched in the name, description, pattern and parameter names", "會喺名稱、描述、表達式同參數名稱入面配對字詞");
            Add("Treat the search as a regular expression over the name, description, pattern and parameter names", "將搜尋當作正則表達式，用喺名稱、描述、表達式同參數名稱");
            Add("No saved pattern matches the search.", "冇已儲存嘅表達式符合搜尋。"); Add("Regex", "正則");

            // ---- Build editors
            Add("Earlier", "移前"); Add("Later", "移後"); Add("Move this part", "移動呢部分");
            Add("Swap this part with the one before it, keeping both exactly as written", "同前面一部分對調，兩部分嘅文字保持不變");
            Add("Swap this part with the one after it, keeping both exactly as written", "同後面一部分對調，兩部分嘅文字保持不變");

            // ---- Files
            Add("FILE NAME PATTERN (OPTIONAL, A REGULAR EXPRESSION, IGNORES CASE)", "檔名表達式（可選，正則表達式，唔分大細楷）");
            Add("File name pattern (optional, a regular expression, ignores case)", "檔名表達式（可選，正則表達式，唔分大細楷）");
            Add("Regular expression for file names", "檔名嘅正則表達式");
            Add("Only files whose name matches this pattern are searched, on top of the masks. Example: ^report-\\d{4}\\.txt$", "除咗遮罩，只搜尋檔名符合呢個表達式嘅檔案。例子：^report-\\d{4}\\.txt$");
            Add("SAVED SEARCHES", "已儲存嘅搜尋"); Add("Saved searches", "已儲存嘅搜尋"); Add("Name for the saved file search", "已儲存檔案搜尋嘅名稱"); Add("Save these settings", "儲存呢啲設定");
            Add("Keep the folder, masks, name pattern and options under this name", "用呢個名稱保留資料夾、遮罩、檔名表達式同選項"); Add("Load", "載入");
            Add("BACKUPS AND HISTORY", "備份同紀錄"); Add("Backups and history", "備份同紀錄"); Add("Choose a backup...", "揀選備份…"); Add("Show history", "顯示紀錄");
            Add("No backups yet. A replace in files creates one.", "未有備份。喺檔案入面取代時會自動建立。"); Add("No searches yet.", "未有搜尋紀錄。");
            Add("Copy the files of this replace run back over their originals, after a confirmation", "確認後，將呢次取代嘅檔案複製返去覆蓋原檔");
            Add("Load this folder and pattern again", "再次載入呢個資料夾同表達式");
            Add("Open the file in your editor at this line when the editor can jump to a line", "用你嘅編輯器開啟檔案；編輯器支援嘅話會跳到呢一行");
            Add("Give the saved search a name first.", "請先為已儲存嘅搜尋命名。");

            // ---- Replace chips
            Add("the whole match, another spelling of $0", "整個配對，同 $0 一樣"); Add("the last group that matched", "最後一個配對到嘅群組");

            // ---- Debug note
            Add("A small built-in interpreter replays one match attempt of the default backtracking engine. It covers literals, classes (including Unicode categories and class subtraction), anchors (including \\G), word boundaries, groups, look-ahead and look-behind, atomic groups, conditionals, inline options, alternation, greedy and lazy repeats and backreferences. Before a trace is shown the match and every capture group are checked against the real .NET engine on the same subject; if they disagree, or the pattern uses something the interpreter does not cover, you are told and nothing is faked.",
                "一個內建嘅小型直譯器會重播預設回溯引擎嘅一次配對嘗試。支援字面字元、字元類（包括 Unicode 類別同字元集相減）、錨點（包括 \\G）、字詞邊界、群組、前瞻同後顧、原子群組、條件式、內嵌選項、或選擇、貪婪同惰性重複，同反向參照。顯示追蹤前，會用同一段文字對照真嘅 .NET 引擎核對配對同每個擷取群組；如果不一致，或者表達式用咗直譯器未支援嘅語法，會直接話你知，絕不作假。");

            // ---- Help pages (titles are in RegexParityNames)
            foreach (var kv in RegexParityNames) Add(kv.Key, kv.Value);
            Add("Most characters match themselves. A backslash turns a special character such as . or ( into plain text, and gives letters special meaning: \\t is a tab, \\n a line feed, \\xNN and \\uNNNN a character by its code.", "大部分字元配對自己。反斜線可以將 . 或 ( 呢類特殊字元變做純文字，亦令字母有特別意思：\\t 係 Tab，\\n 係換行，\\xNN 同 \\uNNNN 以編碼指定字元。");
            Add("\\d is a digit, \\w a letter, digit or underscore, \\s whitespace. The capital forms \\D, \\W and \\S mean everything else. Dot matches any character except a line break.", "\\d 係數字，\\w 係字母、數字或底線，\\s 係空白。大寫嘅 \\D、\\W 同 \\S 代表其餘所有字元。點號配對除換行以外嘅任何字元。");
            Add("Square brackets match one character out of a list or range: [abc], [a-z]. A caret first negates it: [^0-9]. .NET also subtracts one set from another: [a-z-[aeiou]] is a consonant.", "方括號配對清單或範圍入面嘅一個字元：[abc]、[a-z]。開頭加插入符號就係否定：[^0-9]。.NET 仲可以由一個字元集減去另一個：[a-z-[aeiou]] 係輔音。");
            Add("\\p{L} is any letter in any script, \\p{Lu} an uppercase letter, \\p{Nd} a decimal digit, and \\P{...} the opposite. Use them instead of ranges such as [a-z] when text is not only English.", "\\p{L} 係任何文字嘅字母，\\p{Lu} 係大寫字母，\\p{Nd} 係十進制數字，\\P{...} 係相反。文字唔只係英文時，用佢哋代替 [a-z] 呢類範圍。");
            Add("^ and $ match at the start and end of the text, or of each line with the Multiline option. \\A and \\z always mean the very start and end. \\b is the edge of a word and \\B is anywhere else.", "^ 同 $ 配對文字嘅開頭同結尾；用咗多行選項就係每一行嘅開頭同結尾。\\A 同 \\z 永遠代表最開頭同最尾。\\b 係字詞邊緣，\\B 係其他位置。");
            Add("* means zero or more, + one or more, ? optional, {n} exactly n and {n,m} between n and m. They take as much as they can (greedy). A question mark after them makes them take as little as possible (lazy).", "* 代表零次或以上，+ 一次或以上，? 可有可無，{n} 剛好 n 次，{n,m} 介乎 n 至 m 次。佢哋會盡量多取（貪婪）。後面加問號就會盡量少取（惰性）。");
            Add("A vertical bar offers choices: red|blue matches either word. The engine tries the choices from left to right, so put the longer or more specific choice first.", "直線代表選擇：red|blue 配對其中一個字。引擎由左至右逐個試，所以較長或較具體嘅選擇應該放前面。");
            Add("Parentheses group part of a pattern so a quantifier can repeat it, and remember what it matched. Groups are numbered from the left. (?:...) groups without remembering.", "括號將表達式一部分組合，令數量詞可以重複佢，並記住配對到嘅內容。群組由左至右編號。(?:...) 只組合但唔記住。");
            Add("(?<year>\\d{4}) remembers the match under a name instead of a number. In a replacement, ${year} puts it back. Named groups are numbered after the unnamed ones.", "(?<year>\\d{4}) 用名稱而唔係編號記住配對。取代時 ${year} 會放返出嚟。具名群組喺無名群組之後編號。");
            Add("\\1 or \\k<name> matches the same text again that an earlier group matched. It finds repeated words such as the the.", "\\1 或 \\k<name> 再配對一次較早群組配對到嘅同一段文字，可以搵出重複嘅字，例如 the the。");
            Add("(?=...) requires what follows, (?!...) forbids it, (?<=...) requires what precedes and (?<!...) forbids it. They look without consuming text, so they check a context and leave it out of the match.", "(?=...) 要求後面有某內容，(?!...) 禁止後面有，(?<=...) 要求前面有，(?<!...) 禁止前面有。佢哋只睇而唔消耗文字，所以檢查上下文但唔包含喺配對入面。");
            Add("(?>...) matches like a normal group but never gives characters back once it has matched. That makes some patterns much faster and stops catastrophic backtracking.", "(?>...) 配對方式同普通群組一樣，但一旦配對咗就唔會交還字元。咁樣有啲表達式會快好多，亦可以避免災難性回溯。");
            Add("(?<open>...) pushes a capture and (?<-open>...) pops one, failing when there is none. Together they match nested pairs such as parentheses. This exists only in .NET.", "(?<open>...) 推入一個擷取，(?<-open>...) 取走一個，冇嘅話就失敗。兩者配合可以配對括號呢類巢狀成對符號。呢個功能只有 .NET 先有。");
            Add("(?(1)yes|no) matches yes when group 1 took part in the match and no otherwise. (?(name)...) tests a named group.", "(?(1)yes|no) 喺群組 1 有參與配對時配對 yes，否則配對 no。(?(name)...) 測試具名群組。");
            Add("(?i) turns on ignore case from that point, (?-i) turns it off, (?i:...) limits it to a group. (?#text) is a comment, and with the Ignore whitespace option # starts one that runs to the end of the line.", "(?i) 由該處起開啟唔分大細楷，(?-i) 關閉，(?i:...) 只限於一個群組。(?#text) 係註解；用咗忽略空白選項時，# 開始一段到行尾嘅註解。");
            Add("Multiline lets ^ and $ work per line. Singleline lets . match line breaks. Ignore case matches either case. Explicit capture keeps only named groups. Right to left searches from the end. Non-backtracking runs in linear time but has no lookaround, backreferences or atomic groups. Culture invariant ignores the computer's language when comparing case.", "多行令 ^ 同 $ 按行運作。單行令 . 配對換行。唔分大細楷會兩種寫法都配對。明確擷取只保留具名群組。由右至左從尾開始搜尋。非回溯以線性時間運行，但冇前瞻後顧、反向參照同原子群組。文化不變比較大細楷時唔理會電腦嘅語言。");
            Add("In a replacement, $1 or ${name} insert a group, $0 or $& the whole match, $` the text before and $' the text after, $+ the last group that matched, $_ the whole input and $$ a dollar sign.", "取代時，$1 或 ${name} 插入群組，$0 或 $& 插入整個配對，$` 係配對前面嘅文字，$' 係後面嘅文字，$+ 係最後配對到嘅群組，$_ 係整段輸入，$$ 係一個錢號。");
            Add("A pattern with nested quantifiers such as (a+)+ can take exponential time on text that almost matches. This builder stops every match at 200 ms and warns about risky shapes. Use atomic groups, tighter classes or the non-backtracking option.", "有巢狀數量詞（例如 (a+)+）嘅表達式，遇到差少少就配對到嘅文字時，可能要指數級時間。呢個砌法器會喺 200 毫秒停止每次配對，並警告高風險寫法。可以用原子群組、更嚴格嘅字元類或非回溯選項。");
            Add("Recursion (?R), subroutine calls (?1), POSIX classes such as [[:alpha:]] and case-converting replacements such as \\U are not part of .NET. Balancing groups cover many nested cases; Unicode properties replace POSIX classes.", "遞迴 (?R)、子程序呼叫 (?1)、POSIX 字元類（例如 [[:alpha:]]）同轉換大細楷嘅取代（例如 \\U）都唔屬於 .NET。平衡群組可以應付好多巢狀情況；Unicode 屬性可以代替 POSIX 字元類。");

            // ---- Files: archives, binary files and file type settings
            Add("Search inside archives", "搜尋壓縮檔入面");
            Add("Open .zip, .jar and .nupkg files, and .7z, .rar, .tar, .gz, .tgz, .bz2, .xz and .cab files through the bundled 7-Zip, and search their entries. The masks and the name pattern then apply to the entries. Off by default.",
                "開啟 .zip、.jar 同 .nupkg 檔，並用內置嘅 7-Zip 開啟 .7z、.rar、.tar、.gz、.tgz、.bz2、.xz 同 .cab 檔，搜尋入面嘅檔案。遮罩同檔名表達式會改為套用喺入面嘅檔案。預設關閉。");
            Add("Binary files as bytes", "二進制檔逐個位元組搜尋");
            Add("Search a binary file one byte per character: \\x00 to \\xFF in the pattern are byte values, and each match shows its offset and hexadecimal",
                "逐個位元組當一個字元搜尋二進制檔：表達式入面 \\x00 至 \\xFF 代表位元組數值，每個配對都會顯示位移同十六進制");
            Add("Binary files as text", "二進制檔當文字搜尋");
            Add("Read a binary file like any text file (UTF-8 when it is valid, otherwise Latin-1)", "好似普通文字檔咁讀取二進制檔（有效嘅話用 UTF-8，否則用 Latin-1）");
            Add("FILE TYPE SETTINGS", "檔案類型設定"); Add("File type settings", "檔案類型設定");
            Add("Rules by file extension, kept on this computer and used by every file search: the encoding to read the file with, whether to skip it, and how to search it when it looks binary. A forced encoding reads the file as text even when it has null bytes, such as UTF-16 without a byte order mark.",
                "按副檔名設定嘅規則，只留喺呢部電腦，每次搜尋檔案都會用：用咩編碼讀取、要唔要略過，同埋睇落係二進制檔時點樣搜尋。指定咗編碼嘅話，就算檔案有空位元組（例如冇位元組順序標記嘅 UTF-16）都會當文字讀取。");
            Add("Add file type", "加入檔案類型"); Add("Add a rule for this extension", "為呢個副檔名加入規則");
            Add("An extension such as .log or .txt", "副檔名，例如 .log 或 .txt"); Add("Extension for a new file type rule, such as .log", "新檔案類型規則嘅副檔名，例如 .log");
            Add("No file type rules. Every file uses the options above.", "未有檔案類型規則。所有檔案都用上面嘅選項。");
            Add("Type an extension such as .log: letters, digits, _ and - only.", "請輸入副檔名，例如 .log：只可以用字母、數字、_ 同 -。");
            Add("Skip", "略過");
            Add("That match is inside an archive. Unpack the archive to open the file; a replace never writes into archives.", "呢個配對喺壓縮檔入面。請先解壓再開啟檔案；取代永遠唔會寫入壓縮檔。");
            Add("This file is inside an archive. Unpack the archive to open it.", "呢個檔案喺壓縮檔入面。請先解壓再開啟。");
            Add("Looks like a binary file.", "睇落係二進制檔。");
            Add("This archive type needs 7-Zip, which is not available in this build.", "呢種壓縮檔需要 7-Zip，但呢個版本冇附帶。");

            // ---- Build: conditional editor and dragging
            Add("Group has matched", "群組已配對"); Add("Look-ahead", "前瞻"); Add("Negative look-ahead", "否定前瞻"); Add("Look-behind", "後顧"); Add("Negative look-behind", "否定後顧");
            Add("The condition holds when the named or numbered group has captured something", "具名或編號群組擷取到嘢時，條件成立");
            Add("The condition holds when this look-ahead test succeeds at the current position", "呢個前瞻測試喺目前位置成功時，條件成立");
            Add("The condition holds when this negative look-ahead test succeeds at the current position", "呢個否定前瞻測試喺目前位置成功時，條件成立");
            Add("The condition holds when this look-behind test succeeds at the current position", "呢個後顧測試喺目前位置成功時，條件成立");
            Add("The condition holds when this negative look-behind test succeeds at the current position", "呢個否定後顧測試喺目前位置成功時，條件成立");
            Add("Condition", "條件"); Add("Test", "測試"); Add("Yes", "成立"); Add("No", "唔成立");
            Add("Condition test: a group name or number, or the look-around pattern", "條件測試：群組名稱或編號，或者環顧表達式");
            Add("Branch used when the condition holds", "條件成立時用嘅分支"); Add("Branch used when it does not, empty for none", "條件唔成立時用嘅分支，留空即係冇");
            Add("Swap branches", "對調分支"); Add("Exchange the yes and no branches; apply to write the change", "對調成立同唔成立兩個分支；撳套用先會寫入");
            Add("Not applied: the test must name a group in the pattern by name or number, each branch must have no | of its own, and the result must be valid.", "未套用：測試要用名稱或編號指向表達式入面嘅群組，每個分支本身唔可以有 |，結果亦要有效。");
            Add("Not applied: the test cannot be empty, each branch must have no | of its own, and the result must be valid.", "未套用：測試唔可以留空，每個分支本身唔可以有 |，結果亦要有效。");
            Add("Not moved: release the part on another row.", "未移動：請喺另一行放開。"); Add("Not moved: the drag left the tree.", "未移動：拖曳離開咗語法樹。"); Add("Not moved: the drag was interrupted.", "未移動：拖曳被中斷。");

            // ---- Debug: newly traced constructs
            Add("The condition holds, so try the yes branch", "條件成立，所以試成立分支");
            Add("The condition does not hold, so try the no branch", "條件唔成立，所以試唔成立分支");
            Add("The condition does not hold, so try the empty no branch", "條件唔成立，所以試空白嘅唔成立分支");
            Add("Position is at the start of this search (\\G)", "位置喺今次搜尋嘅起點（\\G）"); Add("Position is not at the start of this search (\\G)", "位置唔喺今次搜尋嘅起點（\\G）");
        }

        private static void AddRegexParityPatterns(List<(Regex, Func<Match, string>)> p)
        {
            void AddFn(string rx, Func<Match, string> fn) => p.Add((new Regex(rx, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline), fn));

            AddFn(@"^Show the page: (.+)$", m => "顯示頁面：" + RpName(m.Groups[1].Value));
            AddFn(@"^Make the builder (.+)$", m => "將砌法器設為「" + RpName(m.Groups[1].Value) + "」");
            AddFn(@"^Open the builder on the (.+)$", m => "開啟砌法器時顯示「" + RpName(m.Groups[1].Value) + "」檢視");
            AddFn(@"^Show or hide the (.+) view in the tab strip$", m => "喺分頁列顯示或隱藏「" + RpName(m.Groups[1].Value) + "」檢視");
            AddFn(@"^Start on (.+)$", m => "開頭顯示" + RpName(m.Groups[1].Value));
            AddFn(@"^Show (Build|Test|Debug|Compare|Benchmark|Replace|Files|Code|Library)$", m => "顯示" + RpName(m.Groups[1].Value));
            AddFn(@"^Match the pattern against (.+)$", m => "將表達式配對：" + RpName(m.Groups[1].Value));
            AddFn(@"^Use this encoding for reading files and for the hex view: (.+)$", m => "讀取檔案同十六進制檢視用呢個編碼：" + RpName(m.Groups[1].Value));
            AddFn(@"^Write a snippet that does this: (.+)$", m => "寫一段程式碼做：" + RpName(m.Groups[1].Value));
            AddFn(@"^Sample: (.+) characters · (.+)$", m => "樣本：" + m.Groups[1].Value + " 個字元 · " + RpLineEndings(m.Groups[2].Value));
            AddFn(@"^Pasted (.+) characters from the clipboard\.$", m => "已由剪貼簿貼上 " + m.Groups[1].Value + " 個字元。");
            AddFn(@"^Line breaks converted to (LF|CRLF)\.$", m => "換行已轉做 " + m.Groups[1].Value + "。");
            AddFn(@"^Read (.+) as (.+?)(; only the first (.+) bytes were read)?\.$", m => "已將 " + m.Groups[1].Value + " 當 " + m.Groups[2].Value + " 讀取" + (m.Groups[3].Success ? "；只讀取咗頭 " + m.Groups[4].Value + " 位元組" : "") + "。");
            AddFn(@"^Sample of (.+) characters\. Positions are in UTF-16 units, like \.NET\.$", m => "樣本共 " + m.Groups[1].Value + " 個字元。位置同 .NET 一樣以 UTF-16 單位計算。");
            AddFn(@"^Most added time: (.+) \(about (.+) ms on this sample\)\.$", m => "增加最多時間：" + m.Groups[1].Value + "（喺呢個樣本約 " + m.Groups[2].Value + " 毫秒）。");
            AddFn(@"^Both find the same (\d+) match\(es\)\.(.*)$", m => "兩個都搵到相同嘅 " + m.Groups[1].Value + " 個配對。" + RpSpeed(m.Groups[2].Value));
            AddFn(@"^Saved\. The history keeps the (\d+) newest results\.$", m => "已儲存。紀錄會保留最新嘅 " + m.Groups[1].Value + " 個結果。");
            AddFn(@"^Unknown placeholder\(s\), printed as written: (.+)$", m => "有不明佔位符，會照原樣輸出：" + m.Groups[1].Value);
            AddFn(@"^Saved · (\d+) of (\d+)$", m => "已儲存 · " + m.Groups[1].Value + " / " + m.Groups[2].Value);
            AddFn(@"^Template for (.+)$", m => "範本：" + m.Groups[1].Value);
            AddFn(@"^Value for parameter (.+) of (.+)$", m => "參數 " + m.Groups[1].Value + " 嘅數值（" + m.Groups[2].Value + "）");
            AddFn(@"^No value for: (.+)\. The placeholder stays in the pattern\.$", m => "冇以下參數嘅數值：" + m.Groups[1].Value + "。佔位符會留喺表達式入面。");
            AddFn(@"^Moved (.+) (before|after) (.+)\.$", m => "已將 " + m.Groups[1].Value + " 移到 " + m.Groups[3].Value + (m.Groups[2].Value == "before" ? " 之前。" : " 之後。"));
            AddFn(@"^and (\d+) older result\(s\)\.$", m => "仲有 " + m.Groups[1].Value + " 個較舊嘅結果。");
            AddFn(@"^Restore (.+) · (\d+) file\(s\)$", m => "還原 " + m.Groups[1].Value + " · " + m.Groups[2].Value + " 個檔案");
            AddFn(@"^Opened the file at line (\d+) in (.+)\.$", m => "已用 " + m.Groups[2].Value + " 開啟檔案，跳到第 " + m.Groups[1].Value + " 行。");
            AddFn(@"^#(\d+) line (\d+), at (\d+): (.*)$", m => "第 " + m.Groups[1].Value + " 個，第 " + m.Groups[2].Value + " 行，位置 " + m.Groups[3].Value + "：" + m.Groups[4].Value);
            AddFn(@"^#(\d+) line (\d+), at (\d+): empty match$", m => "第 " + m.Groups[1].Value + " 個，第 " + m.Groups[2].Value + " 行，位置 " + m.Groups[3].Value + "：空配對");
            AddFn(@"^#(\d+) at (\d+): empty match$", m => "第 " + m.Groups[1].Value + " 個，位置 " + m.Groups[2].Value + "：空配對");

            // ---- Files: archives, binary files and file type settings
            AddFn(@"^There is already a rule for (\S+)\.$", m => m.Groups[1].Value + " 已經有規則。");
            AddFn(@"^Leave every (\S+) file out of the search$", m => "搜尋時略過所有 " + m.Groups[1].Value + " 檔案");
            AddFn(@"^Skip (\S+) files$", m => "略過 " + m.Groups[1].Value + " 檔案");
            AddFn(@"^Remove the rule for (\S+)$", m => "移除 " + m.Groups[1].Value + " 嘅規則");
            AddFn(@"^Binary: (Default|Skip|Bytes|Text)$", m => "二進制：" + RpChoice(m.Groups[1].Value));
            AddFn(@"^Encoding: (Auto|UTF-8|UTF-16 LE|UTF-16 BE|Latin-1)$", m => "編碼：" + RpChoice(m.Groups[1].Value));
            AddFn(@"^Binary handling for (\S+) files: (Default|Skip|Bytes|Text)$", m => m.Groups[1].Value + " 檔案嘅二進制處理：" + RpChoice(m.Groups[2].Value));
            AddFn(@"^Encoding for (\S+) files: (Auto|UTF-8|UTF-16 LE|UTF-16 BE|Latin-1)$", m => m.Groups[1].Value + " 檔案嘅編碼：" + RpChoice(m.Groups[2].Value));
            AddFn(@"^When a (\S+) file looks binary: Default uses the options above, Skip leaves it out, Bytes searches it byte by byte, Text reads it as text\. Press to change\.$",
                m => m.Groups[1].Value + " 檔案睇落係二進制時：預設用上面嘅選項，略過即係唔搜尋，位元組即係逐個位元組搜尋，文字即係當文字讀取。撳一下轉換。");
            AddFn(@"^The encoding (\S+) files are read with: Auto detects it, the others force it\. Press to change\.$", m => "讀取 " + m.Groups[1].Value + " 檔案用嘅編碼：自動會偵測，其他會強制使用。撳一下轉換。");
            AddFn(@"^(\d+) file\(s\) with matches inside archives or searched as bytes are not changed: a replace only writes ordinary text files\.$", m => "有 " + m.Groups[1].Value + " 個喺壓縮檔入面或者逐個位元組搜尋嘅檔案有配對，但唔會改：取代只會寫入普通文字檔。");
            AddFn(@"^Byte offset (\d+) in (.+): (.*)$", m => m.Groups[2].Value + " 位元組位移 " + m.Groups[1].Value + "：" + m.Groups[3].Value);
            AddFn(@"^Skipped by the file type setting for (\S+)\.$", m => "因為 " + m.Groups[1].Value + " 嘅檔案類型設定而略過。");
            AddFn(@"^The archive unpacks to more than (\d+) MB, so (it was not searched|the rest of it was not searched)\.$", m => "壓縮檔解壓後超過 " + m.Groups[1].Value + " MB，所以" + (m.Groups[2].Value.StartsWith("the rest") ? "其餘部分" : "") + "冇搜尋。");
            AddFn(@"^7-Zip could not (list|unpack) the archive \(exit code (-?\d+)\)(: it is encrypted)?\.$", m => "7-Zip 無法" + (m.Groups[1].Value == "list" ? "列出" : "解壓") + "壓縮檔（結束代碼 " + m.Groups[2].Value + "）" + (m.Groups[3].Success ? "：壓縮檔已加密" : "") + "。");
            AddFn(@"^The archive could not be read: (.+)$", m => "無法讀取壓縮檔：" + m.Groups[1].Value);
            AddFn(@"^(.+) · (\d+) archive\(s\) opened$", m => Language.Yue(m.Groups[1].Value) is string y ? y + " · 開咗 " + m.Groups[2].Value + " 個壓縮檔" : null);

            // ---- Build: dragging and the conditional editor
            AddFn(@"^Dragging (.+)\. Release it on a neighbouring part at the same level to swap them\.$", m => "拖緊 " + m.Groups[1].Value + "。喺同一層相鄰嘅部分放開就會對調。");

            // ---- Debug: newly traced constructs
            AddFn(@"^Conditional: (group ""(.*)""|group (\d+)) has captured text, so try the yes branch$", m => "條件式：" + (m.Groups[2].Success ? "群組「" + m.Groups[2].Value + "」" : "群組 " + m.Groups[3].Value) + " 已擷取到文字，所以試成立分支");
            AddFn(@"^Conditional: (group ""(.*)""|group (\d+)) has not captured anything, so try the (no branch|empty no branch)$", m => "條件式：" + (m.Groups[2].Success ? "群組「" + m.Groups[2].Value + "」" : "群組 " + m.Groups[3].Value) + " 未擷取到任何嘢，所以試" + (m.Groups[4].Value.StartsWith("empty") ? "空白嘅" : "") + "唔成立分支");
            AddFn(@"^Conditional: test the (negative )?(look-ahead|look-behind) condition without consuming text$", m => "條件式：測試" + (m.Groups[1].Success ? "否定" : "") + (m.Groups[2].Value == "look-ahead" ? "前瞻" : "後顧") + "條件，唔會消耗文字");
            AddFn(@"^Inline options \(\?(.+)\) apply from here to the end of the enclosing group$", m => "內嵌選項 (?" + m.Groups[1].Value + ") 由呢度開始套用，直到所屬群組結尾");
            AddFn(@"^Not supported by the trace: (.+)\. The trace never guesses, so no trace is shown for this pattern\.$", m => "追蹤唔支援：" + RpTrace(m.Groups[1].Value) + "。追蹤絕不估計，所以呢個表達式唔會顯示追蹤。");
        }

        private static string RpChoice(string c) => c switch
        {
            "Default" => "預設", "Skip" => "略過", "Bytes" => "位元組", "Text" => "文字", "Auto" => "自動", _ => c,
        };

        private static string RpTrace(string reason)
        {
            if (reason == "the ignore-whitespace option x") return "忽略空白選項 x";
            var m = Regex.Match(reason, @"^conditional \(\?\((.*)\)\.\.\.\) whose condition names no group, which \.NET tests as an expression$");
            if (m.Success) return "條件式 (?(" + m.Groups[1].Value + ")...)，佢嘅條件冇指向任何群組，.NET 會當佢係表達式測試";
            m = Regex.Match(reason, @"^conditional \(\?\((.*)\)\.\.\.\) whose condition is an expression rather than a group or a look-around test$");
            if (m.Success) return "條件式 (?(" + m.Groups[1].Value + ")...)，佢嘅條件係表達式，唔係群組或環顧測試";
            if (reason == "conditional whose condition is not a group name, a group number or a look-around test") return "條件唔係群組名稱、群組編號或環顧測試嘅條件式";
            if (reason == "a quantifier after inline options") return "內嵌選項後面嘅數量詞";
            if (reason == "inline comment (?#...)") return "內嵌註解 (?#...)";
            m = Regex.Match(reason, @"^Unicode property \\p\{(.+)\} \(only the general categories such as L, Lu, Nd are traced\)$");
            if (m.Success) return "Unicode 屬性 \\p{" + m.Groups[1].Value + "}（只追蹤 L、Lu、Nd 呢類一般類別）";
            return reason;
        }

        private static string RpLineEndings(string s)
        {
            if (s == "no line breaks") return "冇換行";
            return Regex.Replace(s, @"(\d+) (CRLF|LF|CR)", "$1 個 $2");
        }

        private static string RpSpeed(string s)
        {
            s = s.Trim();
            if (s.Length == 0) return "";
            var m = Regex.Match(s, @"^([AB]) is about (.+) times faster\.$");
            if (m.Success) return " " + m.Groups[1].Value + " 大約快 " + m.Groups[2].Value + " 倍。";
            if (s.StartsWith("The times are within noise")) return " 兩者時間相差喺雜訊範圍內。";
            return " " + s;
        }
    }
}
