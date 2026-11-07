using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MonitoringTools.Core
{
    /// <summary>
    /// Hong Kong Cantonese for the dim sum switch, the Base64 drop, the live chart, collapsible panels, resizable
    /// panels, live menu controls and the virtual disk manager.
    /// </summary>
    internal static partial class Strings
    {
        private static void AddFeatures(Dictionary<string, string> t)
        {
            void Add(string en, string yue) => t.TryAdd(en, yue);

            // dim sum switch
            Add("Each time the app starts there is a 10% chance it shows one dim sum dish for a few seconds, with its English and Cantonese names and a picture. It never takes focus, never delays startup, and skips first-run setup and quiet moments such as game mode.",
                "程式每次啟動都有 10% 機會顯示一款點心幾秒鐘，連埋英文同廣東話名稱同一張圖片。佢唔會搶焦點，唔會拖慢啟動，首次設定同遊戲模式呢啲安靜時刻都會跳過。");
            Add("On, and it stays on. School mode is the one thing that holds it back.", "開住，而且會一直開住。只有學校模式先可以暫時收埋佢。");
            Add("Nice try. The dim sum surprise cannot be turned off.", "諗都唔好諗。點心驚喜係關唔到㗎。");
            Add("Still no. The dim sum surprise cannot be turned off.", "都係唔得。點心驚喜係關唔到㗎。");
            Add("It really does stay on. The dim sum surprise cannot be turned off.", "真係會一直開住㗎。點心驚喜係關唔到㗎。");
            Add("Have a har gow instead. The dim sum surprise cannot be turned off.", "不如食粒蝦餃啦。點心驚喜係關唔到㗎。");

            // Base64 beside the file
            Add("Annotate pictures, write Base64 beside a file, publish a folder to GitHub and choose which editor opens your files. Each control says what it does and what it starts as.",
                "為圖片加註解、喺檔案旁邊寫 Base64、將資料夾發佈到 GitHub，以及揀邊個編輯器開啟你嘅檔案。每個控制項都會講明作用同預設值。");
            Add("Base64 beside the file", "喺檔案旁邊寫 Base64");
            Add("Drop any file here and its Base64 text is written next to it as name.ext.base64.txt. Drop that text file and the original comes back next to it, with its extension. Nothing is overwritten: a name that is taken gets a number. Every result is checked by a SHA-256 round trip before it counts, and each one can be undone.",
                "將任何檔案拖落嚟，佢嘅 Base64 文字就會以 name.ext.base64.txt 寫喺佢旁邊。將嗰個文字檔拖返落嚟，原檔就會連副檔名喺旁邊還原。乜都唔會俾覆蓋：名已經有人用就會加個數字。每個結果都要先通過 SHA-256 來回核對先算數，而且每個都可以復原。");
            Add("Drop files here", "將檔案拖落嚟");
            Add("Or choose them. A .base64.txt file turns back into its original.", "或者揀檔案。.base64.txt 檔案會變返做原檔。");
            Add("Choose files…", "揀檔案…");
            Add("Choose files to encode, or .base64.txt files to decode", "揀要編碼嘅檔案，或者要解碼嘅 .base64.txt 檔案");
            Add("Drop files here to write Base64 beside them, or press Enter to choose files", "將檔案拖落嚟就會喺旁邊寫 Base64，或者撳 Enter 揀檔案");
            Add("Base64 progress", "Base64 進度");
            Add("Only files can be converted. Folders are left as they are.", "只可以轉換檔案，資料夾會保持原狀。");
            Add("A conversion is still running. Wait for it, or cancel it first.", "仲有一個轉換做緊。等佢做完，或者先取消佢。");
            Add("Cancelled. The file that was being written was removed, and the files you dropped were not touched.", "已取消。寫到一半嘅檔案已經刪走，你拖落嚟嘅檔案冇郁過。");
            Add("Done. One file written beside its original and checked.", "完成。已經喺原檔旁邊寫好一個檔案並核對過。");
            Add("The usual name was taken, so this one is numbered.", "平時用嘅名已經有人用，所以呢個加咗數字。");
            Add("Show", "顯示"); Add("Undo", "復原");
            Add("Writes name.ext.base64.txt next to each file and checks it decodes back.", "喺每個檔案旁邊寫 name.ext.base64.txt，並核對佢可以解碼返原檔。");
            Add("Turns .base64.txt files back into the originals, beside them.", "將 .base64.txt 檔案喺旁邊還原成原檔。");
            Add("The file is no longer there.", "個檔案已經唔喺度。");
            Add("There is no Base64 text in the file.", "個檔案入面冇 Base64 文字。");
            Add("The Base64 text ends one character short of a whole byte.", "Base64 文字結尾差一個字元先夠一個完整位元組。");
        }

        private static void AddFeaturesPatterns(List<(Regex, Func<Match, string>)> p)
        {
            void Add(string rx, string yue) => p.Add((new Regex(rx, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline), m => m.Result(yue)));
            Add(@"^Encoding (.+?)( \((\d+) of (\d+)\))?$", "編碼緊 $1$2");
            Add(@"^Decoding (.+?)( \((\d+) of (\d+)\))?$", "解碼緊 $1$2");
            Add(@"^Done\. (\d+) files written beside their originals and checked\.$", "完成。已經喺原檔旁邊寫好 $1 個檔案並核對過。");
            Add(@"^(\d+) written, (\d+) stopped\. Each stopped file says why below\.$", "寫好 $1 個，停咗 $2 個。每個停咗嘅檔案下面都有講原因。");
            Add(@"^Undone\. (.+) is in the Recycle Bin, and the file it came from was not touched\.$", "已復原。$1 喺資源回收筒，佢嘅來源檔案冇郁過。");
            Add(@"^(.+) has changed since it was written, so it was left alone\.$", "$1 寫咗之後改過，所以冇郁佢。");
            Add(@"^Byte ([\d,]+) is not Base64 text\.$", "第 $1 個位元組唔係 Base64 文字。");
            Add(@"^There is Base64 text after the padding, at byte ([\d,]+)\.$", "第 $1 個位元組：補位之後仲有 Base64 文字。");
        }
    }
}
