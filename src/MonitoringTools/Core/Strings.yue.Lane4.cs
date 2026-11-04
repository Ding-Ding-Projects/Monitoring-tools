using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MonitoringTools.Core
{
    /// <summary>
    /// Hong Kong Cantonese for the local history manager, the focus and time modes, the palette menu, the stroke width keys and a few
    /// switch captions.
    /// </summary>
    internal static partial class Strings
    {
        private static void AddLane4(Dictionary<string, string> t)
        {
            void Add(string en, string yue) => t.TryAdd(en, yue);

            // ---- local history manager (Records page)
            Add("Local history", "本機歷史");
            Add("Scripts and the commit message template keep earlier versions on this computer. Choose how they are stored and how long they are kept. Nothing here is uploaded.",
                "腳本同提交訊息範本會喺呢部電腦留低舊版本。你可以揀點樣存、存幾耐。呢度冇嘢會上載。");
            Add("History status", "歷史狀態");
            Add("Protection", "保護方式");
            Add("Off", "關"); Add("This Windows account", "呢個 Windows 帳戶"); Add("Password", "密碼");
            Add("Off keeps versions as plain text files. This Windows account encrypts them with a key that only this Windows account can open. Password encrypts them with a key made from a password you choose (8 or more characters). The password is not stored and cannot be recovered, and the history stays locked until you type it. Default: Off.",
                "「關」會將版本存成普通文字檔。「呢個 Windows 帳戶」會用只有呢個 Windows 帳戶開到嘅金鑰加密。「密碼」會用你自己揀嘅密碼（至少 8 個字元）造金鑰加密。密碼唔會被儲存，亦冇得還原，要打咗密碼歷史先會解鎖。預設：關。");
            Add("History password", "歷史密碼"); Add("Type the history password again", "再打一次歷史密碼");
            Add("Apply protection", "套用保護");
            Add("Moves every saved version to the chosen protection. If it cannot read one, nothing is changed.", "將所有已存版本轉去所揀嘅保護方式。如果有一個讀唔到，就乜都唔會改。");
            Add("Unlock history", "解鎖歷史");
            Add("Types the history password. Saved versions can be read and new ones kept until the history is locked again.", "輸入歷史密碼。解鎖後可以讀已存版本、亦可以存新版本，直到再鎖返。");
            Add("Lock history now", "即刻鎖住歷史");
            Add("Forgets the password in memory. Saved versions cannot be read until it is typed again.", "喺記憶體忘記密碼。要再打密碼先讀得返已存版本。");
            Add("The two passwords are different. Nothing was changed.", "兩個密碼唔一樣，乜都冇改。");
            Add("Saved versions are stored as plain text again.", "已存版本重新存成普通文字。");
            Add("Saved versions are now encrypted for this Windows account.", "已存版本而家用呢個 Windows 帳戶加密。");
            Add("Saved versions are now encrypted with your password. They are unlocked until you lock them or close the app.", "已存版本而家用你嘅密碼加密。直到你鎖住或者關閉程式之前都係解咗鎖。");
            Add("The history is unlocked until you lock it or close the app.", "歷史已解鎖，直到你鎖住或者關閉程式。");
            Add("The history is locked.", "歷史已鎖住。");
            Add("Unknown history protection.", "唔認識呢種歷史保護方式。");
            Add("Unlock the history with its password first.", "請先用密碼解鎖歷史。");
            Add("A history password needs at least 8 characters.", "歷史密碼至少要 8 個字元。");
            Add("A saved version could not be read, so nothing was changed.", "有一個已存版本讀唔到，所以乜都冇改。");
            Add("Windows could not protect a key for this account, so nothing was changed.", "Windows 未能為呢個帳戶保護金鑰，所以乜都冇改。");
            Add("This history is not protected by a password.", "呢個歷史冇用密碼保護。");
            Add("That password is not right.", "密碼唔啱。");
            Add("Versions kept per item", "每項保留嘅版本數");
            Add("The most saved versions each script or template keeps; the oldest are dropped first. Default: 50.", "每個腳本或範本最多保留幾多個已存版本；最舊嘅先會被丟棄。預設：50。");
            Add("Drop versions older than", "丟棄多過以下日數嘅版本");
            Add("Saved versions older than this many days are removed; the newest of each item always stays. 0 keeps them for ever. Default: 0.", "超過呢個日數嘅已存版本會被移除；每項最新嘅一個一定保留。0 代表永遠保留。預設：0。");
            Add("days", "日");
            Add("Versions are kept for ever.", "版本會永遠保留。");
            Add("The history is locked. Type the password and choose Unlock history to see and restore saved versions.", "歷史已鎖住。請打密碼再㩒「解鎖歷史」，先睇到同還原已存版本。");
            Add("No script or template has a history yet. The first save of one creates it.", "仲未有腳本或範本有歷史。第一次儲存就會建立。");
            Add("Saved histories", "已存歷史");
            Add("Delete history", "刪除歷史");
            Add("History of this script", "呢個腳本嘅歷史"); Add("History of this commit message template", "呢個提交訊息範本嘅歷史");
            Add("Every save keeps a copy on this computer, up to the limit set on the Records page; the oldest is dropped first. Pick a version to compare it with the text as it is now. Restore puts that version in the editor and saves it, after you confirm. The text you are replacing is kept in the history first.",
                "每次儲存都會喺呢部電腦留一份副本，上限喺「記錄」頁設定；最舊嘅先會被丟棄。揀一個版本就可以同而家嘅文字比較。「還原」會喺你確認後將嗰個版本放入編輯器並儲存。被取代嘅文字會先留喺歷史入面。");
            Add("The history is locked. Unlock it on the Records page to see and restore earlier versions.", "歷史已鎖住。請喺「記錄」頁解鎖，先睇到同還原舊版本。");

            // ---- drawing, palette, tray, appearance editor
            Add("Thinner stroke", "筆畫幼啲"); Add("Thicker stroke", "筆畫粗啲");
            Add("Clear the search", "清除搜尋"); Add("Use the card size", "用卡片大細"); Add("Use the full window", "用全視窗"); Add("Close the palette", "關閉指令面板");
            Add("popup when you copy", "複製時彈出"); Add("popup when the link drops", "斷線時彈出");
            Add("Slants the text. Default: as the element normally looks.", "令文字傾斜。預設：照元素平時嘅樣。");
            Add("Draws a line under the text. Default: off.", "喺文字下面畫條線。預設：關。");
            Add("0 s elapsed", "已過 0 秒");
        }

        private static void AddLane4Patterns(List<(Regex, Func<Match, string>)> p)
        {
            void Add(string rx, string yue) => p.Add((new Regex(rx, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline), m => m.Result(yue)));

            Add(@"^How saved versions are stored: (.+)$", "已存版本點樣儲存：$1");
            Add(@"^The password check could not be read: (.+)$", "讀唔到密碼檢查：$1");
            Add(@"^The change failed and was stopped: (.+)$", "更改失敗，已停止：$1");
            Add(@"^Keeping up to (\d+) versions per item\.$", "每項最多保留 $1 個版本。");
            Add(@"^Keeping up to (\d+) versions per item\. (\d+) older versions were removed\.$", "每項最多保留 $1 個版本。已移除 $2 個舊版本。");
            Add(@"^Versions older than (\d+) days are dropped\. (\d+) were removed now\.$", "超過 $1 日嘅版本會被丟棄。而家已移除 $2 個。");
            Add(@"^(.+)   ·   (\d+) versions?$", "$1   ·   $2 個版本");
            Add(@"^Delete the history of (.+)\?$", "刪除「$1」嘅歷史？");
            Add(@"^Delete the history of (.+)$", "刪除「$1」嘅歷史");
            Add(@"^The history of (.+) was deleted\.$", "「$1」嘅歷史已刪除。");
            Add(@"^All (\d+) saved versions of this item are removed for good\. The script or template itself is not changed\.$", "呢項嘅 $1 個已存版本會永久移除。腳本或範本本身唔會改。");
            Add(@"^Stroke width (\d+)$", "筆畫闊度 $1");
            Add(@"^(\d+) s elapsed$", "已過 $1 秒");
            Add(@"^Took (\d+) s$", "用咗 $1 秒");
        }
    }
}
