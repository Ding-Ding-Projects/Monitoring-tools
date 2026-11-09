using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MonitoringTools.Core
{
    /// <summary>
    /// Patterns for built strings that carry a number, a name or another phrase that has its own entry.
    /// These run before the older patterns, so a more specific wording here wins. A function may return null
    /// to say "this does not apply", and the lookup carries on.
    /// </summary>
    internal static partial class Strings
    {
        private static void AddMorePatterns(List<(Regex, Func<Match, string>)> p)
        {
            void Add(string rx, string yue) => p.Add((new Regex(rx, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline), m => m.Result(yue)));
            void AddFn(string rx, Func<Match, string> fn) => p.Add((new Regex(rx, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline), fn));
            // The Cantonese of an inner phrase when it has one, otherwise the phrase as it is (a name, a code, a path).
            string Inner(string s) => Language.Yue(s) ?? s;
            // The Cantonese of an inner phrase, or null when it has none, so the whole pattern steps aside.
            string Known(string s) => Language.Yue(s);

            // ---- the Conversion page's format list (before "About ..." so a note that starts with "About" is not read as a title)
            AddFn(@"^(.+) · from ((?:video|audio|image)(?:, (?:video|audio|image))*)$", m =>
            {
                var kinds = string.Join("、", m.Groups[2].Value.Split(", ").Select(k => k == "video" ? "影片" : k == "audio" ? "音訊" : "圖片"));
                return Inner(m.Groups[1].Value) + " · 可以由" + kinds + "轉換";
            });
            Add(@"^(\d+) formats$", "$1 種格式");
            // colour previews in a copied text's toast
            Add(@"^Colours in this copy · (\d+)$", "呢次複製有嘅顏色 · $1 個"); Add(@"^Colours in this copy · first (\d+)$", "呢次複製有嘅顏色 · 頭 $1 個");
            Add(@"^copied (#[0-9A-F]{6,8})$", "已複製 $1"); Add(@"^(\d+)% opaque$", "$1% 不透明");
            Add(@"^(\d+) formats\. Copy a path with :format on the end, or pick one here and choose files\.$", "$1 種格式。喺路徑後面加 :格式 再複製，或者喺度揀一種，再揀檔案。");
            AddFn(@"^(All|Video|Audio|Image) (\d+)$", m => (m.Groups[1].Value switch { "All" => "全部", "Video" => "影片", "Audio" => "音訊", _ => "圖片" }) + " " + m.Groups[2].Value);
            AddFn(@"^Show (all|video|audio|image) formats$", m => "顯示" + (m.Groups[1].Value switch { "all" => "所有", "video" => "影片", "audio" => "音訊", _ => "圖片" }) + "格式");
            AddFn(@"^Choose files to convert to (.+)\. Each one is saved next to its original\.$", m => "揀要轉換成「" + Inner(m.Groups[1].Value) + "」嘅檔案。每個都會存喺原檔隔籬。");
            AddFn(@"^Convert files to (.+)$", m => "將檔案轉換成「" + Inner(m.Groups[1].Value) + "」");
            Add(@"^This creates the shortcut :(\S+) and saves its script as (\S+) in the scripts folder\.$", "會建立捷徑 :$1，並將腳本存做 scripts 資料夾入面嘅 $2。");
            // ---- typed number fields: messages, tooltip and stepper names
            Add(@"^Not changed: enter a whole number from (.+) to (.+)\.$", "冇改到：請輸入 $1 至 $2 之間嘅整數。");
            Add(@"^Not changed: enter a number from (.+) to (.+)\.$", "冇改到：請輸入 $1 至 $2 之間嘅數字。");
            Add(@"^Rounded to (.+)\.$", "已四捨五入到 $1。");
            Add(@"^Type a number from (.+) to (.+)\. Up and Down keys step it\.$", "輸入 $1 至 $2 之間嘅數字。用上下鍵一格一格調整。");
            AddFn(@"^Decrease (.+)$", m => "減少「" + Inner(m.Groups[1].Value) + "」");
            AddFn(@"^Increase (.+)$", m => "增加「" + Inner(m.Groups[1].Value) + "」");

            // ---- counts and labels
            AddFn(@"^About (.+)$", m => "關於「" + Inner(m.Groups[1].Value) + "」");
            Add(@"^(\d+) of (\d+) settings and sections$", "$1／$2 個設定同分頁");
            Add(@"^(\d+) settings and sections$", "$1 個設定同分頁");
            Add(@"^(\d+) models?$", "$1 個模型"); Add(@"^(\d+) voices?$", "$1 把語音"); Add(@"^(\d+) entries$", "$1 個項目");
            Add(@"^(\d+) articles?$", "$1 篇文章"); Add(@"^(\d+) shortcuts?$", "$1 個捷徑"); Add(@"^(\d+) notifications?$", "$1 個通知");
            Add(@"^(\d+) fonts$", "$1 款字體"); Add(@"^(\d+) commands$", "$1 個指令"); Add(@"^(\d+) menu items$", "$1 個選單項目");
            Add(@"^(\d+) versions$", "$1 個版本"); Add(@"^(\d+) min$", "$1 分鐘"); Add(@"^(\d+) minutes$", "$1 分鐘"); Add(@"^(\d+) hours?$", "$1 小時");
            Add(@"^(\d+) entries$", "$1 個項目"); Add(@"^(\d+) tickets?$", "$1 張單據"); Add(@"^(\d+) accounts?$", "$1 個帳戶");
            Add(@"^(\d+) node\(s\)$", "$1 個節點"); Add(@"^(\d+) capturing group\(s\)$", "$1 個擷取群組");
            Add(@"^Valid pattern · (\d+) capturing group\(s\)$", "有效嘅表達式 · $1 個擷取群組");

            Add(@"^You have (.+)\. Grab it from Release notes\.$", "你而家用緊 $1。請到「版本說明」下載。");

            // ---- selection, accounts, window titles, dim sum status
            AddFn(@"^Select every (.+) the search currently shows\. Rows the search hides stay unselected\.$", m => "揀晒搜尋目前顯示嘅" + Inner(m.Groups[1].Value) + "。被搜尋隱藏嘅列唔會被揀。");
            AddFn(@"^Select (.+)$", m => "揀 " + Inner(m.Groups[1].Value));
            AddFn(@"^(\d+) releases fetched from GitHub (.+)\.$", m => "已由 GitHub 取得 " + m.Groups[1].Value + " 個版本（" + Inner(m.Groups[2].Value) + "）。");
            Add(@"^(\d+) releases?$", "$1 個版本"); Add(@"^(\d+) rules?$", "$1 條規則");
            AddFn(@"^Account (.+?)(, active)?$", m => "帳戶 " + m.Groups[1].Value + (m.Groups[2].Success ? "，使用中" : ""));
            Add(@"^(.+?)  \(active\)  ·  (.+)$", "$1  （使用中）  ·  $2");
            Add(@"^(\d+) accounts? signed in\. Tokens stay in the GitHub CLI's credential store; this app only reads the names\.$", "$1 個帳戶已登入。權杖留喺 GitHub CLI 嘅憑證庫，呢個程式只會讀取名稱。");
            AddFn(@"^Drawn this launch but not shown: (.+)\.$", m => "今次抽中咗但冇顯示：" + Inner(m.Groups[1].Value) + "。");
            AddFn(@"^Drawn this launch but nothing could be shown: (.+)\.$", m => "今次抽中咗但冇嘢可以顯示：" + Inner(m.Groups[1].Value) + "。");
            AddFn(@"^Starting point: (.+)\. Runs with (.+)\.$", m => "起點：" + Inner(m.Groups[1].Value) + "。用 " + m.Groups[2].Value + " 執行。");
            Add(@"^Runs with: (.+) \((.+)\), set by the starting point you chose\.$", "執行方式：$1 ($2)，由你揀嘅起點決定。");

            // ---- status lines with numbers
            Add(@"^(\S+) context$", "$1 上下文");
            Add(@"^(\d+) ms limit$", "上限 $1 毫秒");
            Add(@"^backtracking$", "回溯");
            Add(@"^(\d+) match\(es\) · ([\d.]+) ms measured$", "$1 個配對 · 量度到 $2 毫秒");
            AddFn(@"^(\d\d:\d\d) to (\d\d:\d\d), (.+): (.+)$", m => m.Groups[1].Value + " 至 " + m.Groups[2].Value + "，" + Inner(m.Groups[3].Value) + "：" + m.Groups[4].Value);
            Add(@"^down (\d+) m (\d+) s · next probe (\d+) s$", "已斷線 $1 分 $2 秒 · $3 秒後再探測");
            AddFn(@"^Now using (.+): (.+)$", m => "而家用緊 " + Inner(m.Groups[1].Value) + "：" + m.Groups[2].Value);
            Add(@"^Watching: (.+)\. Edit the list in the control centre\.$", "監察緊：$1。可以喺控制中心編輯清單。");
            AddFn(@"^Preview: (.+)$", m => "預覽：" + m.Groups[1].Value);
            AddFn(@"^Opens: (.+)$", m => "開啟：" + Inner(m.Groups[1].Value));
            Add(@"^(\d+) x (\d+) chunks = (\d+) x (\d+) blocks, ([\d,]+) chunks in (\d+) batches of at most (\d+) chunks, centred on chunk (.+) \(block (.+)\) in each chosen dimension \(the Overworld always, the Nether and the End if switched on below, with no coordinate scaling\)\. Time and disk grow with the square of the radius\.$",
                "$1 x $2 個區塊 = $3 x $4 格，共 $5 個區塊，分 $6 批，每批最多 $7 個區塊，以區塊 $8（方塊 $9）為中心，用喺每個所揀嘅維度（主世界必定包括，如果下面開啟咗就包括地獄同終界，座標唔作縮放）。所需時間同磁碟空間會隨半徑嘅平方增長。");
            Add(@"^Fits on the GPU\. Needs about ([\d.]+) GB; your best GPU reports ([\d.]+) GB of memory\. Should run fast\.$", "啱晒喺 GPU 度行。大約需要 $1 GB；你最好嘅 GPU 報告有 $2 GB 記憶體。應該行得快。");
            Add(@"^Runs, partly on the CPU\. Needs about ([\d.]+) GB; you have ([\d.]+) GB of free RAM and ([\d.]+) GB of GPU memory\. It will work but slower than a full GPU fit\.$", "一部分喺 CPU 度行。大約需要 $1 GB；你有 $2 GB 可用 RAM 同 $3 GB GPU 記憶體。行得到，不過會慢過完全放入 GPU 嘅情況。");
            Add(@"^Tight: close other programs\. Needs about ([\d.]+) GB, you have ([\d.]+) GB free of ([\d.]+) GB RAM\. It fits only if other programs release memory, and may swap to disk\.$", "好緊：請關閉其他程式。大約需要 $1 GB，你有 $2 GB 可用（總共 $3 GB RAM）。要其他程式釋放記憶體先放得入，而且可能會用到磁碟交換。");
            Add(@"^RAM ([\d.]+) GB free of ([\d.]+) GB · GPU: (.+) \(([\d.]+) GB\) · Disk ([\d.]+) GB free on (.+?) \(models folder (.+)\)$", "RAM：$2 GB 中有 $1 GB 可用 · GPU：$3（$4 GB） · 磁碟：$6 尚有 $5 GB 可用（模型資料夾 $7）");
            Add(@"^i  Port (\d+) is free$", "i  埠 $1 冇人用");
            Add(@"^Showing the built-in snapshot of (\d+) models from (.+)\.$", "顯示內置嘅 $1 個模型快照（$2）。");
            Add(@"^Saved chats are plain JSON files in (.+)\. Attached image bytes are not stored, only the file names; text files are part of the saved message\.$", "已儲存嘅對話係純 JSON 檔案，位置：$1。附加圖片嘅資料唔會儲存，只會儲存檔案名稱；文字檔會成為已儲存訊息嘅一部分。");
            Add(@"^running as a standard user from (.+)$", "以標準使用者身分運行，位置：$1");
            Add(@"^updated (.+)$", "更新於 $1");
            AddFn(@"^Example: (.+?)\s{3}or\s{3}(.+)$", m => "例子：" + m.Groups[1].Value + "   或者   " + m.Groups[2].Value);
            AddFn(@"^Format: (.+)$", m => "格式：" + m.Groups[1].Value.Replace("ordinary phrase", "一般詞語").Replace("your words", "你嘅用字"));
            Add(@"^up to (\d+) KiB$", "最多 $1 KiB");
            Add(@"^The app data folder is: (.+)$", "程式資料資料夾係：$1");

            // ---- counts, times and activity rows
            Add(@"^(\d+) locks?$", "$1 個鎖"); Add(@"^(\d+) kept$", "保留 $1 個");
            Add(@"^Showing the newest (\d+) of (\d+) snapshots\.$", "顯示最新 $1 個，共 $2 個快照。");
            Add(@"^recent  (#\w+)$", "最近用過  $1");
            AddFn(@"^(Mon|Tue|Wed|Thu|Fri|Sat|Sun) (\d\d:\d\d)( · ongoing)?$", m =>
            {
                var d = new System.Collections.Generic.Dictionary<string, string> { ["Mon"] = "一", ["Tue"] = "二", ["Wed"] = "三", ["Thu"] = "四", ["Fri"] = "五", ["Sat"] = "六", ["Sun"] = "日" }[m.Groups[1].Value];
                return "星期" + d + " " + m.Groups[2].Value + (m.Groups[3].Success ? " · 持續中" : "");
            });
            string Span(string s) => Regex.Replace(s, @"^(\d+) h (\d+) m$", "$1 小時 $2 分").Replace(" m ", " 分 ").Replace(" s", " 秒").Replace(" h ", " 小時 ");
            AddFn(@"^(\d{4}-\d\d-\d\d \d\d:\d\d:\d\d)  lasted (.+?)  (.+)$", m => m.Groups[1].Value + "  維持咗 " + Span(m.Groups[2].Value) + "  " + Inner(m.Groups[3].Value));
            AddFn(@"^(\d{4}-\d\d-\d\d \d\d:\d\d:\d\d)  ongoing  (.+)$", m => m.Groups[1].Value + "  持續中  " + Inner(m.Groups[2].Value));
            Add(@"^Encrypted (\d+) items?$", "已加密 $1 個項目"); Add(@"^Decrypted (\d+) items?$", "已解密 $1 個項目");
            Add(@"^Committed (\d+) files? and pushed (.+)$", "已提交 $1 個檔案並推送 $2");
            Add(@"^(:\S+) finished in ([\d.]+) s$", "$1 用咗 $2 秒完成");

            // ---- Ollama
            Add(@"^Add (\S+:\S+) to the pull cart$", "將 $1 加入下載購物車");
            Add(@"^Delete (\S+:\S+)$", "刪除 $1"); Add(@"^Details of (\S+:\S+)$", "$1 嘅詳細資料");
            Add(@"^Delete (\S+:\S+) from disk \(asks twice\)\.$", "由磁碟刪除 $1（會問兩次）。");
            Add(@"^Queues (\S+) in the pull cart below\. Nothing is downloaded until you press Pull all\.$", "將 $1 排入下面嘅下載購物車。撳「全部下載」之前唔會下載任何嘢。");
            Add(@"^Show parameters, template, context length and licence for (\S+)\.$", "顯示 $1 嘅參數、範本、上下文長度同授權。");
            Add(@"^Use (\S+:\S+) for the chat test$", "用 $1 做對話測試");
            Add(@"^(.+)\. ([\d.,]+) GB free on (.+)\.$", "$1。$3 尚有 $2 GB 可用空間。");
            Add(@"^(.+server\.log)\. The last lines are shown below when you press Show log\.$", "$1。撳「顯示紀錄檔」就會喺下面顯示最後幾行。");
            Add(@"^Version ([\d.]+) on ([\d.:]+)\.$", "版本 $1，位址 $2。");

            // ---- Safety
            AddFn(@"^Algorithm: (.+)$", m => "演算法：" + m.Groups[1].Value);
            AddFn(@"^Category: (.+)$", m => "類別：" + Inner(m.Groups[1].Value));
            Add(@"^Digits: (\d+)$", "位數：$1"); Add(@"^Period in seconds: (\d+) s$", "週期（秒）：$1 秒");
            AddFn(@"^Severity: (.+)$", m => "嚴重程度：" + Inner(m.Groups[1].Value));
            AddFn(@"^Default unlock time for the next lock you make: (.+)$", m => "你下一個建立嘅鎖預設解鎖時間：" + Inner(m.Groups[1].Value));
            Add(@"^Locked out for good\? Delete the file locks\.dat in (.+) \(or the whole (.+) folder\) and every lock is gone\.$", "永遠鎖死？刪除 $1 入面嘅 locks.dat 檔案（或者成個 $2 資料夾），所有鎖就會消失。");
            Add(@"^Self-check: (\d+) of (\d+) published RFC 6238 vectors match\.$", "自我檢查：$2 個已公布嘅 RFC 6238 測試向量，吻合 $1 個。");

            // ---- Records and Modes
            AddFn(@"^Export: (.+)$", m => "匯出：" + Inner(m.Groups[1].Value));
            AddFn(@"^Write as: (.+)$", m => "寫成：" + Inner(m.Groups[1].Value));
            AddFn(@"^Show only (.+) notifications$", m => { var k = Known(m.Groups[1].Value); return k == null ? null : "只顯示「" + k + "」通知"; });
            Add(@"^Release notes from GitHub\. You are running (.+)\.$", "來自 GitHub 嘅版本說明。你而家用緊 $1。");
            AddFn(@"^Status: (.+)$", m => "狀態：" + Inner(m.Groups[1].Value));
            AddFn(@"^Following: (.+)$", m => "跟隨中：" + (m.Groups[1].Value == "yes" ? "是" : m.Groups[1].Value == "no" ? "否" : m.Groups[1].Value));
            AddFn(@"^This launch: (.+)$", m => { var k = Known(m.Groups[1].Value); return k == null ? null : "今次啟動：" + k; });
            Add(@"^Times use this computer's clock in (.+)\. A window that crosses midnight belongs to the day it starts on\. Equal start and end mean all day\. Daylight-saving changes follow the wall clock, so a window inside a skipped hour does not happen that day\. If rules overlap on one setting, the one lower in the list wins\.$",
                "時間跟隨呢部電腦嘅時鐘（$1）。跨過午夜嘅時段屬於佢開始嗰一日。開始同結束時間一樣即係成日。夏令時間跟住牆鐘變，所以落入被跳過嗰個鐘嘅時段嗰日唔會發生。如果多條規則重疊喺同一個設定，排喺清單較後嗰條優先。");

            // ---- Tools: the editor list
            AddFn(@"^(✓|✕) (.+?) — (.+)$", m =>
            {
                var tail = m.Groups[3].Value;
                string t;
                if (tail == "not found") t = "未搵到";
                else if (Known(tail) != null) t = Known(tail);
                else if (tail.EndsWith(" (machine install)", StringComparison.Ordinal)) t = tail.Substring(0, tail.Length - " (machine install)".Length) + "（本機安裝）";
                else if (tail.EndsWith(" (user install)", StringComparison.Ordinal)) t = tail.Substring(0, tail.Length - " (user install)".Length) + "（使用者安裝）";
                else t = tail;
                return m.Groups[1].Value + " " + Inner(m.Groups[2].Value) + " — " + t;
            });
            Add(@"^whatever Windows opens this file type with$", "Windows 用嚟開啟呢類檔案嘅程式");
            AddFn(@"^Open in (.+)$", m => "用 " + m.Groups[1].Value + " 開啟");

            // ---- Command palette, help, setup wizard
            AddFn(@"^Pages: (.+)$", m => "頁面：" + Inner(m.Groups[1].Value));
            AddFn(@"^Settings: (.+)$", m => "設定：" + Inner(m.Groups[1].Value));
            AddFn(@"^Actions: (.+)$", m => "操作：" + Inner(m.Groups[1].Value));
            AddFn(@"^Task: (.+)$", m => "任務：" + Inner(m.Groups[1].Value));
            AddFn(@"^Topic: (.+)$", m => "主題：" + Inner(m.Groups[1].Value));
            AddFn(@"^Switch DNS to (.+)$", m => "轉 DNS 去 " + Inner(m.Groups[1].Value));
            AddFn(@"^Step (\d+): (.+), (current|done)$", m => "第 " + m.Groups[1].Value + " 步：" + Inner(m.Groups[2].Value) + "，" + (m.Groups[3].Value == "current" ? "目前" : "已完成"));
            AddFn(@"^Step (\d+): (.+)$", m => "第 " + m.Groups[1].Value + " 步：" + Inner(m.Groups[2].Value));
            AddFn(@"^Pattern for (.+), kept in step with the search box$", m => "「" + Inner(m.Groups[1].Value) + "」嘅表達式，與搜尋框保持同步");
            AddFn(@"^Regex builder for (.+)$", m => "「" + Inner(m.Groups[1].Value) + "」嘅正規表示式建構器");

            // ---- Colour picker
            Add(@"^(HEX|RGB|HSL|HSV|HWB|LAB|LCH|OKLCH|CMYK) value$", "$1 數值"); Add(@"^Copy (HEX|RGB|HSL|HSV|HWB|LAB|LCH|OKLCH|CMYK)$", "複製 $1");
            Add(@"^Copy the (HEX|RGB|HSL|HSV|HWB|LAB|LCH|OKLCH|CMYK) value$", "複製 $1 數值");
            Add(@"^Use colour (.+)$", "用顏色 $1");

            // ---- Regex builder
            Add(@"^Alternative (\d+)$", "選項 $1"); Add(@"^Alternative (\d+) of (\d+)$", "第 $1 個選項，共 $2 個");
            AddFn(@"^(.+)\. Enter selects it in the pattern; Left and Right collapse and expand\.$", m => { var k = Known(m.Groups[1].Value); return k == null ? null : k + "。按 Enter 會喺表達式中選取；按左右方向鍵可以收合同展開。"; });
            Add(@"^Group (\d+)$", "群組 $1"); Add(@"^Group ""(.*)"" \((\d+)\)$", "群組「$1」（$2）");
            Add(@"^Capturing group (\d+): remembers the text it matches \(characters (\d+) to (\d+)\)$", "擷取群組 $1：記住佢配對到嘅文字（第 $2 至 $3 個字元）");
            Add(@"^Capturing group (\d+): remembers the text it matches$", "擷取群組 $1：記住佢配對到嘅文字");
            Add(@"^Capturing group named ""(.*)"" \(number (\d+)\): remembers the text it matches$", "具名擷取群組「$1」（編號 $2）：記住佢配對到嘅文字");
            Add(@"^Enter named group ""(.*)""$", "進入具名群組「$1」");
            Add(@"^The text ""(.*)""$", "文字「$1」"); Add(@"^The literal character ""(.*)""$", "字面字元「$1」"); Add(@"^Literal ""(.*)""$", "字面「$1」");
            Add(@"^backreference to group (\d+)$", "反向參照群組 $1"); Add(@"^exactly (\d+)$", "剛好 $1 個");
            Add(@"^Match any one of (\d+) alternatives, trying them from left to right$", "配對 $1 個選項其中任何一個，由左至右逐個試");
            Add(@"^Iteration (\d+) of (greedy|lazy) (.+)$", "$2 $3 嘅第 $1 次重複");
            Add(@"^Start a match attempt at position (\d+)$", "喺位置 $1 開始一次配對嘗試");
            AddFn(@"^Checked against the \.NET engine: same result \((.+)\)\.$", m => "已同 .NET 引擎核對：結果一樣（" + Inner(m.Groups[1].Value) + "）。");
            AddFn(@"^Not available in \.NET: (.+)$", m => ".NET 冇提供：" + Inner(m.Groups[1].Value));
            AddFn(@"^Not valid: (.+) at offset (\d+)\. (.+)$", m => "表達式無效：" + m.Groups[1].Value + "，位置 " + m.Groups[2].Value + "。" + Inner(m.Groups[3].Value));
            Add(@"^SELECTED: (.*)$", "已選：$1");
            AddFn(@"^Step ([\d,]+) of ([\d,]+) at text position ([\d,]+): (.+)$", m => "第 " + m.Groups[1].Value + " 步，共 " + m.Groups[2].Value + " 步，文字位置 " + m.Groups[3].Value + "：" + Inner(m.Groups[4].Value));
            AddFn(@"^Iteration (\d+) of (greedy|lazy) (.+)$", m => "第 " + m.Groups[1].Value + " 次重複（" + (m.Groups[2].Value == "greedy" ? "貪婪" : "惰性") + " " + m.Groups[3].Value + "）");
            Add(@"^(.+) matches '(.*)'$", "$1 配對「$2」");
            AddFn(@"^(Black|Bold|Light|Medium|Regular|Semibold) weight$", m => Inner(m.Groups[1].Value) + "字重");
            AddFn(@"^Show the (Build|Test|Replace|Debug|Compare|Files|Code|Library|Benchmark) view$", m => "顯示「" + Inner(m.Groups[1].Value) + "」檢視");
            Add(@"^One of (\d+) alternatives$", "$1 個選項其中之一");
            AddFn(@"^Any except (.*)$", m => "除咗 " + m.Groups[1].Value + " 以外嘅任何一個");
            AddFn(@"^One of (.*)$", m => "其中之一：" + m.Groups[1].Value);
            Add(@"^one character that is not in the set (.*)$", "一個唔喺字元集 $1 入面嘅字元");
            Add(@"^one character from the set (.*)$", "一個喺字元集 $1 入面嘅字元");
            Add(@"^Options (.+)$", "選項 $1");
            Add(@"^Same as group (.+)$", "同群組 $1 一樣");
            Add(@"^the same text that the group ""(.*)"" matched$", "同群組「$1」配對到嘅文字一樣");
            Add(@"^the same text that group (\d+) matched \(or an octal character code when there is no such group\)$", "同群組 $1 配對到嘅文字一樣（如果冇呢個群組，就當做八進制字元碼）");
            Add(@"^capturing group (\d+): remembers the text it matches$", "擷取群組 $1：記住佢配對到嘅文字");
            Add(@"^capturing group named ""(.*)"" \(number (\d+)\): remembers the text it matches$", "具名擷取群組「$1」（編號 $2）：記住佢配對到嘅文字");
            Add(@"^the character (.+)$", "字元 $1"); Add(@"^the text ""(.*)""$", "文字「$1」"); Add(@"^Literal (.+)$", "字面 $1"); Add(@"^the literal character (.+)$", "字面字元 $1");
            Add(@"^Condition: group (.+)$", "條件：群組 $1"); Add(@"^the condition: the group ""(.*)"" has captured something$", "條件：群組「$1」已經擷取到嘢");
            Add(@"^Balancing group (.+)$", "平衡群組 $1");
            Add(@"^balancing group: matches only while ""(.*)"" has a capture, removes the latest ""(.*)"" capture, and records the text between that capture and here as a capture of ""(.*)""$", "平衡群組：只喺「$1」有擷取嗰陣先配對，移除最新嘅「$2」擷取，並將嗰個擷取同呢度之間嘅文字記錄成「$3」嘅擷取");
            Add(@"^balancing group: matches only while ""(.*)"" has a capture, and removes the latest ""(.*)"" capture without capturing anything itself$", "平衡群組：只喺「$1」有擷取嗰陣先配對，並移除最新嘅「$2」擷取，自己唔擷取任何嘢");
            AddFn(@"^Repeat the previous item (zero or more times|one or more times|\d+ or more times|exactly once|exactly \d+ times|zero or one time|\d+ to \d+ times)(, as few times as possible|, preferring to match it|, as many times as possible)$", m =>
            {
                var q = m.Groups[1].Value; string phrase;
                var mm = Regex.Match(q, @"^(\d+) or more times$");
                if (q == "zero or more times") phrase = "零次或以上"; else if (q == "one or more times") phrase = "一次或以上";
                else if (mm.Success) phrase = mm.Groups[1].Value + " 次或以上"; else if (q == "exactly once") phrase = "剛好一次";
                else if ((mm = Regex.Match(q, @"^exactly (\d+) times$")).Success) phrase = "剛好 " + mm.Groups[1].Value + " 次";
                else if (q == "zero or one time") phrase = "零次或一次";
                else { mm = Regex.Match(q, @"^(\d+) to (\d+) times$"); phrase = mm.Groups[1].Value + " 至 " + mm.Groups[2].Value + " 次"; }
                var tail = m.Groups[2].Value;
                return "重複前一項 " + phrase + (tail.Contains("few") ? "，盡量少重複" : tail.Contains("preferring") ? "，優先配對佢" : "，盡量多重複");
            });
            AddFn(@"^(\d+) or more(, lazy)?$", m => m.Groups[1].Value + " 個或以上" + (m.Groups[2].Success ? "，惰性" : ""));
            AddFn(@"^Exactly (\d+)(, lazy)?$", m => "剛好 " + m.Groups[1].Value + " 個" + (m.Groups[2].Success ? "，惰性" : ""));
            AddFn(@"^Optional(, lazy)?$", m => "可有可無" + (m.Groups[1].Success ? "，惰性" : ""));
            AddFn(@"^(\d+) to (\d+)(, lazy)?$", m => m.Groups[1].Value + " 至 " + m.Groups[2].Value + " 個" + (m.Groups[3].Success ? "，惰性" : ""));
            // an accessible name made of a token and its description, such as "\w+: 1 or more"
            AddFn(@"^(\S+): (.+)$", m => { var k = Known(m.Groups[2].Value); return k == null ? null : m.Groups[1].Value + "：" + k; });
            // an accessible name made of a title and its description, such as "Zip it. Creates a .zip next to the file"
            AddFn(@"^([^.]+)\. (.+)$", m => { var k = Known(m.Groups[2].Value); return k == null ? null : Inner(m.Groups[1].Value) + "。" + k; });
        }
    }
}
