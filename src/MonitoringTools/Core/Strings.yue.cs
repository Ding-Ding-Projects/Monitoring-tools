using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MonitoringTools.Core
{
    /// <summary>
    /// The Hong Kong Cantonese table: every user-visible English string in the app, written in
    /// natural colloquial Cantonese with traditional characters. Keys are the exact English text
    /// the interface uses (matched ignoring case, so upper-cased eyebrows work too). Paths,
    /// commands, codes, formats, game titles and product names are deliberately not translated.
    /// </summary>
    internal static partial class Strings
    {
        internal static readonly Dictionary<string, string> Table = Build();

        private static Dictionary<string, string> Build()
        {
            var t = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            void Add(string en, string yue) => t[en] = yue;

            // ---- navigation and page titles
            Add("Overview", "總覽"); Add("Activity", "活動紀錄"); Add("Clipboard history", "剪貼簿紀錄");
            Add("Clipboard", "剪貼簿"); Add("Shortcuts and scripts", "捷徑同腳本"); Add("Network and DNS", "網絡同 DNS");
            Add("Game mode", "遊戲模式"); Add("Conversion", "轉檔"); Add("Minecraft servers", "Minecraft 伺服器");
            Add("Service", "背景服務"); Add("Appearance", "外觀"); Add("Language and voice", "語言同語音");
            Add("Modes", "模式"); Add("Records and exports", "紀錄同匯出"); Add("Help and docs", "說明同文件");
            Add("About and updates", "關於同更新"); Add("Settings", "設定"); Add("Pages", "頁面"); Add("Actions", "操作");
            Add("Records", "紀錄"); Add("Help", "說明"); Add("About", "關於"); Add("Updates", "更新");
            Add("Network", "網絡"); Add("History", "歷史"); Add("Shortcuts", "捷徑"); Add("Toasts", "通知");
            Add("Settings and sections", "設定同分頁"); Add("settings and sections", "設定同分頁");
            Add("copies", "複製紀錄"); Add("entries", "項目"); Add("shortcuts", "捷徑"); Add("items", "項目");

            // ---- overview
            Add("Quiet while you play", "你玩緊嘢，所以靜晒"); Add("Everything is quiet", "一切平靜");
            Add("Offline", "離線"); Add("Online", "在線"); Add("Internet", "互聯網"); Add("Clipboard today", "今日複製");
            Add("Off", "關"); Add("On", "開"); Add("Standing by", "待命中"); Add("Latest activity", "最新活動");
            Add("Quick commands", "快捷指令");
            Add("Copy a path with a command on the end. The command runs the moment it lands on the clipboard.", "複製路徑時喺尾加個指令，指令一去到剪貼簿就即刻行。");
            Add("Recently copied", "最近複製"); Add("Gateway", "閘道"); Add("gateway silent", "閘道冇回應");
            Add("Uptime", "連線時間"); Add("Probes", "探測次數"); Add("Copies today", "今日複製次數");
            Add("Gateway avg", "閘道平均"); Add("Outages", "斷線次數"); Add("Connection lost", "連線中斷");
            Add("None since the service started. Nice and quiet.", "服務啟動之後未斷過線，好平靜。");
            Add("Live", "即時"); Add("Every probe of the last few minutes. Gaps are failed checks.", "最近幾分鐘每一次探測，有缺口即係探測失敗。");
            Add("Conversions, pushes, shortcuts, file jobs and outages show up here.", "轉檔、推送、捷徑、檔案工作同斷線都會喺呢度顯示。");
            Add("video or audio to MP4", "影片或音訊轉 MP4"); Add("video to animated GIF", "影片轉動態 GIF"); Add("extract or convert audio", "抽取或轉換音訊");
            Add("image to WebP", "圖片轉 WebP"); Add("image or first frame to PNG", "圖片或第一格轉 PNG"); Add("commit and push the repo", "提交並推送儲存庫");
            Add("your shortcut", "你嘅捷徑");

            // ---- history
            Add("Nothing has been shown yet. Toasts and alerts will appear here as they happen.", "暫時未有通知。彈出通知同警報一出現就會列喺度。");
            Add("Nothing yet. Copy something and it shows up here.","暫時未有嘢，複製啲嘢就會出現喺度。");
            Add("Copy again", "再複製一次");
            Add("Builder", "砌表達式"); Add("Search every page", "搜尋所有頁面"); Add("Filter actions", "篩選動作"); Add("Filter this menu", "篩選呢個選單");
            Add("Select", "揀選"); Add("Done selecting", "揀完"); Add("Select all shown", "揀晒顯示緊嘅"); Add("Save as text…", "存做文字檔…"); Add("Remove from history", "喺紀錄度移除");
            Add("Regex builder", "正則表達式砌法"); Add("Back to plain text", "返去普通文字"); Add("Done", "完成"); Add("Use this list as the sample", "用呢張清單做樣本");
            Add("Emergency exit", "緊急出口"); Add("First key", "第一條鎖匙"); Add("Second key", "第二條鎖匙"); Add("To go ahead, turn both keys, then slide all the way.", "要繼續，就扭兩條鎖匙，再一路拉到盡。");
            Add("Edit appearance…", "編輯外觀…"); Add("Lock this element…", "鎖住呢個元素…"); Add("Unlock this element", "解鎖呢個元素"); Add("Copy text", "複製文字"); Add("Press", "撳"); Add("Open", "開啟"); Add("Edit", "編輯"); Add("Save", "儲存"); Add("Delete", "刪除");
            Add("Remove", "移除"); Add("Cancel", "取消"); Add("Close", "關閉"); Add("Back", "返回"); Add("Next", "下一步");
            Add("Done", "完成"); Add("Skip this step", "跳過呢一步"); Add("Dismiss", "關閉"); Add("Confirm", "確認");

            // ---- clipboard settings
            Add("What shows up in the corner when the clipboard changes.", "剪貼簿有變動時，喺角落彈出嘅通知。");
            Add("Clipboard toasts", "剪貼簿通知"); Add("Show a toast for every copy.", "每次複製都彈出通知。");
            Add("Keep open on hover", "滑鼠停留時保持開啟"); Add("Hovering holds the toast and reveals the actions.", "滑鼠停喺上面就會留住通知同顯示操作。");
            Add("Render Markdown", "顯示 Markdown 格式"); Add("Headings, lists, code and links render in the preview.", "預覽會顯示標題、清單、程式碼同連結。");
            Add("Skip password managers", "略過密碼管理員"); Add("Copies marked private by the source app never pop up or reach history.", "來源程式標記為私人嘅複製，唔會彈出，亦唔會入紀錄。");
            Add("Run :mp4, :push and your own shortcuts when they land on the clipboard.", "當 :mp4、:push 同你自訂嘅捷徑出現喺剪貼簿，就會自動執行。");
            Add("Toast duration", "通知顯示時間"); Add("History size", "紀錄數量");
            Add("Sound effects", "音效"); Add("Short cues when an action finishes, fails, or the connection drops and returns. Quiet during Windows quiet time, presentations and games.", "操作完成、失敗，或者連線中斷同恢復時會有短短嘅提示音。Windows 安靜時間、簡報同遊戲期間唔會出聲。");
            Add("Volume", "音量"); Add("Low", "細聲"); Add("Medium", "中等"); Add("Full", "最大聲");
            Add("Media folder", "媒體資料夾"); Add("Saved clipboard images, audio and text go here.", "儲存咗嘅剪貼簿圖片、音訊同文字會放喺呢度。");

            // ---- shortcuts
            Add("Make your own :commands. Copy any path or text with :name on the end and your batch or PowerShell script runs with it.", "自訂你嘅 :指令。複製任何路徑或文字，尾加 :名稱，就會用佢行你嘅批次檔或 PowerShell 腳本。");
            Add(":push message", ":push 提交訊息"); Add("Built in. Stages everything, commits with this message and pushes the current branch.", "內置功能。會暫存所有改動，用呢個訊息提交，再推送目前分支。");
            Add("Your shortcuts", "你嘅捷徑"); Add("None yet.", "暫時冇。"); Add("New shortcut", "新增捷徑");
            Add("Name: letters, digits, - and _", "名稱：字母、數字、- 同 _"); Add("What it does (shows as the chip tooltip)", "功能說明（會顯示做提示）");
            Add("NAME", "名稱"); Add("DESCRIPTION", "說明"); Add("Runs with", "執行方式"); Add("PowerShell", "PowerShell"); Add("Batch (.bat)", "批次檔 (.bat)");
            Add("Show a console window", "顯示主控台視窗"); Add("Run as administrator", "以系統管理員身分執行");
            Add("Prompts once per run unless the service already has admin rights.", "除非服務已有管理員權限，否則每次執行都會問一次。");
            Add("INSERT", "插入"); Add("Test input (a path or any text)", "測試輸入（路徑或任何文字）");
            Add("Name must start with a letter: letters, digits, - and _ only, not 'push'.", "名稱要以字母開頭，只可以用字母、數字、- 同 _，而且唔可以係 'push'。");
            Add("Save and run", "儲存並執行"); Add("Open in Notepad", "用記事本開啟"); Add("Open scripts folder", "開啟腳本資料夾");
            Add("Default commit message", "預設提交訊息"); Add("Set a default commit message", "設定預設提交訊息");
            Add("Every :push uses this message. Click a snippet to drop it in; it fills in at push time.", "每次 :push 都用呢個訊息。撳一個片段就會插入，推送時先會填入內容。");
            Add("Snippets", "片段"); Add("PREVIEW", "預覽"); Add("Preview", "預覽"); Add("Not now", "遲啲先"); Add("Save and push", "儲存並推送");
            Add("FIRST :PUSH", "第一次 :PUSH"); Add("not set yet: the first :push will ask", "未設定：第一次 :push 會問你");
            Add("Write a batch or PowerShell script, name it, and copy anything with :name to run it.", "寫個批次檔或 PowerShell 腳本，改個名，之後複製任何嘢加 :名稱 就可以執行。");
            Add("Your :commands and the script editor", "你嘅 :指令同腳本編輯器");

            // ---- network
            Add("The offline dialog stays on top until the line comes back or you dismiss it.", "斷線視窗會一直置頂，直到連線恢復或者你自己關閉。");
            Add("Network alerts", "網絡警示"); Add("Show the dialog when the internet drops.", "上網中斷時顯示視窗。");
            Add("Check every", "檢查間隔"); Add("Alert after", "連續失敗幾多次先警示"); Add("Test now", "即刻測試");
            Add("Release & renew IP", "釋放並更新 IP"); Add("Cancelled", "已取消");
            Add("Released, renewed and flushed DNS.", "已釋放、更新 IP 並清除 DNS 快取。");
            Add("In use", "使用中"); Add("Use", "使用"); Add("Name", "名稱"); Add("Primary, e.g. 1.1.1.1", "主要，例如 1.1.1.1");
            Add("Secondary (optional)", "次要（可選）"); Add("Save profile", "儲存設定檔");
            Add("Give it a name and valid IPv4 addresses.", "請輸入名稱同有效嘅 IPv4 位址。");
            Add("Public IP", "公開 IP"); Add("Checking shortly…", "即將檢查…"); Add("Check now", "即刻檢查");
            Add("IP", "IP"); Add("First seen", "首次見到"); Add("Last seen", "最後見到"); Add("Times", "次數");
            Add("Watch the public IP", "監察公開 IP"); Add("Checks every few minutes and right after the network changes.", "每隔幾分鐘檢查一次，網絡有變動之後亦會即時檢查。");
            Add("Optional git remote URL, for example https://github.com/you/ip-history.git", "可選嘅 git 遠端網址，例如 https://github.com/you/ip-history.git");
            Add("Save remote", "儲存遠端"); Add("Saved. The next change pushes there.", "已儲存，下次變動就會推送去嗰度。");
            Add("Push now", "即刻推送"); Add("Pushing…", "推送緊…"); Add("Pushed.", "已推送。"); Add("Open folder", "開啟資料夾");
            Add("Try a command", "試吓指令"); Add("Type what you would copy. It shows what will happen before anything runs.", "輸入你會複製嘅內容，執行之前會先顯示會發生咩事。");
            Add("Run it", "執行"); Add("Not inside a git repository.", "唔係喺 git 儲存庫入面。"); Add("Not inside a git repository", "唔係喺 git 儲存庫入面");
            Add("Not runnable yet: the path before the colon does not exist, or the word after it is not a format, :push or one of your shortcuts.", "暫時行唔到：冒號前面嘅路徑唔存在，或者後面嗰個字唔係格式、:push 或者你嘅捷徑。");
            Add("Format: a path, a colon, then a format such as mp4, gif, mp3 or webp. Also :push and your own :shortcuts.", "格式：一個路徑、一個冒號，再加格式，例如 mp4、gif、mp3 或 webp。亦可以用 :push 同你自訂嘅 :捷徑。");
            Add("Internet connection lost", "網絡連線中斷"); Add("Back online", "已恢復連線"); Add("Router is not responding", "路由器冇回應");
            Add("DNS lookups failing", "DNS 查詢失敗"); Add("DNS servers", "DNS 伺服器"); Add("Adapter", "網絡卡"); Add("Link speed", "連線速度");
            Add("No IP address from the router", "路由器冇分配 IP 位址"); Add("No network connection", "冇網絡連線"); Add("No route to the internet", "去唔到互聯網");
            Add("Sign-in required", "需要登入"); Add("Wi-Fi", "Wi-Fi"); Add("Ethernet", "乙太網絡");
            Add("The adapter has an address but no default gateway, so traffic has nowhere to go.", "網絡卡有位址但冇預設閘道，所以資料無處可去。");
            Add("The adapter is connected but DHCP did not hand out an address. Release and renew usually fixes this.", "網絡卡已連線，但 DHCP 冇派位址俾你。釋放並更新通常可以解決。");
            Add("The line is up, but DNS lookups are failing, so websites by name will not load. Switching DNS usually fixes this.", "線路係通嘅，但 DNS 查詢失敗，所以用名稱開唔到網站。轉 DNS 通常可以解決。");
            Add("This network shows a sign-in page before it lets traffic through (hotel, airport, café Wi-Fi).", "呢個網絡要先登入網頁先放行（例如酒店、機場、咖啡店 Wi-Fi）。");
            Add("Wi-Fi is off or disconnected, the cable is unplugged, or airplane mode is on.", "Wi-Fi 關咗或者斷咗線、網線拔咗，或者開咗飛行模式。");
            Add("Your computer is connected, but the router does not answer and nothing gets past it. Try restarting the router.", "你部電腦有連線，但路由器冇回應，咩都出唔到去。試吓重新啟動路由器。");
            Add("Your router answers, but nothing beyond it does. This is usually an ISP or modem outage.", "路由器有回應，但再出面就冇。通常係網絡供應商或數據機出事。");
            Add("Cloudflare", "Cloudflare"); Add("Google", "Google"); Add("Automatic", "自動"); Add("Automatic (from router)", "自動（由路由器提供）");

            // ---- offline dialog
            Add("PROBE LADDER", "探測階梯"); Add("SWITCH DNS", "轉換 DNS"); Add("Retry now", "即刻重試"); Add("Flush DNS", "清除 DNS 快取");
            Add("Release & renew", "釋放並更新"); Add("Copy details", "複製詳情"); Add("Open sign-in page", "開啟登入頁面"); Add("Network settings", "網絡設定");
            Add("Dismiss until the connection changes", "關閉，直到連線情況有變"); Add("Down since", "斷線時間");
            Add("Flushing the DNS cache…", "清除緊 DNS 快取…"); Add("DNS cache flushed. Checking again…", "DNS 快取已清除，再檢查一次…");
            Add("Releasing and renewing the IP address…", "釋放同更新緊 IP 位址…"); Add("Released and renewed. Checking again…", "已釋放並更新，再檢查一次…");
            Add("Approve the prompt to release and renew the IP address…", "請喺提示按批准，釋放並更新 IP 位址…");
            Add("Cancelled at the prompt.", "喺提示取消咗。"); Add("The connection is back.", "連線恢復喇。"); Add("Details copied.", "已複製詳情。");

            // ---- game mode
            Add("Suppress popups while gaming", "玩遊戲時收埋通知");
            Add("On by default. Toasts are held back while a listed game runs. An outage still going when you stop playing shows then. Quick commands still run.", "預設開啟。清單入面嘅遊戲行緊時會暫停彈出通知。如果你玩完之後仲斷緊線，嗰陣先會顯示。快捷指令照樣執行。");
            Add("Suppress while gaming", "遊戲時收埋通知"); Add("Active now: ", "而家："); Add("Any fullscreen app counts", "任何全螢幕程式都算");
            Add("Exclusive fullscreen games, fullscreen video and presentations.", "獨佔全螢幕遊戲、全螢幕影片同簡報。");
            Add("Fullscreen games, video and presentations.", "全螢幕遊戲、影片同簡報。");
            Add("Watched games", "監察嘅遊戲"); Add("Process names without .exe. Use process:Title to also match the window title (Minecraft Java runs as javaw).", "處理程序名稱唔使加 .exe。用 process:Title 可以同時配對視窗標題（Minecraft Java 係以 javaw 行）。");
            Add("Add game", "新增遊戲"); Add("Restore defaults", "還原預設");
            Add("Fullscreen app", "全螢幕程式"); Add("Fullscreen game", "全螢幕遊戲"); Add("Presentation", "簡報");
            Add("Game mode on", "遊戲模式已開"); Add("Game mode off", "遊戲模式已關");

            // ---- conversion
            Add("Quick conversion", "快速轉檔"); Add("Copy a file path, add :format on the end, and the converted file appears next to it with the same name.", "複製檔案路徑，尾加 :格式，轉好嘅檔案就會出現喺原檔旁邊，名稱相同。");
            Add("Not found. Video and audio conversion needs it.", "搵唔到。影片同音訊轉檔需要佢。"); Add("Install with winget", "用 winget 安裝");
            Add("Installing FFmpeg", "安裝緊 FFmpeg"); Add("Install did not finish", "安裝未完成"); Add("Try winget install Gyan.FFmpeg in a terminal.", "試吓喺終端機行 winget install Gyan.FFmpeg。");
            Add("Pick ffmpeg.exe", "揀 ffmpeg.exe"); Add("Find ffmpeg.exe", "搵 ffmpeg.exe"); Add("Formats", "格式");
            Add("An existing file is never overwritten: the new one gets a (1), (2) suffix.", "現有檔案唔會被覆蓋，新檔案會加 (1)、(2) 後綴。");
            Add("FFmpeg is missing from this build", "呢個版本冇附帶 FFmpeg"); Add("FFmpeg ready", "FFmpeg 已就緒"); Add("Fetch FFmpeg", "下載 FFmpeg"); Add("Fetching FFmpeg", "下載緊 FFmpeg");
            Add("Fetching did not finish", "下載未完成"); Add("Get the release build, which carries FFmpeg inside, or pick ffmpeg.exe below.", "請用附帶 FFmpeg 嘅正式版本，或者喺下面揀 ffmpeg.exe。");
            Add("This copy does not carry its bundled FFmpeg. It can fetch it now and then run this job.", "呢個版本冇附帶 FFmpeg。可以即刻下載，再繼續呢個工作。");
            Add("Conversion cancelled", "轉檔已取消"); Add("Conversion failed", "轉檔失敗"); Add("Nothing was written.", "冇寫入任何嘢。");
            Add("Copy error", "複製錯誤訊息"); Add("Open logs", "開啟日誌"); Add("Open log folder", "開啟日誌資料夾"); Add("Show in folder", "喺資料夾顯示");
            Add("Copy path", "複製路徑"); Add("Copy as file", "複製做檔案"); Add("Copy as path", "複製做路徑"); Add("Copy as paths", "複製做路徑清單");
            Add("Drag the path out to drop the new file anywhere.", "拖曳路徑出去，就可以將新檔案放去任何地方。");
            Add("Cancelling", "取消緊"); Add("Copied", "已複製"); Add("Opened", "已開啟"); Add("Shown", "已顯示"); Add("Saved", "已儲存");
            Add("starting", "開始緊"); Add("working…", "處理緊…"); Add("working", "處理緊"); Add("reading source…", "讀取來源緊…");
            Add("Finished.", "完成。"); Add("No output.", "冇輸出。"); Add("Failed", "失敗"); Add("Copy output", "複製輸出");

            // ---- service
            Add("Background service", "背景服務"); Add("Runs at sign-in under a small supervisor that restarts it if it ever crashes.", "登入時自動啟動，由一個小型監督程式看管，萬一當機會自動重新啟動。");
            Add("Start with Windows", "隨 Windows 啟動"); Add("Launch at sign-in.", "登入時啟動。"); Add("Administrator rights", "系統管理員權限");
            Add("Off by default. Turning it on asks once, then the service starts elevated at every sign-in until you turn it off here. Lets DNS switching and release and renew run without prompts.", "預設關閉。開啟時會問一次，之後每次登入服務都會以提升權限啟動，直到你喺度關閉。令轉 DNS 同釋放更新唔使每次都有提示。");
            Add("Administrator rights unchanged", "系統管理員權限冇變"); Add("The prompt was declined or failed.", "提示被拒絕或者失敗。");
            Add("Restart service", "重新啟動服務"); Add("Open settings file", "開啟設定檔"); Add("Quit until next sign-in", "退出，直到下次登入");
            Add("Personal wording", "個人用語"); Add("Optional. A local JSON file that swaps ordinary phrases in on-screen text for your own words. Drop it on any window, or choose it here. It never leaves this computer; paths, commands and code are never changed.", "可選。一個本機 JSON 檔案，可以將畫面上嘅一般用語換成你自己嘅講法。拖去任何視窗，或者喺度揀檔案。檔案絕對唔會離開呢部電腦，路徑、指令同程式碼唔會改。");
            Add("No file loaded. The app shows its original wording.", "未載入檔案，程式顯示原本用語。"); Add("No file loaded", "未載入檔案");
            Add("Replace file…", "更換檔案…"); Add("Choose file…", "揀檔案…"); Add("Clear and restore original wording", "清除並還原原本用語");
            Add("Uninstall", "解除安裝"); Add("Stops the service, removes it from start-up and deletes the program.", "停止服務、由開機啟動移除，並刪除程式。");
            Add("Uninstall service…", "解除安裝服務…"); Add("Uninstall Monitoring Tools?", "解除安裝 Monitoring Tools？");
            Add("This stops the background service, removes it from start-up and deletes the program.", "會停止背景服務、由開機啟動移除，並刪除程式。");
            Add("This stops the background service, removes it from start-up and deletes the program. Clipboard history is never stored, so nothing copied stays behind.", "會停止背景服務、由開機啟動移除，並刪除程式。剪貼簿紀錄從來唔會儲存，所以唔會留低任何複製內容。");
            Add("Also remove settings, logs and scripts", "同時移除設定、日誌同腳本"); Add("Uninstalled", "已解除安裝");
            Add("Monitoring Tools has been removed. Bye for now.", "Monitoring Tools 已經移除，再見。"); Add("Keep it", "保留");

            // ---- activity
            Add("What it has been doing", "佢做過啲咩"); Add("Kept in memory while the service runs. The log folder keeps the full record.", "服務行緊期間留喺記憶體，完整紀錄喺日誌資料夾。");
            Add("All", "全部"); Add("Conversions", "轉檔"); Add("Pushes", "推送"); Add("Files", "檔案"); Add("Everything", "所有嘢");
            Add("Everything the service has done", "服務做過嘅所有嘢"); Add("Live latency, outages and activity", "即時延遲、斷線同活動");

            // ---- about
            Add("Clipboard toasts, internet watch and quick commands for Windows.", "適用於 Windows 嘅剪貼簿通知、網絡監察同快捷指令。");
            Add("Every change to the project ships as a new release on GitHub.", "項目每次更改都會喺 GitHub 發佈新版本。");
            Add("Check for updates", "檢查更新"); Add("Checking…", "檢查緊…"); Add("Release notes", "版本說明"); Add("Project page", "項目頁面");
            Add("Where things live", "檔案位置"); Add("Program", "程式"); Add("Scripts", "腳本"); Add("Logs", "日誌"); Add("Saved media", "已儲存媒體"); Add("Privacy", "私隱");
            Add("Clipboard history stays in memory and is gone when the service stops. Copies that password managers mark private are skipped. The only network calls are the connectivity probes and this update check.", "剪貼簿紀錄只留喺記憶體，服務停止就消失。密碼管理員標記為私人嘅複製會被略過。唯一嘅網絡連線係連線探測同呢個更新檢查。");
            Add("Could not read the latest version.", "讀取唔到最新版本。"); Add("Version, bundled tools, update check", "版本、內置工具、更新檢查");

            // ---- toasts and file kinds
            Add("Email", "電郵"); Add("Markdown", "Markdown"); Add("Text", "文字"); Add("Link", "連結"); Add("Color", "顏色"); Add("JSON", "JSON");
            Add("Image", "圖片"); Add("Audio clip", "音訊片段"); Add("Folder", "資料夾"); Add("Video file", "影片檔案"); Add("Audio file", "音訊檔案");
            Add("Image file", "圖片檔案"); Add("Document", "文件"); Add("Archive", "壓縮檔"); Add("Code file", "程式碼檔案"); Add("File", "檔案");
            Add("Compose file", "Compose 檔案"); Add("Dockerfile", "Dockerfile");
            Add("Drag the list out to drop all of them. Hover to move, copy, delete, encode or encrypt.", "拖曳清單出去可以放低全部檔案。滑鼠停留可以移動、複製、刪除、編碼或加密。");
            Add("Copied with quotes. Hover for Remove quotes.", "連引號一齊複製。滑鼠停留可以移除引號。");
            Add("Docker Compose file. Hover to see what you can run and what each one does.", "Docker Compose 檔案。滑鼠停留可以睇到可以執行咩同每項嘅功能。");
            Add("Dockerfile. Hover to build an image from it.", "Dockerfile。滑鼠停留可以用佢建立映像。");
            Add("Inside a git repository. Copy it with :push on the end to commit and push.", "喺 git 儲存庫入面。複製時尾加 :push 就可以提交並推送。");
            Add("Bitmap on the clipboard. Drag the preview out to drop it as a PNG file.", "剪貼簿入面係點陣圖。拖曳預覽出去就可以存成 PNG 檔案。");
            Add("Wave audio on the clipboard. Drag this line out to drop it as a .wav file.", "剪貼簿入面係 Wave 音訊。拖曳呢行出去就可以存成 .wav 檔案。");
            Add("Hover to save or convert. Quick commands work here too.", "滑鼠停留可以儲存或轉檔，快捷指令喺度都用得。");
            Add("Public IP changed", "公開 IP 已更改"); Add("new address", "新地址");
            Add("Saved to the IP history repository with first and last seen times.", "已連同首次同最後見到嘅時間，存入 IP 紀錄儲存庫。");
            Add("Copy new IP", "複製新 IP"); Add("Open repository", "開啟儲存庫"); Add("Commit and push", "提交並推送"); Add("Commit", "提交");
            Add("Less", "收埋");

            // ---- file tools window
            Add("File tools", "檔案工具"); Add("Move and copy", "移動同複製"); Add("Move to", "移動到"); Add("Copy to", "複製到");
            Add("Move to a folder…", "移動到資料夾…"); Add("Copy to a folder…", "複製到資料夾…"); Add("Move to…", "移動到…"); Add("Copy to…", "複製到…");
            Add("Moving", "移動緊"); Add("Copying", "複製緊"); Add("Moved", "已移動"); Add("Show", "顯示");
            Add("RECENT · click to copy, shift-click to move", "最近 · 撳一下複製，Shift 加撳一下移動");
            Add("Encode", "編碼"); Add("Decode", "解碼"); Add("Encrypt", "加密"); Add("Decrypt", "解密"); Add("Encoded", "已編碼"); Add("Decoded", "已解碼");
            Add("Encrypted", "已加密"); Add("Decrypted", "已解密"); Add("Deleted", "已刪除"); Add("Password", "密碼");
            Add("Writes name.ext.b64 next to each file.", "會喺每個檔案旁邊寫入 name.ext.b64。"); Add("Turns .b64 files back into the originals.", "將 .b64 檔案還原成原檔。");
            Add("AES-256-GCM. Writes name.ext.mtenc beside each file. There is no way back without the password.", "AES-256-GCM。會喺每個檔案旁邊寫入 name.ext.mtenc。冇密碼就唔可能還原。");
            Add("The passwords do not match.", "兩次密碼唔一致。"); Add("Use at least 8 characters.", "最少用 8 個字元。"); Add("Type a password first.", "請先輸入密碼。");
            Add("Deletes go to the Recycle Bin", "刪除嘅檔案會去資源回收筒"); Add("They go to the Recycle Bin, so you can restore them from there.", "檔案會去資源回收筒，可以喺嗰度還原。");
            Add("Send originals to the Recycle Bin afterwards", "完成後將原檔送去資源回收筒"); Add("Sending to the Recycle Bin…", "送去資源回收筒緊…");
            Add("Copy as Base64 text", "複製做 Base64 文字"); Add("Copy as data URI (for HTML and CSS)", "複製做 data URI（用於 HTML 同 CSS）");
            Add("Move, copy, delete, Base64 and password encryption", "移動、複製、刪除、Base64 同密碼加密");
            Add("Base64 and password encryption for all of them", "全部檔案都可以做 Base64 同密碼加密");
            Add("Previews, move, copy, Base64 and password encryption for anything you copy.", "你複製嘅任何嘢都有預覽、移動、複製、Base64 同密碼加密。");

            // ---- action menu entries
            Add("Add quotes", "加引號"); Add("Remove quotes", "移除引號"); Add("Open in Paint", "用「繪圖」開啟"); Add("Open with…", "用其他程式開啟…"); Add("Open private", "以私人模式開啟");
            Add("Search web", "網上搜尋"); Add("Terminal here", "喺此開啟終端機"); Add("Copy domain", "複製網域"); Add("Copy name", "複製名稱"); Add("Copy names", "複製名稱清單");
            Add("Copy path text", "複製路徑文字"); Add("Copy as plain text", "複製做純文字"); Add("Markdown link", "Markdown 連結"); Add("Strip tracking", "移除追蹤參數");
            Add("Pretty print", "格式化"); Add("Minify", "壓縮"); Add("Minified", "已壓縮"); Add("Formatted", "已格式化"); Add("Tidy spaces", "整理空格"); Add("Tidied", "已整理");
            Add("Title Case", "標題大小寫"); Add("Forward slashes", "正斜線"); Add("Join lines", "合併行"); Add("Joined", "已合併"); Add("Unquoted", "已移除引號");
            Add("Translate", "翻譯"); Add("Decode Base64", "解碼 Base64"); Add("Encode Base64", "編碼 Base64"); Add("Decode to file", "解碼成檔案");
            Add("Save as .json", "儲存為 .json"); Add("Save as .md", "儲存為 .md"); Add("Save as .txt", "儲存為 .txt"); Add("Save PNG", "儲存 PNG"); Add("Save JPG", "儲存 JPG"); Add("Save WAV", "儲存 WAV");
            Add("Save + copy as file", "儲存並複製做檔案"); Add("Your shortcut", "你嘅捷徑"); Add("Cleaned", "已清理");
            Add("Commit everything and push. Same as copying the path with :push on the end.", "提交所有改動並推送。同複製路徑尾加 :push 一樣。");
            Add("Builds the image as {image} using the file's folder as the build context.", "用檔案所在資料夾做建置環境，建立映像。");
            Add("Build image", "建立映像"); Add("Build and run", "建立並執行"); Add("Compose up", "Compose 啟動"); Add("Down", "停止並移除"); Add("Pull", "下載映像"); Add("Restart", "重新啟動");
            Add("Builds what needs building and starts every service in the background.", "建立需要建立嘅部分，並喺背景啟動所有服務。");
            Add("Builds, then runs it in a terminal and removes the container when it stops.", "建立後喺終端機執行，停止時會移除容器。");
            Add("Downloads newer images for the services without starting them.", "下載服務嘅較新映像，但唔會啟動。");
            Add("Opens a terminal that follows the live logs.", "開啟終端機，即時跟住日誌。"); Add("Restarts the running services in place.", "就地重新啟動行緊嘅服務。");
            Add("Stops and removes this project's containers and networks. Volumes stay.", "停止並移除呢個項目嘅容器同網絡，儲存區會保留。");

            // ---- tray menu
            Add("Monitoring Tools menu", "Monitoring Tools 選單"); Add("Filter this menu", "篩選選單"); Add("Command palette", "指令面板");
            Add("Open control centre", "開啟控制中心"); Add("Snooze everything", "全部暫停"); Add("Wake", "喚醒"); Add("Continue setup", "繼續設定");
            Add("Run setup again", "重新執行設定"); Add("Take the tour", "睇導覽"); Add("Test connection now", "即刻測試連線");
            Add("Checking the connection", "檢查緊連線"); Add("Running the probe ladder now.", "而家行探測階梯。"); Add("DNS switch failed", "轉 DNS 失敗");
            Add("Switch", "切換"); Add("Snooze 1 hour", "暫停一小時"); Add("Search commands, pages and settings", "搜尋指令、頁面同設定");
            Add("Control centre", "控制中心"); Add("Show sample toasts", "顯示範例通知"); Add("Preview the offline dialog", "預覽斷線視窗");
            Add("Edit this shortcut", "編輯呢個捷徑"); Add("Switch between a card and the full window", "喺卡片同全視窗之間切換");
            Add("Palette action failed: ", "面板操作失敗："); Add("Clear personal wording", "清除個人用語");
            Add("Recent copies, kept in memory only", "最近複製，只留喺記憶體"); Add("Create and run servers here, in Docker or over SSH", "喺本機、Docker 或者經 SSH 建立同執行伺服器");
            Add("Start-up, administrator rights, uninstall", "開機啟動、系統管理員權限、解除安裝"); Add("Toasts, Markdown, media folder", "通知、Markdown、媒體資料夾");
            Add("Command playground, FFmpeg, formats", "指令試玩、FFmpeg、格式"); Add("Public IP history, DNS profiles, alerts", "公開 IP 紀錄、DNS 設定檔、警示");
            Add("Watched games and fullscreen", "監察嘅遊戲同全螢幕"); Add("Your :commands and the script editor", "你嘅 :指令同腳本編輯器");
            Add("Check public IP now", "即刻檢查公開 IP"); Add("Release and renew IP", "釋放並更新 IP"); Add("Open IP history repository", "開啟 IP 紀錄儲存庫");
            Add("Sound effects", "音效"); Add("Switch DNS", "轉換 DNS");

            // ---- setup wizard and welcome tour
            Add("Set up Monitoring Tools", "設定 Monitoring Tools"); Add("Setup", "設定"); Add("Back (Alt+Left)", "返回 (Alt+左)"); Add("Next (Enter)", "下一步 (Enter)");
            Add("Close (Esc). Progress is kept.", "關閉 (Esc)，進度會保留。"); Add("Progress saves as you go. Close any time and pick up from the tray.", "進度會自動儲存。隨時可以關閉，之後喺系統匣繼續。");
            Add("Start when I sign in", "登入時啟動"); Add("Monitoring Tools runs quietly in the tray. Choose whether it starts when you sign in.", "Monitoring Tools 靜靜地喺系統匣運行。你可以揀登入時係咪自動啟動。");
            Add("Recommended. You can change this later in the control centre.", "建議開啟。之後可以喺控制中心更改。"); Add("Starts at sign-in", "登入時啟動"); Add("Will not start at sign-in", "登入時唔會啟動");
            Add("Show clipboard toasts", "顯示剪貼簿通知"); Add("Every copy shows a toast in the bottom-right corner. Hover it to hold it and see actions.", "每次複製都會喺右下角彈出通知。滑鼠停留可以留住同睇到操作。");
            Add("Show me a toast", "彈個通知睇下"); Add("Keep open while hovered", "滑鼠停留時保持開啟"); Add("Skip copies from password managers", "略過密碼管理員嘅複製");
            Add("Private copies never pop up or reach history.", "私人複製唔會彈出，亦唔會入紀錄。"); Add("Toasts off", "通知已關"); Add("Toasts on", "通知已開");
            Add("Alert me when the internet drops", "上網中斷時通知我"); Add("When the connection drops, a dialog explains where it broke and stays until it is back.", "連線中斷時會有視窗講解邊度出事，並且留低直到恢復。");
            Add("Check my connection now", "即刻檢查我嘅連線"); Add("Checking adapter, gateway, DNS and the internet…", "檢查緊網絡卡、閘道、DNS 同互聯網…");
            Add("Outage alerts on", "斷線警示已開"); Add("Outage alerts off", "斷線警示已關"); Add("Watching: ", "監察緊：");
            Add("Watch my public IP address", "監察我嘅公開 IP 位址"); Add("A dialog on every change, and a history with first and last seen times.", "每次變動都有提示，並記錄首次同最後見到嘅時間。");
            Add("Hold popups while a game runs", "遊戲行緊時暫停通知"); Add("Popups wait while you play. On by default.", "你玩緊嘢時通知會等。預設開啟。");
            Add("Optional. Pick who answers your DNS lookups. Switching asks for administrator approval once.", "可選。揀由邊個回應你嘅 DNS 查詢。切換時會要求一次管理員批准。");
            Add("Cancelled at the approval prompt. Nothing changed.", "喺批准提示取消咗，冇任何更改。"); Add("DNS left as it was", "DNS 保持原狀");
            Add("Optional. Copy a path with :mp4, :gif or :push on the end and it runs. Set the commit message :push uses, and try a conversion.", "可選。複製路徑尾加 :mp4、:gif 或 :push 就會執行。可以設定 :push 用嘅提交訊息，同試吓轉檔。");
            Add("Commit message for :push", ":push 嘅提交訊息"); Add("Commit message set", "已設定提交訊息"); Add("Try a conversion", "試吓轉檔"); Add("Conversion tried", "已試過轉檔");
            Add("Here is what was set up. Everything can be changed later from the control centre.", "以下係已設定嘅內容。之後全部都可以喺控制中心更改。");
            Add("All set", "全部設定好"); Add("Setup finished", "設定完成"); Add("Finished", "已完成"); Add("Skipped", "已略過"); Add("Set earlier", "之前已設定");
            Add("OPTIONAL", "可選");
            Add("Welcome to Monitoring Tools", "歡迎使用 Monitoring Tools"); Add("Welcome tour", "歡迎導覽"); Add("It lives in your tray now.", "佢而家喺你嘅系統匣入面。");
            Add("Everything below works on its own. Press Try it to see each one right now instead of waiting for it to happen.", "下面每項功能都可以獨立使用。撳「試吓」即刻睇效果，唔使等佢自己發生。");
            Add("Right-click the tray icon any time. This tour lives there too.", "隨時右鍵點擊系統匣圖示，呢個導覽亦喺嗰度。"); Add("Try it", "試吓");
            Add("Every copy slides in for 3 seconds. Hover to hold it and see actions made for what you copied.", "每次複製都會彈出 3 秒。滑鼠停留可以留住，並睇到針對你複製內容嘅操作。");
            Add("Copy a path with :mp4, :gif or :push on the end and it just happens. Try converts a sample picture.", "複製路徑尾加 :mp4、:gif 或 :push，就即刻執行。「試吓」會轉換一張範例圖片。");
            Add("When the line drops you see where it broke, plus one-press fixes. It stays until it is back.", "斷線時會睇到邊度出事，仲有一鍵修復。會一直留低，直到恢復。");
            Add("Roblox, Minecraft, League and friends keep the screen. Popups wait until you stop.", "Roblox、Minecraft、League 之類嘅遊戲霸住個畫面，通知會等你玩完先出現。");
            Add("Preview the dialog", "預覽視窗"); Add("Pick games", "揀遊戲"); Add("Open editor", "開啟編輯器"); Add("Your shortcuts", "你嘅捷徑");
            Add("Clipboard toasts", "剪貼簿通知"); Add("Internet watch", "網絡監察"); Add("Connection", "連線"); Add("Commands", "指令"); Add("Input", "輸入");

            // ---- Minecraft servers page
            Add("Create and run Java edition servers on this computer, in Docker, or on another machine over SSH. Versions come straight from Mojang's official catalogue.", "喺呢部電腦、Docker 或者經 SSH 喺另一部機建立同執行 Java 版伺服器。版本直接來自 Mojang 官方目錄。");
            Add("Pick a version, a place to run it, and accept the EULA. Nothing is downloaded until you press Create.", "揀個版本、執行位置，同意 EULA。撳「建立」之前唔會下載任何嘢。");
            Add("New server", "新增伺服器"); Add("Create server", "建立伺服器"); Add("Flavour", "類型"); Add("Where it runs", "執行位置"); Add("This computer", "呢部電腦");
            Add("Docker here", "本機 Docker"); Add("Docker over SSH", "經 SSH 用 Docker"); Add("SSH host", "SSH 主機"); Add("SSH HOST", "SSH 主機");
            Add("Folder on the host (optional)", "主機上嘅資料夾（可選）"); Add("FOLDER ON HOST", "主機上嘅資料夾"); Add("Vanilla", "原版 (Vanilla)"); Add("Paper", "Paper"); Add("Fabric", "Fabric");
            Add("Releases", "正式版"); Add("Snapshots", "快照版"); Add("Beta", "Beta 版"); Add("Alpha", "Alpha 版");
            Add("Flavour: Vanilla. Paper and Fabric run on the Docker targets.", "類型：原版。Paper 同 Fabric 要喺 Docker 目標上執行。");
            Add("Search versions, for example 1.21", "搜尋版本，例如 1.21"); Add("No versions match.", "冇符合嘅版本。"); Add("No version chosen yet.", "未揀版本。"); Add("Choose a version first.", "請先揀版本。");
            Add("Enter the SSH host.", "請輸入 SSH 主機。"); Add("Enter the host first.", "請先輸入主機。"); Add("Reading the host key…", "讀取緊主機金鑰…");
            Add("Approve this host key?", "批准呢個主機金鑰？"); Add("Check host key", "檢查主機金鑰"); Add("Approve", "批准");
            Add("Compare these with the host's own fingerprints before approving.\n\n", "批准之前，請同主機本身嘅指紋對比。\n\n"); Add("Approve only if these match the host:\n", "只有同主機相符先好批准：\n");
            Add("Minecraft EULA", "Minecraft EULA"); Add("I accept the ", "我同意 "); Add("Accept the EULA first; the server will not run without it.", "請先同意 EULA，否則伺服器行唔到。");
            Add("Server name, also used as the message of the day", "伺服器名稱，亦用作每日訊息"); Add("Weekend server", "週末伺服器"); Add("My server", "我嘅伺服器");
            Add("Memory", "記憶體"); Add("Console", "主控台"); Add("Hide console", "收埋主控台"); Add("Send", "傳送"); Add("Start", "啟動"); Add("Stop", "停止");
            Add("Add plugin or mod…", "新增外掛或模組…"); Add("Choose a plugin or mod", "揀外掛或模組"); Add("Open folder", "開啟資料夾");
            Add("Arguments, for example a player name or 'day'", "參數，例如玩家名稱或 'day'");
            Add("Settings · saved to server.properties, applied on the next start", "設定 · 儲存到 server.properties，下次啟動時生效");
            Add("This removes it from the list. Its world and files stay where they are, and a running server is stopped first.", "會由清單移除。世界同檔案會原封不動留低，行緊嘅伺服器會先停止。");
            Add("Maximum players", "最多玩家數目"); Add("Message of the day", "每日訊息"); Add("Difficulty", "難度"); Add("Hardcore", "極限模式"); Add("Player versus player", "玩家對戰 (PvP)");
            Add("Allow flight", "允許飛行"); Add("Allow list only", "只限白名單"); Add("Check accounts with Mojang", "向 Mojang 驗證帳戶");
            Add("View distance (chunks)", "視距（區塊）"); Add("Simulation distance (chunks)", "模擬距離（區塊）"); Add("Spawn protection (blocks)", "出生點保護（方塊）");
            Add("Edit remote settings on the host; this target keeps them there.", "請喺主機上編輯遠端設定，呢個目標會將設定留喺嗰度。");
            Add("The server is not running.", "伺服器冇運行。"); Add("Unknown target.", "未知目標。");

            // ---- appearance, language, modes, records and help pages (titles and subtitles)
            Add("Theme, colour, text size, density and motion.", "主題、顏色、字體大小、密度同動態效果。");
            Add("Language, humour, emoji and the narrator.", "語言、幽默程度、表情符號同朗讀。");
            Add("Notifications, settings history, exports and the changelog.", "通知、設定歷史、匯出同更新日誌。");
            Add("School mode, ADHD modes, the dim sum surprise and scheduled settings.", "學校模式、專注模式、點心驚喜同排程設定。");
            Add("Every feature explained, offline.", "每項功能都有說明，離線都睇得。");

            // ---- wording layer and generic dialogs
            Add("Wording file refused", "用語檔案被拒絕"); Add("Choose a personal wording file", "揀個人用語檔案"); Add("Personal wording applied", "個人用語已套用");
            Add("That file could not be found.", "搵唔到嗰個檔案。"); Add("That file could not be read, so nothing was changed.", "讀取唔到嗰個檔案，所以冇做任何更改。");
            Add("The file is not valid JSON, or it is nested too deeply.", "檔案唔係有效嘅 JSON，或者巢狀層數太深。"); Add("The file has no entries.", "檔案冇任何項目。");
            Add("The file is not UTF-8 text.", "檔案唔係 UTF-8 文字。"); Add("The file repeats a key.", "檔案有重複嘅鍵。"); Add("The file has an unsafe or empty key.", "檔案有不安全或者空白嘅鍵。");
            Add("Every replacement must be text.", "所有替換內容都必須係文字。"); Add("The file must contain one JSON object.", "檔案必須包含一個 JSON 物件。");
            Add("The file has a field this version does not know.", "檔案有呢個版本唔認識嘅欄位。");

            // ---- git push
            Add("Git is not installed", "未安裝 Git"); Add("Install Git for Windows, then try again.", "請安裝 Git for Windows，然後再試。"); Add("Staging", "暫存緊"); Add("Staging failed", "暫存失敗");
            Add("Committing", "提交緊"); Add("Commit failed", "提交失敗"); Add("Pushing", "推送緊"); Add("Push failed", "推送失敗");

            // ---- palette groups and misc
            Add("Search", "搜尋"); Add("Search every page", "搜尋所有頁面"); Add("invalid", "無效"); Add("Tray right-click menu", "系統匣右鍵選單");
            Add("Icon", "圖示"); Add("Samples", "範例"); Add("Library", "資料庫");
            Add("Plain", "簡明"); Add("Light", "淺色"); Add("Friendly", "友善"); Add("Cheeky", "調皮"); Add("Playful", "好玩");

            // ---- the language page itself
            Add("Language", "語言");
            Add("Choose the language of the whole app: every page, toast, dialog, tray menu and palette entry. Switching applies at once to every open window.", "揀成個程式用嘅語言：每一頁、通知、視窗、系統匣選單同面板項目。切換後即時套用到所有開啟中嘅視窗。");
            Add("App language", "程式語言"); Add("Default: English. Bilingual shows English first, then Cantonese: short labels sit on one line, sentences use two lines.", "預設：英文。雙語會先顯示英文，再顯示廣東話：短標籤同行顯示，句子分兩行。");
            Add("Humour", "幽默程度");
            Add("Humour changes only the tone of headline messages such as the offline dialog, conversion results and snooze notices. The facts in them never change. Level 1 is plain and precise; level 5 is playful.", "幽默程度只會改變標題訊息嘅語氣，例如斷線視窗、轉檔結果同暫停通知，內容事實唔會變。1 級最簡明直接，5 級最好玩。");
            Add("English humour", "英文幽默程度"); Add("Default: 3. Used for English headlines, and for the English line in bilingual mode.", "預設：3。用於英文標題，同雙語模式嘅英文行。");
            Add("Cantonese humour", "廣東話幽默程度"); Add("Default: 3. Used for Cantonese headlines, and for the Cantonese line in bilingual mode.", "預設：3。用於廣東話標題，同雙語模式嘅廣東話行。");
            Add("Preview", "預覽"); Add("Level", "級別"); Add("Emoji in dialogs", "視窗內嘅表情符號");
            Add("Show emoji in toast and dialog titles. Turn it off to strip emoji from the start and end of titles. Default: on.", "喺通知同視窗標題顯示表情符號。關閉後會移除標題頭尾嘅表情符號。預設：開啟。");
            Add("Narrator", "朗讀");
            Add("Read toasts aloud", "朗讀通知"); Add("Speaks toast titles with the Windows voice you pick below. Stays quiet in game mode and when snoozed. Default: off.", "用你下面揀嘅 Windows 語音朗讀通知標題。遊戲模式同暫停期間唔會出聲。預設：關閉。");
            Add("Voices", "語音"); Add("Search voices", "搜尋語音");
            Add("English voice", "英文語音"); Add("Cantonese voice", "廣東話語音"); Add("Automatic", "自動");
            Add("Default: automatic, the first installed English voice.", "預設：自動，使用第一個已安裝嘅英文語音。");
            Add("Default: automatic, the first installed Cantonese voice.", "預設：自動，使用第一個已安裝嘅廣東話語音。");
            Add("No Cantonese voice is installed on this computer, so Cantonese is not spoken. Add one in the Windows speech settings.", "呢部電腦冇安裝廣東話語音，所以唔會讀出廣東話。請喺 Windows 語音設定新增。");
            Add("No English voice is installed on this computer. Add one in the Windows speech settings.", "呢部電腦冇安裝英文語音。請喺 Windows 語音設定新增。");
            Add("Open Windows speech settings", "開啟 Windows 語音設定");
            Add("Speaking rate", "朗讀速度"); Add("Default: 1.0×. Slower is 0.5×, faster is 2.0×.", "預設：1.0×。最慢 0.5×，最快 2.0×。");
            Add("Test voice", "測試語音"); Add("Speaks a short sentence in the current language so you can hear the voice and speed.", "用目前語言讀一句短句，等你聽到語音同速度。");
            Add("Stop speaking", "停止朗讀"); Add("Stops the current speech and clears anything waiting.", "停止目前朗讀，並清除排緊隊嘅內容。");
            Add("Narrator is off. Switch on Read toasts aloud first.", "朗讀已關閉，請先開啟「朗讀通知」。");
            Add("No Cantonese voice is installed, so Cantonese is not spoken.", "冇安裝廣東話語音，所以唔會讀出廣東話。");
            Add("Voices installed", "已安裝語音"); Add("No voices match.", "冇符合嘅語音。");

            // ---- tab strip, its menus and dialogs (added only when the table has no entry yet)
            void AddNew(string en, string yue) { t.TryAdd(en, yue); }
            AddNew("Overview", "總覽"); AddNew("Activity", "活動"); AddNew("Clipboard history", "剪貼簿記錄");
            AddNew("Clipboard", "剪貼簿"); AddNew("Shortcuts and scripts", "捷徑同腳本"); AddNew("Network and DNS", "網絡同 DNS");
            AddNew("Game mode", "遊戲模式"); AddNew("Conversion", "轉檔"); AddNew("Minecraft servers", "Minecraft 伺服器");
            AddNew("Local AI models", "本機 AI 模型"); AddNew("Tools", "工具"); AddNew("Safety and locks", "安全同鎖");
            AddNew("Service", "服務"); AddNew("Appearance", "外觀"); AddNew("Language and voice", "語言同語音");
            AddNew("Modes", "模式"); AddNew("Records and exports", "記錄同匯出"); AddNew("Help and docs", "說明同文件");
            AddNew("About and updates", "關於同更新"); AddNew("Tab strip position", "分頁列位置"); AddNew("Which edge of the control centre the tabs sit on. Default: left.", "分頁放喺控制中心邊一邊。預設：左。");
            AddNew("Left", "左"); AddNew("Top", "上"); AddNew("Right", "右");
            AddNew("Bottom", "下"); AddNew("Dock tabs to the left", "將分頁列移到左邊"); AddNew("Dock tabs to the top", "將分頁列移到上邊");
            AddNew("Dock tabs to the right", "將分頁列移到右邊"); AddNew("Dock tabs to the bottom", "將分頁列移到下邊"); AddNew("tab strip on the {0}", "分頁列放喺{0}邊");
            AddNew("Rename group", "重新命名群組"); AddNew("Group name", "群組名稱"); AddNew("Rename", "重新命名");
            AddNew("New group", "新群組"); AddNew("Create group", "建立群組"); AddNew("Move “{0}” into a group", "將「{0}」移入群組");
            AddNew("Search this tab strip…", "搜尋呢個分頁列…"); AddNew("Search all open tabs…", "搜尋所有已開分頁…"); AddNew("Search tab groups…", "搜尋分頁群組…");
            AddNew("Close tabs containing text…", "關閉包含文字嘅分頁…"); AddNew("Close tabs not containing text…", "關閉唔包含文字嘅分頁…"); AddNew("Unpin tab", "取消釘選分頁");
            AddNew("Pin tab", "釘選分頁"); AddNew("Move tab earlier", "分頁移前"); AddNew("Move tab later", "分頁移後");
            AddNew("Move tab up", "分頁上移"); AddNew("Move tab down", "分頁下移"); AddNew("Move… into group…", "移入群組…");
            AddNew("Remove from group", "從群組移除"); AddNew("Search this group…", "搜尋呢個群組…"); AddNew("New group with this tab…", "用呢個分頁建立新群組…");
            AddNew("Edit tab appearance…", "編輯分頁外觀…"); AddNew("Edit group appearance…", "編輯群組外觀…"); AddNew("Close tab", "關閉分頁");
            AddNew("Close other tabs", "關閉其他分頁"); AddNew("Expand group", "展開群組"); AddNew("Collapse group", "收合群組");
            AddNew("Rename group…", "重新命名群組…"); AddNew("Group colour…", "群組顏色…"); AddNew("Remove group (keeps its tabs)", "移除群組（保留分頁）");
            AddNew("New tab…", "新分頁…"); AddNew("New group…", "新群組…"); AddNew("New tab", "新分頁");
            AddNew("More tabs", "更多分頁"); AddNew("Tab options", "分頁選項"); AddNew("More tabs, {0} hidden", "更多分頁，隱藏咗 {0} 個");
            AddNew("Open tabs", "已開分頁"); AddNew("(active)", "（目前）"); AddNew("All open tabs are visible; scroll the strip to move through them", "所有已開分頁都睇到，捲動分頁列就可以逐個睇");
            AddNew("Close (Esc)", "關閉 (Esc)"); AddNew("Close dialog", "關閉視窗"); AddNew("{0} tab", "{0} 個分頁");
            AddNew("{0} tabs", "{0} 個分頁"); AddNew("(pinned)", "（已釘選）"); AddNew("pinned", "已釘選");
            AddNew("group", "群組"); AddNew("selected", "已選取"); AddNew("not selected", "未選取");
            AddNew("collapsed", "已收合"); AddNew("expanded", "已展開"); AddNew("no group", "冇群組");
            AddNew("active", "目前"); AddNew("in", "喺"); AddNew("Control centre", "控制中心");
            AddNew("Pin", "釘選"); AddNew("Unpin", "取消釘選"); AddNew("Enter opens. Delete or Ctrl+W closes. Ctrl+Shift+P pins. Ctrl+Shift+Up or Down reorders. Applications key opens the menu. Shift plus right-click edits the tab appearance.", "按 Enter 開啟。按 Delete 或 Ctrl+W 關閉。Ctrl+Shift+P 釘選。Ctrl+Shift+上或下調整次序。應用程式鍵開選單。Shift 加右鍵可編輯分頁外觀。");
            AddNew("Enter or Space collapses or expands. F2 renames. Applications key opens the group menu. Shift plus right-click edits the group appearance.", "按 Enter 或空白鍵收合或展開。F2 重新命名。應用程式鍵開群組選單。Shift 加右鍵可編輯群組外觀。"); AddNew("Every page of the control centre. Choosing one opens it in a tab, or switches to its tab when it is already open.", "控制中心所有頁面。揀一頁就會喺分頁開啟，如果已經開咗就會切換到佢嘅分頁。"); AddNew("Open. Choosing it switches to its tab.", "已開啟，揀佢就會切換到佢嘅分頁。");
            AddNew("Opens in a new tab.", "會喺新分頁開啟。"); AddNew("Search every page", "搜尋所有頁面"); AddNew("Search groups", "搜尋群組");
            AddNew("No groups yet. Create the first one below and the tab moves into it.", "仲未有群組。喺下面建立第一個，分頁就會移入去。"); AddNew("Create a new group", "建立新群組"); AddNew("New group name", "新群組名稱");
            AddNew("Create group and move", "建立群組並移入"); AddNew("Group colour", "群組顏色"); AddNew("Colours come from the app theme, so they follow light, dark and your appearance choices.", "顏色來自程式主題，所以會跟住淺色、深色同你嘅外觀選擇。");
            AddNew("Current colour", "目前顏色"); AddNew("Primary", "主色"); AddNew("Violet", "紫色");
            AddNew("Amber", "琥珀色"); AddNew("Tertiary", "第三色"); AddNew("Alert", "警示色");
            AddNew("Close tabs containing text", "關閉包含文字嘅分頁"); AddNew("Close tabs not containing text", "關閉唔包含文字嘅分頁"); AddNew("Matches the visible tab label only. Nothing is closed until you press the button below, and the preview shows exactly which tabs would close.", "只會比對睇得到嘅分頁標籤。撳下面個掣之前唔會關閉任何嘢，預覽會清楚顯示邊啲分頁會被關閉。");
            AddNew("Text in the tab label", "分頁標籤內嘅文字"); AddNew("Text the tab label must contain", "分頁標籤必須包含嘅文字"); AddNew("Include pinned tabs (off by default)", "包括已釘選分頁（預設關閉）");
            AddNew("Mode: regular expression", "模式：正規表示式"); AddNew(", match case", "，區分大小寫"); AddNew(", ignore case", "，唔分大小寫");
            AddNew("INVALID", "無效"); AddNew("Mode: plain text, every word must appear", "模式：純文字，每個字都要出現"); AddNew("Mode: plain text, every word must appear for a tab to be kept", "模式：純文字，分頁要包含每個字先會保留");
            AddNew("Enter text first. Nothing closes on an empty query.", "請先輸入文字。冇輸入就唔會關閉任何嘢。"); AddNew("The pattern is not valid, so nothing can close:", "表達式無效，所以唔會關閉任何嘢："); AddNew("{0} of {1} tabs would close.", "{1} 個分頁中有 {0} 個會被關閉。");
            AddNew("{0} pinned tab is protected.", "{0} 個已釘選分頁受保護。"); AddNew("{0} pinned tabs are protected.", "{0} 個已釘選分頁受保護。"); AddNew("Some labels took too long to check and were left out.", "有啲標籤檢查太耐，已經略過。");
            AddNew("and {0} more", "另外仲有 {0} 個"); AddNew("Close tabs", "關閉分頁"); AddNew("Close {0} tab", "關閉 {0} 個分頁");
            AddNew("Close {0} tabs", "關閉 {0} 個分頁"); AddNew("Search this tab strip", "搜尋呢個分頁列"); AddNew("Search the group “{0}”", "搜尋群組「{0}」");
            AddNew("Search tab groups", "搜尋分頁群組"); AddNew("Search all open tabs", "搜尋所有已開分頁"); AddNew("Search tabs in this strip", "搜尋呢個分頁列嘅分頁");
            AddNew("Search tabs in this group", "搜尋呢個群組嘅分頁"); AddNew("Search group names", "搜尋群組名稱"); AddNew("Search every open tab", "搜尋每個已開分頁");
            AddNew("Searches every tab of this strip. Groups and pinned state are shown on each result.", "搜尋呢個分頁列嘅每個分頁。每個結果都會顯示群組同釘選狀態。"); AddNew("Searches only the tabs of this group. It never looks at other groups.", "只搜尋呢個群組嘅分頁，唔會睇其他群組。"); AddNew("Searches the names of tab groups.", "搜尋分頁群組嘅名稱。");
            AddNew("Searches every open tab in this app. There is one window and one strip, shown on each result.", "搜尋呢個程式所有已開分頁。得一個視窗同一個分頁列，每個結果都會顯示。");
            AddNew("Locked", "已鎖"); AddNew("Unlock…", "解鎖…");
            AddNew("This item is locked and will not run until it is unlocked.", "呢個項目已鎖，解鎖之前唔會執行。");
            // ---- School mode password, Focus spotlight and the Modes page controls
            AddNew("Focus spotlight", "專注聚光燈"); AddNew("Password to turn off", "關閉用嘅密碼"); AddNew("PIN or password to turn off", "關閉用嘅 PIN 或密碼");
            AddNew("New password (8 or more characters)", "新密碼（8 個字元或以上）"); AddNew("Repeat password", "重複密碼");
            AddNew("Apply a copied wording file", "複製咗用字檔就即刻套用"); AddNew("Personal wording applied", "個人用字已套用");
            AddNew("Choose a way to turn the mode off: a PIN of 4 to 12 digits, a password of at least 8 characters, or both. Either one works later. They are stored only as salted PBKDF2-SHA256 hashes (200,000 rounds).",
                "請揀一個關閉模式嘅方法：4 至 12 位數字嘅 PIN、至少 8 個字元嘅密碼，或者兩個都設。之後用邊個都得。兩者只會以加鹽 PBKDF2-SHA256 雜湊（200,000 輪）儲存。");
            AddNew("Off by default. Dims everything except the card under keyboard focus or the mouse, so one thing stands out. Also switched with Ctrl+Shift+Space from any window of this app. It never blocks clicks, and it moves without animation when motion is reduced.",
                "預設關閉。將鍵盤焦點或者滑鼠所在嘅卡片以外嘅嘢全部調暗，令一樣嘢突出。喺呢個程式任何視窗都可以用 Ctrl+Shift+Space 開關。佢唔會擋住點擊，減少動態效果時亦唔會有動畫。");
            AddNew("Five independent modes and a Focus spotlight, each off by default. Turn on any mix. They are aids for focus, not medical advice, and they never hide anything you cannot get back with one click.",
                "五個獨立模式加一個專注聚光燈，預設全部關閉。可以隨意組合。呢啲係幫手專注嘅輔助，並非醫療建議，亦唔會隱藏任何撳一下就攞得返嘅嘢。");
            AddNew("Mode name", "模式名稱"); AddNew("Rule name", "規則名稱"); AddNew("Start time", "開始時間"); AddNew("End time", "結束時間");
            AddNew("New PIN", "新 PIN"); AddNew("Repeat PIN", "重複 PIN"); AddNew("PIN to turn off", "關閉用嘅 PIN");
            AddNew("Turn on", "開啟"); AddNew("Turn off", "關閉"); AddNew("Rename", "重新命名");
            AddNew("Dialogs", "對話框");
            AddNew("Headlines are plain while School mode is on. The humour levels are left out until it is off.", "學校模式開咗，標題會用簡單直接嘅講法，幽默程度要等關咗先有。");
            AddNew("Choose a PIN of 4 to 12 digits or a password of at least 8 characters.", "請設定 4 至 12 位數字嘅 PIN 或者至少 8 個字元嘅密碼。");
            AddNew("Choose a PIN of 4 to 12 digits.", "請設定 4 至 12 位數字嘅 PIN。"); AddNew("The two PINs do not match.", "兩個 PIN 唔一致。");
            AddNew("The two passwords do not match.", "兩個密碼唔一致。");
            AddNew("Choose a password of 8 to 128 characters.", "請設定 8 至 128 個字元嘅密碼。");
            AddMore(t);
            AddMinecraft(t);
            AddMinecraftCatalogue(t);
            AddMcCommands(t);
            AddRegexExplain(t);
            AddRegexParity(t);
            AddMcFiles(t);
            AddLane4(t);
            AddToasts(t);
            AddUpdate(t);
            AddMcLane(t);
            AddDocSys(t);
            return t;
        }

        // ------------------------------------------------------------------ patterns for built strings

        internal static readonly List<(Regex Pattern, Func<Match, string> Yue)> Patterns = BuildPatterns();

        private static List<(Regex, Func<Match, string>)> BuildPatterns()
        {
            var p = new List<(Regex, Func<Match, string>)>();
            AddDocSysPatterns(p);
            AddRegexExplainPatterns(p);
            AddRegexParityPatterns(p);
            AddMinecraftCataloguePatterns(p);
            AddMinecraftPatterns(p);
            AddMcCommandsPatterns(p);
            AddMcFilesPatterns(p);
            AddLane4Patterns(p);
            AddToastsPatterns(p);
            AddUpdatePatterns(p);
            AddMcLanePatterns(p);
            AddMorePatterns(p);
            void Add(string rx, string yue) => p.Add((new Regex(rx, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline), m => m.Result(yue)));
            void AddFn(string rx, Func<Match, string> fn) => p.Add((new Regex(rx, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline), fn));

            string Cred(string w) => w == "password" ? "密碼" : w == "PIN" ? "PIN" : "PIN 或密碼";
            AddFn(@"^Wrong (PIN|password|PIN or password)\. (\d+) tries left before a short wait\.$", m => $"{Cred(m.Groups[1].Value)}唔啱。再錯 {m.Groups[2].Value} 次就要等一陣。");
            AddFn(@"^Wrong (PIN|password|PIN or password)\. Wait (\d+) seconds before the next try\.$", m => $"{Cred(m.Groups[1].Value)}唔啱。要等 {m.Groups[2].Value} 秒先可以再試。");
            Add(@"^Too many wrong tries\. Wait (\d+) seconds\.$", "錯咗太多次，請等 $1 秒。"); Add(@"^Locked for another (\d+) seconds\.$", "仲要鎖 $1 秒。");
            Add(@"^(.+) is on\. The app shows English only, and the language choices, humour levels and personal wording are left out until it is off\.$", "$1 開咗。程式只會顯示英文，語言選擇、幽默程度同個人用語要等關咗先有。");
            Add(@"^(.+) is on\. Personal wording is left out until it is off\.$", "$1 開咗。個人用語要等關咗先有。");
            Add(@"^The last (\d+) copies, kept in memory only\. Quitting the app forgets them\.$", "最近 $1 次複製，只留喺記憶體。退出程式就會忘記。");
            AddFn(@"^(\d+) text · (\d+) links · (\d+) files · (\d+) media$", m => $"{m.Groups[1].Value} 段文字 · {m.Groups[2].Value} 條連結 · {m.Groups[3].Value} 個檔案 · {m.Groups[4].Value} 個媒體");
            Add(@"^(\d+) m ago$", "$1 分鐘前"); Add(@"^(\d+) h ago$", "$1 個鐘前"); Add(@"^(\d+) s ago$", "$1 秒前");
            Add(@"^(\d+) games watched · fullscreen on$", "監察緊 $1 隻遊戲 · 全螢幕都計"); Add(@"^(\d+) games watched · fullscreen off$", "監察緊 $1 隻遊戲 · 全螢幕唔計");
            Add(@"^(\d+) of (\d+)$", "$1 / $2"); Add(@"^(\d+) selected$", "揀咗 $1 個");
            Add(@"^(\d+) copies$", "$1 次複製"); Add(@"^(\d+) copy$", "$1 次複製");
            Add(@"^(\d+) text$", "$1 段文字"); Add(@"^(\d+) links?$", "$1 條連結"); Add(@"^(\d+) files?$", "$1 個檔案"); Add(@"^(\d+) media$", "$1 個媒體");
            Add(@"^gateway (\d+ ms)$", "閘道 $1"); Add(@"^dns (\d+ ms|\?)$", "DNS $1");
            Add(@"^(\d+) games watched$", "監察緊 $1 隻遊戲"); Add(@"^fullscreen on$", "全螢幕：開"); Add(@"^fullscreen off$", "全螢幕：關");
            Add(@"^(\d[\d,]*) chars$", "$1 個字元"); Add(@"^(\d[\d,]*) words$", "$1 個字"); Add(@"^(\d+) lines$", "$1 行"); Add(@"^(\d+) items$", "$1 個項目");
            Add(@"^valid$", "有效"); Add(@"^(\d+) top-level (?:keys|items)$", "$1 個頂層項目");
            Add(@"^Converting to (.+)$", "轉換緊做 $1"); Add(@"^Saved as (.+)$", "已儲存為 $1");
            Add(@"^seen (\d+) times before$", "之前見過 $1 次");
            Add(@"^previous held (.+)$", "上一個維持咗 $1"); Add(@"^last seen (.+)$", "最後見到 $1");
            Add(@"^Offline for (\d+) m (\d+) s\.$", "斷線咗 $1 分 $2 秒。");
            Add(@"^Switching DNS to (.+)…$", "轉緊 DNS 去 $1…"); Add(@"^DNS is now (.+)\. Checking again…$", "DNS 而家係 $1，再檢查一次…");
            Add(@"^DNS is now (.+)$", "DNS 而家係 $1"); Add(@"^DNS set to (.+)$", "DNS 已設為 $1"); Add(@"^Switch DNS to (.+)$", "轉 DNS 去 $1");
            Add(@"^Switching to (.+)…$", "轉換緊去 $1…"); Add(@"^Approve the prompt to switch DNS to (.+)…$", "請喺提示按批准，將 DNS 轉去 $1…");
            Add(@"^Toasts on, (\d+) s$", "通知已開，$1 秒");
            Add(@"^STEP (\d+) OF (\d+)(.*)$", "第 $1 步，共 $2 步$3");
            Add(@"^Delete (\d+)\?$", "刪除 $1 個項目？"); Add(@"^Delete (\d+)$", "刪除 $1 個"); Add(@"^Remove (.+)\?$", "移除 $1？");
            Add(@"^Edit :(.+)$", "編輯 :$1"); Add(@"^Active now: (.+)$", "而家：$1"); Add(@"^Chosen: (.+)$", "已揀：$1");
            Add(@"^Minecraft server started: (.+)$", "Minecraft 伺服器已啟動：$1"); Add(@"^Minecraft server stopped: (.+)$", "Minecraft 伺服器已停止：$1");
            Add(@"^Convert (\d+) files?$", "轉換 $1 個檔案"); Add(@"^Convert (\d+) of these to (.+)$", "將其中 $1 個轉換做 $2");
            Add(@"^Encrypt (\d+)$", "加密 $1 個"); Add(@"^Decrypt (\d+)$", "解密 $1 個"); Add(@"^Encode (\d+)$", "編碼 $1 個"); Add(@"^Decode (\d+)$", "解碼 $1 個");
            Add(@"^Snoozed to (.+)$", "已暫停到 $1"); Add(@"^More · (\d+)$", "更多 · $1");
            Add(@"^(\d+) of (\d+)$", "$1／$2");
            Add(@"^Installed at (.+)$", "已安裝喺 $1");
            Add(@"^Offline: (.+)\.$", "離線：$1。");
            Add(@"^(\d+) h (\d+) m$", "$1 小時 $2 分"); Add(@"^(\d+) m$", "$1 分");
            Add(@"^~(\d+) s left$", "尚餘約 $1 秒");
            AddFn(@"^Search (.+?)( with a pattern)?$", m => "搜尋" + (Language.Yue(m.Groups[1].Value) ?? m.Groups[1].Value) + (m.Groups[2].Success ? "（用正規表示式）" : ""));
            AddFn(@"^No (.+) match “(.*)”\.$", m => "冇" + (Language.Yue(m.Groups[1].Value) ?? m.Groups[1].Value) + "符合「" + m.Groups[2].Value + "」。");
            Add(@"^The pattern is not valid, so nothing is filtered: (.+)$", "表達式無效，所以冇篩選任何嘢：$1");
            return p;
        }
    }
}
