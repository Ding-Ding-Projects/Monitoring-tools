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
            Add("A small built-in interpreter replays one match attempt of the default backtracking engine. It covers literals, classes (including Unicode categories), anchors, word boundaries, groups, look-ahead and look-behind, atomic groups, alternation, greedy and lazy repeats and backreferences. Before a trace is shown the match and every capture group are checked against the real .NET engine on the same subject; if they disagree, or the pattern uses something the interpreter does not cover, you are told and nothing is faked.",
                "一個內建嘅小型直譯器會重播預設回溯引擎嘅一次配對嘗試。支援字面字元、字元類（包括 Unicode 類別）、錨點、字詞邊界、群組、前瞻同後顧、原子群組、或選擇、貪婪同惰性重複，同反向參照。顯示追蹤前，會用同一段文字對照真嘅 .NET 引擎核對配對同每個擷取群組；如果不一致，或者表達式用咗直譯器未支援嘅語法，會直接話你知，絕不作假。");

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
