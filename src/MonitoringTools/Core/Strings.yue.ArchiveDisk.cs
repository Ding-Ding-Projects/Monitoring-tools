using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MonitoringTools.Core
{
    /// <summary>Hong Kong Cantonese for the archive window and the virtual disk window.</summary>
    internal static partial class Strings
    {
        private static void AddArchiveDisk(Dictionary<string, string> t)
        {
            void Add(string en, string yue) => t.TryAdd(en, yue);

            // ---- toast buttons
            Add("Archive…", "壓縮…"); Add("Put in a virtual disk…", "放入虛擬磁碟…");
            Add("Make a zip, 7z or tar.gz of this, with every option and a test step. The originals are kept unless you ask otherwise.", "將呢個壓成 zip、7z 或 tar.gz，所有選項齊備，仲有測試步驟。除非你要求，否則會保留原檔。");
            Add("Make a zip, 7z or tar.gz of these, with every option and a test step. The originals are kept unless you ask otherwise.", "將呢啲壓成 zip、7z 或 tar.gz，所有選項齊備，仲有測試步驟。除非你要求，否則會保留原檔。");
            Add("Create a VHD or VHDX, mount it, and copy or move this onto it. Windows asks for permission once.", "建立 VHD 或 VHDX、掛載，再將呢個複製或搬入去。Windows 只會問一次權限。");
            Add("Create a VHD or VHDX, mount it, and copy or move these onto it. Windows asks for permission once.", "建立 VHD 或 VHDX、掛載，再將呢啲複製或搬入去。Windows 只會問一次權限。");
            Add("Nothing to archive", "冇嘢可以壓縮"); Add("Nothing to put in a disk", "冇嘢可以放入磁碟"); Add("The files are no longer there.", "啲檔案已經唔喺度。");

            // ---- shared window parts
            Add("Close", "關閉"); Add("Closes this window (Esc).", "關閉呢個視窗（Esc）。"); Add("Cancel", "取消");
            Add("Esc cancels · Ctrl+Enter creates", "Esc 取消 · Ctrl+Enter 建立");
            Add("Name", "名稱"); Add("Save in", "儲存喺"); Add("Format", "格式"); Add("Size", "大小"); Add("Unit", "單位");
            Add("Try again", "再試一次"); Add("Details", "詳情"); Add("Show or hide the details", "顯示或隱藏詳情");
            Add("Nothing was left behind", "冇留低任何嘢"); Add("Show in folder", "喺資料夾顯示"); Add("Copy path", "複製路徑");
            Add("Starting…", "開始緊…"); Add("Counting the files…", "點算緊檔案…"); Add("The estimate appears when the count is done.", "點算完就會有估算。");
            Add("Fix this to create the archive.", "改好呢個就可以建立壓縮檔。"); Add("Fix this to create the disk.", "改好呢個就可以建立磁碟。");

            // ---- archive window
            Add("Create archive", "建立壓縮檔"); Add("Archive", "壓縮檔");
            Add("The archive's name. The format adds its own ending.", "壓縮檔嘅名稱。格式會自動加返副檔名。");
            Add("Same folder as the first item.", "同第一個項目同一個資料夾。");
            Add("Browse for a folder", "瀏覽資料夾"); Add("Choose the folder the archive is saved in.", "揀壓縮檔儲存嘅資料夾。");
            Add("Choose where the archive is saved", "揀壓縮檔儲存嘅位置");
            Add("Compression level", "壓縮程度"); Add("Store", "只儲存"); Add("Fastest", "最快"); Add("Fast", "快"); Add("Normal", "標準"); Add("Maximum", "最大"); Add("Ultra", "極致");
            Add("Method", "壓縮方法"); Add("Dictionary size", "字典大小"); Add("Solid block", "固實區塊"); Add("Automatic", "自動"); Add("Off", "關"); Add("One block", "一個區塊");
            Add("CPU threads", "CPU 執行緒");
            Add("Dictionary, solid block and threads are 7z options with LZMA or LZMA2. They are hidden for Zip.", "字典、固實區塊同執行緒係 7z 配 LZMA 或 LZMA2 先有嘅選項，Zip 會隱藏。");
            Add("Only LZMA and LZMA2 use a dictionary size.", "淨係 LZMA 同 LZMA2 先用字典大小。"); Add("Only LZMA and LZMA2 build solid blocks.", "淨係 LZMA 同 LZMA2 先會砌固實區塊。");
            Add("Protection", "保護"); Add("Password", "密碼"); Add("Show the password", "顯示密碼"); Add("Shows what you typed.", "顯示你打咗啲乜。"); Add("Password (shown)", "密碼（已顯示）");
            Add("AES-256. Leave empty for no password. The password is handed to 7-Zip on its command line for the moments it runs, and is never saved.", "AES-256。留空就唔設密碼。密碼只會喺 7-Zip 行嗰陣用命令列交畀佢，絕對唔會儲存。");
            Add("Password strength", "密碼強度"); Add("Weak", "弱"); Add("Fair", "普通"); Add("Good", "好"); Add("Strong", "強");
            Add("Encrypt file names", "加密檔案名稱");
            Add("Hides names and sizes too. 7z only.", "連名稱同大小都會隱藏。只限 7z。");
            Add("Hides names and sizes too. 7z only: Zip cannot hide its file names.", "連名稱同大小都會隱藏。只限 7z：Zip 隱藏唔到檔案名稱。");
            Add("Hides names and sizes too. Type a password first.", "連名稱同大小都會隱藏。請先打密碼。");
            Add("Split", "分割"); Add("Split into volumes", "分割成多個分卷");
            Add("Volume size", "分卷大小"); Add("Volume unit", "分卷單位");
            Add("A CD.", "一張光碟。"); Add("FAT32 files stop at 4 GiB minus one byte, so each volume is 4,095 MB.", "FAT32 檔案上限係 4 GiB 減一個位元組，所以每個分卷係 4,095 MB。"); Add("A single-layer DVD.", "單層 DVD。");
            Add("Volume size 700 MB", "分卷大小 700 MB"); Add("Volume size FAT32 4 GB", "分卷大小 FAT32 4 GB"); Add("Volume size DVD 4.4 GB", "分卷大小 DVD 4.4 GB");
            Add("FAT32 4 GB", "FAT32 4 GB"); 
            Add("Contents", "內容"); Add("Include the parent folder", "包括上層資料夾");
            Add("Keeps the folder the items are in as the top folder inside.", "將項目所在嘅資料夾保留做壓縮檔入面最上層嘅資料夾。");
            Add("Exclude patterns", "排除規則"); Add("add pattern…", "加入規則…"); Add("Add an exclude pattern", "加入排除規則");
            Add("Type a pattern and press Enter. Glob: node_modules, *.tmp, build/**. Regex: tested against the relative path.", "打一個規則再撳 Enter。Glob：node_modules、*.tmp、build/**。Regex：會對住相對路徑測試。");
            Add("Glob", "萬用字元"); Add("Regex", "正則"); Add("Exclude pattern type", "排除規則類型"); Add("Builder", "砌表達式");
            Add("Open the regex builder for the exclude patterns", "開啟排除規則嘅正則表達式砌法");
            Add("Opens the app's full regex builder. Press Enter in its box to add the pattern.", "開啟程式完整嘅正則表達式砌法。喺入面撳 Enter 就加入規則。");
            Add("Exclude regex", "排除正則"); Add("Pattern not added", "規則未加入");
            Add("Matched against each file's path from the top folder, with / between names. Press Enter to add the pattern as a chip.", "會對住每個檔案由最上層資料夾起計嘅路徑測試，名稱之間用 /。撳 Enter 就將規則加做標籤。");
            Add("Removes this pattern.", "移除呢個規則。");
            Add("Test the archive after creating", "建立後測試壓縮檔"); Add("Reads it back and checks every file.", "讀返出嚟逐個檔案檢查。");
            Add("On while the originals are deleted: they go only after the test passes.", "刪除原檔期間會保持開啟：測試通過先會刪。");
            Add("Delete the originals", "刪除原檔"); Add("Recycle Bin, after the test passes. Needs the two-key confirmation.", "測試通過後送去資源回收筒。要用雙鑰匙確認。");
            Add("Creates the archive. Nothing already there is overwritten.", "建立壓縮檔。已有嘅檔案唔會被覆蓋。");
            Add("Needs 7-Zip, which this build does not carry.", "需要 7-Zip，不過呢個版本冇附帶。");
            Add("Creating archive", "建立緊壓縮檔"); Add("Archive progress", "壓縮進度"); Add("Cancel and remove the partial archive", "取消並移除未完成嘅壓縮檔");
            Add("Stops and removes the partly written archive (Esc).", "停止並移除寫到一半嘅壓縮檔（Esc）。");
            Add("Scan", "掃描"); Add("Compress", "壓縮"); Add("Test", "測試"); Add("Finish", "完成");
            Add("Archive ready", "壓縮檔搞掂"); Add("Originals", "原檔"); Add("Saved in", "儲存喺"); Add("Kept", "保留"); Add("Not tested", "未測試"); Add("No errors", "冇錯誤"); Add("Failed", "失敗");
            Add("Moving to the Recycle Bin…", "送緊去資源回收筒…"); Add("Deleted: they are in the Recycle Bin", "已刪除：已喺資源回收筒");
            Add("Kept: the deletion was not confirmed", "已保留：冇確認刪除");
            Add("Delete originals", "刪除原檔"); Add("Archive not created", "未建立到壓縮檔");
            Add("Nothing was left behind: the partly written archive was removed, and your files were not touched.", "冇留低任何嘢：寫到一半嘅壓縮檔已移除，你嘅檔案亦冇被動過。");
            Add("Your settings are still here.", "你嘅設定仲喺度。"); Add("Change what is needed and create the archive again.", "改你要改嘅，再建立一次壓縮檔。");
            Add("Cancelled. The partly written archive was removed.", "已取消。寫到一半嘅壓縮檔已移除。"); Add("Nothing was left behind. Your settings are still here.", "冇留低任何嘢。你嘅設定仲喺度。");
            Add("Copied", "已複製");
            Add("There is nothing to archive.", "冇嘢可以壓縮。"); Add("Give the archive a name.", "請幫壓縮檔改個名。");
            Add("The name cannot contain \\ / : * ? \" < > or |.", "名稱唔可以包含 \\ / : * ? \" < > 或 |。");
            Add("Choose a folder to save in.", "請揀儲存嘅資料夾。");
            Add("This needs 7-Zip, which this build does not carry. Zip and tar.gz work without it.", "呢個需要 7-Zip，不過呢個版本冇附帶。Zip 同 tar.gz 唔使佢都得。");
            Add("tar.gz cannot be encrypted or split.", "tar.gz 唔可以加密或者分割。");
            Add("Encrypting file names needs the 7z format.", "加密檔案名稱要用 7z 格式。"); Add("Encrypting file names needs a password.", "加密檔案名稱要有密碼。");
            Add("Volumes must be at least 64 KB.", "分卷最少要 64 KB。"); Add("Every item is excluded by the patterns.", "所有項目都被排除規則排除咗。");
            Add("Cancelled. The disk was detached and its file deleted.", "已取消。磁碟已卸載，檔案已刪除。");
            Add("An estimate from the file types.", "按檔案類型估算。");
            Add("Glob, Regex", "萬用字元, 正則"); Add("Copy, Move, Leave empty", "複製, 搬移, 留空"); Add("Drive letter, Folder", "磁碟機代號, 資料夾");
            Add("Dynamically expanding, Fixed size", "動態擴展, 固定大小"); Add("Keep mounted, Detach", "保持掛載, 卸載");
            Add("NTFS, exFAT, FAT32", "NTFS, exFAT, FAT32 檔案系統");
            Add("NTFS, exFAT, FAT32 (unavailable: Off above 32 GB: Windows formats FAT32 only up to 32 GB here.)", "NTFS, exFAT, FAT32（用唔到：超過 32 GB 就用唔到：呢度 Windows 淨係可以格式化最大 32 GB 嘅 FAT32。）");
            Add("DVD 4.4 GB", "DVD 光碟 4.4 GB");

            // ---- virtual disk window
            Add("Create virtual disk", "建立虛擬磁碟"); Add("Disk file", "磁碟檔案"); Add("Volume", "磁碟區");
            Add("Administrator rights are needed. Creating and mounting a virtual disk makes Windows ask for permission (consent prompt) when you press Create. The app never answers it.", "需要系統管理員權限。你撳「建立」嗰陣，Windows 會問你要唔要批准建立同掛載虛擬磁碟（同意提示）。程式絕對唔會代你答。");
            Add("Browse for the folder the disk file is saved in", "瀏覽磁碟檔案儲存嘅資料夾"); Add("Choose the folder for the disk file.", "揀磁碟檔案嘅資料夾。");
            Add("VHDX: up to 64 TB, safer after a power cut. VHD: older tools, up to 2,040 GB.", "VHDX：最大 64 TB，斷電後更安全。VHD：配舊工具，最大 2,040 GB。");
            Add("VHD: older tools, up to 2,040 GB. VHDX goes up to 64 TB and is safer after a power cut.", "VHD：配舊工具，最大 2,040 GB。VHDX 最大 64 TB，斷電後更安全。");
            Add("Typed number, 1 to 65,536.", "輸入數字，1 至 65,536。"); Add("Size unit", "大小單位");
            Add("Type", "類型"); Add("Dynamically expanding", "動態擴展"); Add("Fixed size", "固定大小");
            Add("Copy", "複製"); Add("Move", "搬移"); Add("Leave empty", "留空"); Add("Contents", "內容");
            Add("Move deletes the originals only after every file copied and matched its size, after the two-key confirmation.", "搬移只會喺每個檔案都複製好而且大小一致、再經雙鑰匙確認之後先刪原檔。");
            Add("Move deletes the originals only after every file copied and matched its size, after the two-key confirmation. They go to the Recycle Bin.", "搬移只會喺每個檔案都複製好而且大小一致、再經雙鑰匙確認之後先刪原檔。原檔會去資源回收筒。");
            Add("The disk starts empty. Nothing is copied or moved.", "磁碟一開始係空嘅，唔會複製或搬移任何嘢。"); Add("Copy leaves the originals where they are.", "複製會將原檔留喺原處。");
            Add("File system", "檔案系統");
            Add("FAT32 is off above 32 GB: Windows formats FAT32 only up to 32 GB here, and no file on it may pass 4 GB. exFAT has no permissions. NTFS keeps permissions and compression.", "超過 32 GB 就用唔到 FAT32：呢度 Windows 淨係可以格式化最大 32 GB 嘅 FAT32，而且入面冇檔案可以超過 4 GB。exFAT 冇權限設定。NTFS 保留權限同壓縮。");
            Add("Volume label", "磁碟區標籤"); Add("NTFS: up to 32 characters. FAT32 and exFAT: up to 11.", "NTFS：最多 32 個字元。FAT32 同 exFAT：最多 11 個。");
            Add("Mount as", "掛載為"); Add("Drive letter", "磁碟機代號"); Add("Folder", "資料夾"); Add("Letter", "代號");
            Add("Next free letter. Folder mode needs an empty NTFS folder, such as D:\\Mounts\\Work.", "下一個空閒代號。資料夾模式要一個空嘅 NTFS 資料夾，例如 D:\\Mounts\\Work。");
            Add("Empty NTFS folder to mount into", "要掛載入去嘅空 NTFS 資料夾"); Add("Browse for the mount folder", "瀏覽掛載資料夾"); Add("Choose an empty folder on an NTFS drive.", "揀 NTFS 磁碟入面一個空資料夾。");
            Add("The folder must exist, be empty, and be on an NTFS drive.", "資料夾要存在、要係空嘅，而且要喺 NTFS 磁碟上面。");
            Add("After", "之後"); Add("When it is done", "完成後"); Add("Keep mounted", "保持掛載"); Add("Detach", "卸載"); Add("Drive letter", "磁碟機代號");
            Add("Create disk", "建立磁碟"); Add("Windows asks for permission when you press this. The app never answers that prompt.", "你撳呢個嗰陣 Windows 會問權限。程式絕對唔會代你答嗰個提示。");
            Add("Asks Windows for permission, then creates the disk.", "會先問 Windows 要權限，再建立磁碟。");
            Add("Off above 32 GB: Windows formats FAT32 only up to 32 GB here.", "超過 32 GB 就用唔到：呢度 Windows 淨係可以格式化最大 32 GB 嘅 FAT32。");
            Add("A file in your selection is over 4 GB, which FAT32 cannot hold.", "你揀嘅檔案有一個超過 4 GB，FAT32 裝唔落。");
            Add("The disk starts empty.", "磁碟一開始係空嘅。");
            Add("Give the disk a name.", "請幫磁碟改個名。"); Add("The folder to save in does not exist.", "要儲存嘅資料夾唔存在。");
            Add("Copy progress", "複製進度"); Add("Cancel, detach and delete what was made", "取消、卸載並刪除已建立嘅嘢");
            Add("Stops, detaches the disk and deletes the file that was made (Esc).", "停止、卸載磁碟，並刪除已建立嘅檔案（Esc）。");
            Add("Permission given", "已批准權限"); Add("Created the file and attached it", "已建立檔案並掛上"); Add("Finish", "完成");
            Add("Disk not created", "未建立到磁碟"); Add("Disk ready", "磁碟搞掂");
            Add("File", "檔案"); Add("Copied", "已複製"); Add("State", "狀態"); Add("Mounted", "已掛載"); Add("Detached", "已卸載");
            Add("Detach the disk", "卸載磁碟"); Add("Detaches the disk. The file stays; double-click it to mount it again.", "卸載磁碟。檔案會保留；連按兩下就可以再掛載。");
            Add("Show file", "顯示檔案"); Add("Not detached", "未卸載");
            Add("To use it again, double-click the file in Explorer: Windows asks for permission and mounts it.", "想再用，就喺檔案總管連按兩下個檔案：Windows 會問權限再掛載。");
            Add("The Windows consent prompt was declined or timed out. No file was created and nothing was attached.", "Windows 同意提示被拒絕或者逾時。冇建立任何檔案，亦冇掛上任何嘢。");
            Add("Cancelling… the disk is detached and deleted as soon as it is ready.", "取消緊…磁碟一準備好就會卸載並刪除。");
            Add("The helper could not start: ", "助手開唔到：");
            Add("The drive has less than 128 MB free, which a new disk needs to start.", "磁碟機剩返唔夠 128 MB，新磁碟起碼要呢個空間先開到頭。");
            Add("The disk file needs a full path.", "磁碟檔案要有完整路徑。"); Add("A virtual disk file must be on a local drive, not on a network share.", "虛擬磁碟檔案一定要喺本機磁碟，唔可以喺網絡共用。");
            Add("The disk file path contains characters that cannot be used.", "磁碟檔案路徑有唔可以用嘅字元。"); Add("The disk file must end in .vhd or .vhdx.", "磁碟檔案一定要以 .vhd 或 .vhdx 結尾。");
            Add("The file system must be NTFS, exFAT or FAT32.", "檔案系統一定要係 NTFS、exFAT 或 FAT32。"); Add("VHDX goes up to 64 TB.", "VHDX 最大 64 TB。");
            Add("VHD goes up to 2,040 GB. Use VHDX for a bigger disk.", "VHD 最大 2,040 GB。要更大就用 VHDX。");
            Add("Windows formats FAT32 only up to 32 GB here. Choose NTFS or exFAT, or a smaller disk.", "呢度 Windows 淨係格式化最大 32 GB 嘅 FAT32。請揀 NTFS 或 exFAT，或者用細啲嘅磁碟。");
            Add("FAT32 needs a disk of at least 64 MB.", "FAT32 要至少 64 MB 嘅磁碟。");
            Add("A file in your selection is larger than 4 GB, which FAT32 cannot hold. Choose NTFS or exFAT.", "你揀嘅檔案有一個大過 4 GB，FAT32 裝唔落。請揀 NTFS 或 exFAT。");
            Add("The volume label can use letters, numbers, spaces and - _ . ( ) only.", "磁碟區標籤只可以用字母、數字、空格同 - _ . ( )。");
            Add("Choose a drive letter.", "請揀磁碟機代號。"); Add("Drive letters before D are kept for the system.", "D 之前嘅代號留畀系統。");
            Add("Choose an empty folder on an NTFS drive to mount into.", "請揀 NTFS 磁碟入面一個空資料夾嚟掛載。"); Add("The mount folder path cannot be used.", "掛載資料夾路徑用唔到。");
            Add("Mount into a folder, not the top of a drive.", "請掛載入資料夾，唔好掛載到磁碟最頂層。"); Add("The mount folder does not exist.", "掛載資料夾唔存在。"); Add("The mount folder is not empty.", "掛載資料夾唔係空嘅。");
            Add("A volume can be mounted only into a folder on an NTFS drive.", "磁碟區只可以掛載入 NTFS 磁碟上面嘅資料夾。");
            Add("Choose how to mount: a drive letter or a folder.", "請揀點樣掛載：磁碟機代號定資料夾。");
        }

        private static void AddArchiveDiskPatterns(List<(Regex, Func<Match, string>)> p)
        {
            void Add(string rx, string yue) => p.Add((new Regex(rx, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline), m => m.Result(yue)));
            Add(@"^(\d+) items? · counting…$", "$1 個項目 · 點算緊…");
            Add(@"^(\d+) items? from (.+) · (.+)$", "$1 個項目，來自 $2 · $3");
            Add(@"^(\d+) items? from (.+) · (.+) · only the first 250,000 files counted$", "$1 個項目，來自 $2 · $3 · 只點算咗頭 250,000 個檔案");
            Add(@"^Typed number, 1 to 16\. Default: (\d+), one for each core\.$", "輸入數字，1 至 16。預設：$1，每個核心一個。");
            Add(@"^Names end in (.+)\.001, (.+)\.002 and so on\.$", "名稱以 $1.001、$2.002 等等作結尾。");
            Add(@"^(\d+) files?, (.+) → about (.+?) · (.+)$", "$1 個檔案，$2 → 大約 $3 · $4");
            Add(@"^An estimate from the file types\. Needs about (.+) free on (.+)\. About (.+?)\.(.*)$", "按檔案類型估算。大約要 $1 空間（$2）。大約 $3。$4");
            Add(@"^(\d+) excluded\.?$", "$1 個已排除");
            Add(@"^The exclude pattern ""(.+)"" is not valid: (.+)$", "排除規則「$1」唔有效：$2");
            Add(@"^There is not enough free space: about (.+) is needed and (.+) is free on (.+)\.$", "空間唔夠：大約要 $1，而 $3 得返 $2。");
            Add(@"^(.+) and (\d+) more volumes?$", "$1 同另外 $2 個分卷");
            Add(@"^Delete the (original|\d+ originals)\?$", "刪除呢 $1？");
            Add(@"^(.+) moves? to the Recycle Bin\. The archive stays\. Only after the archive tested with no errors.$", "$1 會移去資源回收筒。壓縮檔會保留。要壓縮檔測試冇錯誤先會做。");
            Add(@"^(\d+) deleted, (\d+) could not be: (.+)$", "已刪除 $1 個，$2 個刪唔到：$3");
            Add(@"^\+(\d+) more$", "+$1 個更多");
            Add(@"^7-Zip tested every file: everything is Ok( \(7-Zip\))?$", "7-Zip 已測試每個檔案：一切正常$1");
            Add(@"^(\d+) files? read back and matched \(.+\)$", "已讀返 $1 個檔案並確認一致");
            Add(@"^Copy or move (\d+) selected items?( · .+)?$", "複製或搬移已揀嘅 $1 個項目$2");
            Add(@"^Put (the selected item|the \d+ selected items) in$", "將$1放入");
            Add(@"^Fixed takes the full (.+) on (.+) right away\.$", "固定大小會即刻佔用 $2 嘅全部 $1。");
            Add(@"^Starts small and grows up to (.+) as you add files\. Fixed takes the full size on (.+) right away\.$", "一開始好細，隨住加檔案最多脹到 $1。固定大小會即刻佔用 $2 嘅全部空間。");
            Add(@"^([A-Z]:) has (\d[\d.,]* (?:B|KB|MB|GB|TB)) free\.$", "$1 剩返 $2 空間。");
            Add(@"^NTFS: up to 32 characters\. FAT32 and exFAT: up to 11\. (.+) allows (\d+)\.$", "NTFS：最多 32 個字元。FAT32 同 exFAT：最多 11 個。$1 容許 $2 個。");
            Add(@"^A file called (.+) is already there\. Choose another name\.$", "已經有個叫 $1 嘅檔案。請揀另一個名。");
            Add(@"^The label can have (\d+) characters for (.+)\.$", "$2 嘅標籤最多 $1 個字元。");
            Add(@"^The volume label can have (\d+) characters for (.+)\.$", "$2 嘅磁碟區標籤最多 $1 個字元。");
            Add(@"^([A-Z]:) is already in use\.$", "$1 已經有人用。");
            Add(@"^Give the disk a name without .+ or \|\.$", "請幫磁碟改個名，唔好有 \\ / : * ? \" < > 或 |。");
            Add(@"^The disk must be at least (\d+) MB\.$", "磁碟最少要 $1 MB。");
            Add(@"^A fixed disk takes the full size at once, and the drive has (.+) free\.$", "固定磁碟會即刻佔用全部大小，而磁碟機得返 $1。");
            Add(@"^The selected items \((.+)\) do not fit in a (.+) disk once the file system takes its share\. Make the disk bigger or leave it empty\.$", "已揀嘅項目（$1）喺扣咗檔案系統嘅開銷之後放唔落 $2 嘅磁碟。請加大磁碟或者留空。");
            Add(@"^Creating (.+\.vhdx?)$", "建立緊 $1");
            Add(@"^Formatted (NTFS|exFAT|FAT32) as (.+)$", "已將 $2 格式化為 $1");
            Add(@"^Copying (\d+) items?$", "複製緊 $1 個項目");
            Add(@"^Originals$", "原檔");
            Add(@"^Size on ([A-Z]:)$", "$1 上嘅大小");
            Add(@"^(.+), grows as needed$", "$1，按需要脹大");
            Add(@"^(\d+) items?, (\d[\d.,]* (?:B|KB|MB|GB|TB))$", "$1 個項目，$2");
            Add(@"^ · originals are in the Recycle Bin$", " · 原檔喺資源回收筒");
            Add(@"^ · originals kept$", " · 原檔已保留");
            Add(@"^Open (V:|[A-Z]:|the folder)$", "開啟 $1");
            Add(@"^Copying stopped: (.+) The disk was detached and deleted\.$", "複製停咗：$1 磁碟已卸載並刪除。");
            Add(@"^Every file copied and matched its size\. (.+) moves? to the Recycle Bin\. The copy on the disk stays\.$", "每個檔案都複製好而且大小一致。$1 會移去資源回收筒。磁碟上嘅副本會保留。");
            Add(@"^(\d[\d.,]* (?:B|KB|MB|GB|TB)) of (\d[\d.,]* (?:B|KB|MB|GB|TB)) · (.+)/s$", "$1／$2 · $3/s");
            Add(@"^(\d[\d.,]* (?:B|KB|MB|GB|TB)) of (\d[\d.,]* (?:B|KB|MB|GB|TB))$", "$1／$2");
            Add(@"^(\d[\d.,]* (?:B|KB|MB|GB|TB)) from (\d[\d.,]* (?:B|KB|MB|GB|TB)) \((\d+)%\)$", "$1，原本 $2（$3%）");
            Add(@"^about (.+) left$", "仲有大約 $1");
            Add(@"^(.+)/s · about (.+) left$", "$1/s · 仲有大約 $2");
            Add(@"^Exclude pattern (.+), press to remove$", "排除規則 $1，撳一下就移除");
            Add(@"^(VHDX|VHD) · (fixed size|dynamically expanding up to) (.+?) · (NTFS|exFAT|FAT32)(.*) · mounts as (.+) · (.+)$", "$1 · $2 $3 · $4$5 · 掛載為 $6 · $7");
            Add(@"^The file takes (.+) on (.+) at once\. GPT partition, quick format\.$", "檔案會即刻佔用 $2 上嘅 $1。GPT 分割區，快速格式化。");
            Add(@"^The file starts at about 50 MB on (.+)\. GPT partition, quick format\.$", "檔案一開始大約 50 MB（$1 上）。GPT 分割區，快速格式化。");
            Add(@"^Windows Explorer cannot open an AES-256 zip, but 7-Zip can$", "Windows 檔案總管開唔到 AES-256 zip，不過 7-Zip 開到");
            Add(@"^exit code (\d+) · cancelled by the user$", "結束碼 $1 · 用戶取消");
            Add(@"^exit code (-?\d+)$", "結束碼 $1");
            Add(@"^The test failed: (.+) reads back different from the original$", "測試失敗：$1 讀返出嚟同原檔唔一樣");
            Add(@"^(.+) reads back different from the original$", "$1 讀返出嚟同原檔唔一樣");
            Add(@"^(\d+) of (\d+) files are in the archive$", "壓縮檔入面得 $1 個檔案，應該有 $2 個");
            string[] states = { "done", "in progress", "waiting" }; string[] stateYue = { "完成", "進行緊", "等緊" };
            foreach (var (en, yue) in new[] { ("Scan", "掃描"), ("Compress", "壓縮"), ("Test", "測試"), ("Finish", "完成"), ("Permission given", "已批准權限"), ("Created the file and attached it", "已建立檔案並掛上") })
                for (int i = 0; i < 3; i++) Add("^" + Regex.Escape(en) + ", " + states[i] + "$", yue + "，" + stateYue[i]);
            for (int i = 0; i < 3; i++)
            {
                Add(@"^Formatted (NTFS|exFAT|FAT32) as (.+), " + states[i] + "$", "已將 $2 格式化為 $1，" + stateYue[i]);
                Add(@"^Copying (\d+) items?, " + states[i] + "$", "複製緊 $1 個項目，" + stateYue[i]);
            }
            Add(@"^Starts small and grows .*$", "一開始好細，之後會脹大。");
        }
    }
}
