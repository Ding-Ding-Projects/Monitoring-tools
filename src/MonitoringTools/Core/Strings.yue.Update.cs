using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MonitoringTools.Core
{
    /// <summary>Hong Kong Cantonese for the background updater: the About page switch, its status lines and the notice after an update.</summary>
    internal static partial class Strings
    {
        private static void AddUpdate(Dictionary<string, string> t)
        {
            void Add(string en, string yue) => t.TryAdd(en, yue);

            Add("Updates install themselves in the background.", "更新會自己喺背景安裝。");
            Add("Install updates by themselves", "自動安裝更新");
            Add("Downloads each new release in the background, checks it against the checksum GitHub publishes, and installs it at a quiet moment: not during a conversion, a game, or a Minecraft server started here. The service is back in a few seconds; nothing to download or restart. On by default.",
                "喺背景下載每個新版本，用 GitHub 公布嘅校驗碼核對，再揀個靜嘅時候安裝：唔會喺轉檔、打機或者喺呢度開咗 Minecraft 伺服器嘅時候裝。服務幾秒後就返嚟，唔使自己下載亦唔使重新開機。預設：開。");
            Add("Check now", "即刻檢查");
            Add("Checking…", "檢查緊…");
            Add("Looking for a newer version…", "搵緊新版本…");
            Add("Could not read the latest release.", "讀唔到最新版本。");
            Add("Monitoring Tools updated", "Monitoring Tools 已更新");
            Add("It installs by itself in the background.", "佢會自己喺背景安裝。");

            // ---- game started and stopped notices
            Add("Say when a game starts", "開遊戲時通知我");
            Add("Shows a short notice when a game starts (for example \"Roblox detected\") and when it closes, so you know game mode is holding toasts back. The notice never takes focus from the game. On by default.",
                "遊戲開始（例如「偵測到 Roblox」）同關咗嘅時候會出個短通知，等你知道遊戲模式擋住咗啲彈出通知。個通知唔會搶走遊戲嘅焦點。預設：開。");
            Add("Game mode is on. Toasts and alerts wait until you stop playing; quick commands still run.", "遊戲模式開咗。彈出通知同警告會等你打完機先出；快速指令照行。");
            Add("Game mode is off. Anything held back shows now.", "遊戲模式關咗。之前擋住嘅嘢而家出返嚟。");
            Add("Game mode settings", "遊戲模式設定");
            Add("Stop these notices", "唔再通知");

            // ---- authenticator code from a copied QR picture
            Add("Register for codes", "登記驗證碼");
            Add("Registered", "已登記");
            Add("Already registered", "已經登記咗");
            Add("Not registered", "登記唔到");
            Add("Registered for codes", "已登記驗證碼");
            Add("Show codes", "睇驗證碼");

            // ---- codes in copied pictures
            Add("Copy QR text", "複製 QR 內容"); Add("Copy barcode text", "複製條碼內容"); Add("Copy code text", "複製碼嘅內容");
            Add("Open link", "開連結");
            Add("QR code", "QR 碼"); Add("Data Matrix code", "Data Matrix 碼"); Add("Aztec code", "Aztec 碼"); Add("PDF417 code", "PDF417 碼");
            Add("MaxiCode", "MaxiCode 碼"); Add("EAN-13 barcode", "EAN-13 條碼"); Add("EAN-8 barcode", "EAN-8 條碼"); Add("UPC-A barcode", "UPC-A 條碼");
            Add("UPC-E barcode", "UPC-E 條碼"); Add("Code 39 barcode", "Code 39 條碼"); Add("Code 93 barcode", "Code 93 條碼"); Add("Code 128 barcode", "Code 128 條碼");
            Add("ITF barcode", "ITF 條碼"); Add("Codabar barcode", "Codabar 條碼"); Add("GS1 DataBar barcode", "GS1 DataBar 條碼"); Add("Barcode", "條碼");

            // ---- copied folders and Git repositories
            Add("Open in Desktop Material", "用 Desktop Material 開");
            Add("Start a Git repository", "開個 Git 儲存庫");
            Add("Started", "開咗");
            Add("Repository not started", "儲存庫開唔到");
            Add("Git repository started", "Git 儲存庫開咗");
            Add("Clone to…", "複製去…"); Add("Clone failed", "複製唔到"); Add("Already cloned", "已經複製咗"); Add("Cloned", "複製好");
            Add("Choose the folder to clone into this time.", "揀今次要複製去邊個資料夾。");
            Add("Clone GitHub repositories", "複製 GitHub 儲存庫");
            Add("Copy a GitHub link and its toast offers Clone (into the folder below) and Clone to… (choose a folder that time). Private repositories work when the GitHub CLI is signed in.",
                "複製一條 GitHub 連結，通知就會有「複製去」（下面個資料夾）同「複製去…」（今次自己揀資料夾）。GitHub CLI 登入咗嘅話，私人儲存庫都得。");
            Add("Change clone folder…", "改複製資料夾…"); Add("Use Documents\\GitHub", "用 Documents\\GitHub");
        }

        private static void AddUpdatePatterns(List<(Regex, Func<Match, string>)> p)
        {
            void Add(string rx, string yue) => p.Add((new Regex(rx, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline), m => m.Result(yue)));
            void AddFn(string rx, Func<Match, string> fn) => p.Add((new Regex(rx, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline), fn));

            // the Records page when GitHub cannot be reached (rate limits included); the error itself is the server's own text
            AddFn(@"^GitHub could not be reached \((.+)\)\. Showing the copy saved (.+) \((.+)\)\.$", m =>
                $"連唔到 GitHub（{Reason(m.Groups[1].Value)}）。顯示 {m.Groups[2].Value} 儲存嘅副本（{m.Groups[3].Value}）。");
            AddFn(@"^The changelog is unavailable: GitHub could not be reached \((.+)\) and no saved copy exists yet\.$", m =>
                $"更新紀錄用唔到：連唔到 GitHub（{Reason(m.Groups[1].Value)}），亦未有儲存嘅副本。");
            Add(@"^Clone to (.+)$", "複製去 $1");
            Add(@"^Clones (.+) into (.+)\. Change the default folder on the Tools page\.$", "將 $1 複製去 $2。預設資料夾喺「工具」頁改。");
            Add(@"^Cloning (.+)$", "複製緊 $1");
            Add(@"^Clone (.+) into…$", "將 $1 複製去…");
            Add(@"^(.+) already exists and is not empty\.$", "$1 已經存在而且唔係空嘅。");
            Add(@"^Copies what the (.+) holds: (.*)$", "複製 $1 入面嘅內容：$2");
            Add(@"^Opens (https?://.+)$", "開 $1");
            Add(@"^Opens (.+) in Desktop Material\. It is added to Desktop Material's list first if it is not there yet\.$", "用 Desktop Material 開 $1；如果未喺佢個清單入面，會先加入去。");
            Add(@"^Runs git init in (.+) with the bundled Git, on a main branch\. Nothing is committed\.$", "用內置 Git 喺 $1 行 git init，分支係 main，唔會提交任何嘢。");
            Add(@"^(.+) is now a Git repository on the main branch\.$", "$1 而家係 main 分支嘅 Git 儲存庫。");
            Add(@"^That folder is already inside a Git repository\.$", "嗰個資料夾已經喺 Git 儲存庫入面。");
            Add(@"^(.+) is already in the authenticator\.$", "$1 已經喺驗證器入面。");
            Add(@"^(.+) now shows its codes on the Safety page\.$", "$1 嘅驗證碼而家喺「安全」頁顯示。");
            Add(@"^This picture is an authenticator code for (.+)\. Adds it to the built-in authenticator \(Safety page\), stored encrypted for this Windows account\.$",
                "呢張圖係 $1 嘅驗證碼。會加入內置驗證器（「安全」頁），用呢個 Windows 帳戶加密儲存。");
            Add(@"^(.+) detected$", "偵測到 $1");
            Add(@"^(.+) closed$", "$1 已關閉");
            Add(@"^Up to date \(([\d.]+)\)\. Checked ([\d:]+)\.$", "已經係最新（$1）。$2 檢查過。");
            Add(@"^Downloading version ([\d.]+) in the background…$", "喺背景下載緊版本 $1…");
            Add(@"^Version ([\d.]+) is downloaded and checked\.$", "版本 $1 已經下載同核對好。");
            AddFn(@"^Version ([\d.]+) is downloaded and checked\. It installs by itself when (.+)\.$", m =>
                $"版本 {m.Groups[1].Value} 已經下載同核對好，{When(m.Groups[2].Value)}就會自己安裝。");
            Add(@"^Installing version ([\d.]+)\. The service is back in a few seconds\.$", "安裝緊版本 $1，服務幾秒後就返嚟。");
            Add(@"^Version ([\d.]+) has no published checksum, so it was not downloaded\.$", "版本 $1 冇公布校驗碼，所以冇下載。");
            Add(@"^The download of ([\d.]+) did not match its published checksum, so it was thrown away\.$", "版本 $1 下載落嚟對唔上公布嘅校驗碼，已經掉咗。");
            Add(@"^The download says it is version (.+), not ([\d.]+), so it was thrown away\.$", "下載落嚟話自己係版本 $1，唔係 $2，已經掉咗。");
            Add(@"^Update check failed: (.+)$", "檢查更新失敗：$1");
            Add(@"^GitHub's limit for update checks is used up\. It tries again at ([\d:]+)\.$", "GitHub 檢查更新嘅次數用晒，$1 再試。");
            Add(@"^Updated from ([\d.]+) to ([\d.]+) in the background\.$", "已經喺背景由 $1 更新到 $2。");
            Add(@"^Updated to ([\d.]+) in the background\.$", "已經喺背景更新到 $1。");
            Add(@"^You have ([\d.]+)\. It installs by itself in the background\.$", "你而家用緊 $1。佢會自己喺背景安裝。");
        }

        /// <summary>The reasons Changelog.PlainError gives, in Cantonese.</summary>
        private static string Reason(string en) => en switch
        {
            "the request timed out after 10 seconds" => "等咗 10 秒都冇回應",
            "its limit for requests without signing in is used up for now" => "未登入嘅查詢次數暫時用晒",
            "there is no connection to it" => "連唔上",
            "an unexpected error" => "出咗意料之外嘅錯",
            _ when en.StartsWith("it answered with an error, code ") => "佢回覆錯誤，代碼 " + en.Substring("it answered with an error, code ".Length),
            _ => en,
        };

        private static string When(string en) => en switch
        {
            "the conversion finishes" => "轉完檔",
            "that Minecraft server stops" => "嗰個 Minecraft 伺服器停咗",
            "you stop playing or step away for 10 minutes" => "你唔再打機或者走開 10 分鐘",
            "it runs as the installed copy" => "用返已安裝嘅版本行",
            _ => "冇嘢行緊",
        };
    }
}
