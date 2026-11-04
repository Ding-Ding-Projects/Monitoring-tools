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
        }

        private static void AddUpdatePatterns(List<(Regex, Func<Match, string>)> p)
        {
            void Add(string rx, string yue) => p.Add((new Regex(rx, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline), m => m.Result(yue)));
            void AddFn(string rx, Func<Match, string> fn) => p.Add((new Regex(rx, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline), fn));

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
            Add(@"^Updated from ([\d.]+) to ([\d.]+) in the background\.$", "已經喺背景由 $1 更新到 $2。");
            Add(@"^Updated to ([\d.]+) in the background\.$", "已經喺背景更新到 $1。");
            Add(@"^You have ([\d.]+)\. It installs by itself in the background\.$", "你而家用緊 $1。佢會自己喺背景安裝。");
        }

        private static string When(string en) => en switch
        {
            "the conversion finishes" => "轉完檔",
            "that Minecraft server stops" => "嗰個 Minecraft 伺服器停咗",
            "you stop playing" => "你唔再打機",
            "it runs as the installed copy" => "用返已安裝嘅版本行",
            _ => "冇嘢行緊",
        };
    }
}
