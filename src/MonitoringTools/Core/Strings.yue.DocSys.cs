using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MonitoringTools.Core
{
    /// <summary>
    /// Hong Kong Cantonese for the archive, subtitle, font and disc image dialogs. File names, drive letters, paths, times and
    /// format names stay as they are.
    /// </summary>
    internal static partial class Strings
    {
        private static void AddDocSys(Dictionary<string, string> t)
        {
            void Add(string en, string yue) => t.TryAdd(en, yue);

            // ---- shared window parts
            Add("Closes this window (Esc).", "關閉呢個視窗（Esc）。"); Add("Copy details", "複製詳情"); Add("Open folder", "開啟資料夾");
            Add("WHAT WAS NOT CHANGED", "冇改動嘅嘢"); Add("Name", "名稱"); Add("Size", "大小"); Add("Modified", "修改日期");
            Add("Select none", "全部唔揀"); Add("Select all", "全選"); Add("None", "冇");

            // ---- archive
            Add("Extract an archive", "解壓縮檔案"); Add("Archive contents", "壓縮檔內容"); Add("Search inside the archive", "喺壓縮檔入面搜尋");
            Add("Extract to", "解壓到"); Add("Choose folder", "揀資料夾"); Add("Choose where the files go", "揀檔案放去邊度");
            Add("A new folder is made from the archive name.", "會用壓縮檔嘅名開個新資料夾。");
            Add("If a file already exists", "如果檔案已經存在"); Add("Skip", "略過"); Add("Replace", "取代"); Add("Keep both", "兩個都留");
            Add("Files never leave the chosen folder.", "檔案唔會放出所揀嘅資料夾以外。");
            Add("Some paths in this archive are unsafe", "呢個壓縮檔有啲路徑唔安全");
            Add("Entries that would land outside the destination folder are refused and counted when it finishes.", "會跑出目的地資料夾以外嘅項目會被拒絕，完成之後會列出數量。");
            Add("unsafe path, will be refused", "路徑唔安全，會被拒絕");
            Add("Choose a folder to extract into.", "請揀要解壓去嘅資料夾。"); Add("That folder name is not valid.", "呢個資料夾名唔啱用。");
            Add("This archive needs a password", "呢個壓縮檔要密碼");
            Add("The file names are visible, the contents are locked. The password is used once and not stored.", "檔名睇得到，內容就鎖咗。密碼只會用一次，唔會儲起。");
            Add("Password", "密碼"); Add("Show the password while typing", "輸入時顯示密碼"); Add("Unlock and extract", "解鎖並解壓");
            Add("Try again or Cancel.", "再試一次或者取消。"); Add("Type the password, then Unlock and extract.", "輸入密碼，然後撳「解鎖並解壓」。");
            Add("Wrong password for this archive. Nothing was extracted.", "呢個壓縮檔嘅密碼唔啱，冇解壓任何嘢。");
            Add("Wrong password for this archive", "呢個壓縮檔嘅密碼唔啱");
            Add("Nothing more was extracted. Files already written are kept.", "之後冇再解壓。已經寫好嘅檔案會保留。");
            Add("This archive cannot be opened here", "呢個壓縮檔喺度打唔開");
            Add("Nothing was extracted and the archive was not changed.", "冇解壓任何嘢，壓縮檔都冇改動。");
            Add("Zip, tar, tar.gz, tgz and gz archives open here.", "呢度可以開 zip、tar、tar.gz、tgz 同 gz 壓縮檔。");
            Add("Extraction stopped", "解壓停咗");
            Add("Cancel stops now; files already written are kept.", "取消會即刻停；已經寫好嘅檔案會保留。");
            Add("Cancel stops now; files already copied are kept.", "取消會即刻停；已經複製咗嘅檔案會保留。");
            Add("Cancel stopped the extraction.", "已經取消解壓。"); Add("Nothing was extracted into a partial file.", "冇留低寫到一半嘅檔案。");
            Add("Open the folder to see the files.", "開啟資料夾就睇到啲檔案。");
            Add("Files already written are kept; the one in progress was removed.", "已經寫好嘅檔案會保留；寫緊嗰個已經移除。");
            Add("Every selected file was written.", "所揀嘅檔案全部寫好。");
            Add("The unsafe paths below were refused; every other selected file was written.", "下面嘅唔安全路徑已被拒絕；其他所揀嘅檔案全部寫好。");
            Add("Renamed", "改咗名"); Add("Not selected", "冇揀"); Add("Refused", "被拒絕"); Add("0 unsafe paths", "0 個唔安全路徑");
            Add("Delete the archive", "刪除壓縮檔"); Add("Extraction progress", "解壓進度"); Add("Extract", "解壓");
            Add("This archive cannot be opened here.", "呢個壓縮檔喺度打唔開。");
            Add("could not read", "讀唔到"); Add("extracting", "解壓緊"); Add("stopped", "已停止"); Add("done", "完成");
            Add("encrypted", "已加密");

            // ---- subtitles
            Add("Subtitle timing", "字幕時間"); Add("Subtitle cues", "字幕清單"); Add("Start", "開始"); Add("End", "結束"); Add("Text", "文字");
            Add("Shift", "平移"); Add("Stretch", "拉伸"); Add("Convert", "轉換"); Add("What to do", "想做乜");
            Add("Move all by", "全部移動"); Add("Move all by, in seconds", "全部移動幾多秒"); Add("seconds", "秒");
            Add("Negative moves earlier.", "負數即係提早。"); Add("Result for the first and last cue", "第一句同最後一句嘅結果");
            Add("Out of sync by a changing amount?", "差嘅時間一路變？");
            Add("Use Stretch: set where the first and last cue should land and every cue is spread between them.", "用「拉伸」：設定第一句同最後一句應該喺幾時出現，中間每一句會自動平均分佈。");
            Add("First cue should start at", "第一句應該開始於"); Add("Last cue should start at", "最後一句應該開始於"); Add("Frame rate", "幀率");
            Add("From", "由"); Add("To", "去"); Add("SubRip (.srt)", "SubRip (.srt)"); Add("WebVTT (.vtt)", "WebVTT (.vtt)"); Add("To SubRip", "轉成 SubRip"); Add("To WebVTT", "轉成 WebVTT");
            Add("Commas in times become dots, a WEBVTT header is added, and basic styling tags are kept.", "時間嘅逗號會變句號，會加 WEBVTT 標頭，基本樣式標籤會保留。");
            Add("Dots in times become commas, the WEBVTT header and cue settings (position, alignment) are dropped, and basic styling tags are kept.", "時間嘅句號會變逗號，WEBVTT 標頭同字幕設定（位置、對齊）會刪走，基本樣式標籤會保留。");
            Add("Both are the same format, so nothing changes.", "兩個格式一樣，所以冇改動。");
            Add("Save as", "另存為"); Add("Fix overlaps", "修正重疊"); Add("Skip broken cue", "略過壞咗嘅字幕");
            Add("The file can still be shifted. Fix or skip the cues below first if you want a clean result.", "個檔案仍然可以平移。想結果乾淨嘅話，先修正或者略過下面啲字幕。");
            Add("Starts before the last one ends", "喺上一句完之前就開始"); Add("Time cannot be read", "睇唔明個時間"); Add("Ends before it starts", "未開始就完咗");
            Add("Moving by zero changes nothing.", "移動 0 秒就冇改動。");
            Add("Type a number of seconds, for example +2.4 or -0.5.", "請輸入秒數，例如 +2.4 或者 -0.5。");
            Add("Type each time as hours:minutes:seconds,milliseconds, for example 00:00:05,100.", "每個時間請寫成 時:分:秒,毫秒，例如 00:00:05,100。");
            Add("Stretch needs two cues that start at different times.", "拉伸要兩句開始時間唔同嘅字幕。");
            Add("The last cue must start after the first one.", "最後一句一定要喺第一句之後開始。");
            Add("The first cue cannot start before zero.", "第一句唔可以喺零之前開始。");
            Add("Every cue is broken. Nothing can be changed.", "每句字幕都壞咗，改唔到。");
            Add("Pick a new name: the original file is never overwritten.", "請改過名：原本個檔案永遠唔會被覆蓋。");
            Add("The original file was not changed.", "原本個檔案冇改動。"); Add("The file could not be saved", "存唔到個檔案");
            Add("The original file was not changed and no partial file was left.", "原本個檔案冇改動，亦冇留低寫到一半嘅檔案。");
            Add("Check the folder is writable, or pick another name.", "檢查資料夾寫得入，或者改個名。");
            Add("Saved as", "儲存為"); Add("Folder", "資料夾"); Add("Cues", "字幕數目"); Add("Format", "格式"); Add("Dropped", "刪走咗");
            Add("saved", "已儲存"); Add("could not save", "存唔到"); Add("problem found", "發現問題");
            Add("This is not a readable subtitle file", "呢個唔係讀得到嘅字幕檔"); Add("Nothing was changed.", "冇改動任何嘢。");
            Add("SRT and WebVTT files open here.", "呢度可以開 SRT 同 WebVTT 檔案。"); Add("Done.", "完成。");
            Add("Nothing to open", "冇嘢開得到"); Add("The file is no longer there.", "個檔案已經唔喺度。");
            Add("Nothing to extract", "冇嘢解壓"); Add("Nothing to preview", "冇嘢預覽");

            // ---- fonts
            Add("Font preview and install", "字體預覽同安裝"); Add("Sample text", "範例文字"); Add("Family", "字體家族"); Add("Glyphs", "字形"); Add("Licence", "授權");
            Add("Version", "版本"); Add("The font does not say", "字體冇寫"); Add("Install for this user only", "只為呢個使用者安裝");
            Add("No administrator rights. Other people on this computer do not get it.", "唔使管理員權限。呢部電腦上其他人唔會有呢個字體。");
            Add("The preview does not install anything.", "預覽唔會安裝任何嘢。"); Add("Install", "安裝"); Add("Uninstall", "解除安裝");
            Add("Installed for this user only.", "只為呢個使用者安裝咗。"); Add("This font is already installed", "呢個字體已經安裝咗");
            Add("The same file is installed for you. Installing again would change nothing.", "同一個檔案已經為你安裝咗。再裝一次都唔會有分別。");
            Add("Uninstall removes the copy in your user fonts folder.", "解除安裝會移除你使用者字體資料夾入面嗰份副本。");
            Add("Apps that are already open may need a restart to list it.", "已經開咗嘅程式可能要重新開過先會見到。");
            Add("Location", "位置"); Add("Your user fonts folder", "你嘅使用者字體資料夾"); Add("Removal", "移除方法");
            Add("Open this tool again and choose Uninstall", "再開呢個工具，然後揀「解除安裝」"); Add("Open fonts settings", "開啟字體設定");
            Add("This is not a usable font file", "呢個唔係用得到嘅字體檔");
            Add("The font tables are damaged, so Windows would refuse it.", "字體表壞咗，所以 Windows 會拒絕佢。");
            Add("WOFF is a web font format. Windows cannot install it, so convert it to TTF or OTF first.", "WOFF 係網頁字體格式，Windows 裝唔到，請先轉成 TTF 或者 OTF。");
            Add("The file is too small to be a font.", "個檔案細到唔可能係字體。");
            Add("Nothing was installed and the file was not changed.", "冇安裝任何嘢，個檔案都冇改動。");
            Add("Fonts open here as TTF, OTF or TTC.", "呢度可以開 TTF、OTF 或者 TTC 字體。");
            Add("The font could not be installed", "字體裝唔到"); Add("Windows refused the install.", "Windows 拒絕咗安裝。");
            Add("Installed for this user only", "只為呢個使用者安裝咗");
            Add("installed", "已安裝"); Add("already installed", "已經安裝咗"); Add("removed", "已移除"); Add("could not install", "裝唔到");
            Add("Removed for this user.", "已經為呢個使用者移除。");
            Add("The preview does not install anything", "預覽唔會安裝任何嘢");
            Add("Preview font…", "預覽字體…"); Add("Subtitle timing…", "字幕時間…"); Add("Disc image…", "光碟映像…"); Add("Extract…", "解壓…");
            Add("Look inside the archive, pick files and extract them", "睇吓壓縮檔入面有啲乜，揀檔案再解壓");
            Add("Shift, stretch or convert the subtitle timing", "平移、拉伸或者轉換字幕時間");
            Add("See the font at several sizes and install it for you only", "用幾個大小睇字體，亦可以只為你自己安裝");
            Add("Browse, mount or extract from the disc image", "瀏覽、掛載或者喺光碟映像解壓檔案");

            // ---- disc image
            Add("Disc image", "光碟映像"); Add("Disc image contents", "光碟映像內容"); Add("Not mounted", "未掛載");
            Add("Mounting adds a read-only drive. The image file is never changed.", "掛載會加一部唯讀磁碟機。映像檔本身永遠唔會改。");
            Add("Mount as a drive", "掛載成磁碟機"); Add("Extract selected", "解壓所揀嘅"); Add("Eject", "退出"); Add("Mounting", "掛載緊");
            Add("Attaching the image as a drive", "將映像接駁成磁碟機"); Add("Mounting the image", "掛載映像");
            Add("Asking Windows to attach the image. This usually takes a second or two.", "請 Windows 接駁映像，通常一兩秒就得。");
            Add("Mounting was canceled. Nothing is attached.", "已取消掛載，冇接駁任何嘢。");
            Add("Read-only. Eject when you are done so the image file can be moved or deleted.", "唯讀。用完要退出，之後映像檔先可以搬或者刪除。");
            Add("Leaving this window open does not unmount it.", "留住呢個視窗開住唔會取消掛載。");
            Add("Windows could not mount this image", "Windows 掛載唔到呢個映像");
            Add("Nothing was changed. You can still browse and extract the contents without mounting, or copy the file to a local disk first.", "冇改動任何嘢。你仍然可以唔掛載就瀏覽同解壓內容，或者先將個檔案複製去本機磁碟。");
            Add(" The file may be on a network share that does not allow mounting, or it may not be a valid disc image.", " 個檔案可能喺唔准掛載嘅網絡共用位置，或者唔係有效嘅光碟映像。");
            Add("Browse without mounting", "唔掛載就瀏覽"); Add("Nothing is attached.", "冇接駁任何嘢。");
            Add("Back to the image", "返去映像");
            Add("A name that already existed got a number instead of being overwritten.", "已經存在嘅名會加數字，唔會被覆蓋。");
            Add("Every selected file was copied out of the image.", "所揀嘅檔案全部由映像複製出嚟。");
            Add("The image file was not changed.", "映像檔冇改動。");
            Add("Ejected. The drive is gone and the image file can be moved or deleted.", "已退出。磁碟機冇咗，映像檔可以搬或者刪除。");
            Add("Pick files to extract, or mount the image as a drive.", "揀要解壓嘅檔案，或者將映像掛載成磁碟機。");
            Add("No file list for this image", "呢個映像冇檔案清單");
            Add("There are no files to show.", "冇檔案可以顯示。"); Add("No file matches the search.", "冇符合搜尋嘅檔案。");
            Add("mounting", "掛載緊"); Add("could not mount", "掛載唔到"); Add("no label", "冇標籤");
            Add("Windows did not say why.", "Windows 冇講原因。"); Add("Windows did not answer in time.", "Windows 喺時限內冇回應。");
            Add("The image attached but has no drive letter.", "映像接駁咗，但係冇磁碟機代號。");
        }

        private static void AddDocSysPatterns(List<(Regex, Func<Match, string>)> p)
        {
            void Add(string rx, string yue) => p.Add((new Regex(rx, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline), m => m.Result(yue)));
            void AddFn(string rx, Func<Match, string> fn) => p.Add((new Regex(rx, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline), fn));
            string Inner(string s) => Language.Yue(s) ?? s;

            // archive
            Add(@"^(\d+) of (\d+) selected · (.+)$", "已揀 $1／$2 · $3");
            Add(@"^(\d+) files? selected · (.+)$", "已揀 $1 個檔案 · $2");
            Add(@"^Extract (\d+) files?$", "解壓 $1 個檔案");
            Add(@"^Extracted (\d+) files?, (.+)$", "已解壓 $1 個檔案，$2");
            Add(@"^(\d+) files? · (.+) unpacked · (.+) zipped$", "$1 個檔案 · 解壓後 $2 · 壓縮後 $3");
            Add(@"^encrypted · (\d+) files?$", "已加密 · $1 個檔案");
            Add(@"^(\d+) of (\d+) rows are drawn\. Search to narrow the list\.$", "已顯示 $1／$2 行。請搜尋嚟縮窄清單。");
            AddFn(@"^(Collapse|Expand) (\S+)$", m => (m.Groups[1].Value == "Collapse" ? "收埋 " : "展開 ") + m.Groups[2].Value);
            Add(@"^Select folder (\S+)$", "揀資料夾 $1");
            Add(@"^Select (?!all$|none$)(\S+)$", "揀 $1");
            Add(@"^A (\S+) archive needs 7-Zip to be read, and 7-Zip is not bundled with this build\. Zip, tar, tar\.gz, tgz and gz archives open here\.$", "$1 壓縮檔要 7-Zip 先讀到，而呢個版本冇附帶 7-Zip。呢度可以開 zip、tar、tar.gz、tgz 同 gz 壓縮檔。");
            Add(@"^This file could not be read as (.+): (.+)$", "呢個檔案讀唔到做 $1：$2");
            Add(@"^Extracted (\d+) files?, (.+)\. Every selected file was written\.$", "已解壓 $1 個檔案，$2。所揀嘅檔案全部寫好。");
            Add(@"^Stopped after (\d+) files?$", "解壓咗 $1 個檔案就停咗");
            Add(@"^(\d+) files? already existed, so Keep both saved (?:it|them) under (?:a )?new names?\.$", "$1 個檔案已經存在，所以「兩個都留」用新名儲存咗。");
            Add(@"^(\d+) files? already existed and (?:was|were) skipped\.$", "$1 個檔案已經存在，已略過。");
            Add(@"^(\d+) files?$", "$1 個檔案");
            Add(@"^(\d+) unsafe paths?(?:: (.+))?$", "$1 個唔安全路徑$2");
            Add(@"^File (\d+) of (\d+)$", "第 $1 個，共 $2 個檔案");
            Add(@"^(\S+) and (\d+) more$", "$1，另外仲有 $2 個");
            // subtitles
            Add(@"^(\d+) cues? change$", "會改 $1 句字幕");
            Add(@"^(\d+) cues? · (.+)$", "$1 句字幕 · $2");
            Add(@"^(\d+) cues are written as (WebVTT|SubRip)\.$", "$1 句字幕會寫成 $2。");
            Add(@"^(\d+) cues? overlaps?$", "$1 句字幕重疊"); Add(@"^(\d+) cues? overlap and (\d+) (?:is|are) broken$", "$1 句字幕重疊，$2 句壞咗");
            Add(@"^1 cue overlaps and 1 is broken$", "1 句字幕重疊，1 句壞咗");
            Add(@"^(\d+) cues? (?:is|are) broken$", "$1 句字幕壞咗");
            Add(@"^(\d+) more problem cues?\.$", "仲有 $1 句有問題嘅字幕。");
            Add(@"^Fixed (\d+) overlaps?\.$", "已修正 $1 處重疊。"); Add(@"^Skipped (\d+) broken cues?\.$", "已略過 $1 句壞咗嘅字幕。");
            Add(@"^Negative moves earlier\. Cue 1 would start at (.+)\.$", "負數即係提早。第 1 句會喺 $1 開始。");
            Add(@"^Cue (\d+) would start before 00:00:00,000\. Move by less\.$", "第 $1 句會喺 00:00:00,000 之前開始。請移少啲。");
            Add(@"^Now (\S+)$", "而家 $1");
            AddFn(@"^Cue (\d+): (.+)$", m => "第 " + m.Groups[1].Value + " 句：" + Inner(m.Groups[2].Value));
            Add(@"^Frame rate (\S+) to (\S+)$", "幀率 $1 轉 $2");
            Add(@"^Preset (\S+ → \S+) fps filled in the two times\.$", "預設 $1 fps 已經填好兩個時間。");
            Add(@"^Speed change: × ([\d.]+) \((.+) fps equivalent\)\. Longest cue stays under (\d+) s\.$", "速度變化：× $1（相當於 $2 fps）。最長嗰句會喺 $3 秒以內。");
            Add(@"^Dropped and listed afterwards: (.+)\.$", "會刪走，之後會列出：$1。");
            Add(@"^The first 200 of (\d+) cues are shown\. Every cue is changed\.$", "只顯示頭 200 句，共 $1 句。每句都會改。");
            Add(@"^Saved (\S+\.(?:srt|vtt))$", "已儲存 $1");
            // fonts
            Add(@"^(\d+) pixel sample$", "$1 像素樣本"); Add(@"^(\d+) pixels$", "$1 像素");
            Add(@"^(.+) is installed for you$", "$1 已經為你安裝咗"); Add(@"^(.+) was removed$", "$1 已經移除");
            Add(@"^It is gone from your user fonts\. Apps that are open may keep showing it until they restart\.$", "已經由你嘅使用者字體移除。開住嘅程式可能要重新開過先會唔再顯示。");
            Add(@"^Could not uninstall: (.+)$", "解除安裝唔到：$1");
            AddFn(@"^([\d,]+) · ((?:Latin|Cyrillic|Greek|Hebrew|Arabic|Thai|Kana|CJK|Hangul)(?:, (?:Latin|Cyrillic|Greek|Hebrew|Arabic|Thai|Kana|CJK|Hangul))*)$", m =>
                m.Groups[1].Value + " · " + string.Join("、", Array.ConvertAll(m.Groups[2].Value.Split(", "), s => s switch { "Latin" => "拉丁", "Cyrillic" => "西里爾", "Greek" => "希臘", "Hebrew" => "希伯來", "Arabic" => "阿拉伯", "Thai" => "泰文", "Kana" => "假名", "Hangul" => "韓文", _ => "中日韓漢字" })));
            // disc image
            Add(@"^Mounted as drive (\S+)$", "已掛載做磁碟機 $1"); Add(@"^Open drive (\S+)$", "開啟磁碟機 $1"); Add(@"^mounted as (\S+)$", "已掛載做 $1");
            AddFn(@"^ISO 9660 · (.+) · (.+)$", m => "ISO 9660 · " + Inner(m.Groups[1].Value) + " · " + m.Groups[2].Value);
            Add(@"^Stopped after (\d+) files?$", "解壓咗 $1 個檔案就停咗");
            Add(@"^The file list could not be read here: (.+) You can still try Mount as a drive\.$", "喺度讀唔到檔案清單：$1 你仍然可以試吓「掛載成磁碟機」。");
            Add(@"^The contents of a (\S+) image cannot be listed without mounting it\. Mount it as a drive to see them\.$", "$1 映像唔掛載就列唔到內容。請掛載成磁碟機先睇到。");
            Add(@"^Could not eject: (.+)$", "退出唔到：$1");
        }
    }
}
