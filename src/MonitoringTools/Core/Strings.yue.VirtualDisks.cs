using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MonitoringTools.Core
{
    /// <summary>
    /// Hong Kong Cantonese for the Virtual disks page: the disk library, the details of one disk, each action and its
    /// messages, New virtual disk, Quick pack, and the checks and errors of the disk engine behind them.
    /// </summary>
    internal static partial class Strings
    {
        private static void AddVirtualDisks(Dictionary<string, string> t)
        {
            void Add(string en, string yue) => t.TryAdd(en, yue);

            // the page and what this computer offers
            Add("Virtual disks", "虛擬磁碟");
            Add("VHD, VHDX and VHD Set files, and ISO images. Every action says which way it ran and can copy the same steps as a script. Nothing here needs Hyper-V.",
                "VHD、VHDX 同 VHD 集合檔案，仲有 ISO 映像。每個動作都會講明用咗邊條路做，亦可以將同樣步驟複製成指令碼。呢度乜都唔使 Hyper-V。");
            Add("This needs Windows", "呢度要用 Windows");
            Add("Virtual disks are managed through the Windows virtual disk service, which Windows 8 and later have on every edition. The feature article in Help and docs explains everything this page does.",
                "虛擬磁碟經 Windows 虛擬磁碟服務管理，Windows 8 或之後嘅每個版本都有。「說明同文件」入面嘅功能文章會解釋呢頁做到嘅所有嘢。");
            Add("Ways this computer can work with virtual disks", "呢部電腦處理虛擬磁碟嘅方法");
            Add("Windows virtual disk service", "Windows 虛擬磁碟服務");
            Add("Windows virtual disk service, then the Storage commands", "Windows 虛擬磁碟服務，再用 Storage 指令");
            Add("Checking for the Storage commands, DiskPart and Hyper-V…", "檢查緊 Storage 指令、DiskPart 同 Hyper-V…");
            Add("Storage commands", "Storage 指令");
            Add("Storage commands: not found. Volumes are prepared with DiskPart instead.", "Storage 指令：搵唔到。會改用 DiskPart 準備磁碟區。");
            Add("DiskPart", "DiskPart 磁碟工具");
            Add("DiskPart: not found in the Windows folder.", "DiskPart：Windows 資料夾入面搵唔到。");
            Add("Hyper-V commands", "Hyper-V 指令");
            Add("not part of Windows Home. Nothing here needs them.", "Windows 家用版冇呢啲指令。呢度乜都唔使用佢哋。");
            Add("turned off. Nothing here needs them; an administrator can turn on Hyper-V in Windows Features and restart.", "已關閉。呢度乜都唔使用佢哋；系統管理員可以喺「Windows 功能」開啟 Hyper-V，再重新啟動。");
            Add("could not be checked. Nothing here needs them.", "檢查唔到。呢度乜都唔使用佢哋。");

            // the disk library
            Add("Disk library", "磁碟庫");
            Add("No virtual disks yet. Add files, scan a folder, drop a .vhd, .vhdx, .vhds, .avhdx or .iso file on this window, or create one below.",
                "暫時未有虛擬磁碟。可以加入檔案、掃描資料夾、將 .vhd、.vhdx、.vhds、.avhdx 或 .iso 檔案拖落呢個視窗，或者喺下面建立一個。");
            Add("Every virtual disk attached on this computer and every file you added. Choose one to see everything about it.",
                "呢部電腦掛載咗嘅每個虛擬磁碟，同你加入嘅每個檔案。揀一個就睇到佢嘅所有資料。");
            Add("Add files…", "加入檔案…");
            Add("Add virtual disk files", "加入虛擬磁碟檔案");
            Add("Scan a folder…", "掃描資料夾…");
            Add("Scan a folder for virtual disks (three levels deep, at most 500 files)", "喺資料夾搵虛擬磁碟（向下三層，最多 500 個檔案）");
            Add("Search virtual disks", "搜尋虛擬磁碟");
            Add("Search the disk library", "搜尋磁碟庫");
            Add("Attached", "已掛載");
            Add("VHD Set", "VHD 集合");
            Add("unknown", "未知");
            Add("fixed", "固定大小");
            Add("dynamic", "動態擴充");
            Add("differencing", "差異");
            Add("The file is no longer there.", "檔案已經唔喺度。");

            // one disk
            Add("Reading the disk…", "讀緊磁碟…");
            Add("Attach…", "掛載…");
            Add("Detach…", "卸載…");
            Add("Grow…", "擴大…");
            Add("Shrink…", "縮細…");
            Add("Compact…", "壓縮…");
            Add("Merge into parent…", "合併入父磁碟…");
            Add("Reconnect parent…", "重新連接父磁碟…");
            Add("New identifier…", "新識別碼…");
            Add("An ISO image cannot change size.", "ISO 映像改唔到大細。");
            Add("Grow a VHD Set from Hyper-V, which manages its checkpoints.", "VHD 集合要喺 Hyper-V 擴大，因為佢嘅檢查點由 Hyper-V 管理。");
            Add("Only a VHDX can shrink.", "只有 VHDX 先可以縮細。");
            Add("An ISO image cannot change.", "ISO 映像改唔到。");
            Add("A fixed disk has no unused blocks to return.", "固定大小嘅磁碟冇未用嘅區塊可以交還。");
            Add("An ISO image cannot be converted here.", "呢度轉換唔到 ISO 映像。");
            Add("An ISO image has no identifier to change.", "ISO 映像冇識別碼可以改。");
            Add("Virtual size", "虛擬大小");
            Add("On disk", "磁碟上佔用");
            Add("Smallest safe size", "最細安全大小");
            Add("Block size", "區塊大小");
            Add("Sectors, logical and physical", "磁區（邏輯同實體）");
            Add("4K alignment", "4K 對齊");
            Add("aligned", "已對齊");
            Add("not aligned", "未對齊");
            Add("Fragmentation", "碎片");
            Add("Change tracking", "變更追蹤");
            Add("yes", "係");
            Add("no", "唔係");
            Add("on", "開");
            Add("off", "關");
            Add("Parent chain: the parent is missing. Reconnect it to use this disk.", "父磁碟鏈：搵唔到父磁碟。重新連接佢先用得呢個磁碟。");
            Add("Parent chain: the chain opens, so it is usable.", "父磁碟鏈：成條鏈開得到，所以用得。");
            Add("Removed from the list. The file itself was not touched.", "已經由清單移除。檔案本身冇郁過。");
            Add("Inside", "入面");
            Add("Reading its volumes…", "讀緊佢嘅磁碟區…");
            Add("No partitions. The disk is empty and can be prepared from Create below, or with Disk Management.", "冇分割區。磁碟係空嘅，可以用下面嘅「建立」或者「磁碟管理」準備。");
            Add("Its volumes could not be read.", "讀唔到佢嘅磁碟區。");

            // the actions
            Add("Attach the image", "掛載映像");
            Add("Attach", "掛載");
            Add("An ISO image attaches read-only, with a drive letter.", "ISO 映像會以唯讀方式掛載，並有磁碟機代號。");
            Add("Read-write, or read-only so nothing on it can change. It stays attached until you detach it.", "可以讀寫，或者唯讀令入面乜都改唔到。佢會一直掛住，直到你卸載。");
            Add("Access", "存取方式");
            Add("Read and write", "讀寫");
            Add("Read-only", "唯讀");
            Add("Drive letters", "磁碟機代號");
            Add("Give it drive letters", "畀佢磁碟機代號");
            Add("No drive letters", "唔要磁碟機代號");
            Add("Close any files open on it first. Detaching never deletes the file or what is on it.", "請先關閉入面開緊嘅檔案。卸載絕對唔會刪除檔案或者入面嘅嘢。");
            Add("Grow", "擴大");
            Add("New size", "新大小");
            Add("In GB.", "以 GB 計。");
            Add("Extend its last volume into the new space", "將最後一個磁碟區延伸到新空間");
            Add("On by default. Off leaves the new space unallocated.", "預設開啟。關咗嘅話，新空間會保持未配置。");
            Add("Shrink to the smallest safe size", "縮細到最細安全大小");
            Add("Shrinking its volume first lets the disk get smaller. Make a copy first if what is on it matters.", "先縮細佢嘅磁碟區，磁碟就可以再細啲。入面啲嘢重要嘅話，請先複製一份。");
            Add("Shrink its volume first", "先縮細佢嘅磁碟區");
            Add("On by default. The volume shrinks to what it holds plus 64 MB, then the disk shrinks to its smallest safe size.", "預設開啟。磁碟區會縮到內容大小再加 64 MB，然後磁碟再縮到最細安全大小。");
            Add("The volume and the disk get smaller. Space past what the volume holds is given up. Windows refuses to cut into data, but a copy first is wise.",
                "磁碟區同磁碟都會變細，超出磁碟區內容嘅空間會放棄。Windows 唔會切入資料，不過最好先複製一份。");
            Add("Shrink", "縮細");
            Add("Compact the disk", "壓縮磁碟");
            Add("Returns unused space inside the disk to this drive. The disk is attached read-only while it runs and put back as it was. Quick reclaims unused blocks; Full scan also looks for zeroed blocks and takes longer.",
                "將磁碟入面未用嘅空間交還畀呢個磁碟機。進行期間磁碟會以唯讀方式掛載，完成後還原返原狀。「快速」會收回未用嘅區塊；「完整掃描」仲會搵全零嘅區塊，時間較長。");
            Add("Mode", "模式");
            Add("Quick", "快速");
            Add("Full scan", "完整掃描");
            Add("Quick, on the detached file", "快速，喺已卸載嘅檔案上做");
            Add("Writes a new file beside it in the format and type you choose. The original stays as it is.", "按你揀嘅格式同類型，喺旁邊寫一個新檔案。原本嗰個保持不變。");
            Add("New format", "新格式");
            Add("New type", "新類型");
            Add("Grows as it fills", "隨內容增長");
            Add("grows as it fills", "隨內容增長");
            Add("fixed size", "固定大小");
            Add("Merge into its parent", "合併入佢嘅父磁碟");
            Add("Its changes are written into its parent. Afterwards this child no longer works, and neither does any other child of that parent.",
                "佢嘅改動會寫入佢嘅父磁碟。之後呢個子磁碟就用唔到，嗰個父磁碟嘅其他子磁碟都一樣。");
            Add("The parent changes for good. This child and every other child made from the same parent stop working. Make a copy of the parent first if you are not sure.",
                "父磁碟會永久改變。呢個子磁碟，同埋由同一個父磁碟整出嚟嘅其他子磁碟，全部都會用唔到。唔肯定嘅話，請先複製父磁碟。");
            Add("Merge", "合併");
            Add("Reconnect the parent", "重新連接父磁碟");
            Add("Point this child at its parent's new place. The parent must be the same disk it was made from.", "將呢個子磁碟指去父磁碟嘅新位置。父磁碟一定要係整佢嗰陣用嘅同一個磁碟。");
            Add("Choose the parent…", "揀父磁碟…");
            Add("Choose the parent disk", "揀父磁碟");
            Add("Give it a new identifier", "畀佢一個新識別碼");
            Add("Two copies of one disk share an identifier, so Windows will not attach both. A new one fixes that. Anything that found this disk by its old identifier will not find it any more.",
                "同一個磁碟嘅兩個副本會共用識別碼，所以 Windows 唔會兩個都掛載。換個新識別碼就解決到。之前靠舊識別碼搵呢個磁碟嘅嘢，以後就搵唔到佢。");
            Add("Anything that found this disk by its old identifier, such as a virtual machine or a backup, will not find it any more.", "之前靠舊識別碼搵呢個磁碟嘅嘢，例如虛擬機或者備份，以後就搵唔到佢。");
            Add("Set a new identifier", "設定新識別碼");
            Add("Script", "指令碼");
            Add("The same steps as a script", "同樣步驟嘅指令碼");
            Add("Copy the script", "複製指令碼");
            Add("Windows asks for permission to change the disk. Waiting…", "Windows 會問你批唔批准改動磁碟。等緊…");

            // a new virtual disk
            Add("New virtual disk", "新虛擬磁碟");
            Add("Start from a preset or set every option. Nothing is written until you press Create.", "可以由預設組合開始，或者逐項設定。撳「建立」之前乜都唔會寫入。");
            Add("Portable data disk", "可攜式資料磁碟");
            Add("Dev Drive", "開發磁碟機");
            Add("Native boot disk", "原生開機磁碟");
            Add("Scratch copy of a disk", "磁碟嘅臨時副本");
            Add("Child of a disk", "磁碟嘅子磁碟");
            Add("Copy of a disk", "磁碟副本");
            Add("Choose the disk to copy", "揀要複製嘅磁碟");
            Add("In GB. 3 MB at least; a VHD up to 2,040 GB, a VHDX up to 64 TB.", "以 GB 計。最少 3 MB；VHD 最大 2,040 GB，VHDX 最大 64 TB。");
            Add("Default", "預設");
            Add("Sectors", "磁區");
            Add("512 bytes", "512 位元組");
            Add("4096 bytes", "4096 位元組");
            Add("Physical sector size", "實體磁區大小");
            Add("Prepare it", "準備方式");
            Add("Leave it empty", "留空");
            Add("Make it a Dev Drive", "設成開發磁碟機");
            Add("Off by default. Needs Windows 11 build 10.0.22621.2338 or later and 50 GB at least. Trust and filters stay this computer's settings.",
                "預設關閉。需要 Windows 11 組建 10.0.22621.2338 或之後，同埋最少 50 GB。信任同篩選器沿用呢部電腦嘅設定。");
            Add("Leave it attached afterwards", "完成後保持掛載");
            Add("On by default, with a drive letter, ready to use.", "預設開啟，有磁碟機代號，即刻用得。");
            Add("Save as…", "另存為…");
            Add("Save the new disk as", "將新磁碟儲存為");
            Add("Windows asks once for administrator permission. Cancelling it changes nothing.", "Windows 會問一次系統管理員權限。取消嘅話乜都唔會改。");
            Add("Create and prepare", "建立並準備");
            Add("Create", "建立");
            Add("Checked", "已檢查");
            Add("Needs attention", "要留意");

            // Quick pack
            Add("Quick pack", "快速打包");
            Add("Quick pack…", "快速打包…");
            Add("Open in Virtual disks", "喺虛擬磁碟頁打開");
            Add("Pack this into one new VHDX beside it, which grows by itself and checks every file by SHA-256. Windows asks for permission once.",
                "將呢個打包入佢旁邊一個新 VHDX，唔夠位會自己擴大，每個檔案都用 SHA-256 核對。Windows 只會問一次權限。");
            Add("Pack these into one new VHDX beside them, which grows by itself and checks every file by SHA-256. Windows asks for permission once.",
                "將呢啲打包入佢哋旁邊一個新 VHDX，唔夠位會自己擴大，每個檔案都用 SHA-256 核對。Windows 只會問一次權限。");
            Add("Adds this disk to the disk library and shows everything about it: attach, grow, shrink, compact, convert and more.",
                "將呢個磁碟加入磁碟庫，顯示佢嘅所有資料：掛載、擴大、縮細、壓縮、轉換等等。");
            Add("Adds these disks to the disk library and shows everything about them: attach, grow, shrink, compact, convert and more.",
                "將呢啲磁碟加入磁碟庫，顯示佢哋嘅所有資料：掛載、擴大、縮細、壓縮、轉換等等。");
            Add("Drop files and folders here, or choose them. One new VHDX is made in the folder they came from and filled with everything you dropped. It grows by itself if it runs short, every file is checked against its original by SHA-256, and your originals are never moved or changed.",
                "將檔案同資料夾拖落嚟，或者揀佢哋。程式會喺佢哋原本嘅資料夾整一個新 VHDX，將你拖落嚟嘅嘢全部放入去。唔夠位會自己擴大，每個檔案都會用 SHA-256 同原檔核對，你嘅原檔絕對唔會被搬走或者改動。");
            Add("Counting what you dropped…", "點算緊你拖落嚟嘅嘢…");
            Add("Drop files and folders to pack them", "拖檔案同資料夾落嚟打包");
            Add("Or choose them. Windows asks once for administrator permission, to make and attach the disk.", "或者揀佢哋。Windows 會問一次系統管理員權限，用嚟建立同掛載磁碟。");
            Add("Choose files to pack", "揀要打包嘅檔案");
            Add("Choose a folder to pack", "揀要打包嘅資料夾");
            Add("Drop files and folders here to pack them into a new VHDX", "將檔案同資料夾拖落嚟，打包入新 VHDX");
            Add("Nothing to pack: the items are no longer there.", "冇嘢可以打包：嗰啲項目已經唔喺度。");
            Add("The items came from more than one folder, so the disk goes in the first item's folder.", "啲項目來自多過一個資料夾，所以磁碟會放喺第一個項目嘅資料夾。");
            Add("Copy permissions too", "連權限一齊複製");
            Add("Off by default. Keeps who can open each file.", "預設關閉。保留邊個可以開每個檔案。");
            Add("Fit it tightly when done", "完成後縮到啱啱好");
            Add("Off by default. Shrinks the disk to its smallest safe size plus a small margin.", "預設關閉。將磁碟縮到最細安全大小，再留少少餘量。");
            Add("How the size was worked out", "大小點樣計出嚟");
            Add("Files and folders", "檔案同資料夾");
            Add("Rounding every file up to whole clusters", "每個檔案進位到完整叢集");
            Add("File system records for every file and folder", "每個檔案同資料夾嘅檔案系統記錄");
            Add("Partition tables and reserved space", "分割表同保留空間");
            Add("Headroom", "預留空間");
            Add("Starting size", "起始大小");
            Add("Asking Windows for permission to make and attach the disk.", "請 Windows 批准建立同掛載磁碟。");
            Add("Every drive letter from D to Z is in use, so the disk has nowhere to appear.", "由 D 到 Z 嘅磁碟機代號全部用晒，磁碟冇地方出現。");
            Add("Windows asked for permission and it was not given, so nothing was made.", "Windows 問咗權限但冇批准，所以乜都冇建立。");
            Add("The disk was not made.", "磁碟冇建立到。");
            Add("The disk is at the largest size a VHDX allows.", "磁碟已經去到 VHDX 容許嘅最大大小。");
            Add("The disk could not grow.", "磁碟擴大唔到。");
            Add("no answer", "冇回應");
            Add("Time left", "剩餘時間");
            Add("under a minute", "唔夠一分鐘");
            Add("working it out", "計緊");
            Add("Grew by itself", "自動擴大");
            Add("not yet", "未有");
            Add("not needed", "唔使");
            Add("Packed and checked", "已打包並核對");
            Add("Open in the disk library", "喺磁碟庫打開");
            Add("Attach now", "即刻掛載");
            Add("Pack again", "再打包一次");
            Add("Windows denied read access.", "Windows 拒絕讀取。");
            Add("Open in another app, which locks it.", "喺另一個程式開緊，被佢鎖住。");
            Add("Part of it is locked by another app.", "有部分被另一個程式鎖住。");
            Add("It was removed while packing.", "打包期間被刪除咗。");
            Add("Did not match its original after copying, so it is not counted.", "複製後同原檔唔一致，所以唔計。");
            Add("Nothing was dropped.", "冇拖落任何嘢。");
            Add("The disk is full. Growing it…", "磁碟滿咗，擴大緊…");
            Add("The disk is full.", "磁碟滿咗。");

            // the elevated helper's steps, as the Quick pack log shows them
            Add("Ready", "準備好");
            Add("Left mounted", "保持掛載");
            Add("Detached and deleted", "已卸載並刪除");
            Add("The disk could not be detached.", "卸載唔到磁碟。");
            Add("Detached, but the file could not be deleted.", "已卸載，但刪除唔到檔案。");
            Add("The disk keeps its size.", "磁碟保持原本大小。");
            Add("The new size is not bigger than the disk.", "新大小唔比磁碟大。");
            Add("The disk is at the largest size its format allows.", "磁碟已經去到佢格式容許嘅最大大小。");
            Add("PowerShell did not finish in 10 minutes.", "PowerShell 10 分鐘內都未做完。");

            // what a finished job says
            Add("Attached read-only.", "已用唯讀方式掛載。");
            Add("Attached.", "已掛載。");
            Add("It was not attached.", "佢本來就冇掛載。");
            Add("Detached.", "已卸載。");
            Add("A fixed disk has no unused blocks to return. Convert it to dynamically expanding first.", "固定大小嘅磁碟冇未用嘅區塊可以交還。請先轉換成動態擴充。");
            Add("Only a differencing disk can merge into its parent.", "只有差異磁碟先可以合併入父磁碟。");
            Add("Detach it first. A merge needs the whole chain detached.", "請先卸載。合併需要成條鏈都卸載咗。");
            Add("Detach it first. Its identifier changes only while it is not in use.", "請先卸載。識別碼只可以喺冇用緊嘅時候改。");
            Add("Detach it first.", "請先卸載。");
            Add("Detach it first. A disk converts only while it is not in use.", "請先卸載。磁碟只可以喺冇用緊嘅時候轉換。");
            Add("The request is not one this version understands.", "呢個版本唔明白呢個要求。");
            Add("Windows asked for permission and it was not given, so nothing was changed.", "Windows 問咗權限但冇批准，所以乜都冇改。");
            Add("The helper did not start.", "輔助程式冇啟動到。");

            // the checks before anything is written
            Add("The disk file must end in .vhd, .vhdx, .vhds, .avhd, .avhdx or .iso.", "磁碟檔案一定要以 .vhd、.vhdx、.vhds、.avhd、.avhdx 或 .iso 結尾。");
            Add("An ISO image is made by other tools; this creates VHD, VHDX and VHD Set files.", "ISO 映像要用其他工具整；呢度建立嘅係 VHD、VHDX 同 VHD 集合檔案。");
            Add("Choose fixed size or dynamically expanding.", "請揀固定大小或者動態擴充。");
            Add("3 MB is the smallest disk any format allows.", "所有格式最細都要 3 MB。");
            Add("A VHD holds at most 2,040 GB. Choose VHDX for a bigger disk, or a smaller size.", "VHD 最大 2,040 GB。要更大就揀 VHDX，或者揀細啲嘅大小。");
            Add("A VHDX holds at most 64 TB.", "VHDX 最大 64 TB。");
            Add("Only a VHDX or VHD Set takes a block size.", "只有 VHDX 或者 VHD 集合先可以設區塊大小。");
            Add("The block size is a power of two from 1 MB to 256 MB.", "區塊大小要係 1 MB 至 256 MB 之間嘅 2 嘅次方。");
            Add("The logical sector size is 512 or 4096 bytes.", "邏輯磁區大小係 512 或 4096 位元組。");
            Add("The physical sector size is 512 or 4096 bytes.", "實體磁區大小係 512 或 4096 位元組。");
            Add("Choose GPT, MBR or leave it empty.", "請揀 GPT、MBR 或者留空。");
            Add("The file system is NTFS, ReFS, exFAT, FAT32 or FAT.", "檔案系統要係 NTFS、ReFS、exFAT、FAT32 或 FAT。");
            Add("The label can use letters, numbers, spaces and - _ . ( ) only.", "標籤只可以用字母、數字、空格同 - _ . ( )。");
            Add("A Dev Drive is ReFS.", "開發磁碟機一定係 ReFS。");
            Add("A Dev Drive needs 50 GB at least.", "開發磁碟機最少要 50 GB。");
            Add("Windows formats FAT32 only up to 32 GB. Choose NTFS or exFAT.", "Windows 只會將 32 GB 或以下嘅磁碟格式化成 FAT32。請揀 NTFS 或 exFAT。");
            Add("A VHD holds at most 2,040 GB.", "VHD 最大 2,040 GB。");
            Add("Choose the new size.", "請揀新大小。");
            Add("An ISO image cannot be changed.", "ISO 映像改唔到。");
            Add("An ISO image has no parent.", "ISO 映像冇父磁碟。");
            Add("The new file must end in .vhd, .vhdx or .vhds.", "新檔案一定要以 .vhd、.vhdx 或 .vhds 結尾。");
            Add("Choose fixed size or dynamically expanding for the new file.", "請為新檔案揀固定大小或者動態擴充。");

            // the disk service
            Add("The Windows virtual disk service is not on this computer.", "呢部電腦冇 Windows 虛擬磁碟服務。");
            Add("Only .vhd, .vhdx and .vhds files can be created.", "只可以建立 .vhd、.vhdx 同 .vhds 檔案。");
        }

        private static void AddVirtualDisksPatterns(List<(Regex, Func<Match, string>)> p)
        {
            const RegexOptions Opt = RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline;
            void Add(string rx, string yue) => p.Add((new Regex(rx, Opt), m => m.Result(yue)));
            void AddFn(string rx, Func<Match, string> fn) => p.Add((new Regex(rx, Opt), fn));
            // a part that has its own entry is translated too; anything else (a name, a path, a message from Windows) stays as it is
            string Y(string en) => string.IsNullOrEmpty(en) ? en : Language.Yue(en) ?? en;
            string G(Match m, int i) => m.Groups[i].Value;
            // two sentences side by side: a space only when the first one ends in English, such as a message from Windows
            string Then(string a, string b) => a.Length > 0 && !Language.HasCjk(a.Substring(a.Length - 1)) ? a + " " + b : a + b;
            const string Size = @"(\d[\d.,]* (?:B|KB|MB|GB|TB))";
            const string Disk = @"([^\\/:*?""<>|\r\n]+?\.(?:vhdx|vhds|vhd|avhdx|avhd|iso))";
            string Did(string verb) => verb.ToLowerInvariant() switch
            {
                "attached" => "已掛載", "detached" => "已卸載", "grew" => "已擴大", "shrank" => "已縮細", "compacted" => "已壓縮",
                "merged" => "已合併", "converted" => "已轉換", "created" => "已建立", _ => verb
            };
            string Couldnt(string verb) => verb.ToLowerInvariant() switch
            {
                "attach" => "掛載", "detach" => "卸載", "grow" => "擴大", "shrink" => "縮細", "compact" => "壓縮",
                "merge" => "合併", "convert" => "轉換", "create" => "建立", "resize" => "改大細", _ => verb
            };

            // the page and the library
            AddFn(@"^Hyper-V commands: (.+)$", m => "Hyper-V 指令：" + Y(G(m, 1)));
            Add(@"^No virtual disk files in (.+)\.$", "$1 入面冇虛擬磁碟檔案。");
            Add(@"^Found (\d+) virtual disk files? in (.+)\.$", "喺 $2 搵到 $1 個虛擬磁碟檔案。");
            Add(@"^Added " + Disk + @" to the disk library\.$", "已將 $1 加入磁碟庫。");
            Add(@"^Added (\d+) virtual disks to the disk library\.$", "已將 $1 個虛擬磁碟加入磁碟庫。");
            Add(@"^" + Size + @" on disk$", "磁碟上佔 $1");
            Add(@"^Attached, disk (\d+)$", "已掛載，磁碟 $1");
            AddFn(@"^(VHDX|VHD Set|VHD|ISO|unknown), (fixed|dynamic|differencing)$", m => Y(G(m, 1)) + "，" + Y(G(m, 2)));
            Add(@"^(\d+) / (\d+) bytes$", "$1 / $2 位元組");
            Add(@"^(\d+) bytes$", "$1 位元組");
            Add(@"^yes, disk (\d+)$", "係，磁碟 $1");
            Add(@"^on, ([0-9a-f-]{8,})$", "開，$1");
            AddFn(@"^Its volumes could not be read: (.+)$", m => "讀唔到佢嘅磁碟區：" + G(m, 1));
            AddFn(@"^Partition (\d+): (.+?)(?:, " + Size + @" free)?$", m => "分割區 " + G(m, 1) + "：" + G(m, 2).Replace(", ", "，") + (m.Groups[3].Success ? "，剩餘 " + G(m, 3) : ""));

            // the action panels
            Add(@"^Now " + Size + @"\. The new space goes to its last volume unless you turn that off\. A VHD grows to 2,040 GB at most, a VHDX to 64 TB\.$",
                "而家係 $1。新空間會撥入最後一個磁碟區，除非你關咗呢個選項。VHD 最大可以擴到 2,040 GB，VHDX 最大 64 TB。");
            Add(@"^Its smallest safe size is now " + Size + @"\. Shrinking its volume first lets the disk get smaller\. Make a copy first if what is on it matters\.$",
                "佢而家嘅最細安全大小係 $1。先縮細佢嘅磁碟區，磁碟就可以再細啲。入面啲嘢重要嘅話，請先複製一份。");
            Add(@"^Shrink " + Disk + @"\?$", "縮細 $1？");
            Add(@"^Its changes are written into (.+?)\. Afterwards this child no longer works, and neither does any other child of that parent\.$",
                "佢嘅改動會寫入 $1。之後呢個子磁碟就用唔到，嗰個父磁碟嘅其他子磁碟都一樣。");
            Add(@"^Merge " + Disk + @" into its parent\?$", "將 $1 合併入佢嘅父磁碟？");
            Add(@"^Give " + Disk + @" a new identifier\?$", "畀 $1 一個新識別碼？");

            // what a job said, with the disk's name in front
            AddFn(@"^" + Disk + @": (.+?) Done through the (.+)\.$", m => G(m, 1) + "：" + Y(G(m, 2)) + "經由 " + Y(G(m, 3)) + "完成。");
            AddFn(@"^" + Disk + @": not done\. (.+)$", m => G(m, 1) + "：未完成。" + Y(G(m, 2)));
            AddFn(@"^(.+?) Route: (.+)\.$", m => Y(G(m, 1)) + "途徑：" + Y(G(m, 2)) + "。");

            // recorded actions and activity
            AddFn(@"^(Attached|Detached|Grew|Shrank|Compacted|Merged|Converted|Created) " + Disk + @"$", m => Did(G(m, 1)) + " " + G(m, 2));
            Add(@"^Gave a new identifier to " + Disk + @"$", "已為 $1 設定新識別碼");
            Add(@"^Reconnected the parent of " + Disk + @"$", "已重新連接 $1 嘅父磁碟");
            AddFn(@"^Could not (attach|detach|grow|shrink|compact|merge|convert|create) " + Disk + @"(?:: (.+))?$", m => Couldnt(G(m, 1)) + "唔到 " + G(m, 2) + (m.Groups[3].Success ? "：" + Y(G(m, 3)) : ""));
            AddFn(@"^Could not give a new identifier to " + Disk + @"(?:: (.+))?$", m => "設定唔到 " + G(m, 1) + " 嘅新識別碼" + (m.Groups[2].Success ? "：" + Y(G(m, 2)) : ""));
            AddFn(@"^Could not reconnect the parent of " + Disk + @"(?:: (.+))?$", m => "重新連接唔到 " + G(m, 1) + " 嘅父磁碟" + (m.Groups[2].Success ? "：" + Y(G(m, 2)) : ""));

            // a new virtual disk
            Add(@"^" + Disk + @" is a new name\. Nothing is overwritten\.$", "$1 係新名稱，乜都唔會被覆蓋。");
            Add(@"^Takes " + Size + @" now\. (.+?) has " + Size + @" free\.$", "而家要用 $1。$2 有 $3 可用。");
            Add(@"^Takes " + Size + @" now, and (.+?) has " + Size + @" free: " + Size + @" short\.$", "而家要用 $1，但 $2 只有 $3 可用：仲差 $4。");
            Add(@"^Starts small and can grow to " + Size + @"\. Windows does not set that space aside on (.+?), so keep it free\.$", "開始時好細，最大可以去到 $1。Windows 唔會喺 $2 預留呢啲空間，所以記得留位。");

            // Quick pack: the plan
            Add(@"^(.+)\.vhdx is already in that folder, so the new one is numbered\. Nothing is ever overwritten\.$", "嗰個資料夾已經有 $1.vhdx，所以新嗰個會加編號。乜都唔會被覆蓋。");
            AddFn(@"^([\d,]+) files? in ([\d,]+) folders?, " + Size + "$", m => G(m, 2) + " 個資料夾入面有 " + G(m, 1) + " 個檔案，" + G(m, 3));
            Add(@"^(.+?) has " + Size + @" free\. The pack needs about " + Size + @" there\.$", "$1 有 $2 可用，打包大約要用 $3。");
            Add(@"^(.+?) has " + Size + @" free, and the pack needs about " + Size + @": " + Size + @" short\.$", "$1 有 $2 可用，但打包大約要 $3：仲差 $4。");
            Add(@"^Pack " + Size + "$", "打包 $1");

            // Quick pack: while it runs
            AddFn(@"^Made " + Disk + @": " + Size + @", (fixed size|grows as it fills), GPT, (\w+), at ([A-Z]):\.$", m => "已建立 " + G(m, 1) + "：" + G(m, 2) + "，" + Y(G(m, 3)) + "，GPT，" + G(m, 4) + "，喺 " + G(m, 5) + ":。");
            Add(@"^Ran out of room at (.*?)\. Growing the disk from " + Size + @" to " + Size + @"\.$", "去到 $1 冇晒位。磁碟由 $2 擴大到 $3。");
            Add(@"^Grew from " + Size + @" to " + Size + @", extended the volume, and carried on, checking that file again\.$", "由 $1 擴大到 $2，延伸咗磁碟區，再繼續，重新核對嗰個檔案。");
            Add(@"^" + Size + " of " + Size + "$", "$1 / $2");
            Add(@"^Packed (\d+) percent$", "已打包 $1%");
            Add(@"^([\d,]+) of ([\d,]+)$", "$1 / $2");
            Add(@"^about (\d+) min$", "大約 $1 分鐘");
            Add(@"^(\d+) times?$", "$1 次");
            Add(@"^Now copying (.+)$", "而家複製緊 $1");
            Add(@"^Grown\. Carrying on from (.+)\.$", "擴大咗。由 $1 繼續。");

            // Quick pack: how it ended
            Add(@"^All ([\d,]+) files match their originals by SHA-256\. The disk is detached and sits beside what you dropped\.$", "全部 $1 個檔案都同原檔嘅 SHA-256 一致。磁碟已卸載，放喺你拖落嚟嘅嘢旁邊。");
            Add(@"^Partial: (\d+) files? (?:was|were) skipped$", "部分完成：略過咗 $1 個檔案");
            Add(@"^([\d,]+) of ([\d,]+) files match their originals\. The pack is not complete until the ones below are in\.$", "$2 個檔案入面有 $1 個同原檔一致。下面嗰啲未放入去之前，打包都未算完成。");
            AddFn(@"^" + Size + " to " + Size + @"(?:, " + Size + " to " + Size + ")*$", m => m.Value.Replace(" to ", " → ").Replace(", ", "，"));
            Add(@"^It could not be read: (.+)\.$", "讀唔到：$1。");
            Add(@"^Cancelled\. Detached and removed " + Disk + @", the only file this run made\. Your files were not touched\.$", "已取消。已卸載並移除 $1，即係今次唯一整出嚟嘅檔案。你嘅檔案冇郁過。");
            AddFn(@"^(.+?) The new disk was detached and removed; your files were not touched\.$", m => Then(Y(G(m, 1)), "新磁碟已卸載並移除；你嘅檔案冇郁過。"));
            AddFn(@"^Could not fit it tightly: (.+?)\. It keeps its size\.$", m => "縮唔到啱啱好：" + Y(G(m, 1)) + "。佢保持原本大小。");
            Add(@"^Detached\. " + Disk + @" sits beside the originals\.$", "已卸載。$1 放喺原檔旁邊。");
            Add(@"^Quick pack cancelled: " + Disk + @" removed$", "快速打包已取消：已移除 $1");
            Add(@"^Quick pack stopped: " + Disk + "$", "快速打包停咗：$1");
            Add(@"^Quick pack, partial: " + Disk + "$", "快速打包（部分）：$1");
            Add(@"^Quick pack: " + Disk + "$", "快速打包：$1");
            AddFn(@"^([\d,]+) of ([\d,]+) files match by SHA-256\. Virtual size " + Size + @", on disk " + Size + @"\.(?: Grew (\d+) times?\.)?$",
                m => G(m, 2) + " 個檔案入面有 " + G(m, 1) + " 個 SHA-256 一致。虛擬大小 " + G(m, 3) + "，磁碟上佔 " + G(m, 4) + "。" + (m.Groups[5].Success ? "自動擴大咗 " + G(m, 5) + " 次。" : ""));
            Add(@"^Packed with skipped files into " + Disk + "$", "已打包入 $1，有檔案略過");
            Add(@"^Packed into " + Disk + "$", "已打包入 $1");

            // the elevated helper
            Add(@"^Formatted (ReFS) as (.+)$", "已將 $2 格式化為 $1");
            Add(@"^Grew to " + Size + "$", "已擴大到 $1");
            Add(@"^Fitted tightly: " + Size + " down to " + Size + @"\.$", "已縮到啱啱好：由 $1 縮到 $2。");
            Add(@"^(.+?) is " + Size + @" short of the room the disk needs to grow\.$", "$1 仲差 $2 先夠位畀磁碟擴大。");
            AddFn(@"^The disk grew, but its volume could not be extended: (.+)$", m => "磁碟擴大咗，但延伸唔到磁碟區：" + G(m, 1));
            Add(@"^The disk grew, but its volume did not come back at (.+)\.$", "磁碟擴大咗，但磁碟區冇喺 $1 返嚟。");
            AddFn(@"^Its volume could not shrink: (.+)$", m => "佢嘅磁碟區縮唔到：" + G(m, 1));

            // what a finished job says
            AddFn(@"^(.+?) The new file was removed, so nothing was left behind\.$", m => Then(Y(G(m, 1)), "新檔案已經刪除，冇留低任何嘢。"));
            Add(@"^It was already attached at (.+)\.$", "佢已經掛載咗喺 $1。");
            Add(@"^The new size is not bigger than " + Size + @"\.$", "新大小唔比 $1 大。");
            Add(@"^Grown to " + Size + @" and its volume extended\.$", "已擴大到 $1，磁碟區亦已延伸。");
            Add(@"^Grown to " + Size + @"\.$", "已擴大到 $1。");
            AddFn(@"^The disk grew, but its volume was not extended\. (.+)$", m => "磁碟擴大咗，但磁碟區冇延伸到。" + Y(G(m, 1)));
            AddFn(@"^Could not (extend the volume|shrink the volume|prepare the disk): (.+)$", m => G(m, 1).ToLowerInvariant() switch
            {
                "extend the volume" => "延伸唔到磁碟區：", "shrink the volume" => "縮唔到磁碟區：", _ => "準備唔到磁碟："
            } + G(m, 2));
            Add(@"^Shrunk from " + Size + " to " + Size + @", its smallest safe size\.$", "由 $1 縮細到 $2，即係佢嘅最細安全大小。");
            AddFn(@"^Compacted \((Quick|Full scan|Quick, on the detached file)\)\. On disk " + Size + " before, " + Size + @" after\.$",
                m => "已壓縮（" + Y(G(m, 1)) + "）。磁碟上佔用由 " + G(m, 2) + " 變成 " + G(m, 3) + "。");
            Add(@"^Merged into (.+?)\. This child no longer counts as a disk\.$", "已合併入 $1。呢個子磁碟唔再算係一個磁碟。");
            Add(@"^New identifier ([0-9a-f-]{36})\.$", "新識別碼 $1。");
            Add(@"^Now points at (.+)\.$", "而家指向 $1。");
            AddFn(@"^The new file did not come out the same size, so it was removed\.\s*(.*)$", m => "新檔案大小唔一致，所以已經刪除。" + G(m, 1));
            AddFn(@"^Converted to (VHDX|VHD), (fixed|dynamic), beside it as " + Disk + @"\. The original is unchanged\.$", m => "已轉換成 " + G(m, 1) + "（" + Y(G(m, 2)) + "），喺旁邊存做 " + G(m, 3) + "。原本嗰個冇改。");
            AddFn(@"^Created " + Disk + @", (GPT|MBR|none), (.+?), (attached|detached)\.$",
                m => "已建立 " + G(m, 1) + "，" + (G(m, 2) == "none" ? "留空" : G(m, 2)) + "，" + Y(G(m, 3)) + "，" + (G(m, 4).ToLowerInvariant() == "attached" ? "已掛載" : "已卸載") + "。");
            Add(@"^Created " + Disk + @"\.$", "已建立 $1。");
            Add(@"^The helper stopped \(exit (-?\d+)\) before it reported anything\.$", "輔助程式未回報任何嘢就停咗（結束碼 $1）。");

            // the checks before anything is written
            AddFn(@"^(The disk file|The parent|The source|The new file) (needs a full path|must be on a local drive, not on a network share|has characters that cannot be used)\.$", m =>
                (G(m, 1).ToLowerInvariant() switch { "the disk file" => "磁碟檔案", "the parent" => "父磁碟", "the source" => "來源", _ => "新檔案" }) +
                (G(m, 2).ToLowerInvariant() switch { "needs a full path" => "要用完整路徑", "has characters that cannot be used" => "有用唔到嘅字元", _ => "一定要喺本機磁碟，唔可以喺網絡共用" }) + "。");
            Add(@"^(.+) already exists\. Choose another name; nothing is ever overwritten\.$", "$1 已經存在。請揀另一個名；乜都唔會被覆蓋。");
            AddFn(@"^The (parent|source) (.+) is not there\.$", m => (G(m, 1).ToLowerInvariant() == "parent" ? "父磁碟 " : "來源 ") + G(m, 2) + " 唔喺度。");
            Add(@"^" + Disk + @" is not there\.$", "$1 唔喺度。");
            Add(@"^The label can have (\d+) characters for (.+)\.$", "$2 嘅標籤最多可以有 $1 個字元。");

            // the disk service
            AddFn(@"^Could not open " + Disk + @": (.+)$", m => "開唔到 " + G(m, 1) + "：" + G(m, 2));
            AddFn(@"^(Attach|Detach|Grow|Resize|Compact|Merge|Create) failed: (.+)$", m => Couldnt(G(m, 1)) + "失敗：" + G(m, 2));
            AddFn(@"^The new identifier could not be set: (.+)$", m => "設定唔到新識別碼：" + G(m, 1));
            AddFn(@"^The parent could not be set: (.+)$", m => "設定唔到父磁碟：" + G(m, 1));
        }
    }
}
