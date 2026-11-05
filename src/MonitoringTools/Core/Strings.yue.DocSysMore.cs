using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MonitoringTools.Core
{
    /// <summary>
    /// Hong Kong Cantonese for the 7-Zip archive formats, the Ask rule for names that exist, font collections, and the tray rows that
    /// eject a disc image. File names, drive letters, font names and format names stay as they are.
    /// </summary>
    internal static partial class Strings
    {
        private static void AddDocSysMore(Dictionary<string, string> t)
        {
            void Add(string en, string yue) => t.TryAdd(en, yue);

            // ---- archive: formats through 7-Zip
            Add("Zip, 7z, tar, gz, xz and bz2 archives open here.", "呢度可以開 zip、7z、tar、gz、xz 同 bz2 壓縮檔。");
            Add("Zip, 7z, RAR, tar, gz, xz and bz2 archives open here.", "呢度可以開 zip、7z、RAR、tar、gz、xz 同 bz2 壓縮檔。");
            Add("A RAR archive is read through the full 7-Zip (7z.exe with 7z.dll), which is missing from this copy of the app. Zip, 7z, tar, gz, xz and bz2 archives open here.",
                "RAR 壓縮檔要經完整版 7-Zip（7z.exe 連 7z.dll）讀，但係呢個程式入面搵唔到。呢度可以開 zip、7z、tar、gz、xz 同 bz2 壓縮檔。");
            Add("7-Zip does not recognise it as an archive; it may be damaged or of another type.", "7-Zip 認唔出佢係壓縮檔，可能已經損壞，或者係另一種檔案。");
            Add("7-Zip could not read it.", "7-Zip 讀唔到佢。");
            Add("The full 7-Zip, which reads RAR, is missing from this copy of the app.", "呢個程式入面搵唔到讀 RAR 用嘅完整版 7-Zip。");
            Add("This archive type cannot be read here. Zip, 7z, tar, gz, xz and bz2 archives open here.", "呢度讀唔到呢種壓縮檔。呢度可以開 zip、7z、tar、gz、xz 同 bz2 壓縮檔。");
            Add("This zip uses AES encryption, which is read through 7-Zip, and 7-Zip is missing from this copy of the app.", "呢個 zip 用 AES 加密，要經 7-Zip 讀，但係呢個程式入面搵唔到 7-Zip。");
            Add("encrypted · names hidden", "已加密 · 檔名已隱藏");
            Add("Even the file names are locked, so the list appears after the password. The password is used once and not stored.", "連檔名都鎖埋，所以要輸入密碼之後先見到清單。密碼只會用一次，唔會儲起。");
            Add("unpacking", "解壓緊");
            Add("Every locked file in this archive has an unsafe path, so nothing would be extracted.", "呢個壓縮檔入面每個鎖咗嘅檔案路徑都唔安全，所以乜都唔會解壓。");
            Add("7-Zip could not find the first locked file to check the password.", "7-Zip 搵唔到第一個鎖咗嘅檔案嚟核對密碼。");
            Add("7-Zip is missing from this copy of the app.", "呢個程式入面搵唔到 7-Zip。"); Add("7-Zip did not start.", "7-Zip 開唔到。");
            Add("7-Zip could not read it.", "7-Zip 讀唔到。");

            // ---- archive: Ask
            Add("Ask", "問我"); Add("You choose for each file that already exists.", "每個已經存在嘅檔案都由你揀。");
            Add("waiting for you", "等緊你"); Add("In the folder", "資料夾入面"); Add("In the archive", "壓縮檔入面"); Add("Path", "路徑");
            Add("date not stored", "冇儲日期");
            Add("Do the same for every other file that already exists", "其他已經存在嘅檔案都照樣做");
            Add("Cancel the extraction", "取消解壓"); Add("Skip this file", "略過呢個檔案"); Add("Keep both files", "兩個檔案都留"); Add("Replace the file in the folder", "取代資料夾入面嘅檔案");
            Add("Choose what happens to this file. The extraction waits for your answer.", "揀吓呢個檔案點處理。解壓會等你答咗先繼續。");
            Add("Keep both saves the new file under a numbered name.", "「兩個都留」會用加咗數字嘅名儲新檔案。");

            // ---- fonts: collections
            Add("Face", "字款"); Add("Faces", "字款"); Add("1 face", "1 個字款");
            Add("Install adds every face in this collection, because Windows installs the file as one.", "安裝會加入呢個字體集入面每個字款，因為 Windows 會將成個檔案一齊安裝。");

            // ---- tray: images this app mounted
            Add("The drive is gone and the image file can be moved or deleted.", "磁碟機冇咗，映像檔可以搬或者刪除。");
        }

        private static void AddDocSysMorePatterns(List<(Regex, Func<Match, string>)> p)
        {
            void Add(string rx, string yue) => p.Add((new Regex(rx, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline), m => m.Result(yue)));

            // archive
            Add(@"^A (\S+) archive is read through 7-Zip, and 7-Zip is missing from this copy of the app\.$", "$1 壓縮檔要經 7-Zip 讀，但係呢個程式入面搵唔到 7-Zip。");
            Add(@"^(.+) already exists in the folder\.$", "資料夾入面已經有 $1。");
            Add(@"^(?!A |The |This )(.+) already exists$", "$1 已經存在");
            Add(@"^Extracted (\d+) files?, with a problem$", "已解壓 $1 個檔案，但有問題");
            Add(@"^7-Zip did not write (\d+) files?: (.+)\.$", "7-Zip 冇寫到 $1 個檔案：$2。");
            Add(@"^7-Zip stopped: (.+)$", "7-Zip 停咗：$1");
            // fonts
            Add(@"^(\d+) faces: (.+)$", "$1 個字款：$2");
            Add(@"^1 face: (.+)$", "1 個字款：$1");
            Add(@"^(.+) · (\d+) faces · (.+)$", "$1 · $2 個字款 · $3");
            Add(@"^Preview the face (.+)$", "預覽字款 $1");
            Add(@"^All (\d+) faces are installed for you$", "$1 個字款全部已經為你安裝咗");
            // tray
            Add(@"^Eject (.+)$", "退出 $1");
            Add(@"^Ejected (.+)$", "已退出 $1");
            Add(@"^Could not eject (?!:)(.+)$", "退出唔到 $1");
        }
    }
}
