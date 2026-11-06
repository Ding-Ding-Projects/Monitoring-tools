using System.Collections.Generic;

namespace MonitoringTools.Core
{
    /// <summary>
    /// Hong Kong Cantonese for the smart clipboard: recognised copies, quick conversions, lookups and the secret guard,
    /// clipboard history, the paste queue, pins, snippets, recipes, per-app rules, extensions and clipboard settings.
    /// </summary>
    internal static partial class Strings
    {
        private static void AddSmart(Dictionary<string, string> t)
        {
            void Add(string en, string yue) => t.TryAdd(en, yue);

            // ---- toast headings
            Add("You copied an amount", "你複製咗一個數量"); Add("You copied an amount of money", "你複製咗一筆錢");
            Add("You copied a time", "你複製咗一個時間"); Add("You copied a Unix time", "你複製咗一個 Unix 時間");
            Add("You copied a sum", "你複製咗一條數"); Add("You copied a phone number", "你複製咗一個電話號碼");
            Add("You copied an address", "你複製咗一個地址"); Add("You copied a flight number", "你複製咗一個航班編號");
            Add("You copied a book number", "你複製咗一個書號"); Add("You copied a bank account number (IBAN)", "你複製咗一個銀行戶口號碼（IBAN）");
            Add("You copied a tracking number", "你複製咗一個追蹤號碼"); Add("You copied a schedule", "你複製咗一個排程");
            Add("You copied a character", "你複製咗一個字元"); Add("You copied a stack trace", "你複製咗一段錯誤堆疊");
            Add("You copied a sign-in token (JWT)", "你複製咗一個登入權杖（JWT）"); Add("You copied an IP address", "你複製咗一個 IP 地址");
            Add("You copied a Git clone link", "你複製咗一條 Git 複製連結"); Add("You copied a Windows error code", "你複製咗一個 Windows 錯誤碼");
            Add("You copied a GUID", "你複製咗一個 GUID"); Add("You copied SQL", "你複製咗 SQL"); Add("You copied XML", "你複製咗 XML");
            Add("You copied a regular expression", "你複製咗一個正規表示式"); Add("You copied a package", "你複製咗一個套件");
            Add("You copied a Windows path", "你複製咗一個 Windows 路徑"); Add("You copied a table", "你複製咗一個表格");
            Add("You copied a secret", "你複製咗一個秘密"); Add("Tap one to copy it", "撳一個就複製");
            Add("kept out of history", "唔會入歷史"); Add("from an extension", "嚟自擴充功能"); Add("Show all", "顯示全部");

            // ---- toast buttons
            Add("All conversions", "所有換算"); Add("Call", "打電話"); Add("Text", "傳短訊"); Add("Open in Maps", "喺地圖開");
            Add("Directions", "路線"); Add("Flight status", "航班狀態"); Add("Open Library", "Open Library"); Add("Clear it now", "即刻清走");
            Add("Check a file against it", "用佢核對檔案"); Add("Ping", "Ping"); Add("Trace route", "追蹤路由"); Add("Who owns it", "邊個擁有");
            Add("Open in browser", "喺瀏覽器開"); Add("Clone", "複製倉庫"); Add("Search Microsoft Learn", "搜尋 Microsoft Learn");
            Add("Copy a new GUID", "複製一個新 GUID"); Add("Copy formatted", "複製排好版嘅"); Add("Copy tidy", "複製整齊版");
            Add("Explain and test it", "解釋同測試"); Add("Search the error", "搜尋呢個錯誤"); Add("Open as a table", "用表格開");
            Add("Translate with your local AI", "用本機 AI 翻譯"); Add("Translate on the web", "上網翻譯");
            Add("Copy all as a list", "全部複製成清單"); Add("Pin on screen", "釘喺螢幕"); Add("Read the text", "讀出入面啲字");
            Add("Summarise with your local AI", "用本機 AI 撮要"); Add("Rewrite more simply", "改寫得簡單啲"); Add("Explain with your local AI", "用本機 AI 解釋");
            Add("Copy as YAML", "複製成 YAML"); Add("SQL IN (…) list", "SQL IN (…) 清單");

            // ---- quick conversions
            Add("Quick conversions", "快速換算"); Add("Type or paste anything with a number and a unit.", "打或者貼任何有數字同單位嘅嘢。");
            Add("Click any result to copy it.", "撳任何結果就會複製。"); Add("Weight", "重量"); Add("Length", "長度"); Add("Volume", "容量");
            Add("Temperature", "溫度"); Add("Area", "面積"); Add("Speed", "速度"); Add("Data size", "資料大小"); Add("Time", "時間");
            Add("Energy", "能量"); Add("Pressure", "壓力"); Add("Fuel use", "油耗"); Add("Angle", "角度"); Add("Number bases", "進制"); Add("Money", "錢");

            // ---- clipboard history and power features
            Add("Clipboard history", "剪貼簿歷史"); Add("Search your copies", "搜尋你複製過嘅嘢"); Add("All", "全部"); Add("Links", "連結");
            Add("Pictures", "圖片"); Add("Files", "檔案"); Add("Pinned", "已釘選"); Add("Paste", "貼上"); Add("Plain", "純文字");
            Add("Pin or unpin", "釘選或取消"); Add("Add to the paste queue", "加入排隊貼上"); Add("Copy without pasting", "淨係複製唔貼");
            Add("Remove from history", "喺歷史移除"); Add("Open the History page", "開歷史頁");
            Add("Paste queue", "排隊貼上"); Add("Start pasting", "開始貼"); Add("Pause", "暫停"); Add("After each paste press Tab", "每次貼完撳 Tab");
            Add("Reverse the order", "倒轉次序"); Add("Clear the queue", "清空隊伍"); Add("Copy everything as one note", "全部複製成一段");
            Add("Queue finished", "隊伍貼晒"); Add("Pinned copy", "釘咗嘅複製"); Add("Copy again", "再複製"); Add("See-through", "透明度");
            Add("Clicks pass through", "滑鼠穿透"); Add("Collect mode is on", "收集模式開咗"); Add("Collect mode is off", "收集模式關咗");
            Add("Nothing to undo", "冇嘢可以還原"); Add("Clipboard put back", "剪貼簿還原咗"); Add("Nothing to paste as plain text", "冇純文字可以貼");
            Add("Fill in the snippet", "填片語"); Add("Paste it", "貼出嚟"); Add("Cancel", "取消");

            // ---- snippets, recipes, rules
            Add("Snippets", "片語"); Add("Type a trigger in any app and it becomes the text.", "喺任何程式打觸發字，就會變成段文字。");
            Add("New snippet", "新片語"); Add("Name", "名"); Add("Trigger", "觸發字"); Add("Shortcut (optional)", "快捷鍵（可選）");
            Add("Insert", "插入"); Add("Saved as you type.", "打字時已經儲存。"); Add("Duplicate this snippet", "複製呢個片語"); Add("Delete this snippet", "刪除呢個片語");
            Add("Start triggers with ; so normal typing never fires one. Typing is only compared with your triggers; nothing you type is kept.", "觸發字用 ; 開頭，咁平時打字就唔會誤觸。打嘅字只會用嚟對觸發字，乜都唔會留低。");
            Add("Transform recipes", "轉換配方"); Add("Chains of text changes you can run in one press.", "一撳就做晒嘅一連串文字修改。");
            Add("New recipe", "新配方"); Add("Steps, in order", "步驟（按次序）"); Add("Add a step", "加一步"); Add("Try it on", "試吓用喺"); Add("Result", "結果");
            Add("Show on text toasts", "喺文字提示顯示"); Add("Run on the clipboard", "用喺剪貼簿"); Add("Saved as you change it.", "改嘅時候已經儲存。");
            Add("Per-app rules", "每個程式嘅規則"); Add("Toast and history", "提示同歷史"); Add("History only, no toast", "淨係歷史，冇提示");
            Add("Never keep", "永遠唔留"); Add("Nothing", "乜都唔做"); Add("Strip tracking from links", "刪走連結嘅追蹤"); Add("Keep as plain text", "保留做純文字");
            Add("Clear after 30 s", "30 秒後清走"); Add("Open tables as a table", "表格用表格開"); Add("Pick a running app", "揀一個開緊嘅程式"); Add("Add a rule", "加規則");
            Add("Trim spaces", "去頭尾空格"); Add("Tidy spaces", "整理空格"); Add("lower case", "細楷"); Add("UPPER CASE", "大楷"); Add("Title Case", "首字大楷");
            Add("Remove accents", "去重音符號"); Add("Drop symbols", "刪符號"); Add("Join lines", "合併行"); Add("Remove duplicate lines", "刪重複行");
            Add("Sort lines", "排序行"); Add("Remove blank lines", "刪空行"); Add("Wrap in quotes", "加引號"); Add("Lines to a comma list", "行變逗號清單");

            // ---- extensions
            Add("Extensions", "擴充功能"); Add("Add an extension", "加擴充功能"); Add("Open the folder", "開資料夾");
            Add("Reload all extensions", "重新載入所有擴充功能"); Add("How to write an extension", "點樣寫擴充功能"); Add("Turn every extension off", "關晒所有擴充功能");
            Add("NOTICES THESE COPIES", "會留意呢啲複製"); Add("ADDS THESE BUTTONS", "會加呢啲按鈕"); Add("ALLOWED TO", "准許"); Add("LAST RUNS", "最近執行");
            Add("Allow and turn on", "准許並開啟"); Add("Not run yet.", "未執行過。");
            Add("Read text you copy that matches its patterns", "讀你複製而又符合佢樣式嘅文字"); Add("Read any text you copy", "讀你複製嘅任何文字");
            Add("Put new text on the clipboard", "將新文字放上剪貼簿"); Add("Open web links", "開網頁連結"); Add("Use the internet", "用互聯網");
            Add("This extension is new or asks for something new, so it stays off until you allow it.", "呢個擴充功能係新嘅，或者要求咗新嘢，所以要你准許先會開。");
            Add("It never sees copies from password managers or anything that looks like a key, token or password.", "佢永遠睇唔到密碼管理器嘅複製，亦睇唔到似金鑰、權杖或者密碼嘅嘢。");

            // ---- clipboard settings
            Add("Clipboard settings", "剪貼簿設定"); Add("Search clipboard settings", "搜尋剪貼簿設定"); Add("Done", "完成");
            Add("AUTOMATIC LOOKUPS", "自動查詢"); Add("KINDS OF COPIES", "複製嘅種類"); Add("SECRETS", "秘密"); Add("EXTENSIONS", "擴充功能");
            Add("SNIPPETS, RECIPES AND APP RULES", "片語、配方同程式規則"); Add("SHORTCUTS", "快捷鍵");
            Add("Look things up on the internet automatically", "自動上網查資料"); Add("Money amounts", "金額"); Add("Book numbers and package names", "書號同套件名");
            Add("Public IP addresses", "公開 IP 地址"); Add("Home currency", "本地貨幣"); Add("Developer kinds", "開發者類"); Add("Everyday kinds", "日常類");
            Add("Numbers and tables", "數字同表格"); Add("Secret guard", "秘密守衛"); Add("Manage extensions", "管理擴充功能"); Add("Clipboard shortcuts", "剪貼簿快捷鍵");
            Add("Paste as plain text", "貼純文字"); Add("Undo the clipboard", "還原剪貼簿"); Add("Collect mode", "收集模式"); Add("Type it instead of pasting", "用打字代替貼上");
            Add("Changes save straight away.", "改咗即刻儲存。"); Add("No setting matches.", "冇設定符合。");

            // ---- picture text
            Add("Text and codes in this picture", "呢張圖入面嘅字同碼"); Add("Copy the text", "複製文字"); Add("Choose every line", "揀晒所有行");
            Add("Save the picture", "儲存圖片"); Add("Read again", "再讀一次"); Add("Copy what it says", "複製佢寫嘅嘢"); Add("Open the link", "開連結");
        }
    }
}
